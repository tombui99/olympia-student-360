using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using OlympiaStudent360.Api.Controllers;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;
using Xunit;

namespace OlympiaStudent360.Tests;

public sealed partial class MySqlResilienceTests
{
    private const string DiligenceMetricIds = "canvas-assignments-submitted,canvas-submitted-assignments-graded," +
        "canvas-assignments-missing,canvas-assignments-late,canvas-current-score-avg";

    private static DashboardController MetricsController(AppDbContext db, IMemoryCache cache,
        UserAccessSnapshot? scope = null)
    {
        var http = new DefaultHttpContext();
        if (scope is not null) http.Items[UserAccessService.HttpContextItemKey] = scope;
        return new(db, cache, new InFlightRequestCoalescer(), NullLogger<DashboardController>.Instance,
            new CanvasReportingPeriodService(db), new UserAccessService(db, new HttpContextAccessor { HttpContext = http }))
        { ControllerContext = new() { HttpContext = http } };
    }

    private static async Task<List<DashboardMetricDto>> ReadMetrics(DashboardController controller,
        string? ids = DiligenceMetricIds, int? month = null, string? week = null) =>
        Assert.IsType<List<DashboardMetricDto>>(Assert.IsType<OkObjectResult>((await controller.Metrics(
            "2026-2027", "hk1", null, null, null, null, null, null, null, null, ids,
            month: month, weekStart: week)).Result).Value);

    private static async Task SeedDashboardMetrics(AppDbContext db)
    {
        var course = new CanvasCourse { CanvasCourseId = 73001, Name = "Math" };
        var assignment = new CanvasAssignment { CanvasAssignmentId = 73002, Course = course };
        var first = new StudentProfile { SISUserId = "metrics-a", FullName = "A", Homeroom = "8A" };
        var second = new StudentProfile { SISUserId = "metrics-b", FullName = "B", Homeroom = "8B" };
        foreach (var (student, multiplier) in new[] { (first, 1), (second, 2) })
        foreach (var key in DiligenceMetricIds.Split(','))
        {
            var isScore = key == CanvasDashboardAggregateService.CurrentScoreAverage;
            db.CanvasDashboardMetricAggregates.Add(new()
            {
                Student = student, Course = course, MetricKey = key, AcademicYear = "2026-2027", Term = "hk1",
                Assignment = isScore ? null : assignment,
                AcademicPeriodStart = new DateTime(2026, 10, 1), PeriodStart = new DateTime(2026, 10, 1),
                AcademicActivityDate = isScore ? null : new DateTime(2026, 10, 26),
                ValueSum = isScore ? 70 + multiplier * 10 : multiplier, SampleCount = isScore ? 1 : 0
            });
        }
        db.CanvasDashboardAggregateBuilds.Add(new() { SchemaVersion = 7, CompletedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    [MySqlFact]
    public async Task DiligenceMetricsReadOnlySnapshotAndReuseCacheWithoutLeakingStudentScope()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using (var seed = test.Open()) await SeedDashboardMetrics(seed);
        var capture = new TranscriptSqlCapture();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>(test.Options)
            .AddInterceptors(capture).Options);
        using var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 512 });
        var controller = MetricsController(db, cache);
        var metrics = await ReadMetrics(controller);
        Assert.Equal(5, metrics.Count);
        Assert.Equal(85m, metrics.Single(m => m.Id == CanvasDashboardAggregateService.CurrentScoreAverage).Value);
        Assert.All(metrics.Where(m => m.Id != CanvasDashboardAggregateService.CurrentScoreAverage), m => Assert.Equal(3m, m.Value));
        Assert.Equal(3, capture.Commands.Count); // Definitions, build readiness, and grouped snapshot totals.
        Assert.DoesNotContain(capture.Commands, sql => new[] { "CanvasCourseScores", "CanvasSubmissions",
            "DashboardMetricObservations", "StudentBehaviours", "StudentProjects", "StudentMentalHealthRecords",
            "StudentBusAttendanceRecords", "ImportedReportAggregates" }.Any(sql.Contains));

        var reads = capture.Commands.Count;
        Assert.Equal(metrics, await ReadMetrics(controller));
        Assert.Equal(reads + 1, capture.Commands.Count); // Authorization metadata is still checked on cache hits.

        var restricted = new UserAccessSnapshot("class-teacher", "Teacher",
            new HashSet<string> { TeacherAccessScopes.Class }, ["8A"], []);
        var scoped = await ReadMetrics(MetricsController(db, cache, restricted), month: 10, week: "2026-10-26");
        Assert.Equal(80m, scoped.Single(m => m.Id == CanvasDashboardAggregateService.CurrentScoreAverage).Value);
        Assert.All(scoped.Where(m => m.Id != CanvasDashboardAggregateService.CurrentScoreAverage), m => Assert.Equal(1m, m.Value));
        Assert.All(await ReadMetrics(MetricsController(db, cache, restricted), month: 11, week: "2026-11-02"),
            m => Assert.Equal(m.Id == CanvasDashboardAggregateService.CurrentScoreAverage ? 80m : 0m, m.Value));
    }

    [MySqlFact]
    public async Task MixedAndAllMetricsKeepStoredAndImportedPeriodAliases()
    {
        await using var test = await TestDatabase.CreateAsync();
        await using var db = test.Open();
        await SeedDashboardMetrics(db);
        const string yearAlias = "Năm học 26-27";
        const string termAlias = "Học kỳ 1";
        db.DashboardMetricObservations.Add(new()
        {
            Metric = new() { Key = "school-total", Label = "School total", Domain = "Academics", Source = "Local" },
            AcademicYear = yearAlias, Term = termAlias, Value = 17,
            PeriodStart = new DateTime(2026, 10, 1), PeriodEnd = new DateTime(2026, 11, 1)
        });
        db.ImportedReportAggregates.Add(new()
        {
            Source = "student-behaviours", StudentKey = "a", StudentName = "A", AcademicYear = yearAlias,
            Term = termAlias, EventDay = new DateTime(2026, 10, 26), RecordCount = 4
        });
        db.ImportedReportBuilds.Add(new() { SchemaVersion = 1, CompletedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        using var cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 512 });
        var controller = MetricsController(db, cache);
        foreach (var ids in new[] { DiligenceMetricIds + ",school-total,behaviour-records", null })
        {
            var metrics = await ReadMetrics(controller, ids);
            Assert.Equal(17m, metrics.Single(m => m.Id == "school-total").Value);
            Assert.Equal(4m, metrics.Single(m => m.Id == "behaviour-records").Value);
            Assert.Equal(85m, metrics.Single(m => m.Id == CanvasDashboardAggregateService.CurrentScoreAverage).Value);
        }
    }
}
