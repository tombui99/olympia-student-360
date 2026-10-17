using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

/// <summary>
/// Configurable, governed reports over ETL-materialized Canvas facts. Report requests never read
/// CanvasSubmissions or CanvasCourseScores; thresholds are applied to persisted sufficient statistics.
/// </summary>
[Route("api/learning-reports")]
[ApiController]
[Authorize]
public sealed class LearningReportsController(AppDbContext db, UserAccessService access) : ControllerBase
{
    private static readonly HashSet<string> ReportKeys =
    [
        "missing-frequency", "homework-achievement", "reading-completion", "grading-coverage"
    ];

    private const int MaximumPageSize = 1000;

    [HttpGet("options")]
    public async Task<ActionResult<LearningReportOptions>> Options(CancellationToken cancellationToken)
    {
        if (!access.Current.CanAccessAcademic) return Forbid();
        var visibleFacts = access.ApplyStudentScope(
            db.CanvasDashboardMetricAggregates.AsNoTracking(), item => item.Student);
        var courseIds = await visibleFacts.Select(item => item.CanvasCourseId)
            .Distinct().ToListAsync(cancellationToken);
        var courses = await db.CanvasCourses.AsNoTracking()
            .Where(item => courseIds.Contains(item.CanvasCourseId))
            .OrderBy(item => item.Name)
            .Select(item => new LearningCourseOption(item.CanvasCourseId, item.Name ?? item.CanvasCourseId.ToString()))
            .ToListAsync(cancellationToken);
        var assignmentGroups = await db.CanvasAssignments.AsNoTracking()
            .Where(item => courseIds.Contains(item.CanvasCourseId) &&
                item.AssignmentGroupName != null && item.AssignmentGroupName != "")
            .Select(item => item.AssignmentGroupName!)
            .Distinct().OrderBy(item => item).ToListAsync(cancellationToken);
        return Ok(new LearningReportOptions(courses, assignmentGroups));
    }

