using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;
using OlympiaStudent360.Api.SisSync;
using Xunit;

namespace OlympiaStudent360.Tests;

public sealed partial class MySqlResilienceTests
{
    [MySqlFact]
    public async Task SisReplayAndParentContactChangesDoNotInvalidateReports()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        async Task Sync(object student, object? health = null)
        {
            using var http = SisFixtures.Http([student], healthVisits: [health ?? SisFixtures.HealthVisit()]);
            await new SisSyncRunner().RunAsync(db, http, SisFixtures.Options(health: true), true, default);
            await new SisReportRefresher().RunAsync(db, default);
        }
        await Sync(SisFixtures.Student());
        var marker = await db.ImportedReportChanges.MaxAsync(x => x.Id);
        var build = await db.ImportedReportBuilds.AsNoTracking().SingleAsync();
        await Sync(SisFixtures.Student());
        var changed = JsonSerializer.SerializeToNode(SisFixtures.Student())!;
        changed["father"]!["phone"] = "+84999999999";
        changed["contact_phone"] = "+84988888888";
        changed["updated_at"] = "2026-10-31T09:00:00+07:00";
        await Sync(changed);
        Assert.Equal(marker, await db.ImportedReportChanges.MaxAsync(x => x.Id));
        Assert.Equal(build.Id, (await db.ImportedReportBuilds.AsNoTracking().SingleAsync()).Id);
        Assert.Equal("+84999999999", (await db.SISStudentParents.AsNoTracking().SingleAsync()).PhoneNumber);
        Assert.Equal("+84988888888", (await db.StudentProfiles.AsNoTracking().SingleAsync()).PhoneNumber);
        var riskIds = await db.StudentRiskSnapshots.Select(x => x.Id).ToArrayAsync();
        var mealBuildId = (await db.ErpMealReportBuilds.AsNoTracking().SingleAsync()).Id;
        var visit = JsonSerializer.SerializeToNode(SisFixtures.HealthVisit())!;
        visit["symptoms"] = "Changed symptoms";
        visit["updated_at"] = "2026-10-31T09:00:00+07:00";
        await Sync(changed, visit);
        Assert.Equal(ReportArea.Health, (await db.ImportedReportChanges.OrderByDescending(x => x.Id).FirstAsync()).Areas);
        Assert.Equal(riskIds, await db.StudentRiskSnapshots.Select(x => x.Id).ToArrayAsync());
        Assert.Equal(mealBuildId, (await db.ErpMealReportBuilds.AsNoTracking().SingleAsync()).Id);
    }

    [MySqlFact]
    public async Task HealthChangeReplacesOnlyHealthFactsAndLeavesRiskAndMealsUntouched()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        await SeedScopedReports(db);
        var behaviourIds = await db.ImportedReportAggregates.Where(x => x.Source == "student-behaviours").Select(x => x.Id).ToArrayAsync();
        var healthIds = await db.ImportedReportAggregates.Where(x => x.Source == "student-health-visits").Select(x => x.Id).ToArrayAsync();
        var riskIds = await db.StudentRiskSnapshots.Select(x => x.Id).ToArrayAsync();
        var mealIds = await db.ErpMealDailyAggregates.Select(x => x.Id).ToArrayAsync();
        var mealBuild = await db.ErpMealReportBuilds.AsNoTracking().SingleAsync();
        var visit = await db.StudentHealthVisits.FirstAsync();
        visit.Diagnosis = "Updated diagnosis";
        await db.SaveChangesAsync();
        Assert.Equal(ReportArea.Health, (await db.ImportedReportChanges.OrderByDescending(x => x.Id).FirstAsync()).Areas);
        await new SisReportRefresher().RunAsync(db, default);
        Assert.Equal(behaviourIds, await db.ImportedReportAggregates.Where(x => x.Source == "student-behaviours").Select(x => x.Id).ToArrayAsync());
        Assert.Equal(riskIds, await db.StudentRiskSnapshots.Select(x => x.Id).ToArrayAsync());
        Assert.Equal(mealIds, await db.ErpMealDailyAggregates.Select(x => x.Id).ToArrayAsync());
        Assert.Equal(mealBuild.Id, (await db.ErpMealReportBuilds.AsNoTracking().SingleAsync()).Id);
        Assert.DoesNotContain(await db.ImportedReportAggregates.Where(x => x.Source == "student-health-visits").Select(x => x.Id).ToArrayAsync(), id => healthIds.Contains(id));
        Assert.True(await db.ImportedReportAggregates.AnyAsync(x => x.Diagnosis == "Updated diagnosis"));
    }

    [MySqlTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StudentDimensionChangesRefreshOnlyThatStudentsAffectedSummaries(bool campusOnly)
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        var (first, second) = await SeedScopedReports(db);
        var otherImported = await db.ImportedReportAggregates.Where(x => x.StudentProfileId == second.Id).Select(x => x.Id).ToArrayAsync();
        var riskIds = await db.StudentRiskSnapshots.Select(x => x.Id).ToArrayAsync();
        var mealIds = await db.ErpMealDailyAggregates.Where(x => campusOnly || x.StudentProfileId == second.Id).Select(x => x.Id).ToArrayAsync();
        if (campusOnly) first.Campus = "New campus";
        else { first.FullName = "Updated student"; first.Homeroom = "6H2"; }
        await db.SaveChangesAsync();
        var change = await db.ImportedReportChanges.OrderByDescending(x => x.Id).FirstAsync();
        Assert.Equal(first.Id, change.StudentProfileId);
        Assert.Equal(ReportArea.Imported | (campusOnly ? ReportArea.None : ReportArea.Meals), change.Areas);
        await new SisReportRefresher().RunAsync(db, default);
        Assert.Equal(otherImported, await db.ImportedReportAggregates.Where(x => x.StudentProfileId == second.Id).Select(x => x.Id).ToArrayAsync());
        Assert.Equal(riskIds, await db.StudentRiskSnapshots.Select(x => x.Id).ToArrayAsync());
        Assert.Equal(mealIds, await db.ErpMealDailyAggregates.Where(x => campusOnly || x.StudentProfileId == second.Id).Select(x => x.Id).ToArrayAsync());
        var rows = await db.ImportedReportAggregates.Where(x => x.StudentProfileId == first.Id).ToListAsync();
        Assert.NotEmpty(rows);
        if (campusOnly) Assert.All(rows, x => Assert.Equal("New campus", x.Campus));
        else
        {
            Assert.All(rows, x => { Assert.Equal("Updated student", x.StudentName); Assert.Equal("6H2", x.Homeroom); });
            Assert.All(await db.ErpMealReportDetails.Where(x => x.StudentProfileId == first.Id).ToListAsync(), x => Assert.Equal("Updated student", x.StudentName));
        }
    }

    [MySqlFact]
    public async Task CanvasPublicationInvalidatesRiskOnlyAndQueueRetainsUnconsumedMealChanges()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        var (first, _) = await SeedScopedReports(db);
        var facts = await db.ImportedReportAggregates.Select(x => x.Id).ToArrayAsync();
        var meals = await db.ErpMealDailyAggregates.Select(x => x.Id).ToArrayAsync();
        var riskIds = await db.StudentRiskSnapshots.Select(x => x.Id).ToArrayAsync();
        db.CanvasDashboardAggregateBuilds.Add(new CanvasDashboardAggregateBuild { CompletedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        Assert.Equal(ReportArea.Risk, (await db.ImportedReportChanges.OrderByDescending(x => x.Id).FirstAsync()).Areas);
        await new SisReportRefresher().RunAsync(db, default);
        Assert.Equal(facts, await db.ImportedReportAggregates.Select(x => x.Id).ToArrayAsync());
        Assert.Equal(meals, await db.ErpMealDailyAggregates.Select(x => x.Id).ToArrayAsync());
        Assert.DoesNotContain(await db.StudentRiskSnapshots.Select(x => x.Id).ToArrayAsync(), id => riskIds.Contains(id));
        first.FullName = "Retained update";
        await db.SaveChangesAsync();
        var update = await db.ImportedReportChanges.OrderByDescending(x => x.Id).FirstAsync();
        await new ImportedReportAggregateService(db, new CanvasReportingPeriodService(db)).RefreshIfNeededAsync();
        Assert.True(await db.ImportedReportChanges.AnyAsync(x => x.Id == update.Id));
        await new ErpMealReportAggregateService(db).RefreshIfNeededAsync();
        Assert.Equal("Retained update", (await db.ErpMealDailyAggregates.AsNoTracking().SingleAsync(x => x.StudentProfileId == first.Id)).StudentName);
    }

    [MySqlFact]
    public async Task NewSisStudentDoesNotReplaceExistingStudentsSummaries()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        await SeedScopedReports(db);
        var facts = await db.ImportedReportAggregates.Select(x => x.Id).ToArrayAsync();
        var riskIds = await db.StudentRiskSnapshots.Select(x => x.Id).ToArrayAsync();
        var mealIds = await db.ErpMealDailyAggregates.Select(x => x.Id).ToArrayAsync();
        using var http = SisFixtures.Http([SisFixtures.Student()]);
        await new SisSyncRunner().RunAsync(db, http, SisFixtures.Options(), true, default);
        await new SisReportRefresher().RunAsync(db, default);
        Assert.Equal(facts, await db.ImportedReportAggregates.Select(x => x.Id).ToArrayAsync());
        Assert.Equal(mealIds, await db.ErpMealDailyAggregates.Select(x => x.Id).ToArrayAsync());
        var remainingRiskIds = await db.StudentRiskSnapshots.Select(x => x.Id).ToArrayAsync();
        Assert.All(riskIds, id => Assert.Contains(id, remainingRiskIds));
        var newId = await db.StudentProfiles.Where(x => x.SchoolOnlineUserId != null).Select(x => x.Id).SingleAsync();
        Assert.True(await db.StudentRiskSnapshots.AnyAsync(x => x.StudentProfileId == newId));
    }

    [MySqlFact]
    public async Task ScopedReportMigrationResumesAfterPartialDdl()
    {
        await using var test = await TestDatabase.CreateAsync(createSchema: false);
        await using var db = test.Open();
        await db.GetService<IMigrator>().MigrateAsync("20261104063344_EnforceSchoolOnlineSourceOwnership");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO ImportedReportBuilds (LastChangeId, CompletedAtUtc, CreatedAt, UpdatedAt) VALUES (0, UTC_TIMESTAMP(), UTC_TIMESTAMP(), UTC_TIMESTAMP())");
        await db.Database.ExecuteSqlRawAsync("INSERT INTO ErpMealReportBuilds (LastSyncRunId, LastStudentChangeId, CompletedAtUtc, CreatedAt, UpdatedAt) VALUES (0, 0, UTC_TIMESTAMP(), UTC_TIMESTAMP(), UTC_TIMESTAMP())");
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE ImportedReportChanges ADD COLUMN Areas int NOT NULL DEFAULT 127");
        await db.MigrateWithRecoveryAsync();
        db.ImportedReportChanges.Add(new ImportedReportChange { Areas = ReportArea.Health, StudentProfileId = 123 });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var change = await db.ImportedReportChanges.OrderByDescending(x => x.Id).FirstAsync();
        Assert.Equal(ReportArea.Health, change.Areas);
        Assert.Equal(123, change.StudentProfileId);
        Assert.Equal(1, (await db.ImportedReportBuilds.AsNoTracking().SingleAsync()).SchemaVersion);
        Assert.Equal(1, (await db.ErpMealReportBuilds.AsNoTracking().SingleAsync()).SchemaVersion);
        Assert.Contains("20261104145633_ScopeReportInvalidation", await db.Database.GetAppliedMigrationsAsync());
    }

    [MySqlFact]
    public async Task DashboardFreshnessShowsSisAndCanvasCompletionsIndependently()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        Assert.Null(await DashboardSyncFreshness.LatestSuccessfulSisAtUtcAsync(db));
        var latest = new DateTime(2026, 11, 4, 10, 6, 0, DateTimeKind.Utc);
        foreach (var (status, time) in new[] { ("completed", latest.AddDays(-1)), ("completed", latest),
            ("failed", latest.AddHours(1)), ("running", latest.AddHours(2)) })
            db.SisSyncRuns.Add(new SisSyncRun { SourceKey = "dashboard-sis-test", Mode = "incremental",
                Status = status, StartedAtUtc = time.AddMinutes(-1), CompletedAtUtc = time });
        var canvasTime = latest.AddHours(-8);
        var canvas = CompletedRun(canvasTime);
        canvas.CompletedAtUtc = canvasTime;
        db.CanvasSyncRuns.Add(canvas);
        db.ImportedReportBuilds.Add(new ImportedReportBuild { SchemaVersion = 1, CompletedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var controller = new OlympiaStudent360.Api.Controllers.DashboardController(db, cache, new InFlightRequestCoalescer(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<OlympiaStudent360.Api.Controllers.DashboardController>.Instance,
            new CanvasReportingPeriodService(db), new UserAccessService(db, new Microsoft.AspNetCore.Http.HttpContextAccessor()))
        {
            ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext() }
        };
        var response = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>((await controller.Overview("2026-2027", "all")).Result);
        var overview = Assert.IsType<OlympiaStudent360.Api.Core.DTOs.DashboardOverviewDto>(response.Value);
        Assert.Equal(latest, overview.LastSuccessfulSisSyncAtUtc);
        Assert.Equal(canvasTime, overview.LastSuccessfulCanvasSyncAtUtc);
        Assert.Equal(DateTimeKind.Utc, overview.LastSuccessfulSisSyncAtUtc!.Value.Kind);
        var json = JsonSerializer.SerializeToElement(overview, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.EndsWith("Z", json.GetProperty("lastSuccessfulSisSyncAtUtc").GetString());
        Assert.EndsWith("Z", json.GetProperty("lastSuccessfulCanvasSyncAtUtc").GetString());
    }

    [MySqlFact]
    public async Task DashboardFreshnessUsesSuccessfulRunCompletionAndSerializesUtc()
    {
        await using var test = await TestDatabase.CreateAsync(migrate: true);
        await using var db = test.Open();
        Assert.Null(await DashboardSyncFreshness.LatestSuccessfulCanvasAtUtcAsync(db));
        var latest = new DateTime(2026, 11, 3, 18, 8, 0, DateTimeKind.Utc);
        var completed = CompletedRun(latest);
        completed.CompletedAtUtc = latest;
        db.CanvasSyncRuns.Add(completed);
        var failed = CompletedRun(latest.AddHours(1));
        failed.Status = "failed";
        db.CanvasSyncRuns.Add(failed);
        db.CanvasSyncStates.Add(new CanvasSyncState { SourceKey = "newer-state", LastStatus = "failed", LastSuccessfulWatermarkUtc = latest.AddHours(2) });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var actual = await DashboardSyncFreshness.LatestSuccessfulCanvasAtUtcAsync(db);
        Assert.Equal(latest, actual);
        Assert.Equal(DateTimeKind.Utc, actual!.Value.Kind);
        Assert.EndsWith("Z\"", JsonSerializer.Serialize(actual));
    }

    private static async Task<(StudentProfile First, StudentProfile Second)> SeedScopedReports(AppDbContext db)
    {
        var first = new StudentProfile { SISUserId = "scoped-one", FullName = "First", GradeLevel = "6", Homeroom = "6H1" };
        var second = new StudentProfile { SISUserId = "scoped-two", FullName = "Second", GradeLevel = "6", Homeroom = "6H1" };
        db.StudentProfiles.AddRange(first, second);
        foreach (var student in new[] { first, second })
        {
            db.StudentHealthVisits.Add(new StudentHealthVisit { Student = student, FullName = student.FullName,
                SisSourceKey = "scope", SisVisitId = student.SISUserId, ExamTime = DateTime.UtcNow, Diagnosis = "Diagnosis" });
            db.StudentBehaviours.Add(new StudentBehaviour { Student = student, IncidentDate = DateTime.UtcNow,
                BehaviourType = "Đi học muộn", AcademicYear = "2026-2027" });
        }
        await db.SaveChangesAsync();
        foreach (var student in new[] { first, second })
            db.ErpMealStatuses.Add(new ErpMealStatus { StudentProfileId = student.Id, Feed = ErpMealFeeds.MissingOrder,
                SourceRecordKey = student.SISUserId, ErpStudentId = student.SISUserId, MealDate = DateTime.UtcNow.Date,
                Meal = "Lunch", OrderStatus = "Không đặt ăn", MatchStatus = "Matched", ImportedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        await new SisReportRefresher().RunAsync(db, default);
        return (first, second);
    }
}
