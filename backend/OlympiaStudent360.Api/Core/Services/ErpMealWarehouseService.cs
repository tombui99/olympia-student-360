using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Core.Services;

public static class ErpMealFeeds
{
    public const string MissingTicket = "missing_ticket";
    public const string MissingOrder = "missing_order";
    public const string Canteen = "canteen";
}

// SourceRecordKey is the ERP record id when available. An export adapter can use a
// file fingerprint and row number instead. It must be stable when the same source is retried.
public sealed record ErpMealStatusInput(
    string SourceRecordKey, string ErpStudentId, string? StudentName, string? Email,
    string? ClassCode, DateTime MealDate, string Meal, string? OrderStatus,
    string? TicketStatus);

public sealed record ErpCanteenPurchaseInput(
    string SourceRecordKey, string ErpStudentId, string? StudentName, string? ClassCode,
    DateTime PurchaseDate, string? ProductCode, string? ProductName, int Quantity);

public sealed class ErpMealWarehouseService(AppDbContext db)
{
    /// <summary>Revisit rows imported before their SIS profile existed.</summary>
    public async Task<int> RelinkUnmatchedAsync(CancellationToken cancellationToken = default)
    {
        var identity = await LoadIdentityAsync(cancellationToken);
        var updated = 0;
        var statuses = await db.ErpMealStatuses
            .Where(x => x.StudentProfileId == null).ToListAsync(cancellationToken);
        foreach (var row in statuses)
        {
            var match = identity.Match(row.ErpStudentId);
            if (row.StudentProfileId == match.StudentProfileId && row.MatchStatus == match.Status) continue;
            row.StudentProfileId = match.StudentProfileId;
            row.MatchStatus = match.Status;
            updated++;
        }
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();

        var purchases = await db.ErpCanteenPurchases
            .Where(x => x.StudentProfileId == null).ToListAsync(cancellationToken);
        foreach (var row in purchases)
        {
            var match = identity.Match(row.ErpStudentId);
            if (row.StudentProfileId == match.StudentProfileId && row.MatchStatus == match.Status) continue;
            row.StudentProfileId = match.StudentProfileId;
            row.MatchStatus = match.Status;
            updated++;
        }
        await db.SaveChangesAsync(cancellationToken);
        db.ChangeTracker.Clear();
        if (updated > 0)
        {
            db.ErpMealSyncRuns.Add(new ErpMealSyncRun
            {
                Feed = "relink", SourceDescription = "SIS student-code relink",
                Status = "completed", UpdatedRows = updated,
                StartedAtUtc = DateTime.UtcNow, CompletedAtUtc = DateTime.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        return updated;
    }

    public async Task<ErpMealSyncRun> ImportStatusesAsync(
        string feed, string sourceDescription, IReadOnlyCollection<ErpMealStatusInput> source,
        CancellationToken cancellationToken = default)
    {
        if (feed is not (ErpMealFeeds.MissingTicket or ErpMealFeeds.MissingOrder))
            throw new ArgumentException("Unknown meal-status feed", nameof(feed));
        ValidateKeys(source.Select(x => x.SourceRecordKey));
        var identity = await LoadIdentityAsync(cancellationToken);
        var run = NewRun(feed, sourceDescription, source.Count);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.ErpMealSyncRuns.Add(run);
            foreach (var batch in source.Chunk(500))
            {
                var keys = batch.Select(x => x.SourceRecordKey).ToArray();
                var existing = await db.ErpMealStatuses
                    .Where(x => x.Feed == feed && keys.Contains(x.SourceRecordKey))
                    .ToDictionaryAsync(x => x.SourceRecordKey, cancellationToken);
                foreach (var item in batch)
                {
                    var match = identity.Match(item.ErpStudentId);
                    if (!existing.TryGetValue(item.SourceRecordKey, out var row))
                    {
                        row = new ErpMealStatus { Feed = feed, SourceRecordKey = item.SourceRecordKey };
                        db.ErpMealStatuses.Add(row);
                        run.InsertedRows++;
                    }
                    else run.UpdatedRows++;

                    row.ErpStudentId = item.ErpStudentId.Trim();
                    row.StudentProfileId = match.StudentProfileId;
                    row.MatchStatus = match.Status;
                    row.StudentName = item.StudentName;
                    row.Email = item.Email;
                    row.ClassCode = item.ClassCode;
                    row.MealDate = item.MealDate.Date;
                    row.Meal = item.Meal;
                    row.OrderStatus = item.OrderStatus;
                    row.TicketStatus = item.TicketStatus;
                    row.ImportedAtUtc = DateTime.UtcNow;
                    CountMatch(run, match.Status);
                }
                await db.SaveChangesAsync(cancellationToken);
                db.ChangeTracker.Clear();
                db.ErpMealSyncRuns.Attach(run);
            }
            run.Status = "completed";
            run.CompletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return run;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            await SaveFailedRunAsync(run, ex, cancellationToken);
            throw;
        }
    }

    public async Task<ErpMealSyncRun> ImportPurchasesAsync(
        string sourceDescription, IReadOnlyCollection<ErpCanteenPurchaseInput> source,
        CancellationToken cancellationToken = default)
    {
        ValidateKeys(source.Select(x => x.SourceRecordKey));
        var identity = await LoadIdentityAsync(cancellationToken);
        var run = NewRun(ErpMealFeeds.Canteen, sourceDescription, source.Count);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            db.ErpMealSyncRuns.Add(run);
            foreach (var batch in source.Chunk(500))
            {
                var keys = batch.Select(x => x.SourceRecordKey).ToArray();
                var existing = await db.ErpCanteenPurchases
                    .Where(x => keys.Contains(x.SourceRecordKey))
                    .ToDictionaryAsync(x => x.SourceRecordKey, cancellationToken);
                foreach (var item in batch)
                {
                    var match = identity.Match(item.ErpStudentId);
                    if (!existing.TryGetValue(item.SourceRecordKey, out var row))
                    {
                        row = new ErpCanteenPurchase { SourceRecordKey = item.SourceRecordKey };
                        db.ErpCanteenPurchases.Add(row);
                        run.InsertedRows++;
                    }
                    else run.UpdatedRows++;

                    row.ErpStudentId = item.ErpStudentId.Trim();
                    row.StudentProfileId = match.StudentProfileId;
                    row.MatchStatus = match.Status;
                    row.StudentName = item.StudentName;
                    row.ClassCode = item.ClassCode;
                    row.PurchaseDate = item.PurchaseDate.Date;
                    row.ProductCode = item.ProductCode;
                    row.ProductName = item.ProductName;
                    row.Quantity = item.Quantity;
                    row.ImportedAtUtc = DateTime.UtcNow;
                    CountMatch(run, match.Status);
                }
                await db.SaveChangesAsync(cancellationToken);
                db.ChangeTracker.Clear();
                db.ErpMealSyncRuns.Attach(run);
            }
            run.Status = "completed";
            run.CompletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return run;
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            db.ChangeTracker.Clear();
            await SaveFailedRunAsync(run, ex, cancellationToken);
            throw;
        }
    }

    private async Task SaveFailedRunAsync(ErpMealSyncRun run, Exception exception, CancellationToken ct)
    {
        run.Id = 0;
        run.Status = "failed";
        run.Error = exception.Message[..Math.Min(exception.Message.Length, 2000)];
        run.CompletedAtUtc = DateTime.UtcNow;
        db.ErpMealSyncRuns.Add(run);
        await db.SaveChangesAsync(ct);
    }

    private async Task<IdentityMap> LoadIdentityAsync(CancellationToken ct)
    {
        var profiles = await db.StudentProfiles.AsNoTracking()
            .Select(x => new IdentityProfile(x.Id, x.SISStudentCode))
            .ToListAsync(ct);
        return new IdentityMap(profiles);
    }

    private static ErpMealSyncRun NewRun(string feed, string description, int count) => new()
    {
        Feed = feed, SourceDescription = description, Status = "running",
        ReceivedRows = count, StartedAtUtc = DateTime.UtcNow
    };

    private static void ValidateKeys(IEnumerable<string> keys)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Length > 128 || !seen.Add(key))
                throw new InvalidOperationException("Source record keys must be nonempty, unique within a feed, and at most 128 characters.");
        }
    }

    private static void CountMatch(ErpMealSyncRun run, string status)
    {
        if (status == "unmatched") run.UnmatchedRows++;
        if (status == "conflict") run.ConflictRows++;
    }
}

public sealed record IdentityProfile(int Id, string? StudentCode);
public sealed record IdentityMatch(int? StudentProfileId, string Status);

/// <summary>Links ERP student IDs only to the authoritative SISStudentCode.</summary>
public sealed class IdentityMap
{
    private readonly Dictionary<string, int?> _codes;

    public IdentityMap(IEnumerable<IdentityProfile> profiles)
    {
        _codes = profiles.Select(x => (Key: NormalizeCode(x.StudentCode), x.Id))
            .Where(x => x.Key is not null)
            .GroupBy(x => x.Key!, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Count() == 1 ? (int?)x.First().Id : null,
                StringComparer.OrdinalIgnoreCase);
    }

    public IdentityMatch Match(string? erpStudentId)
    {
        var code = NormalizeCode(erpStudentId);
        if (code is not null && _codes.TryGetValue(code, out var profileId))
            return profileId is { } id
                ? new(id, "student_code")
                : new(null, "conflict");
        return new(null, "unmatched");
    }

    private static string? NormalizeCode(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
