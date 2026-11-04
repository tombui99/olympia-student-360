using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports/subject-support")]
public sealed class SubjectSupportReportsController(
    AppDbContext db, UserAccessService access, CanvasReportingPeriodService periods) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SubjectSupportSummary>> Summary(
        string? year = null, string? term = "all", long? subjectId = null, CancellationToken cancellationToken = default)
    {
        if (!access.Current.CanAccessAcademic) return Forbid();
        if (!ValidFilters(year, term, subjectId)) return BadRequest();
        var report = await Load(year, term, subjectId, cancellationToken);
        return report is null ? NotFound() : Ok(report.Summary);
    }

    [HttpGet("students")]
    public async Task<ActionResult<SubjectSupportStudentPage>> Students(
        long subjectId, string? year = null, string? term = "all", string? grade = null,
        string? homeroom = null, bool supportOnly = false, string? search = null,
        int page = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        if (!access.Current.CanAccessAcademic) return Forbid();
        if (subjectId <= 0 || !ValidFilters(year, term, subjectId) || search?.Length > 100 ||
            grade?.Length > 20 || homeroom?.Length > 100) return BadRequest();
        var report = await Load(year, term, subjectId, cancellationToken);
        if (report is null) return NotFound();
        IEnumerable<SubjectSupportStudent> rows = report.Students;
        if (!string.IsNullOrWhiteSpace(grade)) rows = rows.Where(row => (row.GradeLevel ?? "—") == grade);
        if (!string.IsNullOrWhiteSpace(homeroom)) rows = rows.Where(row => (row.Homeroom ?? "—") == homeroom);
        if (supportOnly) rows = rows.Where(row => row.NeedsSupport);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            rows = rows.Where(row => new[] { row.FullName, row.SISUserId, row.SISStudentCode, row.Homeroom }
                .Any(label => label?.Contains(value, StringComparison.OrdinalIgnoreCase) == true));
        }
        var students = rows.OrderBy(row => row.FullName).ThenBy(row => row.Id).ToList();
        pageSize = Math.Clamp(pageSize, 1, 1000);
        var pages = (int)Math.Ceiling(students.Count / (double)pageSize);
        page = Math.Clamp(page, 1, Math.Max(1, pages));
        return Ok(new SubjectSupportStudentPage(students.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            page, pageSize, students.Count, pages));
    }

    private static bool ValidFilters(string? year, string? term, long? subjectId) =>
        (string.IsNullOrWhiteSpace(year) || DashboardPeriodNormalizer.IsAcademicYear(year)) &&
        (string.IsNullOrWhiteSpace(term) || term is "all" or "hk1" or "hk2") &&
        (subjectId is null or > 0);

    private async Task<Report?> Load(string? year, string? term, long? subjectId, CancellationToken ct)
    {
        var period = await periods.ResolveAsync(year, term ?? "all");
        var threshold = await db.StudentClassificationSettings.AsNoTracking().OrderBy(item => item.Id)
            .Select(item => (decimal?)item.HighRiskCanvasAverageBelow).FirstOrDefaultAsync(ct)
            ?? new StudentClassificationSettingsDto().HighRiskCanvasAverageBelow;
        var roster = access.ApplyStudentScope(db.StudentProfiles.AsNoTracking().CurrentRoster(db));
        var enrollments = db.CanvasEnrollments.AsNoTracking().Where(enrollment =>
            enrollment.StudentProfileId != null && roster.Any(student => student.Id == enrollment.StudentProfileId) &&
            enrollment.Type == "StudentEnrollment" &&
            (enrollment.EnrollmentState == "active" || enrollment.EnrollmentState == "completed"));
        // Normalize only the small course catalog in memory; student membership and score
        // averaging remain scoped SQL queries over existing Canvas warehouse facts.
        var catalog = await enrollments.Select(enrollment => new
            { enrollment.CanvasCourseId, enrollment.Course!.EnrollmentTermName }).Distinct().ToListAsync(ct);
        var courseIds = catalog.Where(course => !string.IsNullOrWhiteSpace(course.EnrollmentTermName) &&
                DashboardPeriodNormalizer.AcademicYear(course.EnrollmentTermName) == period.AcademicYear)
            .Select(course => course.CanvasCourseId).ToList();
        var periodCourseIds = await CanvasReportingPeriodService.FilterScores(db.CanvasCourseScores.AsNoTracking(), period)
            .Where(score => enrollments.Any(enrollment => enrollment.CanvasEnrollmentId == score.CanvasEnrollmentId &&
                enrollment.StudentProfileId == score.StudentProfileId))
            .Select(score => score.Enrollment!.CanvasCourseId).Distinct().ToListAsync(ct);
        courseIds = courseIds.Concat(periodCourseIds).Distinct().ToList();
        var subjects = await CanvasReportFilters.VisibleSubaccounts(db, courseIds, ct);
        var selected = subjectId.HasValue ? subjects.FirstOrDefault(subject => subject.Id == subjectId) : subjects.FirstOrDefault();
        if (subjectId.HasValue && selected is null) return null;
        var students = new List<SubjectSupportStudent>();
        if (selected is not null)
        {
            enrollments = enrollments.WhereMatchesAnyValue(enrollment => enrollment.CanvasCourseId, courseIds)
                .Where(enrollment => enrollment.Course!.CanvasAccountId != null)
                .WhereMatchesAnyValue(enrollment => enrollment.Course!.CanvasAccountId!.Value, selected.AccountIds);
            var members = await enrollments.Select(enrollment => new
            {
                enrollment.Student!.Id, enrollment.Student.SISStudentCode, enrollment.Student.SISUserId,
                enrollment.Student.FullName, Grade = enrollment.Student.GradeLevel ?? enrollment.Student.CanvasGradeLevel,
                enrollment.Student.Homeroom
            }).Distinct().ToListAsync(ct);
            var scoreFacts = CanvasReportingPeriodService.FilterUsableScores(
                CanvasReportingPeriodService.FilterScores(db.CanvasCourseScores.AsNoTracking(), period))
                .Where(score => enrollments.Any(enrollment => enrollment.CanvasEnrollmentId == score.CanvasEnrollmentId &&
                    enrollment.StudentProfileId == score.StudentProfileId));
            var scores = await scoreFacts.GroupBy(score => score.StudentProfileId!.Value)
                .Select(group => new { Id = group.Key, Average = group.Average(score => (decimal)score.FinalScore!.Value) })
                .ToDictionaryAsync(row => row.Id, row => Math.Round(row.Average, 1), ct);
            students = members.Select(student =>
            {
                decimal? score = scores.TryGetValue(student.Id, out var average) ? average : null;
                return new SubjectSupportStudent(student.Id, student.SISStudentCode, student.SISUserId, student.FullName,
                    Clean(student.Grade), Clean(student.Homeroom), score, score.HasValue && score.Value < threshold);
            }).ToList();
        }
        var grades = students.GroupBy(student => student.GradeLevel ?? "—")
            .Select(group => Cohort(group, group.Key, "")).OrderBy(row => row.Grade).ToList();
        var classes = students.GroupBy(student => new { Grade = student.GradeLevel ?? "—", Homeroom = student.Homeroom ?? "—" })
            .Select(group => Cohort(group, group.Key.Grade, group.Key.Homeroom))
            .OrderBy(row => row.Grade).ThenBy(row => row.Homeroom).ToList();
        return new Report(new SubjectSupportSummary(period.AcademicYear, period.Term, selected?.Id, selected?.Name,
            threshold, subjects, grades, classes), students);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SubjectSupportCohort Cohort(IEnumerable<SubjectSupportStudent> source, string grade, string homeroom)
    {
        var students = source.ToList();
        var scores = students.Select(student => student.AverageCanvasScorePercentage).OfType<decimal>().ToList();
        return new SubjectSupportCohort(grade, homeroom, students.Count, students.Count(student => student.NeedsSupport),
            scores.Count == 0 ? null : Math.Round(scores.Average(), 1), scores.Count, students.Count - scores.Count);
    }

    private sealed record Report(SubjectSupportSummary Summary, List<SubjectSupportStudent> Students);
}

public sealed record SubjectSupportCohort(string Grade, string Homeroom, int Students, int AtRisk,
    decimal? AverageCanvasScorePercentage, int ScoredStudents, int NoScoreStudents);
public sealed record SubjectSupportSummary(string PeriodYear, string PeriodTerm, long? SubjectId, string? SubjectName,
    decimal LowScoreThreshold, List<CanvasReportSubaccountOption> Subjects,
    List<SubjectSupportCohort> GradeDistribution, List<SubjectSupportCohort> ClassDistribution);
public sealed record SubjectSupportStudent(int Id, string? SISStudentCode, string SISUserId, string FullName,
    string? GradeLevel, string? Homeroom, decimal? AverageCanvasScorePercentage, bool NeedsSupport);
public sealed record SubjectSupportStudentPage(List<SubjectSupportStudent> Items,
    int Page, int PageSize, int TotalCount, int TotalPages);
