using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

/// <summary>
/// Ready-made school leadership and subject leadership reports over materialized Canvas facts.
/// All four assessment categories, including GPA, are literal Canvas assignment groups.
/// </summary>
[Route("api/leadership-reports")]
[ApiController]
[Authorize]
public sealed class LeadershipReportsController(AppDbContext db, UserAccessService access) : ControllerBase
{
    private const int MaximumExportPageSize = 1000;

    [HttpGet("options")]
    public async Task<ActionResult<LeadershipReportOptions>> Options(CancellationToken cancellationToken)
    {
        if (!access.Current.CanAccessAcademic) return Forbid();
        var facts = VisibleFacts();
        var courseIds = await facts.Select(item => item.CanvasCourseId).Distinct()
            .ToListAsync(cancellationToken);
        var periods = await facts.Select(item => new { item.AcademicYear, item.Term })
            .Distinct().ToListAsync(cancellationToken);
        var studentOptions = await facts.Select(item => new
            {
                Grade = item.Student.GradeLevel ?? item.Student.CanvasGradeLevel ?? item.Course.CanvasGradeLevel,
                item.Student.Homeroom
            })
            .Distinct().ToListAsync(cancellationToken);
        var categories = CanvasReportFilters.AvailableCategories(await facts
            .Where(item => !item.IsFinalCourseScore && item.AssessmentCategory != null)
            .Select(item => item.AssessmentCategory).Distinct().ToListAsync(cancellationToken));
        var accounts = await CanvasReportFilters.VisibleSubaccounts(db, courseIds, cancellationToken);

        return Ok(new LeadershipReportOptions(
            periods.Select(item => item.AcademicYear)
                .Where(DashboardPeriodNormalizer.IsAcademicYear).Distinct()
                .OrderByDescending(year => year).ToList(),
            periods.Select(item => item.Term).Where(term => term is "hk1" or "hk2")
                .Distinct().OrderBy(term => term).ToList(),
            studentOptions.Select(item => item.Grade).OfType<string>()
                .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct()
                .OrderBy(value => value).ToList(),
            studentOptions.Select(item => item.Homeroom).OfType<string>()
                .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct()
                .OrderBy(value => value).ToList(),
            studentOptions.Where(item => !string.IsNullOrWhiteSpace(item.Grade) &&
                    !string.IsNullOrWhiteSpace(item.Homeroom))
                .Select(item => new CanvasReportCohortOption(item.Grade!, item.Homeroom!))
                .Distinct().OrderBy(item => item.Grade).ThenBy(item => item.Homeroom).ToList(),
            accounts, categories));
    }

