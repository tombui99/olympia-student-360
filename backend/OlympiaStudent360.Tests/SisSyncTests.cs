using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Controllers;
using OlympiaStudent360.Api.Data;
using OlympiaStudent360.Api.SisSync;
using Xunit;

namespace OlympiaStudent360.Tests;

public sealed class SisApiClientTests
{
    [Fact]
    public async Task MissingScopeFailsBeforeAnyDataRequest()
    {
        var count = 0;
        using var http = new HttpClient(new SisHandler((_, _) =>
        {
            count++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(
                """{"access_token":"test","token_type":"Bearer","expires_in":3600,"scope":"catalog.read"}""") });
        }));
        var error = await Assert.ThrowsAsync<SisApiException>(() => new SisApiClient(http, SisFixtures.Options()).CheckScopesAsync(default));
        Assert.Contains("students.read", error.Message);
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task PaginationPreservesFiltersEncodesOffsetsAndReusesToken()
    {
        var requests = new List<Uri>();
        var tokens = 0;
        using var http = new HttpClient(new SisHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/oauth/token")
            {
                tokens++;
                Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
                return Task.FromResult(SisFixtures.Token());
            }
            requests.Add(request.RequestUri);
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            return Task.FromResult(SisFixtures.Page([], requests.Count == 1, requests.Count == 1 ? "opaque+cursor=" : null));
        }));
        var client = new SisApiClient(http, SisFixtures.Options());
        var pages = new List<SisPage>();
        await foreach (var page in client.ReadAsync("students", new Dictionary<string, string> { ["school_year_id"] = "278" },
            "2026-10-30T08:00:00+07:00", default)) pages.Add(page);
        Assert.Equal(2, pages.Count);
        Assert.Equal(1, tokens);
        Assert.Contains("%2B07%3A00", requests[0].Query);
        Assert.All(requests, x => Assert.Contains("school_year_id=278", x.Query));
        Assert.Contains("cursor=opaque%2Bcursor%3D", requests[1].Query);
        Assert.DoesNotContain("updated_since", requests[1].Query);
    }

    [Fact]
    public async Task Refreshes401OnceAndNeverLeaksErrorBody()
    {
        var tokens = 0;
        var reads = 0;
        using var http = new HttpClient(new SisHandler((request, _) => Task.FromResult(
            request.RequestUri!.AbsolutePath == "/oauth/token" ? (++tokens > 0 ? SisFixtures.Token() : throw new Exception())
            : (++reads > 0 ? new HttpResponseMessage(HttpStatusCode.Unauthorized) { Content = new StringContent("secret-student-data") } : throw new Exception()))));
        var client = new SisApiClient(http, SisFixtures.Options());
        var error = await Assert.ThrowsAsync<SisApiException>(async () =>
        { await foreach (var _ in client.ReadAsync("students", new Dictionary<string, string>(), null, default)) { } });
        Assert.Equal(2, tokens);
        Assert.Equal(2, reads);
        Assert.DoesNotContain("secret-student-data", error.Message);
        Assert.Contains("401", error.Message);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(503)]
    public async Task RetriesRetryAfterResponses(int status)
    {
        var reads = 0;
        using var http = new HttpClient(new SisHandler((request, _) =>
        {
            if (request.RequestUri!.AbsolutePath == "/oauth/token") return Task.FromResult(SisFixtures.Token());
            if (++reads > 1) return Task.FromResult(SisFixtures.Page([]));
            var response = new HttpResponseMessage((HttpStatusCode)status);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero);
            return Task.FromResult(response);
        }));
        await foreach (var _ in new SisApiClient(http, SisFixtures.Options()).ReadAsync("schools", new Dictionary<string, string>(), null, default)) { }
        Assert.Equal(2, reads);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RejectsRepeatedCursorOrChangedWatermark(bool repeatCursor)
    {
        var reads = 0;
        using var http = new HttpClient(new SisHandler((request, _) => Task.FromResult(request.RequestUri!.AbsolutePath == "/oauth/token"
            ? SisFixtures.Token() : SisFixtures.Page([], true, "same", ++reads > 1 && !repeatCursor ? "2026-11-02T09:00:00+07:00" : SisFixtures.Watermark))));
        await Assert.ThrowsAsync<SisApiException>(async () =>
        { await foreach (var _ in new SisApiClient(http, SisFixtures.Options()).ReadAsync("schools", new Dictionary<string, string>(), null, default)) { } });
    }

    [Fact]
    public void AttendanceKeysIncludeTypeAndIdsStayStrings()
    {
        using var first = JsonDocument.Parse("""{"id":"9007199254740993123","type":"HOMEROOM"}""");
        using var second = JsonDocument.Parse("""{"id":"9007199254740993123","type":"COURSE"}""");
        Assert.NotEqual(SisSyncRunner.RecordKey("attendances", first.RootElement), SisSyncRunner.RecordKey("attendances", second.RootElement));
        Assert.Equal("9007199254740993123", SisSyncRunner.RecordKey("students", first.RootElement));
        var months = SisSyncRunner.MonthWindows(new DateOnly(2026, 10, 15), new DateOnly(2026, 11, 3)).ToArray();
        Assert.Equal(new DateOnly(2026, 10, 1), months[0].From);
        Assert.Equal(new DateOnly(2026, 11, 30), months[1].To);
        Assert.All(months, x => Assert.InRange(x.To.DayNumber - x.From.DayNumber + 1, 1, 31));
    }
}