    [HttpGet("assignments")]
    public async Task<ActionResult<List<LearningAssignmentOption>>> Assignments(
        [FromQuery] long[] courseIds, [FromQuery] string? search, CancellationToken cancellationToken)
    {
        if (!access.Current.CanAccessAcademic) return Forbid();
        if (courseIds.Length is 0 or > 100 || courseIds.Any(id => id <= 0)) return BadRequest();
        var visible = access.ApplyStudentScope(
            db.CanvasDashboardMetricAggregates.AsNoTracking(), item => item.Student);
        var query = db.CanvasAssignments.AsNoTracking()
            .WhereMatchesAnyValue(item => item.CanvasCourseId, courseIds)
            .Where(item => visible.Any(fact => fact.CanvasAssignmentId == item.CanvasAssignmentId));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim()[..Math.Min(search.Trim().Length, 80)];
            query = query.Where(item => item.Name != null && item.Name.Contains(term));
        }
        return Ok(await query.OrderBy(item => item.Name).Take(1000)
            .Select(item => new LearningAssignmentOption(item.CanvasAssignmentId,
                item.CanvasCourseId, item.Name ?? item.CanvasAssignmentId.ToString()))
            .ToListAsync(cancellationToken));
    }

    [HttpPost("run")]
    public async Task<ActionResult<LearningReportResult>> Run(
        LearningReportQuery request, CancellationToken cancellationToken)
    {
        if (!access.Current.CanAccessAcademic) return Forbid();
        var error = Validate(request);
        if (error is not null) return BadRequest(error);
        var pageSize = Math.Clamp(request.PageSize, 1, request.Export ? MaximumPageSize : 100);

        // No raw warehouse fallback: an empty ETL aggregate produces an explicit unavailable result.
        if (!await db.CanvasDashboardAggregateBuilds.AsNoTracking()
                .AnyAsync(item => item.SchemaVersion >= CanvasDashboardAggregateService.ReportSchemaVersion,
                    cancellationToken))
            return Ok(new LearningReportResult([], 0, false, "aggregate_unavailable", request.Page, pageSize));

        var year = DashboardPeriodNormalizer.AcademicYear(request.AcademicYear!);
        var facts = access.ApplyStudentScope(
                db.CanvasDashboardMetricAggregates.AsNoTracking(), item => item.Student)
            .Where(item => item.AcademicYear == year);
        if (!string.IsNullOrWhiteSpace(request.Term) && request.Term != "all")
        {
            var term = DashboardPeriodNormalizer.Term(request.Term);
            facts = facts.Where(item => item.Term == term);
        }
        if (request.Month is >= 1 and <= 12)
        {
            var month = request.Month.Value;
            facts = facts.Where(item => item.AcademicPeriodStart != null &&
                item.AcademicPeriodStart.Value.Month == month);
        }
        if (request.CourseIds.Count > 0)
            facts = facts.WhereMatchesAnyValue(item => item.CanvasCourseId, request.CourseIds);
        if (request.AssignmentGroupNames.Count > 0)
            facts = facts.Where(item => item.Assignment != null)
                .WhereMatchesAny(item => item.Assignment!.AssignmentGroupName, request.AssignmentGroupNames);

        if (request.AssignmentIds.Count > 0)
            facts = facts.Where(item => item.CanvasAssignmentId != null)
                .WhereMatchesAnyValue(item => item.CanvasAssignmentId!.Value, request.AssignmentIds);
        if (request.ReportKey == "reading-completion" && request.AssignmentIds.Count == 0)
            return Ok(new LearningReportResult([], 0, true, "select_assignments", request.Page, pageSize));

        if (request.ReportKey == "grading-coverage")
            return Ok(await GradingCoverage(facts, request, pageSize, cancellationToken));

        var keys = request.ReportKey switch
        {
            "missing-frequency" => new[] { CanvasDashboardAggregateService.AssignmentsSubmitted,
                CanvasDashboardAggregateService.AssignmentsMissing },
            "homework-achievement" => new[] { CanvasDashboardAggregateService.SubmissionScoreAverage },
            _ => new[] { CanvasDashboardAggregateService.AssignmentsSubmitted,
                CanvasDashboardAggregateService.AssignmentsMissing }
        };
        var grouped = facts.WhereMatchesAny(item => item.MetricKey, keys)
            .GroupBy(item => new
            {
                item.StudentProfileId, item.Student.FullName,
                item.Student.SISUserId, item.Student.Homeroom,
                Grade = item.Student.GradeLevel ?? item.Student.CanvasGradeLevel
            })
            .Select(group => new
            {
                StudentId = group.Key.StudentProfileId,
                Name = group.Key.FullName,
                SisUserId = group.Key.SISUserId,
                group.Key.Homeroom,
                group.Key.Grade,
                Submitted = group.Sum(item => item.MetricKey == CanvasDashboardAggregateService.AssignmentsSubmitted
                    ? item.ValueSum : 0),
                Missing = group.Sum(item => item.MetricKey == CanvasDashboardAggregateService.AssignmentsMissing
                    ? item.ValueSum : 0),
                ScoreSum = group.Sum(item => item.MetricKey == CanvasDashboardAggregateService.SubmissionScoreAverage
                    ? item.ValueSum : 0),
                ScoreCount = group.Sum(item => item.MetricKey == CanvasDashboardAggregateService.SubmissionScoreAverage
                    ? item.SampleCount : 0)
            });
        grouped = request.ReportKey switch
        {
            "missing-frequency" => grouped.Where(item => item.Submitted + item.Missing > 0 &&
                item.Missing <= request.MaxMissing),
            "homework-achievement" => grouped.Where(item => item.ScoreCount > 0 &&
                item.ScoreSum >= request.MinScore * item.ScoreCount),
            _ => grouped.Where(item => item.Submitted + item.Missing > 0 &&
                item.Submitted * 100 >= request.MinCompletion * (item.Submitted + item.Missing))
        };
        var total = await grouped.CountAsync(cancellationToken);
        var pageNumber = Math.Min(request.Page, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)));
        var page = await grouped.OrderBy(item => item.Grade).ThenBy(item => item.Homeroom)
            .ThenBy(item => item.Name).ThenBy(item => item.StudentId)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var rows = page.Select(item =>
        {
            var score = item.ScoreCount > 0 ? item.ScoreSum / item.ScoreCount : (double?)null;
            var expected = item.Submitted + item.Missing;
            var completion = expected > 0 ? item.Submitted / expected * 100 : (double?)null;
            return new LearningReportRow(item.StudentId, item.SisUserId, item.Name,
                item.Grade, item.Homeroom, (int)item.Missing, (int)item.Submitted,
                0, Round(score), Round(completion), "review");
        }).ToList();
        return Ok(new LearningReportResult(rows, total, true, null, pageNumber, pageSize));
    }

    private static async Task<LearningReportResult> GradingCoverage(
        IQueryable<CanvasDashboardMetricAggregate> facts, LearningReportQuery request,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var grouped = facts.Where(item =>
                item.MetricKey == CanvasDashboardAggregateService.AssignmentsSubmitted ||
                item.MetricKey == CanvasDashboardAggregateService.SubmittedAssignmentsGraded)
            .GroupBy(item => new { item.CanvasCourseId, item.Course.Name })
            .Select(group => new
            {
                group.Key.CanvasCourseId,
                group.Key.Name,
                Submitted = group.Sum(item => item.MetricKey == CanvasDashboardAggregateService.AssignmentsSubmitted
                    ? item.ValueSum : 0),
                Graded = group.Sum(item => item.MetricKey == CanvasDashboardAggregateService.SubmittedAssignmentsGraded
                    ? item.ValueSum : 0)
            });
        var total = await grouped.CountAsync(cancellationToken);
        var pageNumber = Math.Min(request.Page, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)));
        var page = await grouped.OrderBy(item => item.Name).ThenBy(item => item.CanvasCourseId)
            .Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        var rows = page.Select(item =>
        {
            var rate = item.Submitted > 0 ? item.Graded / item.Submitted * 100 : (double?)null;
            return new LearningReportRow(null, null, item.Name ?? item.CanvasCourseId.ToString(), null, null, 0,
                (int)item.Submitted, (int)item.Graded, null, Round(rate),
                rate >= request.MinCompletion ? "green" : "review");
        }).ToList();
        return new LearningReportResult(rows, total, true, null, pageNumber, pageSize);
    }

    private static double? Round(double? value) => value.HasValue ? Math.Round(value.Value, 1) : null;

    private static string? Validate(LearningReportQuery query)
    {
        if (!ReportKeys.Contains(query.ReportKey)) return "Unknown report type.";
        if (string.IsNullOrWhiteSpace(query.AcademicYear) ||
            !DashboardPeriodNormalizer.IsAcademicYear(
                DashboardPeriodNormalizer.AcademicYear(query.AcademicYear)))
            return "Select a valid academic year.";
        if (!string.IsNullOrWhiteSpace(query.Term) && query.Term != "all" &&
            DashboardPeriodNormalizer.Term(query.Term) is not ("hk1" or "hk2"))
            return "Select a valid reporting term.";
        if (query.Month is < 1 or > 12) return "Month must be between 1 and 12.";
        if (query.CourseIds is null || query.AssignmentIds is null ||
            query.AssignmentGroupNames is null)
            return "Invalid course or assignment selections.";
        if (query.CourseIds.Count > 100 || query.AssignmentIds.Count > 200 ||
            query.AssignmentGroupNames.Count > 100 ||
            query.CourseIds.Any(id => id <= 0) || query.AssignmentIds.Any(id => id <= 0))
            return "Too many or invalid course or assignment selections.";
        if (query.AssignmentGroupNames.Any(name => string.IsNullOrWhiteSpace(name) || name.Length > 255))
            return "Invalid assignment group selection.";
        if (query.MaxMissing is < 0 or > 1000 || query.MinScore is < 0 or > 100 ||
            query.MinCompletion is < 0 or > 100)
            return "Invalid report thresholds.";
        var maximumPageSize = query.Export ? MaximumPageSize : 100;
        if (query.Page is < 1 or > 100000 || query.PageSize is < 1 || query.PageSize > maximumPageSize)
            return "Invalid report page.";
        return null;
    }
}

public sealed record LearningReportQuery
{
    public string ReportKey { get; init; } = "missing-frequency";
    public string? AcademicYear { get; init; }
    public string? Term { get; init; } = "all";
    public int? Month { get; init; }
    public List<long> CourseIds { get; init; } = [];
    public List<long> AssignmentIds { get; init; } = [];
    public List<string> AssignmentGroupNames { get; init; } = [];
    public int MaxMissing { get; init; } = 2;
    public double MinScore { get; init; } = 80;
    public double MinCompletion { get; init; } = 90;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 15;
    public bool Export { get; init; }
}

public sealed record LearningCourseOption(long Id, string Name);
public sealed record LearningAssignmentOption(long Id, long CourseId, string Name);
public sealed record LearningReportOptions(List<LearningCourseOption> Courses,
    List<string> AssignmentGroups);
public sealed record LearningReportRow(int? StudentId, string? SisUserId, string Name,
    string? Grade, string? Homeroom, int Missing, int Submitted, int Graded,
    double? Score, double? Rate, string Status);
public sealed record LearningReportResult(List<LearningReportRow> Rows, int TotalCount,
    bool AggregateAvailable, string? Notice, int Page, int PageSize);