    [HttpPost("run")]
    public async Task<ActionResult<LeadershipReportResult>> Run(
        LeadershipReportQuery request, CancellationToken cancellationToken)
    {
        if (!access.Current.CanAccessAcademic) return Forbid();
        var error = Validate(request);
        if (error is not null) return BadRequest(error);

        var category = CanvasReportFilters.CanonicalCategory(request.AssessmentCategory)!;
        var pageSize = request.Export ? Math.Min(request.PageSize, MaximumExportPageSize)
            : Math.Min(request.PageSize, 100);
        const string scoreSource = "canvas-submission-score-percent";
        if (!await CanvasDashboardAggregateService.HasReadableSnapshotAsync(
                db, cancellationToken))
            return Ok(EmptyResult(request, pageSize, scoreSource, "aggregate_unavailable", false));

        var year = DashboardPeriodNormalizer.AcademicYear(request.AcademicYear!);
        var facts = VisibleFacts().Where(item => item.AcademicYear == year);
        if (!string.IsNullOrWhiteSpace(request.Term) && request.Term != "all")
        {
            var term = DashboardPeriodNormalizer.Term(request.Term);
            facts = facts.Where(item => item.Term == term);
        }
        if (!string.IsNullOrWhiteSpace(request.Grade) && request.Grade != "all")
        {
            var grade = request.Grade.Trim();
            facts = facts.Where(item =>
                (item.Student.GradeLevel ?? item.Student.CanvasGradeLevel ?? item.Course.CanvasGradeLevel) == grade);
        }
        if (!string.IsNullOrWhiteSpace(request.Homeroom) && request.Homeroom != "all")
        {
            var homeroom = request.Homeroom.Trim();
            facts = facts.Where(item => item.Student.Homeroom == homeroom);
        }
        if (request.SubaccountIds.Count > 0)
        {
            var accountIds = await CanvasReportFilters.AccountAndDescendantIds(
                db, request.SubaccountIds, cancellationToken);
            facts = facts.Where(item => item.Course.CanvasAccountId != null)
                .WhereMatchesAnyValue(item => item.Course.CanvasAccountId!.Value, accountIds);
        }

        // Final Canvas course scores are a separate assessment view. Keep the shared school
        // filters, but never apply assignment-group or coefficient selections to these facts.
        var finalCourseFacts = request.ReportKey == "assessment-results"
            ? facts.Where(item => item.IsFinalCourseScore)
            : null;

        facts = facts.Where(item => !item.IsFinalCourseScore);

        if (category != "all")
            facts = facts.Where(item => item.AssessmentCategory == category);

        // Keep the grouped result as IQueryable. Summary and breakdowns are reduced by SQL and
        // only the requested detail page crosses the database boundary.
        IQueryable<LeadershipFactRow> rowsQuery;
        if (request.ReportKey == "assessment-results")
        {
            rowsQuery = facts.GroupBy(item => new
                {
                    item.StudentProfileId, item.Student.SISUserId, item.Student.FullName,
                    Grade = item.Student.GradeLevel ?? item.Student.CanvasGradeLevel ?? item.Course.CanvasGradeLevel,
                    item.Student.Homeroom, item.CanvasCourseId, CourseName = item.Course.Name,
                    item.Course.CanvasAccountId, item.AssignmentGroupName
                })
                .Select(group => new LeadershipFactRow(group.Key.StudentProfileId,
                    group.Key.SISUserId, group.Key.FullName, group.Key.Grade,
                    group.Key.Homeroom, group.Key.CanvasCourseId, group.Key.CourseName,
                    group.Key.CanvasAccountId, group.Key.AssignmentGroupName, null,
                    group.Sum(item => item.Submitted), group.Sum(item => item.Missing),
                    group.Sum(item => item.Late), group.Sum(item => item.Graded),
                    group.Sum(item => item.ScoreSum), group.Sum(item => item.ScoreCount)));
        }
        else
        {
            rowsQuery = facts.GroupBy(item => new
                {
                    item.StudentProfileId, item.Student.SISUserId, item.Student.FullName,
                    Grade = item.Student.GradeLevel ?? item.Student.CanvasGradeLevel ?? item.Course.CanvasGradeLevel,
                    item.Student.Homeroom, item.CanvasCourseId, CourseName = item.Course.Name,
                    item.Course.CanvasAccountId
                })
                .Select(group => new LeadershipFactRow(group.Key.StudentProfileId,
                    group.Key.SISUserId, group.Key.FullName, group.Key.Grade,
                    group.Key.Homeroom, group.Key.CanvasCourseId, group.Key.CourseName,
                    group.Key.CanvasAccountId, null, null,
                    group.Sum(item => item.Submitted), group.Sum(item => item.Missing),
                    group.Sum(item => item.Late), group.Sum(item => item.Graded),
                    group.Sum(item => item.ScoreSum), group.Sum(item => item.ScoreCount)));
        }

        var totalCount = await rowsQuery.CountAsync(cancellationToken);
        var page = Math.Min(request.Page,
            Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize)));
        var skip = (page - 1) * pageSize;
        List<LeadershipFactRow> pageFacts;
        if (request.ReportKey == "assessment-results")
        {
            pageFacts = await facts.GroupBy(item => new
                {
                    item.StudentProfileId, item.Student.SISUserId, item.Student.FullName,
                    Grade = item.Student.GradeLevel ?? item.Student.CanvasGradeLevel ?? item.Course.CanvasGradeLevel,
                    item.Student.Homeroom, item.CanvasCourseId, CourseName = item.Course.Name,
                    item.Course.CanvasAccountId, item.AssignmentGroupName
                })
                .OrderBy(group => group.Key.Grade).ThenBy(group => group.Key.Homeroom)
                .ThenBy(group => group.Key.FullName).ThenBy(group => group.Key.CourseName)
                .ThenBy(group => group.Key.AssignmentGroupName)
                .Skip(skip).Take(pageSize)
                .Select(group => new LeadershipFactRow(group.Key.StudentProfileId,
                    group.Key.SISUserId, group.Key.FullName, group.Key.Grade,
                    group.Key.Homeroom, group.Key.CanvasCourseId, group.Key.CourseName,
                    group.Key.CanvasAccountId, group.Key.AssignmentGroupName, null,
                    group.Sum(item => item.Submitted), group.Sum(item => item.Missing),
                    group.Sum(item => item.Late), group.Sum(item => item.Graded),
                    group.Sum(item => item.ScoreSum), group.Sum(item => item.ScoreCount)))
                .ToListAsync(cancellationToken);
        }
        else
        {
            pageFacts = await facts.GroupBy(item => new
                {
                    item.StudentProfileId, item.Student.SISUserId, item.Student.FullName,
                    Grade = item.Student.GradeLevel ?? item.Student.CanvasGradeLevel ?? item.Course.CanvasGradeLevel,
                    item.Student.Homeroom, item.CanvasCourseId, CourseName = item.Course.Name,
                    item.Course.CanvasAccountId
                })
                .OrderBy(group => group.Key.Grade).ThenBy(group => group.Key.Homeroom)
                .ThenBy(group => group.Key.FullName).ThenBy(group => group.Key.CourseName)
                .Skip(skip).Take(pageSize)
                .Select(group => new LeadershipFactRow(group.Key.StudentProfileId,
                    group.Key.SISUserId, group.Key.FullName, group.Key.Grade,
                    group.Key.Homeroom, group.Key.CanvasCourseId, group.Key.CourseName,
                    group.Key.CanvasAccountId, null, null,
                    group.Sum(item => item.Submitted), group.Sum(item => item.Missing),
                    group.Sum(item => item.Late), group.Sum(item => item.Graded),
                    group.Sum(item => item.ScoreSum), group.Sum(item => item.ScoreCount)))
                .ToListAsync(cancellationToken);
        }

        if (!request.IncludeSummary)
        {
            var pageAccountIds = pageFacts.Select(item => item.SubaccountId ?? 0)
                .Where(id => id > 0).Distinct().ToList();
            var pageAccountNames = await db.CanvasAccounts.AsNoTracking()
                .Where(item => pageAccountIds.Contains(item.CanvasAccountId))
                .Select(item => new { item.CanvasAccountId, item.Name })
                .ToDictionaryAsync(item => item.CanvasAccountId,
                    item => CanvasReportFilters.SubjectDisplayName(
                        item.Name, item.CanvasAccountId), cancellationToken);
            var pageRows = pageFacts.Select(item => ToRow([item], pageAccountNames)).ToList();
            return Ok(new LeadershipReportResult(request.ReportKey, true,
                totalCount == 0 ? "no_matching_data" : null, scoreSource,
                new LeadershipReportSummary(0, 0, 0, 0, 0, 0, 0, null, null, null),
                new LeadershipReportBreakdowns([], [], [], [], [], []), pageRows,
                totalCount, page, pageSize, null));
        }

        var monthTotals = await facts.GroupBy(item => item.AcademicPeriodStart)
            .Select(group => new
            {
                Month = group.Key,
                StudentCount = group.Select(item => item.StudentProfileId).Distinct().Count(),
                CourseCount = group.Select(item => item.CanvasCourseId).Distinct().Count(),
                Submitted = group.Sum(item => item.Submitted),
                Missing = group.Sum(item => item.Missing),
                Late = group.Sum(item => item.Late),
                Graded = group.Sum(item => item.Graded),
                ScoreSum = group.Sum(item => item.ScoreSum),
                ScoreCount = group.Sum(item => item.ScoreCount)
            })
            .ToListAsync(cancellationToken);

        var summaryFact = await facts.GroupBy(_ => 1).Select(group => new
        {
            StudentCount = group.Select(item => item.StudentProfileId).Distinct().Count(),
            CourseCount = group.Select(item => item.CanvasCourseId).Distinct().Count(),
            Submitted = group.Sum(item => item.Submitted),
            Missing = group.Sum(item => item.Missing),
            Late = group.Sum(item => item.Late),
            Graded = group.Sum(item => item.Graded),
            ScoreSum = group.Sum(item => item.ScoreSum),
            ScoreCount = group.Sum(item => item.ScoreCount)
        }).SingleOrDefaultAsync(cancellationToken);
        var summary = summaryFact is null
            ? new LeadershipReportSummary(0, 0, 0, 0, 0, 0, 0, null, null, null)
            : new LeadershipReportSummary(summaryFact.StudentCount, summaryFact.CourseCount,
                (int)Math.Max(Math.Max(summaryFact.Submitted + summaryFact.Missing,
                    summaryFact.Graded), summaryFact.ScoreCount), (int)summaryFact.Submitted,
                (int)summaryFact.Missing, (int)summaryFact.Late, (int)summaryFact.Graded,
                Score(summaryFact.ScoreSum, summaryFact.ScoreCount),
                Percent(summaryFact.Submitted, summaryFact.Submitted + summaryFact.Missing),
                Percent(summaryFact.Graded, summaryFact.Submitted));

        var gradeFacts = await facts.GroupBy(item =>
                (item.Student.GradeLevel ?? item.Student.CanvasGradeLevel ?? item.Course.CanvasGradeLevel) ?? "—")
            .Select(group => new LeadershipBreakdownFact(group.Key, group.Key, group.Count(),
                group.Select(item => item.StudentProfileId).Distinct().Count(),
                group.Select(item => item.CanvasCourseId).Distinct().Count(),
                group.Sum(item => item.Submitted), group.Sum(item => item.Missing),
                group.Sum(item => item.Late), group.Sum(item => item.Graded),
                group.Sum(item => item.ScoreSum), group.Sum(item => item.ScoreCount)))
            .ToListAsync(cancellationToken);
        var homeroomFacts = await facts.GroupBy(item => item.Student.Homeroom ?? "—")
            .Select(group => new LeadershipBreakdownFact(group.Key, group.Key, group.Count(),
                group.Select(item => item.StudentProfileId).Distinct().Count(),
                group.Select(item => item.CanvasCourseId).Distinct().Count(),
                group.Sum(item => item.Submitted), group.Sum(item => item.Missing),
                group.Sum(item => item.Late), group.Sum(item => item.Graded),
                group.Sum(item => item.ScoreSum), group.Sum(item => item.ScoreCount)))
            .ToListAsync(cancellationToken);
        var subaccountFacts = await facts.GroupBy(item => item.Course.CanvasAccountId)
            .Select(group => new LeadershipBreakdownFact(
                group.Key.HasValue ? group.Key.Value.ToString() : "—",
                group.Key.HasValue ? group.Key.Value.ToString() : "—", group.Count(),
                group.Select(item => item.StudentProfileId).Distinct().Count(),
                group.Select(item => item.CanvasCourseId).Distinct().Count(),
                group.Sum(item => item.Submitted), group.Sum(item => item.Missing),
                group.Sum(item => item.Late), group.Sum(item => item.Graded),
                group.Sum(item => item.ScoreSum), group.Sum(item => item.ScoreCount)))
            .ToListAsync(cancellationToken);
        var courseFacts = await facts.GroupBy(item => new { item.CanvasCourseId, item.Course.Name })
            .Select(group => new LeadershipBreakdownFact(group.Key.CanvasCourseId.ToString(),
                group.Key.Name ?? group.Key.CanvasCourseId.ToString(), group.Count(),
                group.Select(item => item.StudentProfileId).Distinct().Count(),
                group.Select(item => item.CanvasCourseId).Distinct().Count(),
                group.Sum(item => item.Submitted), group.Sum(item => item.Missing),
                group.Sum(item => item.Late), group.Sum(item => item.Graded),
                group.Sum(item => item.ScoreSum), group.Sum(item => item.ScoreCount)))
            .ToListAsync(cancellationToken);
        var categoryFacts = await facts.GroupBy(item => item.AssessmentCategory ?? "—")
            .Select(group => new LeadershipBreakdownFact(group.Key, group.Key,
                group.Count(),
                group.Select(item => item.StudentProfileId).Distinct().Count(),
                group.Select(item => item.CanvasCourseId).Distinct().Count(),
                group.Sum(item => item.Submitted), group.Sum(item => item.Missing),
                group.Sum(item => item.Late), group.Sum(item => item.Graded),
                group.Sum(item => item.ScoreSum), group.Sum(item => item.ScoreCount)))
            .ToListAsync(cancellationToken);

        var resultAccountIds = subaccountFacts.Select(item => long.TryParse(item.Key, out var id) ? id : 0)
            .Concat(pageFacts.Select(item => item.SubaccountId ?? 0)).Where(id => id > 0)
            .Distinct().ToList();
        var accountNames = await db.CanvasAccounts.AsNoTracking()
            .Where(item => resultAccountIds.Contains(item.CanvasAccountId))
            .Select(item => new { item.CanvasAccountId, item.Name })
            .ToDictionaryAsync(item => item.CanvasAccountId,
                item => CanvasReportFilters.SubjectDisplayName(
                    item.Name, item.CanvasAccountId), cancellationToken);
        var rows = pageFacts.Select(item => ToRow([item], accountNames)).ToList();
        var breakdowns = new LeadershipReportBreakdowns(
            ToBreakdowns(gradeFacts), ToBreakdowns(homeroomFacts),
            ToBreakdowns(subaccountFacts, item => long.TryParse(item.Key, out var id) &&
                accountNames.TryGetValue(id, out var name) ? name : item.Label),
            ToBreakdowns(courseFacts), ToBreakdowns(categoryFacts),
            monthTotals.Select(item =>
            {
                var label = item.Month?.ToString("yyyy-MM") ?? "Undated";
                return new LeadershipReportBreakdown(label, label, item.StudentCount,
                    item.StudentCount, item.CourseCount, (int)item.Submitted, (int)item.Missing,
                    (int)item.Late, (int)item.Graded, Score(item.ScoreSum, item.ScoreCount),
                    Percent(item.Submitted, item.Submitted + item.Missing),
                    Percent(item.Graded, item.Submitted));
            }).OrderBy(item => item.Key, StringComparer.Ordinal).ToList());
        var finalCourseScores = finalCourseFacts is null ? null
            : await LoadFinalCourseScores(finalCourseFacts, cancellationToken);
        var notice = totalCount == 0 ? "no_matching_data" : null;
        return Ok(new LeadershipReportResult(request.ReportKey, true, notice, scoreSource, summary,
            breakdowns, rows, totalCount,
            page, pageSize, finalCourseScores));
    }

    private static async Task<FinalCourseScoreSection> LoadFinalCourseScores(
        IQueryable<CanvasLeadershipReportAggregate> facts, CancellationToken cancellationToken)
    {
        var summaryFact = await facts.GroupBy(_ => 1).Select(group => new
            {
                StudentCount = group.Select(item => item.StudentProfileId).Distinct().Count(),
                CourseCount = group.Select(item => item.CanvasCourseId).Distinct().Count(),
                ScoreSum = group.Sum(item => item.ScoreSum),
                ScoreCount = group.Sum(item => item.ScoreCount)
            }).SingleOrDefaultAsync(cancellationToken);
        var courseRows = await facts.GroupBy(item => new { item.CanvasCourseId, item.Course.Name })
            .Select(group => new FinalCourseScoreBreakdownFact(
                group.Key.CanvasCourseId.ToString(), group.Key.Name ?? group.Key.CanvasCourseId.ToString(),
                group.Select(item => item.StudentProfileId).Distinct().Count(), 1,
                group.Sum(item => item.ScoreSum), group.Sum(item => item.ScoreCount)))
            .ToListAsync(cancellationToken);
        var gradeRows = await facts.GroupBy(item =>
                (item.Student.GradeLevel ?? item.Student.CanvasGradeLevel ?? item.Course.CanvasGradeLevel) ?? "—")
            .Select(group => new FinalCourseScoreBreakdownFact(group.Key, group.Key,
                group.Select(item => item.StudentProfileId).Distinct().Count(),
                group.Select(item => item.CanvasCourseId).Distinct().Count(),
                group.Sum(item => item.ScoreSum), group.Sum(item => item.ScoreCount)))
            .ToListAsync(cancellationToken);
        var homeroomRows = await facts.GroupBy(item => item.Student.Homeroom ?? "—")
            .Select(group => new FinalCourseScoreBreakdownFact(group.Key, group.Key,
                group.Select(item => item.StudentProfileId).Distinct().Count(),
                group.Select(item => item.CanvasCourseId).Distinct().Count(),
                group.Sum(item => item.ScoreSum), group.Sum(item => item.ScoreCount)))
            .ToListAsync(cancellationToken);
        var months = await facts.GroupBy(item => item.AcademicPeriodStart)
            .Select(group => new
            {
                Month = group.Key,
                StudentCount = group.Select(item => item.StudentProfileId).Distinct().Count(),
                CourseCount = group.Select(item => item.CanvasCourseId).Distinct().Count(),
                ScoreSum = group.Sum(item => item.ScoreSum),
                ScoreCount = group.Sum(item => item.ScoreCount)
            })
            .ToListAsync(cancellationToken);

        List<FinalCourseScoreBreakdown> Breakdown(IEnumerable<FinalCourseScoreBreakdownFact> source) =>
            source.Select(item => new FinalCourseScoreBreakdown(item.Key, item.Label,
                    item.StudentCount, item.CourseCount, item.ScoreCount,
                    Score(item.ScoreSum, item.ScoreCount)))
                .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase).ToList();

        var summary = summaryFact is null
            ? new FinalCourseScoreSummary(0, 0, 0, null)
            : new FinalCourseScoreSummary(summaryFact.StudentCount, summaryFact.CourseCount,
                summaryFact.ScoreCount, Score(summaryFact.ScoreSum, summaryFact.ScoreCount));

        return new FinalCourseScoreSection(
            "canvas-course-final-score-percent", summary,
            Breakdown(courseRows), Breakdown(gradeRows), Breakdown(homeroomRows),
            months.Select(item =>
            {
                var label = item.Month?.ToString("yyyy-MM") ?? "Undated";
                return new FinalCourseScoreBreakdown(label, label, item.StudentCount,
                    item.CourseCount, item.ScoreCount, Score(item.ScoreSum, item.ScoreCount));
            }).OrderBy(item => item.Key, StringComparer.Ordinal).ToList());
    }

    private IQueryable<CanvasLeadershipReportAggregate> VisibleFacts() =>
        access.ApplyStudentScope(db.CanvasLeadershipReportAggregates.AsNoTracking(), item => item.Student);

    private static LeadershipReportRow ToRow(
        IEnumerable<LeadershipFactRow> source, IReadOnlyDictionary<long, string> accountNames)
    {
        var group = source.ToList();
        var first = group[0];
        var submitted = group.Sum(item => item.Submitted);
        var missing = group.Sum(item => item.Missing);
        var late = group.Sum(item => item.Late);
        var graded = group.Sum(item => item.Graded);
        var scoreSum = group.Sum(item => item.ScoreSum);
        var scoreCount = group.Sum(item => item.ScoreCount);
        var groupNames = group.Select(item => item.AssignmentGroupName).Distinct().ToList();
        var groupName = groupNames.Count == 1 ? groupNames[0] : null;
        var category = groupName is null ? null
            : CanvasReportFilters.CategoryForGroup(groupName);
        var accountId = first.SubaccountId;
        return new LeadershipReportRow(first.StudentId, first.SisUserId, first.Name,
            first.Grade, first.Homeroom, first.CourseId,
            first.CourseName ?? first.CourseId.ToString(), accountId,
            accountId.HasValue && accountNames.TryGetValue(accountId.Value, out var name) ? name : null,
            category, groupName,
            (int)Math.Max(Math.Max(submitted + missing, graded), scoreCount),
            (int)submitted, (int)missing, (int)late, (int)graded,
            Score(scoreSum, scoreCount), scoreSum, scoreCount,
            first.Month?.ToString("yyyy-MM"));
    }

    private static List<LeadershipReportBreakdown> ToBreakdowns(
        IEnumerable<LeadershipBreakdownFact> source,
        Func<LeadershipBreakdownFact, string>? label = null) => source.Select(item =>
            new LeadershipReportBreakdown(item.Key, label?.Invoke(item) ?? item.Label,
                item.Count, item.StudentCount, item.CourseCount,
                (int)item.Submitted, (int)item.Missing, (int)item.Late, (int)item.Graded,
                Score(item.ScoreSum, item.ScoreCount),
                Percent(item.Submitted, item.Submitted + item.Missing),
                Percent(item.Graded, item.Submitted)))
            .OrderBy(item => item.Label, StringComparer.CurrentCultureIgnoreCase).ToList();

    private static double? Score(double sum, int count) => count > 0
        ? Math.Round(sum / count, 1) : null;
    private static double? Percent(double part, double total) => total > 0
        ? Math.Round(part / total * 100, 1) : null;

    private static LeadershipReportResult EmptyResult(LeadershipReportQuery request, int pageSize,
        string scoreSource, string notice, bool available) =>
        new(request.ReportKey, available, notice, scoreSource,
            new LeadershipReportSummary(0, 0, 0, 0, 0, 0, 0, null, null, null),
            new LeadershipReportBreakdowns([], [], [], [], [], []), [], 0, request.Page, pageSize,
            request.ReportKey == "assessment-results"
                ? new FinalCourseScoreSection("canvas-course-final-score-percent",
                    new FinalCourseScoreSummary(0, 0, 0, null), [], [], [], [])
                : null);

    private static string? Validate(LeadershipReportQuery request)
    {
        if (request.ReportKey is not ("learning-engagement" or "assessment-results"))
            return "Unknown report type.";
        if (string.IsNullOrWhiteSpace(request.AcademicYear) ||
            !DashboardPeriodNormalizer.IsAcademicYear(request.AcademicYear))
            return "Select a valid academic year.";
        if (!string.IsNullOrWhiteSpace(request.Term) && request.Term != "all" &&
            DashboardPeriodNormalizer.Term(request.Term) is not ("hk1" or "hk2"))
            return "Select a valid reporting term.";
        if (request.SubaccountIds is null || request.SubaccountIds.Count > 50 ||
            request.SubaccountIds.Any(id => id <= 0))
            return "Invalid subaccount selection.";
        if (request.Grade?.Length > 50 || request.Homeroom?.Length > 100)
            return "Invalid grade or homeroom selection.";
        var category = CanvasReportFilters.CanonicalCategory(request.AssessmentCategory);
        if (category is null) return "Unknown assessment category.";
        if (request.Page is < 1 or > 100000 || request.PageSize < 1 ||
            request.PageSize > (request.Export ? MaximumExportPageSize : 100))
            return "Invalid report page.";
        return null;
    }

    private sealed record LeadershipFactRow(int StudentId, string? SisUserId, string Name,
        string? Grade, string? Homeroom, long CourseId, string? CourseName, long? SubaccountId,
        string? AssignmentGroupName, DateTime? Month, double Submitted, double Missing,
        double Late, double Graded, double ScoreSum, int ScoreCount);
    private sealed record LeadershipBreakdownFact(string Key, string Label, int Count,
        int StudentCount, int CourseCount, double Submitted, double Missing, double Late,
        double Graded, double ScoreSum, int ScoreCount);
    private sealed record FinalCourseScoreBreakdownFact(string Key, string Label,
        int StudentCount, int CourseCount, double ScoreSum, int ScoreCount);
}