public sealed partial class MySqlResilienceTests
{
    [MySqlFact]
    public async Task SisHealthVisitsPopulatePageAndReportsAndRejectLocalEditsAndDeletes()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        db.StudentHealthVisits.Add(new StudentHealthVisit { FullName = "Manual student", ExamTime = new DateTime(2026, 11, 2), Symptoms = "Manual symptoms" });
        await db.SaveChangesAsync();
        var options = SisFixtures.Options(health: true);
        async Task Sync(object[] visits, bool full = false)
        {
            using var http = SisFixtures.Http([SisFixtures.Student()], healthVisits: visits);
            await new SisSyncRunner().RunAsync(db, http, options, full, default);
            db.ChangeTracker.Clear();
        }
        await Sync([SisFixtures.HealthVisit()], true);
        var visit = await db.StudentHealthVisits.SingleAsync(x => x.SisVisitId == "visit-1");
        var id = visit.Id;
        Assert.NotNull(visit.StudentProfileId);
        Assert.Equal(new DateTime(2026, 11, 2, 1, 30, 0), visit.ExamTime);
        Assert.Equal("Bình thường", visit.Classification);
        visit.Notes = "School follow-up";
        visit.NotificationSent = true;
        visit.ExaminedBy = "School nurse";
        visit.CustomFieldsJson = """{"follow_up":"Local detail"}""";
        await db.SaveChangesAsync();
        await Sync([SisFixtures.HealthVisit(symptoms: "Updated source symptoms", notes: "Updated source notes")]);
        visit = await db.StudentHealthVisits.SingleAsync(x => x.SisVisitId == "visit-1");
        Assert.Equal(id, visit.Id);
        Assert.Equal("Updated source symptoms", visit.Symptoms);
        Assert.Equal("Updated source notes", visit.Notes);
        Assert.False(visit.NotificationSent);
        Assert.Null(visit.ExaminedBy);
        Assert.Contains("Local detail", visit.CustomFieldsJson);
        Assert.Equal(1, await db.StudentHealthVisits.CountAsync());
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        accessor.HttpContext.Items[UserAccessService.HttpContextItemKey] = new UserAccessSnapshot("admin", "Admin", new HashSet<string>(), [], []);
        var controller = new ModuleRecordsController(db, new UserAccessService(db, accessor), new StudentRecordFieldOptionService(db));
        var page = Assert.IsType<ModuleRecordsPageDto>(Assert.IsType<OkObjectResult>((await controller.List("student-health-visits")).Result).Value);
        var displayed = page.Items.Single(x => x.Id == id);
        Assert.Equal("SchoolOnline SIS", displayed.EnteredByName);
        Assert.Equal("Updated source notes", displayed.Values["notes"]);
        await new ImportedReportAggregateService(db, new CanvasReportingPeriodService(db)).RebuildAsync();
        Assert.Equal(1, await db.ImportedReportAggregates.Where(x => x.Source == "student-health-visits").SumAsync(x => x.RecordCount));
        await Sync([SisFixtures.HealthVisit(deleted: true)]);
        Assert.Equal(0, await db.StudentHealthVisits.CountAsync());
        Assert.True((await db.StudentHealthVisits.IgnoreQueryFilters().SingleAsync(x => x.Id == id)).SisIsDeleted);
        await new ImportedReportAggregateService(db, new CanvasReportingPeriodService(db)).RebuildAsync();
        Assert.Equal(0, await db.ImportedReportAggregates.Where(x => x.Source == "student-health-visits").SumAsync(x => x.RecordCount));
        await Sync([SisFixtures.HealthVisit()]);
        Assert.Equal(1, await db.StudentHealthVisits.CountAsync());
        Assert.IsType<ConflictObjectResult>(await controller.Update("student-health-visits", id,
            new UpdateModuleRecordDto { Values = new() { ["notes"] = "Cannot overwrite SIS data" } }));
        Assert.IsType<ConflictObjectResult>(await controller.Delete("student-health-visits", id));
        await Sync([SisFixtures.HealthVisit()], true);
        Assert.Equal(1, await db.StudentHealthVisits.CountAsync());
        Assert.Equal("Source notes", (await db.StudentHealthVisits.SingleAsync(x => x.Id == id)).Notes);
        var manual = await db.StudentHealthVisits.IgnoreQueryFilters().SingleAsync(x => x.SisVisitId == null);
        Assert.Equal("Manual symptoms", manual.Symptoms);
        Assert.IsType<ConflictObjectResult>(await controller.Delete("student-health-visits", manual.Id));
        var access = new UserAccessService(db, accessor);
        var fields = new StudentRecordFieldOptionService(db);
        var students = new StudentsController(db, new CanvasReportingPeriodService(db), access, fields);
        Assert.IsType<ConflictObjectResult>(students.CreateHealthVisit(visit.StudentProfileId!.Value, new CreateStudentHealthVisitDto()).Result);
        Assert.IsType<ConflictObjectResult>(new ImportsController(db, access, fields).ImportHealthVisits(new ExcelHealthVisitImportDto()).Result);
        var customFields = new ImportModuleFieldsController(db);
        Assert.Empty(Assert.IsType<List<ImportModuleFieldDto>>(Assert.IsType<OkObjectResult>((await customFields.List("student-health-visits")).Result).Value));
        Assert.IsType<ConflictObjectResult>((await customFields.Create("student-health-visits", new SaveImportModuleFieldDto())).Result);
        Assert.Equal(2, await db.StudentHealthVisits.IgnoreQueryFilters().CountAsync());
    }

    [MySqlTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SisDeletedAccountNeverClaimsAnActiveStudentsEmailAlias(bool knownRetiredProfile)
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        if (knownRetiredProfile)
        {
            db.StudentProfiles.Add(new StudentProfile { SISUserId = "retired", SISStudentCode = "OS-RETIRED", FullName = "Retired student" });
            await db.SaveChangesAsync();
        }
        var retired = JsonSerializer.SerializeToNode(SisFixtures.Student(deleted: true))!;
        retired["id"] = "9007199254740993000";
        retired["student_user_id"] = "retired-user";
        retired["student_code"] = "OS-RETIRED";
        retired["username"] = "retired";
        retired["email"] = "student@old-domain.test";
        // Put the retired account first to prove source ordering cannot take the active alias.
        using var http = SisFixtures.Http([retired, SisFixtures.Student()]);
        await new SisSyncRunner().RunAsync(db, http, SisFixtures.Options(), true, default);
        db.ChangeTracker.Clear();
        var active = await db.StudentProfiles.SingleAsync(x => x.SISStudentCode == "OS001");
        Assert.Equal("student", active.SISUserId);
        Assert.False(active.SISIsDeleted);
        Assert.Equal(knownRetiredProfile ? 2 : 1, await db.StudentProfiles.CountAsync());
        Assert.Equal(2, await db.SisWarehouseRecords.CountAsync(x => x.Endpoint == "students"));
        if (knownRetiredProfile)
        {
            var retiredProfile = await db.StudentProfiles.SingleAsync(x => x.SISStudentCode == "OS-RETIRED");
            Assert.Equal("retired", retiredProfile.SISUserId);
            Assert.True(retiredProfile.SISIsDeleted);
        }
    }

    [MySqlFact]
    public async Task SisInterruptedSchemaMigrationResumesWithoutDroppingCommittedData()
    {
        await using var test = await TestDatabase.CreateAsync(targetMigration: "20261102094148_IndexTranscriptAssessmentEvidence");
        await using var db = test.Open();
        const string id = "20261103090437_AddSchoolOnlineSisWarehouse";
        var assembly = db.GetService<IMigrationsAssembly>();
        var migration = assembly.CreateMigration(assembly.Migrations[id], db.Database.ProviderName!);
        var model = db.GetService<IModelRuntimeInitializer>().Initialize(migration.TargetModel);
        var generator = db.GetService<IMigrationsSqlGenerator>();
        // Simulate a process interrupted after the additive columns and first table committed.
        foreach (var operation in migration.UpOperations.Take(7))
            foreach (var command in generator.Generate([operation], model))
                await db.Database.ExecuteSqlRawAsync(command.CommandText);
        db.SisSyncRuns.Add(new SisSyncRun { SourceKey = "retained", Mode = "full", Status = "completed", StartedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        await db.MigrateWithRecoveryAsync();
        Assert.Contains(id, await db.Database.GetAppliedMigrationsAsync());
        Assert.Equal("retained", (await db.SisSyncRuns.SingleAsync()).SourceKey);
        Assert.Empty(await db.SisSyncStates.ToListAsync());
        Assert.Empty(await db.SisWarehouseRecords.ToListAsync());
    }

    [MySqlFact]
    public async Task SisHistoricalBackfillDoesNotReplaceCurrentStudentOrParents()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        using (var http = SisFixtures.Http([SisFixtures.Student()]))
            await new SisSyncRunner().RunAsync(db, http, SisFixtures.Options(), true, default);
        using (var http = SisFixtures.Http([SisFixtures.Student(year: "277")]))
            await new SisSyncRunner().RunAsync(db, http, SisFixtures.Options(schoolYearId: "277"), true, default);
        var student = await db.StudentProfiles.SingleAsync();
        Assert.Equal("278", student.SchoolOnlineSchoolYearId);
        Assert.Equal("New student", student.FullName);
        Assert.Single(await db.SISStudentParents.ToListAsync());
        Assert.Equal(2, await db.SisWarehouseRecords.CountAsync(x => x.Endpoint == "students"));
    }

    [MySqlFact]
    public async Task SisUpdatesExistingRosterPreservesCanvasAndParentNotesAndAppliesTombstones()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        var student = new StudentProfile { SISUserId = "student", SISStudentCode = "OS001", FullName = "Old", CanvasUserId = 77, Campus = "Manual campus", Program = "Manual programme", EducationLevel = "Stale level" };
        db.StudentProfiles.Add(student);
        await db.SaveChangesAsync();
        var parent = new SISStudentParent { Student = student, SISUserId = "student", Relationship = "Cha", FullName = "Old parent", AgeGroup = "Stale age", EmploymentSector = "Stale sector" };
        db.SISStudentParents.Add(parent);
        await db.SaveChangesAsync();
        db.StudentParentNotes.Add(new StudentParentNote { SISStudentParentId = parent.Id, Notes = "Staff context", UpdatedByName = "Test staff", UpdatedByUserId = "test-staff" });
        await db.SaveChangesAsync();
        var options = SisFixtures.Options();
        using (var http = SisFixtures.Http([SisFixtures.Student()]))
            await new SisSyncRunner().RunAsync(db, http, options, true, default);
        db.ChangeTracker.Clear();
        student = await db.StudentProfiles.SingleAsync();
        Assert.Equal(77, student.CanvasUserId);
        Assert.Equal("120345", student.SchoolOnlineUserId);
        Assert.Equal("6", student.GradeLevel);
        Assert.Equal(TeacherSchoolLevels.LowerSecondary, student.EducationLevel);
        Assert.Equal("Manual campus", student.Campus);
        Assert.Equal("Manual programme", student.Program);
        Assert.Null((await db.SISStudentParents.SingleAsync()).AgeGroup);
        Assert.Null((await db.SISStudentParents.SingleAsync()).EmploymentSector);
        Assert.Equal("New student", student.FullName);
        Assert.Equal("New parent", (await db.SISStudentParents.SingleAsync()).FullName);
        Assert.Equal("Staff context", (await db.StudentParentNotes.SingleAsync()).Notes);
        using (var http = SisFixtures.Http([SisFixtures.Student(deleted: true, father: false)], "2026-11-02T08:00:00+07:00"))
            await new SisSyncRunner().RunAsync(db, http, options, false, default);
        db.ChangeTracker.Clear();
        Assert.True((await db.StudentProfiles.SingleAsync()).SISIsDeleted);
        Assert.Empty(await db.SISStudentParents.ToListAsync());
        Assert.Single(await db.SISStudentParents.IgnoreQueryFilters().ToListAsync());
        Assert.Single(await db.StudentParentNotes.ToListAsync());
        Assert.Equal(2, await db.SisSyncStates.CountAsync());
        Assert.All(await db.SisSyncRuns.ToListAsync(), x => Assert.Equal("completed", x.Status));
    }

    [MySqlFact]
    public async Task SisFailedPaginationKeepsOldWatermarkAndReplayIsIdempotent()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        var options = SisFixtures.Options();
        using (var http = SisFixtures.Http([SisFixtures.Student()]))
            await new SisSyncRunner().RunAsync(db, http, options, true, default);
        using (var http = SisFixtures.Http([SisFixtures.Student()], "2026-11-02T08:00:00+07:00", failSecondPage: true))
            await Assert.ThrowsAsync<SisApiException>(() => new SisSyncRunner().RunAsync(db, http, options, false, default));
        db.ChangeTracker.Clear();
        Assert.Equal(SisFixtures.Watermark, (await db.SisSyncStates.SingleAsync(x => x.Endpoint == "students")).LastSuccessfulWatermark);
        var requestedSince = new List<string>();
        using (var http = SisFixtures.Http([SisFixtures.Student()], "2026-11-02T08:00:00+07:00", queries: requestedSince))
            await new SisSyncRunner().RunAsync(db, http, options, false, default);
        Assert.Contains(requestedSince, x => x.Contains("updated_since=2026-11-01T08%3A00%3A00%2B07%3A00"));
        Assert.Equal(1, await db.StudentProfiles.CountAsync());
        Assert.Equal(1, await db.SisWarehouseRecords.CountAsync(x => x.Endpoint == "students"));
    }

    [MySqlFact]
    public async Task SisTimetableReplacementWaitsForCompleteScanAndHandlesEmptyRange()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        var options = SisFixtures.Options(timetable: true);
        using (var http = SisFixtures.Http([SisFixtures.Student()], timetable: [SisFixtures.Lesson("old")]))
            await new SisSyncRunner().RunAsync(db, http, options, true, default);
        using (var http = SisFixtures.Http([SisFixtures.Student()], timetable: [SisFixtures.Lesson("new")], failTimetable: true))
            await Assert.ThrowsAsync<SisApiException>(() => new SisSyncRunner().RunAsync(db, http, options, false, default));
        Assert.Equal("old", (await db.SisWarehouseRecords.SingleAsync(x => x.Endpoint == "timetable")).SourceId);
        using (var http = SisFixtures.Http([SisFixtures.Student()], timetable: []))
            await new SisSyncRunner().RunAsync(db, http, options, false, default);
        Assert.Empty(await db.SisWarehouseRecords.Where(x => x.Endpoint == "timetable").ToListAsync());
    }

    [MySqlTheory]
    [InlineData("is_active")]
    [InlineData("school")]
    [InlineData("school_year")]
    [InlineData("wrong_school_year")]
    public async Task SisMalformedStudentDoesNotDeactivateRosterOrAdvanceWatermark(string field)
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        using (var http = SisFixtures.Http([SisFixtures.Student()]))
            await new SisSyncRunner().RunAsync(db, http, SisFixtures.Options(), true, default);
        var malformed = JsonSerializer.SerializeToNode(SisFixtures.Student())!.AsObject();
        if (field == "wrong_school_year") malformed["school_year"]!["id"] = "277";
        else malformed.Remove(field);
        using (var http = SisFixtures.Http([malformed], watermark: "2026-11-02T08:00:00+07:00"))
            await Assert.ThrowsAsync<SisApiException>(() => new SisSyncRunner().RunAsync(db, http, SisFixtures.Options(), true, default));
        db.ChangeTracker.Clear();
        Assert.Equal(SisFixtures.Watermark, (await db.SisSyncStates.SingleAsync(x => x.Endpoint == "students")).LastSuccessfulWatermark);
        var student = await db.StudentProfiles.CurrentRoster(db).SingleAsync();
        Assert.Equal("278", student.SchoolOnlineSchoolYearId);
        Assert.True(student.SISIsActive);
        Assert.False(student.SISIsDeleted);
    }

    [MySqlFact]
    public async Task SisIdentityConflictRollsBackPage()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        db.StudentProfiles.AddRange(
            new StudentProfile { SISUserId = "other", SISStudentCode = "OS001", FullName = "Code owner" },
            new StudentProfile { SISUserId = "student", FullName = "Username owner" });
        await db.SaveChangesAsync();
        using (var http = SisFixtures.Http([SisFixtures.Student()]))
            await Assert.ThrowsAsync<SisApiException>(() => new SisSyncRunner().RunAsync(db, http, SisFixtures.Options(), true, default));
        Assert.False(await db.SisSyncStates.AnyAsync(x => x.Endpoint == "students"));
        Assert.False(await db.SisWarehouseRecords.AnyAsync(x => x.Endpoint == "students"));
        Assert.Equal(2, await db.StudentProfiles.CountAsync());
    }

    [MySqlFact]
    public async Task SisCurrentRosterExcludesLegacyInactiveAndPriorYearStudentsAndKeepsHistory()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        db.StudentProfiles.AddRange(
            new StudentProfile { SISUserId = "legacy", FullName = "Legacy student" },
            new StudentProfile { SISUserId = "old-year", FullName = "Prior year student", SchoolOnlineUserId = "prior-user",
                SchoolOnlineSchoolId = "10", SchoolOnlineSchoolYearId = "277", SISIsActive = true });
        await db.SaveChangesAsync();
        Assert.Equal(2, await db.StudentProfiles.CurrentRoster(db).CountAsync());
        using (var http = SisFixtures.Http([SisFixtures.Student()]))
            await new SisSyncRunner().RunAsync(db, http, SisFixtures.Options(), true, default);
        db.ChangeTracker.Clear();
        Assert.Equal("student", (await db.StudentProfiles.CurrentRoster(db).SingleAsync()).SISUserId);
        Assert.Equal(3, await db.StudentProfiles.CountAsync());
        Assert.False((await db.StudentProfiles.SingleAsync(x => x.SISUserId == "old-year")).SISIsActive);
        Assert.True((await db.SisSyncStates.SingleAsync(x => x.Endpoint == "students")).ProjectsCurrentRoster);
        var access = new UserAccessService(db, new HttpContextAccessor());
        var controller = new StudentsController(db, new CanvasReportingPeriodService(db), access, new StudentRecordFieldOptionService(db));
        var search = Assert.IsType<List<ManualEntryStudentDto>>(Assert.IsType<OkObjectResult>((await controller.SearchManualEntryStudents("student")).Result).Value);
        Assert.Single(search);
        var student = await db.StudentProfiles.SingleAsync(x => x.SISUserId == "student");
        Assert.IsType<OkObjectResult>(await controller.SaveLocalDetails(student.Id, new UpdateStudentLocalDetailsDto { Campus = "New campus", Program = "New programme" }));
        using (var http = SisFixtures.Http([SisFixtures.Student()]))
            await new SisSyncRunner().RunAsync(db, http, SisFixtures.Options(), true, default);
        db.ChangeTracker.Clear();
        student = await db.StudentProfiles.SingleAsync(x => x.SISUserId == "student");
        Assert.Equal("New campus", student.Campus);
        Assert.Equal("New programme", student.Program);
        var inactive = JsonSerializer.SerializeToNode(SisFixtures.Student())!;
        inactive["is_active"] = false;
        using (var http = SisFixtures.Http([inactive]))
            await new SisSyncRunner().RunAsync(db, http, SisFixtures.Options(), false, default);
        Assert.Empty(await db.StudentProfiles.CurrentRoster(db).ToListAsync());
        Assert.Equal(3, await db.StudentProfiles.CountAsync());
    }

    [MySqlFact]
    public async Task SisLegacyDashboardMetricsAreArchivedAndCannotReappearInCatalogOrValues()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        var retired = new DashboardMetric { Key = "legacy-sis-metric", Label = "Old SIS", Domain = "Attendance", Source = " SIS " };
        var retained = new DashboardMetric { Key = "base-metric", Label = "ERP", Domain = "Meals", Source = "BaseERP" };
        db.DashboardMetricObservations.AddRange(
            new DashboardMetricObservation { Metric = retired, Value = 999, Campus = "Old SIS campus", PeriodStart = new DateTime(2026, 11, 1), PeriodEnd = new DateTime(2026, 12, 1) },
            new DashboardMetricObservation { Metric = retained, Value = 5, PeriodStart = new DateTime(2026, 11, 1), PeriodEnd = new DateTime(2026, 12, 1) });
        await db.SaveChangesAsync();
        Assert.Equal("base-metric", (await db.DashboardMetrics.SingleAsync()).Key);
        Assert.Equal(5, (await db.DashboardMetricObservations.SingleAsync()).Value);
        Assert.Equal(2, await db.DashboardMetricObservations.IgnoreQueryFilters().CountAsync());
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var access = new UserAccessService(db, new HttpContextAccessor());
        var controller = new DashboardController(db, cache, new InFlightRequestCoalescer(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardController>.Instance,
            new CanvasReportingPeriodService(db), access) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var catalog = Assert.IsAssignableFrom<IEnumerable<DashboardMetricDto>>(Assert.IsType<OkObjectResult>((await controller.Metrics(null, null, null, null, null, null, null, null, null, null, metricIds: null, catalogOnly: true)).Result).Value);
        Assert.DoesNotContain(catalog, x => x.Id == "legacy-sis-metric");
        var result = Assert.IsAssignableFrom<IEnumerable<DashboardMetricDto>>(Assert.IsType<OkObjectResult>((await controller.Metrics(null, null, null, null, null, null, null, null, null, null, metricIds: "legacy-sis-metric")).Result).Value);
        var placeholder = Assert.Single(result);
        Assert.Equal("source_retired", placeholder.UnavailableReason);
        Assert.Null(placeholder.Value);
        Assert.Empty(placeholder.Series);
        var options = Assert.IsType<DashboardFilterOptionsDto>(Assert.IsType<OkObjectResult>((await controller.FilterOptions(null)).Result).Value);
        Assert.DoesNotContain("Old SIS campus", options.Campuses);
    }

    [MySqlFact]
    public async Task SisOwnershipMigrationResumesAfterDroppingObsoleteColumns()
    {
        await using var test = await TestDatabase.CreateAsync(targetMigration: "20261104024508_AddSisHealthVisitSync");
        await using var db = test.Open();
        await db.Database.ExecuteSqlRawAsync("INSERT INTO StudentHealthVisits (FullName, ExamTime, CreatedAt, NotificationSent, SisIsDeleted, IsLocallyHidden) VALUES ('Archived visit', UTC_TIMESTAMP(), UTC_TIMESTAMP(), FALSE, FALSE, FALSE)");
        const string id = "20261104063344_EnforceSchoolOnlineSourceOwnership";
        var assembly = db.GetService<IMigrationsAssembly>();
        var migration = assembly.CreateMigration(assembly.Migrations[id], db.Database.ProviderName!);
        var model = db.GetService<IModelRuntimeInitializer>().Initialize(migration.TargetModel);
        var generator = db.GetService<IMigrationsSqlGenerator>();
        foreach (var operation in migration.UpOperations.Take(2))
            foreach (var command in generator.Generate([operation], model))
                await db.Database.ExecuteSqlRawAsync(command.CommandText);
        await db.MigrateWithRecoveryAsync();
        Assert.Contains(id, await db.Database.GetAppliedMigrationsAsync());
        Assert.Empty(await db.StudentHealthVisits.ToListAsync());
        Assert.Equal("Archived visit", (await db.StudentHealthVisits.IgnoreQueryFilters().SingleAsync()).FullName);
        Assert.True(await db.ImportedReportChanges.AnyAsync());
    }

    [MySqlFact]
    public async Task SisFullReconciliationMarksMissingStudentWithoutDeletingProfile()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        using (var http = SisFixtures.Http([SisFixtures.Student()]))
            await new SisSyncRunner().RunAsync(db, http, SisFixtures.Options(), true, default);
        using (var http = SisFixtures.Http([]))
            await Assert.ThrowsAsync<SisApiException>(() => new SisSyncRunner().RunAsync(db, http, SisFixtures.Options(), true, default));
        Assert.False((await db.StudentProfiles.SingleAsync()).SISIsDeleted);
        using (var http = SisFixtures.Http([]))
            await new SisSyncRunner().RunAsync(db, http, SisFixtures.Options(allowEmpty: true), true, default);
        db.ChangeTracker.Clear();
        Assert.Equal("OUT_OF_SCOPE", (await db.StudentProfiles.SingleAsync()).SISStatus);
        Assert.True((await db.SisWarehouseRecords.SingleAsync(x => x.Endpoint == "students")).IsDeleted);
    }
}

