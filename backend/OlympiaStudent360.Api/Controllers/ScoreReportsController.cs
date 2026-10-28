using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

/// <summary>
/// Focused score views requested by school and subject leadership. The source scores are
/// persisted Canvas report facts; this controller never calculates from raw submissions.
/// </summary>
[Route("api/score-reports")]
[ApiController]
[Authorize]
public sealed class ScoreReportsController(
    AppDbContext db, UserAccessService access) : ControllerBase
{
    private static readonly HashSet<string> ViewKeys =
    [
        "subject-grade-average", "subject-letter-distribution", "grade-letter-distribution",
        "class-letter-distribution", "score-histogram", "assessment-average",
        "current-course-score", "class-subject-distribution", "top-students", "bottom-students"
    ];
    private const string TestSource = "canvas-submission-score-percent";
    private const string CurrentSource = "canvas-current-score-percent";

    [HttpGet("options")]
    public async Task<ActionResult<ScoreReportOptions>> Options(CancellationToken cancellationToken)
    {
        if (!access.Current.CanAccessAcademic) return Forbid();
        var visible = VisibleLeadershipFacts();
        // Options include both programmes so ELA remains available after selecting ESL alone.
        var courseRows = await LoadScoreCourses(visible.Select(item => item.CanvasCourseId),
            CanvasScoreCoursePolicy.Programmes, cancellationToken);
        var courseIds = courseRows.Select(course => course.CanvasCourseId).ToHashSet();
        visible = visible.WhereMatchesAnyValue(item => item.CanvasCourseId, courseIds);
        var periods = await visible.Select(item => new { item.AcademicYear, item.Term })
            .Distinct().ToListAsync(cancellationToken);
        var students = await visible.Select(item => new
            {
                Grade = item.Student.GradeLevel ?? item.Student.CanvasGradeLevel ?? item.Course.CanvasGradeLevel,
                item.Student.Homeroom
            }).Distinct().ToListAsync(cancellationToken);
        var categories = CanvasReportFilters.AvailableCategories(await visible
            .Where(item => !item.IsFinalCourseScore && item.AssessmentCategory != null)
            .Select(item => item.AssessmentCategory).Distinct().ToListAsync(cancellationToken));
        var subjectOptions = courseRows
            .Select(course => CanvasCourseSubject(course.CanvasCourseId, course.SisCourseId,
                course.CourseCode, course.Name))
            .Select(subject => new ScoreSubjectOption(subject.Code, subject.Name))
            .OrderBy(option => option.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

        // Course sections are options only when a visible student is enrolled in them.
        var visibleEnrollments = access.ApplyStudentScope(
            db.CanvasEnrollments.AsNoTracking().Where(item => item.CanvasCourseSectionId != null &&
                item.Type == "StudentEnrollment" &&
                (item.EnrollmentState == "active" || item.EnrollmentState == "completed")),
            item => item.Student);
        var sectionRows = (await db.CanvasCourseSections.AsNoTracking()
                .Where(section => visibleEnrollments.Any(enrollment =>
                    enrollment.CanvasCourseSectionId == section.CanvasSectionId))
                .Select(item => new { item.Name, item.CanvasCourseId })
                .ToListAsync(cancellationToken))
            .Where(section => courseIds.Contains(section.CanvasCourseId))
            .Where(section => !string.IsNullOrWhiteSpace(section.Name))
            .ToList();
        var subaccounts = await CanvasReportFilters.VisibleSubaccounts(
            db, courseIds, cancellationToken);
        var subjectOptionByAccountId = subaccounts
            .SelectMany(option => option.AccountIds.Select(accountId => new
            {
                AccountId = accountId,
                OptionId = option.Id
            }))
            .ToDictionary(item => item.AccountId, item => item.OptionId);
        var parentByAccountId = await db.CanvasAccounts.AsNoTracking()
            .ToDictionaryAsync(account => account.CanvasAccountId,
                account => account.ParentAccountId, cancellationToken);
        var accountByCourseId = courseRows.ToDictionary(course => course.CanvasCourseId,
            course => course.CanvasAccountId);

        List<long> SelectableAncestors(long courseId)
        {
            if (!accountByCourseId.TryGetValue(courseId, out var accountId)) return [];
            var result = new List<long>();
            var visited = new HashSet<long>();
            while (accountId is { } id && visited.Add(id))
            {
                if (subjectOptionByAccountId.TryGetValue(id, out var optionId)) result.Add(optionId);
                accountId = parentByAccountId.GetValueOrDefault(id);
            }
            return result;
        }

        var sectionOptions = sectionRows
            .GroupBy(section => section.Name!, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ScoreSectionOption(group.First().Name!, group
                .SelectMany(section => SelectableAncestors(section.CanvasCourseId))
                .Distinct().OrderBy(id => id).ToList()))
            .OrderBy(option => option.Name, StringComparer.CurrentCultureIgnoreCase).ToList();

        return Ok(new ScoreReportOptions(
            periods.Select(item => item.AcademicYear).Where(DashboardPeriodNormalizer.IsAcademicYear)
                .Distinct().OrderByDescending(value => value).ToList(),
            periods.Select(item => item.Term).Where(value => value is "hk1" or "hk2")
                .Distinct().OrderBy(value => value).ToList(),
            students.Select(item => item.Grade).OfType<string>().Where(value => value.Length > 0)
                .Distinct().OrderBy(value => value).ToList(),
            students.Select(item => item.Homeroom).OfType<string>().Where(value => value.Length > 0)
                .Distinct().OrderBy(value => value).ToList(),
            students.Where(item => !string.IsNullOrWhiteSpace(item.Grade) &&
                    !string.IsNullOrWhiteSpace(item.Homeroom))
                .Select(item => new CanvasReportCohortOption(item.Grade!, item.Homeroom!))
                .Distinct().OrderBy(item => item.Grade).ThenBy(item => item.Homeroom).ToList(),
            subaccounts, categories,
            subjectOptions, sectionOptions,
            TeacherSchoolLevels.Values.ToList()));
    }

    [HttpPost("run")]
    public async Task<ActionResult<ScoreReportResult>> Run(
        ScoreReportQuery request, CancellationToken cancellationToken)
    {
        if (!access.Current.CanAccessAcademic) return Forbid();
        var error = Validate(request);
        if (error is not null) return BadRequest(error);
        request = NormalizeReportSpecificFilters(request);

        var isLetter = request.ViewKey is "subject-letter-distribution" or
            "grade-letter-distribution" or "class-letter-distribution" or "class-subject-distribution";
        // Each report owns its Canvas score source. Only the dedicated current-score view reads
        // enrollment current_score; every assessment view reads graded assignment-group points.
        var isCurrent = request.ViewKey == "current-course-score";
        var source = isCurrent ? CurrentSource : TestSource;
        var unit = request.ViewKey == "current-course-score" ? "percent" : "score10";
        if (!await CanvasDashboardAggregateService.HasReadableSnapshotAsync(
                db, cancellationToken, requireLeadershipActivityDates: !isCurrent && request.WeekStart is not null))
            return Ok(EmptyResult(request.ViewKey, source, unit, "aggregate_unavailable", false));

        if (request.ViewKey == "grade-letter-distribution" && request.SubaccountIds.Count != 1)
            return Ok(EmptyResult(request.ViewKey, source, unit, "select_subject", true));
        if (request.ViewKey == "class-letter-distribution" &&
            (request.SubaccountIds.Count != 1 || string.IsNullOrWhiteSpace(request.Grade)))
            return Ok(EmptyResult(request.ViewKey, source, unit, "select_subject_and_grade", true));
        if (request.ViewKey == "class-subject-distribution" &&
            string.IsNullOrWhiteSpace(request.CanvasSectionName) && request.CanvasSectionId is null)
            return Ok(EmptyResult(request.ViewKey, source, unit, "select_section", true));

        var courseMap = await LoadCourseMap(request.AcademicYear!, isCurrent,
            request.EnglishProgrammes, cancellationToken);
        HashSet<long>? subjectCourseIds = courseMap.Keys.ToHashSet();
        if (!string.IsNullOrWhiteSpace(request.SubjectCode))
            subjectCourseIds = courseMap.Values
                .Where(course => string.Equals(course.Subject.Code, request.SubjectCode,
                    StringComparison.OrdinalIgnoreCase))
                .Select(course => course.Id).ToHashSet();
        var subaccountIds = request.SubaccountIds.Count == 0 ? null
            : await CanvasReportFilters.AccountAndDescendantIds(
                db, request.SubaccountIds, cancellationToken);
        if (request.ViewKey == "score-histogram")
            return Ok(await Histogram(request, courseMap, subjectCourseIds, subaccountIds,
                cancellationToken));

        var observations = isCurrent
            ? await CurrentObservations(request, subjectCourseIds, subaccountIds, cancellationToken)
            : await TestObservations(request, subjectCourseIds, subaccountIds, cancellationToken);
        var scores = observations.Where(row => row.ScoreCount > 0 && courseMap.ContainsKey(row.CourseId))
            .Select(row => new ScoredObservation(row, courseMap[row.CourseId].Subject,
                request.ViewKey == "current-course-score" ? 1.0 : 0.1)).ToList();
        if (request.ViewKey == "class-subject-distribution")
            scores = await ExpandByCanvasSection(scores, request, cancellationToken);
        var studentCourses = StudentCourseScores(scores);

        List<ScoreReportRow> rows = request.ViewKey switch
        {
            "subject-grade-average" => AverageRows(studentCourses,
                row => $"{row.Source.Subject.Code}|{row.Source.Observation.Grade}",
                row => row.Source.Subject.Name, includeGrade: true),
            "assessment-average" or "current-course-score" => AverageRows(studentCourses,
                row => row.Source.Subject.Code, row => row.Source.Subject.Name),
            "top-students" => StudentRankRows(studentCourses, request.RankingLimit, descending: true),
            "bottom-students" => StudentRankRows(studentCourses, request.RankingLimit, descending: false),
            "subject-letter-distribution" => LetterRows(studentCourses,
                row => row.Source.Subject.Code, row => row.Source.Subject.Name),
            "grade-letter-distribution" => LetterRows(studentCourses,
                row => row.Source.Observation.Grade ?? "—",
                row => row.Source.Observation.Grade ?? "—",
                includeGrade: true),
            "class-letter-distribution" => LetterRows(studentCourses,
                row => row.Source.Observation.Homeroom ?? "—",
                row => row.Source.Observation.Homeroom ?? "—",
                includeHomeroom: true),
            "class-subject-distribution" => LetterRows(studentCourses,
                row => row.Source.Subject.Code, row => row.Source.Subject.Name),
            _ => []
        };
        if (request.ViewKey == "subject-grade-average")
            rows = rows.OrderBy(row => row.SubjectName, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(row => row.Grade).ToList();
        var total = isLetter ? TotalFromRows(rows) : Total(studentCourses);
        if (isLetter)
            total = total with
            {
                StudentCount = studentCourses.Select(row => row.Source.Observation.StudentId)
                    .Distinct().Count()
            };
        if (request.ViewKey is "top-students" or "bottom-students")
            total = TotalFromRows(rows);
        var notice = rows.Count == 0 ? "no_matching_data" : null;
        if (notice == "no_matching_data" && isCurrent && !string.IsNullOrWhiteSpace(request.Term) &&
            request.Term != "all")
        {
            var yearwide = await CurrentObservations(request with { Term = "all" },
                subjectCourseIds, subaccountIds, cancellationToken);
            if (yearwide.Count > 0) notice = "term_current_score_unavailable";
        }
        return Ok(new ScoreReportResult(request.ViewKey, true,
            notice, source, unit, rows, total));
    }

    private async Task<Dictionary<long, CourseInfo>> LoadCourseMap(
        string academicYear, bool isCurrent, IReadOnlyCollection<string> englishProgrammes,
        CancellationToken cancellationToken)
    {
        var year = DashboardPeriodNormalizer.AcademicYear(academicYear);
        var visibleCourseIds = isCurrent
            ? VisibleMetrics().Where(item => item.AcademicYear == year &&
                item.MetricKey == CanvasDashboardAggregateService.CurrentScoreAverage)
                .Select(item => item.CanvasCourseId)
            : VisibleLeadershipFacts().Where(item => item.AcademicYear == year &&
                !item.IsFinalCourseScore).Select(item => item.CanvasCourseId);
        var courses = await LoadScoreCourses(visibleCourseIds, englishProgrammes, cancellationToken);
        return courses.ToDictionary(item => item.CanvasCourseId, item => new CourseInfo(
            item.CanvasCourseId, CanvasCourseSubject(item.CanvasCourseId, item.SisCourseId,
                item.CourseCode, item.Name)));
    }

    private async Task<List<ScoreCourse>> LoadScoreCourses(
        IQueryable<long> visibleCourseIds, IReadOnlyCollection<string> englishProgrammes,
        CancellationToken cancellationToken)
    {
        var courses = await db.CanvasCourses.AsNoTracking()
            .Where(course => visibleCourseIds.Any(id => id == course.CanvasCourseId))
            .Select(course => new ScoreCourse(course.CanvasCourseId, course.SisCourseId,
                course.CourseCode, course.Name, course.CanvasAccountId))
            .ToListAsync(cancellationToken);
        var accounts = await db.CanvasAccounts.AsNoTracking()
            .Select(account => new { account.CanvasAccountId, account.ParentAccountId, account.Name })
            .ToListAsync(cancellationToken);
        var englishAccounts = CanvasScoreCoursePolicy.EnglishAccountIds(accounts.Select(account =>
            (account.CanvasAccountId, account.ParentAccountId, account.Name)));
        // Course IDs survive in the stored report facts. Apply the policy when reading
        // them; no ETL rebuild or deletion of ELA history is needed.
        return courses.Where(course => CanvasScoreCoursePolicy.Include(course.Name, course.CourseCode,
            course.CanvasAccountId is { } id && englishAccounts.Contains(id), englishProgrammes)).ToList();
    }

    private static ScoreSubject CanvasCourseSubject(long canvasCourseId, string? sisCourseId,
        string? courseCode, string? courseName)
    {
        // Score reports follow the Canvas warehouse directly. Keep each Canvas course distinct
        // and show the exact Canvas course name instead of translating it through the transcript
        // subject catalog or any workbook-derived subject list.
        var name = FirstNonBlank(courseName, courseCode, sisCourseId)
            ?? $"Canvas course {canvasCourseId}";
        return new ScoreSubject($"canvas-course-{canvasCourseId}", name);
    }

    private static string? FirstNonBlank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private IQueryable<CanvasLeadershipReportAggregate> VisibleLeadershipFacts() =>
        access.ApplyStudentScope(db.CanvasLeadershipReportAggregates.AsNoTracking(), item => item.Student);

    private IQueryable<CanvasDashboardMetricAggregate> VisibleMetrics() =>
        access.ApplyStudentScope(db.CanvasDashboardMetricAggregates.AsNoTracking(), item => item.Student);

    private async Task<List<ScoreObservation>> TestObservations(ScoreReportQuery request,
        HashSet<long>? subjectCourseIds, List<long>? subaccountIds, CancellationToken cancellationToken)
    {
        var facts = VisibleLeadershipFacts().Where(item => !item.IsFinalCourseScore &&
            item.PossiblePoints > 0 &&
            item.AcademicYear == DashboardPeriodNormalizer.AcademicYear(request.AcademicYear!));
        facts = ApplyShared(facts, request, subjectCourseIds, subaccountIds);
        facts = CanvasActivityPeriodFilter.Apply(facts, request.Month, request.WeekStart);
        var category = CanvasReportFilters.CanonicalCategory(request.AssessmentCategory)!;
        if (category != "all") facts = facts.Where(item => item.AssessmentCategory == category);

        return await facts.GroupBy(item => new
            {
                item.StudentProfileId, item.Student.SISUserId, item.Student.FullName,
                Grade = item.Student.GradeLevel ?? item.Student.CanvasGradeLevel ?? item.Course.CanvasGradeLevel,
                item.Student.Homeroom, item.CanvasCourseId
            })
            .Select(group => new ScoreObservation(group.Key.StudentProfileId,
                group.Key.SISUserId, group.Key.FullName, group.Key.Grade,
                group.Key.Homeroom, group.Key.CanvasCourseId,
                group.Sum(item => item.EarnedPoints) /
                    group.Sum(item => item.PossiblePoints) * 100, 1))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<ScoreObservation>> CurrentObservations(ScoreReportQuery request,
        HashSet<long>? subjectCourseIds, List<long>? subaccountIds, CancellationToken cancellationToken)
    {
        var facts = VisibleMetrics().Where(item =>
            item.MetricKey == CanvasDashboardAggregateService.CurrentScoreAverage &&
            item.SampleCount > 0 &&
            item.AcademicYear == DashboardPeriodNormalizer.AcademicYear(request.AcademicYear!));
        facts = ApplyShared(facts, request, subjectCourseIds, subaccountIds);
        // Canvas current_score is an independent course-grade measure. Assignment-group filters
        // apply only to submission/test views and are deliberately not used here.
        var termRows = await facts.GroupBy(item => new
            {
                item.StudentProfileId, item.Student.SISUserId, item.Student.FullName,
                Grade = item.Student.GradeLevel ?? item.Student.CanvasGradeLevel ?? item.Course.CanvasGradeLevel,
                item.Student.Homeroom, item.CanvasCourseId, item.Term
            })
            .Select(group => new CurrentScoreFactRow(group.Key.StudentProfileId,
                group.Key.SISUserId, group.Key.FullName, group.Key.Grade,
                group.Key.Homeroom, group.Key.CanvasCourseId, group.Key.Term,
                group.Sum(item => item.ValueSum), group.Sum(item => item.SampleCount)))
            .ToListAsync(cancellationToken);
        // All-term current_score represents the current course state. Prefer Canvas's overall
        // course score; if absent, use the latest available period, never the average of overall
        // and period rows for the same learner/course. An explicit term uses only that term.
        return termRows.GroupBy(item => new { item.StudentId, item.CourseId })
            .Select(group =>
            {
                var picked = group.OrderByDescending(item => item.Term == "")
                    .ThenByDescending(item => item.Term == "hk2")
                    .ThenByDescending(item => item.Term == "hk1")
                    .First();
                return new ScoreObservation(picked.StudentId, picked.SisUserId,
                    picked.Name, picked.Grade, picked.Homeroom, picked.CourseId,
                    picked.ScoreSum, picked.ScoreCount);
            }).ToList();
    }

    private IQueryable<CanvasLeadershipReportAggregate> ApplyShared(
        IQueryable<CanvasLeadershipReportAggregate> facts, ScoreReportQuery request,
        HashSet<long>? subjectCourseIds, List<long>? subaccountIds)
    {
        if (!string.IsNullOrWhiteSpace(request.Term) && request.Term != "all")
            facts = facts.Where(item => item.Term == DashboardPeriodNormalizer.Term(request.Term));
        if (!string.IsNullOrWhiteSpace(request.Grade) && request.Grade != "all")
            facts = facts.Where(item => (item.Student.GradeLevel ??
                item.Student.CanvasGradeLevel ?? item.Course.CanvasGradeLevel) == request.Grade);
        if (!string.IsNullOrWhiteSpace(request.Homeroom) && request.Homeroom != "all")
            facts = facts.Where(item => item.Student.Homeroom == request.Homeroom);
        if (!string.IsNullOrWhiteSpace(request.SchoolLevel) && request.SchoolLevel != "all")
        {
            var grades = SchoolLevelGradeValues(request.SchoolLevel);
            facts = facts.WhereMatchesAny(item => item.Student.GradeLevel ??
                item.Student.CanvasGradeLevel ?? item.Course.CanvasGradeLevel, grades);
        }
        if (request.CanvasCourseId is { } courseId)
            facts = facts.Where(item => item.CanvasCourseId == courseId);
        if (subjectCourseIds is not null)
            facts = facts.WhereMatchesAnyValue(item => item.CanvasCourseId, subjectCourseIds);
        if (subaccountIds is not null)
            facts = facts.Where(item => item.Course.CanvasAccountId != null)
                .WhereMatchesAnyValue(item => item.Course.CanvasAccountId!.Value, subaccountIds);
        if (request.CanvasSectionId is { } sectionId)
            facts = facts.Where(item => db.CanvasEnrollments.Any(enrollment =>
                enrollment.StudentProfileId == item.StudentProfileId &&
                enrollment.CanvasCourseId == item.CanvasCourseId &&
                enrollment.CanvasCourseSectionId == sectionId &&
                enrollment.Type == "StudentEnrollment" &&
                (enrollment.EnrollmentState == "active" || enrollment.EnrollmentState == "completed")));
        if (!string.IsNullOrWhiteSpace(request.CanvasSectionName))
            facts = facts.Where(item => db.CanvasEnrollments.Any(enrollment =>
                enrollment.StudentProfileId == item.StudentProfileId &&
                enrollment.CanvasCourseId == item.CanvasCourseId &&
                enrollment.Section!.Name == request.CanvasSectionName &&
                enrollment.Type == "StudentEnrollment" &&
                (enrollment.EnrollmentState == "active" || enrollment.EnrollmentState == "completed")));
        return facts;
    }

    private IQueryable<CanvasDashboardMetricAggregate> ApplyShared(
        IQueryable<CanvasDashboardMetricAggregate> facts, ScoreReportQuery request,
        HashSet<long>? subjectCourseIds, List<long>? subaccountIds)
    {
        if (!string.IsNullOrWhiteSpace(request.Term) && request.Term != "all")
            facts = facts.Where(item => item.Term == DashboardPeriodNormalizer.Term(request.Term));
        if (!string.IsNullOrWhiteSpace(request.Grade) && request.Grade != "all")
            facts = facts.Where(item => (item.Student.GradeLevel ??
                item.Student.CanvasGradeLevel ?? item.Course.CanvasGradeLevel) == request.Grade);
        if (!string.IsNullOrWhiteSpace(request.Homeroom) && request.Homeroom != "all")
            facts = facts.Where(item => item.Student.Homeroom == request.Homeroom);
        if (!string.IsNullOrWhiteSpace(request.SchoolLevel) && request.SchoolLevel != "all")
        {
            var grades = SchoolLevelGradeValues(request.SchoolLevel);
            facts = facts.WhereMatchesAny(item => item.Student.GradeLevel ??
                item.Student.CanvasGradeLevel ?? item.Course.CanvasGradeLevel, grades);
        }
        if (request.CanvasCourseId is { } courseId)
            facts = facts.Where(item => item.CanvasCourseId == courseId);
        if (subjectCourseIds is not null)
            facts = facts.WhereMatchesAnyValue(item => item.CanvasCourseId, subjectCourseIds);
        if (subaccountIds is not null)
            facts = facts.Where(item => item.Course.CanvasAccountId != null)
                .WhereMatchesAnyValue(item => item.Course.CanvasAccountId!.Value, subaccountIds);
        if (request.CanvasSectionId is { } sectionId)
            facts = facts.Where(item => db.CanvasEnrollments.Any(enrollment =>
                enrollment.StudentProfileId == item.StudentProfileId &&
                enrollment.CanvasCourseId == item.CanvasCourseId &&
                enrollment.CanvasCourseSectionId == sectionId &&
                enrollment.Type == "StudentEnrollment" &&
                (enrollment.EnrollmentState == "active" || enrollment.EnrollmentState == "completed")));
        if (!string.IsNullOrWhiteSpace(request.CanvasSectionName))
            facts = facts.Where(item => db.CanvasEnrollments.Any(enrollment =>
                enrollment.StudentProfileId == item.StudentProfileId &&
                enrollment.CanvasCourseId == item.CanvasCourseId &&
                enrollment.Section!.Name == request.CanvasSectionName &&
                enrollment.Type == "StudentEnrollment" &&
                (enrollment.EnrollmentState == "active" || enrollment.EnrollmentState == "completed")));
        return facts;
    }

    private async Task<ScoreReportResult> Histogram(ScoreReportQuery request,
        Dictionary<long, CourseInfo> courseMap, HashSet<long>? subjectCourseIds,
        List<long>? subaccountIds, CancellationToken cancellationToken)
    {
        // Distribution rows use each student's rounded course result from graded Canvas
        // assignment-group earned/possible totals.
        var observations = await TestObservations(
            request, subjectCourseIds, subaccountIds, cancellationToken);
        var scores = StudentCourseScores(observations
            .Where(row => row.ScoreCount > 0 && courseMap.ContainsKey(row.CourseId))
            .Select(row => new ScoredObservation(row, courseMap[row.CourseId].Subject, 0.1))
            .ToList());
        var bins = scores.GroupBy(row =>
        {
            if (row.ScoreValue < 5) return 0;
            if (row.ScoreValue >= 9.5) return 10;
            return 1 + (int)Math.Floor((row.ScoreValue - 5) / 0.5);
        }).ToDictionary(group => group.Key, group => group.ToList());
        var rows = Enumerable.Range(0, 11).Select(index =>
        {
            bins.TryGetValue(index, out var bucket);
            bucket ??= [];
            var start = index == 0 ? 0 : 5 + (index - 1) * 0.5;
            double? end = index == 0 ? 5 : index == 10 ? null : start + 0.5;
            var label = index == 0 ? "0–<5" : index == 10 ? "9.5–10+" :
                $"{start:0.0}–<{end:0.0}";
            var sum = bucket.Sum(row => row.ScoreValue);
            return new ScoreReportRow
            {
                Key = index.ToString(), Label = label,
                BinStart = start, BinEnd = end,
                ScoreCount = bucket.Count,
                StudentCount = bucket.Select(row => row.Source.Observation.StudentId)
                    .Distinct().Count(),
                ScoreSum = sum, AverageScore = Average(sum, bucket.Count)
            };
        }).ToList();
        var totalCount = rows.Sum(row => row.ScoreCount);
        var totalSum = rows.Sum(row => row.ScoreSum);
        var distinctStudents = scores.Select(row => row.Source.Observation.StudentId)
            .Distinct().Count();
        var notice = totalCount == 0 ? "no_matching_data" : null;
        return new ScoreReportResult(request.ViewKey, true,
            notice, TestSource, "score10", rows,
            new ScoreReportTotal(totalSum, totalCount, distinctStudents,
                Average(totalSum, totalCount),
                0, 0, 0, 0, 0, null));
    }

    private static List<StudentSubjectScore> StudentCourseScores(List<ScoredObservation> scores) =>
        scores.Select(row =>
        {
            // Round each student/course result before aggregation so report bands and totals
            // use the same displayed precision.
            var value = row.ScoreSum / row.Observation.ScoreCount;
            if (row.Scale == 0.1)
                value = Math.Round(value, 1, MidpointRounding.AwayFromZero);
            return new StudentSubjectScore(row, value, row.SectionName);
        }).ToList();

    private async Task<List<ScoredObservation>> ExpandByCanvasSection(
        List<ScoredObservation> scores, ScoreReportQuery request,
        CancellationToken cancellationToken)
    {
        if (scores.Count == 0) return scores;
        var courseIds = scores.Select(row => row.Observation.CourseId).Distinct().ToList();
        var keySet = scores.Select(row => (row.Observation.StudentId, row.Observation.CourseId))
            .ToHashSet();
        var enrollmentRows = await db.CanvasEnrollments.AsNoTracking()
            .Where(item => item.StudentProfileId != null &&
                item.CanvasCourseSectionId != null && item.Type == "StudentEnrollment" &&
                (item.EnrollmentState == "active" || item.EnrollmentState == "completed"))
            .WhereMatchesAnyValue(item => item.CanvasCourseId, courseIds)
            .Select(item => new
            {
                StudentId = item.StudentProfileId!.Value,
                item.CanvasCourseId,
                item.CanvasCourseSectionId,
                item.EnrollmentState,
                SectionName = item.Section!.Name
            }).Distinct().ToListAsync(cancellationToken);
        var lookup = enrollmentRows.Where(item => keySet.Contains((item.StudentId, item.CanvasCourseId)) &&
                !string.IsNullOrWhiteSpace(item.SectionName) &&
                (string.IsNullOrWhiteSpace(request.CanvasSectionName) ||
                 item.SectionName == request.CanvasSectionName) &&
                (request.CanvasSectionId is null ||
                 item.CanvasCourseSectionId == request.CanvasSectionId))
            .GroupBy(item => (item.StudentId, item.CanvasCourseId))
            .ToDictionary(group => group.Key,
                group => group.OrderByDescending(item => item.EnrollmentState == "active")
                    .ThenBy(item => item.CanvasCourseSectionId)
                    .Select(item => item.SectionName!).First());
        return scores.Where(row => lookup.ContainsKey(
                (row.Observation.StudentId, row.Observation.CourseId)))
            .Select(row => row with
            {
                SectionName = lookup[(row.Observation.StudentId, row.Observation.CourseId)]
            }).ToList();
    }

    private static List<ScoreReportRow> AverageRows(List<StudentSubjectScore> scores,
        Func<StudentSubjectScore, string> key, Func<StudentSubjectScore, string> label,
        bool includeGrade = false) =>
        scores.GroupBy(key).Select(group =>
        {
            var first = group.First();
            var sum = group.Sum(row => row.ScoreValue);
            var count = group.Count();
            return new ScoreReportRow
            {
                Key = group.Key, Label = label(first),
                SubjectCode = first.Source.Subject.Code, SubjectName = first.Source.Subject.Name,
                Grade = includeGrade ? first.Source.Observation.Grade : null,
                ScoreSum = sum, ScoreCount = count,
                StudentCount = group.Select(row => row.Source.Observation.StudentId).Distinct().Count(),
                AverageScore = Average(sum, count)
            };
        }).OrderBy(row => row.Label, StringComparer.CurrentCultureIgnoreCase).ToList();

    private static List<ScoreReportRow> StudentRankRows(
        List<StudentSubjectScore> scores, int limit, bool descending)
    {
        var grouped = scores.GroupBy(row => row.Source.Observation.StudentId).Select(group =>
        {
            var first = group.First().Source.Observation;
            var studentAverage = group.Average(row => row.ScoreValue);
            return new ScoreReportRow
            {
                Key = first.StudentId.ToString(), Label = first.Name,
                StudentId = first.StudentId, SisUserId = first.SisUserId,
                Grade = first.Grade, Homeroom = first.Homeroom,
                ScoreSum = studentAverage, ScoreCount = 1, StudentCount = 1,
                AverageScore = Math.Round(studentAverage, 2)
            };
        });
        return (descending
                ? grouped.OrderByDescending(row => row.AverageScore)
                : grouped.OrderBy(row => row.AverageScore))
            .ThenBy(row => row.Label, StringComparer.CurrentCultureIgnoreCase)
            .Take(limit).ToList();
    }

    private static List<ScoreReportRow> LetterRows(List<StudentSubjectScore> scores,
        Func<StudentSubjectScore, string> key, Func<StudentSubjectScore, string> label,
        bool includeGrade = false, bool includeHomeroom = false)
    {
        return scores.GroupBy(key).Select(group =>
        {
            var first = group.First();
            // School report bands apply to each rounded 10-point course result:
            // A+ >=9, A >=8, B >=6.5, C >=5, D <5.
            var values = group.Select(row => Math.Round(row.ScoreValue, 1,
                    MidpointRounding.AwayFromZero))
                .ToList();
            var aPlus = values.Count(value => value >= 9);
            var a = values.Count(value => value >= 8 && value < 9);
            var b = values.Count(value => value >= 6.5 && value < 8);
            var c = values.Count(value => value >= 5 && value < 6.5);
            var d = values.Count(value => value < 5);
            var sum = group.Sum(row => row.ScoreValue);
            var count = group.Count();
            return new ScoreReportRow
            {
                Key = group.Key, Label = label(first),
                SubjectCode = includeGrade || includeHomeroom ? null : first.Source.Subject.Code,
                SubjectName = includeGrade || includeHomeroom ? null : first.Source.Subject.Name,
                Grade = includeGrade ? first.Source.Observation.Grade : null,
                Homeroom = includeHomeroom ? first.Source.Observation.Homeroom : null,
                ScoreSum = sum, ScoreCount = count,
                StudentCount = group.Select(row => row.Source.Observation.StudentId)
                    .Distinct().Count(),
                AverageScore = Average(sum, count),
                APlus = aPlus, A = a, B = b, C = c, D = d,
                CdPercent = Percent(c + d, values.Count)
            };
        }).OrderBy(row => row.Label, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private static ScoreReportTotal Total(List<StudentSubjectScore> scores)
    {
        var sum = scores.Sum(row => row.ScoreValue);
        var count = scores.Count;
        var studentCount = scores.Select(row => row.Source.Observation.StudentId).Distinct().Count();
        return new ScoreReportTotal(sum, count, studentCount, Average(sum, count),
            0, 0, 0, 0, 0, null);
    }

    private static ScoreReportTotal TotalFromRows(List<ScoreReportRow> rows)
    {
        var sum = rows.Sum(row => row.ScoreSum);
        var count = rows.Sum(row => row.ScoreCount);
        var aPlus = rows.Sum(row => row.APlus);
        var a = rows.Sum(row => row.A);
        var b = rows.Sum(row => row.B);
        var c = rows.Sum(row => row.C);
        var d = rows.Sum(row => row.D);
        return new ScoreReportTotal(sum, count, rows.Sum(row => row.StudentCount),
            Average(sum, count), aPlus, a, b, c, d,
            aPlus + a + b + c + d > 0 ? Percent(c + d, aPlus + a + b + c + d) : null);
    }

    private static ScoreReportResult EmptyResult(string key, string source, string unit,
        string notice, bool available) => new(key, available, notice, source, unit, [],
            new ScoreReportTotal(0, 0, 0, null, 0, 0, 0, 0, 0, null));

    private static List<string> SchoolLevelGradeValues(string schoolLevel) =>
        TeacherSchoolLevels.Grades(TeacherSchoolLevels.Canonical(schoolLevel) ?? "")
            .SelectMany(value => new[]
            {
                value, value.PadLeft(2, '0'), $"Grade{value}",
                $"Grade{value.PadLeft(2, '0')}", $"Grade {value}",
                $"Grade {value.PadLeft(2, '0')}"
            }).Distinct().ToList();

    private static double? Average(double sum, int count) => count > 0
        ? Math.Round(sum / count, 2) : null;
    private static double? Percent(double part, double total) => total > 0
        ? Math.Round(part / total * 100, 1) : null;

    private static string? Validate(ScoreReportQuery request)
    {
        var activityError = CanvasActivityPeriodFilter.Validate(request.AcademicYear, request.Month, request.WeekStart);
        if (activityError is not null) return activityError;
        if (request.ViewKey == "current-course-score" && (request.Month is not null || request.WeekStart is not null))
            return "Canvas current course scores are semester-level measures; activity filters apply to assignment reports.";
        if (!ViewKeys.Contains(request.ViewKey)) return "Unknown score report.";
        if (string.IsNullOrWhiteSpace(request.AcademicYear) ||
            !DashboardPeriodNormalizer.IsAcademicYear(request.AcademicYear))
            return "Select a valid academic year.";
        if (!string.IsNullOrWhiteSpace(request.Term) && request.Term != "all" &&
            DashboardPeriodNormalizer.Term(request.Term) is not ("hk1" or "hk2"))
            return "Select a valid reporting term.";
        if (CanvasReportFilters.CanonicalCategory(request.AssessmentCategory) is null)
            return "Unknown assessment category.";
        if (request.EnglishProgrammes is null || request.EnglishProgrammes.Count > 2 ||
            request.EnglishProgrammes.Any(value => !CanvasScoreCoursePolicy.Programmes.Contains(value)))
            return "Select ESL, ELA, or both English programmes.";
        if (!string.IsNullOrWhiteSpace(request.SchoolLevel) && request.SchoolLevel != "all" &&
            TeacherSchoolLevels.Canonical(request.SchoolLevel) is null)
            return "Unknown school level.";
        if (request.Grade?.Length > 50 || request.Homeroom?.Length > 100 ||
            request.SubjectCode?.Length > 150 || request.CanvasCourseId <= 0 ||
            request.CanvasSectionId <= 0 || request.CanvasSectionName?.Length > 150)
            return "Invalid score report filter.";
        if (request.SubaccountIds is null || request.SubaccountIds.Count > 50 ||
            request.SubaccountIds.Any(id => id <= 0))
            return "Invalid subaccount selection.";
        if (request.RankingLimit is not (10 or 25 or 50 or 100))
            return "Invalid ranking limit.";
        return null;
    }

    private static ScoreReportQuery NormalizeReportSpecificFilters(ScoreReportQuery request)
    {
        // These comparison reports are driven by the Canvas subject selection. Exact course
        // filters would narrow a cross-grade comparison to one course and make the two controls
        // conflict. The subject distribution also compares subjects across the selected cohort,
        // so a Canvas section restriction belongs only to the dedicated class-subject report.
        if (request.ViewKey is "grade-letter-distribution" or "class-letter-distribution")
            request = request with { SubjectCode = null, CanvasCourseId = null };
        if (request.ViewKey != "class-subject-distribution")
            request = request with { CanvasSectionId = null, CanvasSectionName = null };
        return request;
    }

    private sealed record ScoreSubject(string Code, string Name);
    private sealed record ScoreCourse(long CanvasCourseId, string? SisCourseId,
        string? CourseCode, string? Name, long? CanvasAccountId);
    private sealed record CourseInfo(long Id, ScoreSubject Subject);
    private sealed record ScoreObservation(int StudentId, string? SisUserId, string Name,
        string? Grade, string? Homeroom, long CourseId, double ScoreSum, int ScoreCount);
    private sealed record CurrentScoreFactRow(int StudentId, string? SisUserId, string Name,
        string? Grade, string? Homeroom, long CourseId, string Term,
        double ScoreSum, int ScoreCount);
    private sealed record ScoredObservation(ScoreObservation Observation,
        ScoreSubject Subject, double Scale, string? SectionName = null)
    {
        public double ScoreSum => Observation.ScoreSum * Scale;
    }
    private sealed record StudentSubjectScore(ScoredObservation Source,
        double ScoreValue, string? SectionName);
}

public sealed record ScoreReportQuery
{
    public int? Month { get; init; }
    public string? WeekStart { get; init; }
    public string ViewKey { get; init; } = "subject-grade-average";
    public string? AcademicYear { get; init; }
    public string? Term { get; init; } = "all";
    public string? Grade { get; init; }
    public string? Homeroom { get; init; }
    public string? SchoolLevel { get; init; }
    public long? CanvasSectionId { get; init; }
    public string? CanvasSectionName { get; init; }
    public string? SubjectCode { get; init; }
    public long? CanvasCourseId { get; init; }
    public List<long> SubaccountIds { get; init; } = [];
    public string AssessmentCategory { get; init; } = "all";
    public List<string> EnglishProgrammes { get; init; } = ["ESL", "ELA"];
    public int RankingLimit { get; init; } = 10;
}

public sealed record ScoreSubjectOption(string Code, string Name);
public sealed record ScoreSectionOption(string Name, List<long> SubaccountIds);
public sealed record ScoreReportOptions(List<string> AcademicYears, List<string> Terms,
    List<string> Grades, List<string> Homerooms, List<CanvasReportCohortOption> Cohorts,
    List<CanvasReportSubaccountOption> Subaccounts, List<string> AssessmentCategories,
    List<ScoreSubjectOption> Subjects, List<ScoreSectionOption> Sections,
    List<string> SchoolLevels);
public sealed class ScoreReportRow
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public string? SubjectCode { get; init; }
    public string? SubjectName { get; init; }
    public string? Grade { get; init; }
    public string? Homeroom { get; init; }
    public long? CanvasSectionId { get; init; }
    public string? SectionName { get; init; }
    public int? StudentId { get; init; }
    public string? SisUserId { get; init; }
    /// <summary>Unrounded sum, in the same unit as AverageScore.</summary>
    public double ScoreSum { get; init; }
    public int ScoreCount { get; init; }
    public int StudentCount { get; init; }
    public double? AverageScore { get; init; }
    public int APlus { get; init; }
    public int A { get; init; }
    public int B { get; init; }
    public int C { get; init; }
    public int D { get; init; }
    public double? CdPercent { get; init; }
    public double? BinStart { get; init; }
    public double? BinEnd { get; init; }
}
public sealed record ScoreReportTotal(double ScoreSum, int ScoreCount, int StudentCount,
    double? AverageScore, int APlus, int A, int B, int C, int D, double? CdPercent);
public sealed record ScoreReportResult(string ViewKey, bool AggregateAvailable, string? Notice,
    string ScoreSource, string Unit, List<ScoreReportRow> Rows, ScoreReportTotal Total);