public sealed record LeadershipReportQuery
{
    public string ReportKey { get; init; } = "learning-engagement";
    public string? AcademicYear { get; init; }
    public string? Term { get; init; } = "all";
    public string? Grade { get; init; }
    public string? Homeroom { get; init; }
    public List<long> SubaccountIds { get; init; } = [];
    public string AssessmentCategory { get; init; } = "all";
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public bool Export { get; init; }
    public bool IncludeSummary { get; init; } = true;
}

public sealed record LeadershipReportOptions(List<string> AcademicYears, List<string> Terms,
    List<string> Grades, List<string> Homerooms, List<CanvasReportCohortOption> Cohorts,
    List<CanvasReportSubaccountOption> Subaccounts, List<string> AssessmentCategories);
public sealed record LeadershipReportSummary(int StudentCount, int CourseCount, int ActivityCount,
    int Submitted, int Missing, int Late, int Graded, double? AverageScorePercent,
    double? CompletionRatePercent, double? GradingRatePercent);
public sealed record LeadershipReportBreakdown(string Key, string Label, int Count,
    int StudentCount, int CourseCount, int Submitted, int Missing, int Late, int Graded,
    double? AverageScorePercent, double? CompletionRatePercent, double? GradingRatePercent);
public sealed record LeadershipReportBreakdowns(List<LeadershipReportBreakdown> ByGrade,
    List<LeadershipReportBreakdown> ByHomeroom, List<LeadershipReportBreakdown> BySubaccount,
    List<LeadershipReportBreakdown> ByCourse, List<LeadershipReportBreakdown> ByAssessmentCategory,
    List<LeadershipReportBreakdown> ByMonth);