internal sealed class SisHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request, ct);
}

internal static class SisFixtures
{
    internal const string Watermark = "2026-11-01T08:00:00+07:00";
    internal static SisEtlOptions Options(bool timetable = false, bool allowEmpty = false, string? schoolYearId = null, bool health = false) => new()
    {
        BaseUri = new Uri("https://sis.test/"), ClientId = "test-client", ClientSecret = "test-secret",
        RequestIntervalMilliseconds = 0, Resources = health ? ["schools", "students", "health/visits"] : timetable ? ["schools", "students", "timetable"] : ["schools", "students"],
        FromDate = new DateOnly(2026, 11, 1), ToDate = new DateOnly(2026, 11, 30), AllowEmptyStudents = allowEmpty,
        SchoolYearIds = schoolYearId is null ? [] : [schoolYearId]
    };
    internal static HttpResponseMessage Token() => Json(new
    { access_token = "test-token", token_type = "Bearer", expires_in = 3600, scope = "catalog.read students.read timetable.read health-visits.read" });
    internal static HttpResponseMessage Page(object[] records, bool more = false, string? cursor = null, string watermark = Watermark) => Json(new
    { data = records, meta = new { has_more = more, next_cursor = cursor, watermark, api_version = "1.0" } });
    private static HttpResponseMessage Json(object value) => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(value)) };
    internal static object Student(bool deleted = false, bool father = true, string year = "278") => new
    {
        id = year == "278" ? "9007199254740993123" : "9007199254740993122", student_user_id = "120345", student_code = "OS001", username = "student",
        full_name = year == "278" ? "New student" : "Historical student", email = "student@school.test", gender = "UNKNOWN", date_of_birth = "2014-01-01",
        grade = new { id = "6", code = "06", name = "Khối 6" }, homeroom_class = new { id = "22", name = "6H1", code = "6H1" },
        school = new { id = "10" }, school_year = new { id = year }, status = "STUDYING",
        father = father ? new { full_name = "New parent", phone = "+84912345678", email = "parent@school.test", job = "Teacher", birth_year = 1980 } : null,
        mother = (object?)null, guardian = (object?)null, is_active = !deleted, is_deleted = deleted, updated_at = "2026-10-31T08:00:00+07:00"
    };
    internal static object Lesson(string id) => new { id, school = new { id = "10" }, school_year = new { id = "278" }, date = "2026-11-03", is_deleted = false, updated_at = "2026-10-31T08:00:00+07:00" };
    internal static object HealthVisit(bool deleted = false, string symptoms = "Source symptoms", string notes = "Source notes") => new
    {
        id = "visit-1", student = new { user_id = "120345", code = "OS001", full_name = "New student", gender = "UNKNOWN" },
        school = new { id = "10" }, school_year = new { id = "278" }, homeroom_class = new { id = "22", name = "6H1" },
        visited_at = "2026-11-01T18:30:00Z", severity = "NORMAL", symptoms, diagnosis = new { id = "1", code = "D1", name = "Diagnosis" },
        treatment = "Rest", medicine = "Source medicine", notes, parent_notified = false, is_deleted = deleted, updated_at = "2026-10-31T08:00:00+07:00"
    };
    internal static HttpClient Http(object[] students, string watermark = Watermark, bool failSecondPage = false,
        object[]? timetable = null, bool failTimetable = false, List<string>? queries = null, object[]? healthVisits = null) => new(new SisHandler((request, _) =>
    {
        var path = request.RequestUri!.AbsolutePath;
        var query = request.RequestUri.Query;
        if (path == "/oauth/token") return Task.FromResult(Token());
        if (path.EndsWith("/schools")) return Task.FromResult(Page([new
        {
            id = "10", is_deleted = false, is_active = true, updated_at = "2026-10-31T08:00:00+07:00",
            school_years = new[]
            {
                new { id = "278", is_current = true, is_deleted = false, start_date = "2026-08-01", end_date = "2027-06-30" },
                new { id = "277", is_current = false, is_deleted = false, start_date = "2025-08-01", end_date = "2026-06-30" }
            }
        }], watermark: watermark));
        if (path.EndsWith("/students"))
        {
            queries?.Add(query);
            if (failSecondPage && query.Contains("cursor=")) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
            return Task.FromResult(Page(students, failSecondPage, failSecondPage ? "next" : null, watermark));
        }
        if (path.EndsWith("/timetable"))
        {
            Assert.DoesNotContain("updated_since", query);
            if (failTimetable && query.Contains("cursor=")) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden));
            return Task.FromResult(Page(timetable ?? [], failTimetable, failTimetable ? "next" : null, watermark));
        }
        if (path.EndsWith("/health/visits")) return Task.FromResult(Page(healthVisits ?? [], watermark: watermark));
        throw new InvalidOperationException("Unexpected fixture endpoint.");
    }));
}
