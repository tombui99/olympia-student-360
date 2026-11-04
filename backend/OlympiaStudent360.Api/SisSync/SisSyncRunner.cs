using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using MySql.Data.MySqlClient;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.SisSync;

public sealed class SisSyncRunner
{
    public async Task RunAsync(AppDbContext db, HttpClient http, SisEtlOptions options, bool full, CancellationToken ct)
    {
        var dbOptions = (DbContextOptions<AppDbContext>)db.GetService<IDbContextOptions>();
        await ApplicationDatabase.CreateRetryStrategy(db).ExecuteAsync(async token =>
        {
            await using var attempt = new AppDbContext(dbOptions);
            await RunAttemptAsync(attempt, new SisApiClient(http, options), options, full, token);
        }, ct);
    }

    private static async Task RunAttemptAsync(AppDbContext db, SisApiClient api, SisEtlOptions options, bool full, CancellationToken ct)
    {
        await db.MigrateWithRecoveryAsync(ct);
        // Both imports update StudentProfiles. Share Canvas's session lock while SIS changes
        // student identity/dimensions and Canvas links its identities and builds reports.
        await using var heldLock = await SisProjectionLease.AcquireAsync(db, ct);
        await db.SisSyncRuns.Where(x => x.SourceKey == options.SourceKey && x.Status == "running")
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "failed")
                .SetProperty(x => x.CompletedAtUtc, DateTime.UtcNow)
                .SetProperty(x => x.Error, "Previous SIS process was interrupted."), ct);
        var first = !await db.SisSyncRuns.AnyAsync(x => x.SourceKey == options.SourceKey && x.Status == "completed", ct);
        var run = new SisSyncRun
        {
            SourceKey = options.SourceKey, Mode = full ? "full" : "incremental",
            Status = "running", StartedAtUtc = DateTime.UtcNow
        };
        db.SisSyncRuns.Add(run);
        await db.SaveChangesAsync(ct);
        var runId = run.Id;
        try
        {
            await api.CheckScopesAsync(ct);
            var totals = new Totals();
            var catalog = SisEndpoints.All.Single(x => x.Name == "schools");
            await ScanAsync(db, api, options, catalog, new SortedDictionary<string, string>(), true, false, totals, ct);
            var documents = await db.SisWarehouseRecords.AsNoTracking()
                .Where(x => x.SourceKey == options.SourceKey && x.Endpoint == "schools" && !x.IsDeleted)
                .Select(x => x.PayloadJson).ToListAsync(ct);
            var years = new Dictionary<string, SchoolYear>(StringComparer.Ordinal);
            var current = new HashSet<string>(StringComparer.Ordinal);
            var foundSchools = new HashSet<string>(StringComparer.Ordinal);
            foreach (var text in documents)
            {
                using var json = JsonDocument.Parse(text);
                var school = json.RootElement;
                var schoolId = SisJson.RequiredId(school);
                foundSchools.Add(schoolId);
                if (options.SchoolIds.Count > 0 && !options.SchoolIds.Contains(schoolId)) continue;
                foreach (var year in school.GetProperty("school_years").EnumerateArray())
                {
                    if (SisJson.Bool(year, "is_deleted") == true) continue;
                    var id = SisJson.RequiredId(year);
                    if (SisJson.Bool(year, "is_current") == true) current.Add(id);
                    years[id] = new SchoolYear(id, SisJson.Date(year, "start_date"), SisJson.Date(year, "end_date"));
                }

            }
            if (options.SchoolIds.Any(id => !foundSchools.Contains(id)))
                throw new SisApiException("A configured SIS school is absent from the accessible catalog.");
            var selected = options.SchoolYearIds.Count == 0 ? current.Order().ToArray() : options.SchoolYearIds.ToArray();
            if (selected.Length == 0 || selected.Any(id => !years.ContainsKey(id)))
                throw new SisApiException("No valid SIS school year was discovered. Check the catalog or Sis:SchoolYearIds.");
            foreach (var endpoint in SisEndpoints.All.Where(x => x.Name != "schools" && options.Resources.Contains(x.Name)))
            {
                IEnumerable<string?> endpointYears = endpoint.Year ? selected : new string?[] { null };
                IEnumerable<string?> schools = endpoint.School && options.SchoolIds.Count > 0 ? options.SchoolIds.Cast<string?>() : new string?[] { null };
                foreach (var yearId in endpointYears)
                foreach (var schoolId in schools)
                {
                    var baseFilters = new SortedDictionary<string, string>(StringComparer.Ordinal);
                    if (yearId is not null) baseFilters["school_year_id"] = yearId;
                    if (schoolId is not null) baseFilters["school_id"] = schoolId;
                    IEnumerable<string?> types = endpoint.Typed ? new[] { "HOMEROOM", "COURSE" } : new string?[] { null };
                    foreach (var type in types)
                    {
                        var typed = new SortedDictionary<string, string>(baseFilters, StringComparer.Ordinal);
                        if (type is not null) typed["type"] = type;
                        if (!endpoint.Daily)
                        {
                            await ScanAsync(db, api, options, endpoint, typed, full,
                                yearId is not null && current.Contains(yearId), totals, ct);
                            continue;
                        }
                        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
                        var year = years[yearId!];
                        var historical = full || first || !await HasDailyScopeAsync(db, options.SourceKey, endpoint.Name, typed, ct);
                        var from = options.FromDate ?? (historical && endpoint.Name != "timetable"
                            ? year.Start is { } start ? DateOnly.FromDateTime(start)
                                : throw new SisApiException("School year has no start date. Configure Sis:FromDate and Sis:ToDate for daily backfill.")
                            : today.AddDays(-(options.LookbackDays - 1)));
                        var to = options.ToDate ?? today.AddDays(endpoint.Name == "timetable" ? options.TimetableFutureDays : 0);
                        if (options.ToDate is null && year.End is { } end && DateOnly.FromDateTime(end) < to) to = DateOnly.FromDateTime(end);
                        foreach (var window in MonthWindows(from, to))
                        {
                            var filters = new SortedDictionary<string, string>(typed, StringComparer.Ordinal)
                            { ["from_date"] = window.From.ToString("yyyy-MM-dd"), ["to_date"] = window.To.ToString("yyyy-MM-dd") };
                            await ScanAsync(db, api, options, endpoint, filters, full || endpoint.Name == "timetable", false, totals, ct);
                        }
                    }
                }
                if (endpoint.Name == "students" && current.Count > 0 && current.All(selected.Contains))
                {
                    // A new school year may omit former students entirely. Retain their
                    // profiles and facts, but remove them from current selections after
                    // every selected current-year student scan has succeeded.
                    var expiredQuery = db.StudentProfiles.Where(x => x.SchoolOnlineUserId != null && x.SISIsActive != false);
                    foreach (var currentYear in current)
                        expiredQuery = expiredQuery.Where(x => x.SchoolOnlineSchoolYearId == null || x.SchoolOnlineSchoolYearId != currentYear);
                    if (options.SchoolIds.Count > 0)
                        expiredQuery = expiredQuery.WhereMatchesAny(x => x.SchoolOnlineSchoolId, options.SchoolIds);
                    var expired = await expiredQuery.ToListAsync(ct);
                    foreach (var student in expired)
                    {
                        student.SISIsActive = false;
                        student.SISStatus = "OUT_OF_SCOPE";
                        student.UpdatedAt = DateTime.UtcNow;
                    }
                    await db.SaveChangesAsync(ct);
                    db.ChangeTracker.Clear();
                }
            }
            var currentStudents = await db.StudentProfiles.CurrentRoster(db).CountAsync(ct);
            var healthVisits = db.StudentHealthVisits.Where(x => x.SisSourceKey == options.SourceKey);
            var visitCount = options.Resources.Contains("health/visits") ? await healthVisits.CountAsync(ct) : 0;
            var linkedVisitCount = options.Resources.Contains("health/visits")
                ? await healthVisits.CountAsync(x => x.StudentProfileId != null, ct) : 0;
            run = await db.SisSyncRuns.SingleAsync(x => x.Id == runId, ct);
            run.Status = "completed";
            run.Records = totals.Records;
            run.Scans = totals.Scans;
            run.CompletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            Console.WriteLine($"SIS sync completed: {totals.Records} records, {totals.Scans} scans. Student report changes queued for the existing report worker.");
            Console.WriteLine($"Student 360 current roster: {currentStudents} active students.");
            if (options.Resources.Contains("health/visits"))
                Console.WriteLine($"SIS health visits: {visitCount} visible, {linkedVisitCount} linked, {visitCount - linkedVisitCount} unlinked.");
        }
        catch (Exception ex)
        {
            db.ChangeTracker.Clear();
            try
            {
                run = await db.SisSyncRuns.SingleAsync(x => x.Id == runId, CancellationToken.None);
                run.Status = ex is OperationCanceledException ? "cancelled" : "failed";
                run.CompletedAtUtc = DateTime.UtcNow;
                run.Error = SafeError(ex);
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch { Console.Error.WriteLine("Could not persist SIS failure status; the next run will recover interrupted history."); }
            throw;
        }
    }

    private static async Task ScanAsync(AppDbContext db, SisApiClient api, SisEtlOptions options, SisEndpoint endpoint,
        SortedDictionary<string, string> filters, bool full, bool projectStudents, Totals totals, CancellationToken ct)
    {
        var filterJson = JsonSerializer.Serialize(filters);
        var filterKey = SisEtlOptions.Hash(filterJson);
        var state = await db.SisSyncStates.AsNoTracking().SingleOrDefaultAsync(x => x.SourceKey == options.SourceKey &&
            x.Endpoint == endpoint.Name && x.FilterKey == filterKey, ct);
        var since = !full && endpoint.Incremental ? state?.LastSuccessfulWatermark : null;
        var scan = Guid.NewGuid().ToString("N");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var timetable = new List<JsonElement>();
        string? watermark = null;
        var count = 0;
        Console.WriteLine($"Scanning SIS {endpoint.Name} ({(since is null ? "snapshot" : "incremental")}).");
        await foreach (var page in api.ReadAsync(endpoint.Name, filters, since, ct))
        {
            foreach (var record in page.Records)
                if (!seen.Add(RecordKey(endpoint.Name, record))) throw new SisApiException("SIS returned a duplicate record key within a scan.");
            watermark = page.Watermark;
            count += page.Records.Count;
            if (endpoint.Name == "timetable") { timetable.AddRange(page.Records); continue; }
            await using (var transaction = await db.Database.BeginTransactionAsync(ct))
            {
                var batch = await SisProjectionBatch.LoadAsync(db, options.SourceKey, endpoint.Name, page.Records, projectStudents, ct);
                foreach (var record in page.Records)
                    await UpsertAsync(db, options.SourceKey, endpoint.Name, filters, record, scan, projectStudents, batch, ct);
                await batch.FlushAsync(ct);
                await transaction.CommitAsync(ct);
            }
            db.ChangeTracker.Clear();
            Console.WriteLine($"Stored SIS {endpoint.Name} page: {page.Records.Count} records; {count} scanned in this scope.");
        }
        if (watermark is null) throw new SisApiException("SIS scan returned no envelope.");
        if (endpoint.Name == "schools" && count == 0) throw new SisApiException("Empty SIS school catalog rejected.");
        if (endpoint.Name == "students" && since is null && count == 0 && !options.AllowEmptyStudents)
            throw new SisApiException("Empty SIS student snapshot rejected. Verify scope/year or explicitly configure Sis:AllowEmptyStudents.");
        await using (var transaction = await db.Database.BeginTransactionAsync(ct))
        {
            if (endpoint.Name == "timetable")
            {
                // Buffer the complete scan before replacing: failed pagination never clears a live range.
                var scoped = ScopedRecords(db, options.SourceKey, endpoint.Name, filters);
                await scoped.ExecuteDeleteAsync(ct);
                var batch = await SisProjectionBatch.LoadAsync(db, options.SourceKey, endpoint.Name, timetable, false, ct);
                foreach (var record in timetable)
                    await UpsertAsync(db, options.SourceKey, endpoint.Name, filters, record, scan, false, batch, ct);
                await batch.FlushAsync(ct);
            }
            if (since is null && endpoint.Name is "students" or "schools")
            {
                var missing = await ScopedRecords(db, options.SourceKey, endpoint.Name, filters)
                    .Where(x => x.LastSeenScanId != scan && !x.IsDeleted).ToListAsync(ct);
                foreach (var row in missing)
                {
                    row.IsDeleted = true;
                    row.UpdatedAt = DateTime.UtcNow;
                    if (projectStudents && row.StudentUserId is not null)
                    {
                        var profiles = await db.StudentProfiles.Where(x => x.SchoolOnlineUserId == row.StudentUserId &&
                            x.SchoolOnlineSchoolYearId == row.SchoolYearId).ToListAsync(ct);
                        foreach (var profile in profiles)
                        {
                            profile.SISIsDeleted = true;
                            profile.SISIsActive = false;
                            profile.SISStatus = "OUT_OF_SCOPE";
                            profile.UpdatedAt = DateTime.UtcNow;
                            await db.SISStudentParents.IgnoreQueryFilters().Where(x => x.StudentProfileId == profile.Id)
                                .ExecuteUpdateAsync(s => s.SetProperty(x => x.SISIsDeleted, true), ct);
                        }
                    }
                }
            }
            state = await db.SisSyncStates.SingleOrDefaultAsync(x => x.SourceKey == options.SourceKey &&
                x.Endpoint == endpoint.Name && x.FilterKey == filterKey, ct);
            if (state is null)
            {
                state = new SisSyncState { SourceKey = options.SourceKey, Endpoint = endpoint.Name, FilterKey = filterKey, FiltersJson = filterJson };
                db.SisSyncStates.Add(state);
            }
            state.LastSuccessfulWatermark = watermark;
            state.ProjectsCurrentRoster = endpoint.Name == "students" && projectStudents;
            state.LastCompletedAtUtc = DateTime.UtcNow;
            state.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        db.ChangeTracker.Clear();
        totals.Records += count;
        totals.Scans++;
    }

    private static IQueryable<SisWarehouseRecord> ScopedRecords(AppDbContext db, string source, string endpoint, IReadOnlyDictionary<string, string> filters)
    {
        var query = db.SisWarehouseRecords.Where(x => x.SourceKey == source && x.Endpoint == endpoint);
        if (filters.TryGetValue("school_year_id", out var year)) query = query.Where(x => x.SchoolYearId == year);
        if (filters.TryGetValue("school_id", out var school)) query = query.Where(x => x.SchoolId == school);
        if (filters.TryGetValue("type", out var type)) query = query.Where(x => x.RecordType == type);
        if (filters.TryGetValue("from_date", out var from))
        {
            var start = DateTime.ParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var end = DateTime.ParseExact(filters["to_date"], "yyyy-MM-dd", CultureInfo.InvariantCulture);
            query = query.Where(x => x.RecordDate >= start && x.RecordDate <= end);
        }
        return query;
    }

    private static async Task<bool> HasDailyScopeAsync(AppDbContext db, string source, string endpoint,
        SortedDictionary<string, string> filters, CancellationToken ct)
    {
        var existing = await db.SisSyncStates.AsNoTracking().Where(x => x.SourceKey == source && x.Endpoint == endpoint &&
            x.LastSuccessfulWatermark != null).Select(x => x.FiltersJson).ToListAsync(ct);
        foreach (var json in existing)
        {
            var candidate = JsonSerializer.Deserialize<SortedDictionary<string, string>>(json)!;
            candidate.Remove("from_date");
            candidate.Remove("to_date");
            if (candidate.Count == filters.Count && candidate.All(x => filters.GetValueOrDefault(x.Key) == x.Value)) return true;
        }
        return false;
    }

    private static async Task UpsertAsync(AppDbContext db, string source, string endpoint, IReadOnlyDictionary<string, string> filters,
        JsonElement record, string scan, bool project, SisProjectionBatch batch, CancellationToken ct)
    {
        var key = RecordKey(endpoint, record);
        var updated = SisJson.Text(record, "updated_at", 50);
        if (!SisApiClient.TryTimestamp(updated, out var timestamp)) throw new SisApiException("SIS record is missing a valid updated_at timestamp.");
        var school = SisJson.RefId(record, "school");
        var year = SisJson.RefId(record, "school_year");
        if (endpoint == "students" && (school is null || year is null))
            throw new SisApiException("SIS student response is missing its school or school_year reference.");
        if (endpoint == "students") SisJson.RequiredBool(record, "is_active");
        var type = SisJson.Text(record, "type", 50);
        var date = SisJson.Date(record, "date");
        if (filters.TryGetValue("school_id", out var schoolFilter) && school is not null && school != schoolFilter ||
            filters.TryGetValue("school_year_id", out var yearFilter) && year is not null && year != yearFilter ||
            filters.TryGetValue("type", out var typeFilter) && type != typeFilter ||
            filters.TryGetValue("from_date", out var from) && (date is null ||
                date < DateTime.ParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture) ||
                date > DateTime.ParseExact(filters["to_date"], "yyyy-MM-dd", CultureInfo.InvariantCulture)))
            throw new SisApiException("SIS returned a record outside the requested filters.");
        batch.Warehouse.TryGetValue(key, out var row);
        if (row is null)
        {
            row = new SisWarehouseRecord { SourceKey = source, Endpoint = endpoint, RecordKey = key, SourceId = SisJson.RequiredId(record) };
            batch.Warehouse.Add(key, row);
        }
        row.LastSeenScanId = scan;
        // A replay must not roll back a newer committed source document.
        if (row.SourceUpdatedAtUtc > timestamp.UtcDateTime) return;
        row.SchoolId = school ?? filters.GetValueOrDefault("school_id");
        row.SchoolYearId = year ?? filters.GetValueOrDefault("school_year_id");
        row.StudentUserId = endpoint == "students" ? SisJson.RequiredId(record, "student_user_id") : SisJson.Text(SisJson.Child(record, "student"), "user_id");
        row.RecordType = type;
        row.RecordDate = date;
        row.IsDeleted = SisJson.RequiredBool(record, "is_deleted");
        row.IsActive = SisJson.Bool(record, "is_active");
        row.SourceUpdatedAtUtc = timestamp.UtcDateTime;
        row.PayloadJson = record.GetRawText();
        row.UpdatedAt = DateTime.UtcNow;
        if (project && endpoint == "students") await new SisStudentProjector(db, batch.Students, batch.Parents).ProjectAsync(record, ct);
        if (endpoint == "health/visits")
            await new SisHealthVisitProjector(db, batch.Students, batch.Visits, batch.ChangedVisits,
                () => batch.HealthReportsChanged = true).ProjectAsync(source, record, ct);
    }

    internal static string RecordKey(string endpoint, JsonElement record) => endpoint == "attendances"
        ? (SisJson.Text(record, "type", 50) ?? throw new SisApiException("SIS attendance is missing type.")) + ":" + SisJson.RequiredId(record)
        : SisJson.RequiredId(record);

    // Always whole calendar months: recurring schedules reuse the same filter/watermark keys.
    internal static IEnumerable<(DateOnly From, DateOnly To)> MonthWindows(DateOnly from, DateOnly to)
    {
        for (var month = new DateOnly(from.Year, from.Month, 1); month <= to; month = month.AddMonths(1))
            yield return (month, month.AddMonths(1).AddDays(-1));
    }

    public static string SafeError(Exception ex)
    {
        if (ex is SisApiException) return ex.Message;
        if (ex is OperationCanceledException) return "SIS sync cancelled.";
        for (Exception? current = ex; current is not null; current = current.InnerException)
            if (current is MySqlException mysql)
                return $"SIS database operation failed ({ex.GetType().Name}; MySQL error {mysql.Number}). Credentials, SQL text and parameters suppressed.";
        return $"SIS sync failed ({ex.GetType().Name}); credentials, response data and database parameters suppressed.";
    }
    private sealed record SchoolYear(string Id, DateTime? Start, DateTime? End);
    private sealed class Totals { public int Records; public int Scans; }
}