public sealed record LeadershipReportRow(int StudentId, string? SisUserId, string Name,
    string? Grade, string? Homeroom, long CourseId, string CourseName,
    long? SubaccountId, string? SubaccountName, string? AssessmentCategory,
    string? AssignmentGroupName, int ActivityCount, int Submitted, int Missing,
    int Late, int Graded, double? AverageScorePercent, double ScoreSum,
    int ScoreCount, string? Month);
public sealed record LeadershipReportResult(string ReportKey, bool AggregateAvailable,
    string? Notice, string ScoreSource, LeadershipReportSummary Summary,
    LeadershipReportBreakdowns Breakdowns, List<LeadershipReportRow> Rows,
    int TotalCount, int Page, int PageSize, FinalCourseScoreSection? FinalCourseScores);
/// <summary>
/// Canvas final-course-score observations. A student/course may contribute more than one
/// observation when Canvas supplies both a course total and grading-period totals.
/// </summary>
public sealed record FinalCourseScoreSummary(int StudentCount, int CourseCount,
    int ScoreCount, double? AverageScorePercent);
public sealed record FinalCourseScoreBreakdown(string Key, string Label, int StudentCount,
    int CourseCount, int ScoreCount, double? AverageScorePercent);
public sealed record FinalCourseScoreSection(string ScoreSource, FinalCourseScoreSummary Summary,
    List<FinalCourseScoreBreakdown> ByCourse, List<FinalCourseScoreBreakdown> ByGrade,
    List<FinalCourseScoreBreakdown> ByHomeroom, List<FinalCourseScoreBreakdown> ByMonth);
