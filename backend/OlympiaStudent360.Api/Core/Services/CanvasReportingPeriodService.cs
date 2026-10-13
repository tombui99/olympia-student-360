using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Core.Services;

public sealed record CanvasReportingPeriod(
    string AcademicYear,
    string Term,
    DateTime Start,
    DateTime End);

/// <summary>
/// Resolves the semester whose Canvas grading-period facts should be used for student-facing
/// averages. Canvas dates are authoritative; the school-calendar ranges are only a fallback.
/// </summary>
public sealed class CanvasReportingPeriodService
{
    private readonly AppDbContext _context;
    private Task<List<CanvasReportingPeriod>>? _periodsTask;

    public CanvasReportingPeriodService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CanvasReportingPeriod> ResolveAsync(
        string? requestedYear = null,
        string? requestedTerm = null,
        DateTime? asOf = null)
    {
        var now = asOf ?? DateTime.UtcNow;
        var allTerms = string.Equals(requestedTerm?.Trim(), "all", StringComparison.OrdinalIgnoreCase);
        var year = string.IsNullOrWhiteSpace(requestedYear) || requestedYear == "all"
            ? null
            : DashboardPeriodNormalizer.AcademicYear(requestedYear.Trim());
        if (year is not null && !DashboardPeriodNormalizer.IsAcademicYear(year)) year = null;

        var term = string.IsNullOrWhiteSpace(requestedTerm) || requestedTerm == "all"
            ? null
            : DashboardPeriodNormalizer.Term(requestedTerm.Trim());
        if (term is not ("hk1" or "hk2")) term = null;

        // A report rebuild resolves many years/terms in one scope. Reuse this narrow period
        // catalog instead of rescanning Canvas score metadata for every combination.
        var periods = await (_periodsTask ??= LoadPeriodsAsync());

        if (allTerms)
        {
            var fullYear = year
                ?? periods.OrderByDescending(period =>
                        DashboardPeriodNormalizer.AcademicYearStart(period.AcademicYear))
                    .Select(period => period.AcademicYear)
                    .FirstOrDefault()
                ?? DashboardPeriodNormalizer.AcademicYear(now);
            var (fullYearStart, fullYearEnd) = DashboardPeriodNormalizer.AcademicYearRange(fullYear);
            return new CanvasReportingPeriod(fullYear, "all", fullYearStart, fullYearEnd);
        }

        var candidates = year is null
            ? periods
            : periods.Where(period => period.AcademicYear.Equals(year, StringComparison.OrdinalIgnoreCase)).ToList();
        if (term is not null)
        {
            candidates = candidates.Where(period => period.Term == term).ToList();
        }

        var selected = candidates
            .Where(period => period.Start <= now && now < period.End)
            .OrderByDescending(period => period.Start)
            .FirstOrDefault();

        if (selected is null && candidates.Count > 0)
        {
            selected = candidates
                .Where(period => period.Start <= now)
                .OrderByDescending(period => period.Start)
                .FirstOrDefault()
                ?? candidates.OrderBy(period => period.Start).First();
        }

        if (selected is not null) return selected;

        var fallbackYear = year ?? DashboardPeriodNormalizer.AcademicYear(now);
        var fallbackTerm = term ?? ResolveFallbackTerm(fallbackYear, now);
        var (start, end) = DashboardPeriodNormalizer.PeriodRange(fallbackYear, fallbackTerm);
        return new CanvasReportingPeriod(fallbackYear, fallbackTerm, start, end);
    }

    private async Task<List<CanvasReportingPeriod>> LoadPeriodsAsync()
    {
        var sourceRows = await _context.CanvasCourseScores.AsNoTracking()
            .Where(score => score.GradingPeriodId != null
                && score.GradingPeriodTitle != null
                && score.GradingPeriodStartDate != null
                && score.GradingPeriodEndDate != null
                && score.Enrollment!.Course!.EnrollmentTermName != null)
            .Select(score => new
            {
                YearLabel = score.Enrollment!.Course!.EnrollmentTermName!,
                TermLabel = score.GradingPeriodTitle!,
                Start = score.GradingPeriodStartDate!.Value,
                End = score.GradingPeriodEndDate!.Value
            })
            .Distinct()
            .ToListAsync();

        return sourceRows
            .Select(row => new
            {
                AcademicYear = DashboardPeriodNormalizer.AcademicYear(row.YearLabel, row.End),
                Term = DashboardPeriodNormalizer.Term(row.TermLabel),
                row.Start,
                row.End
            })
            .Where(row => DashboardPeriodNormalizer.IsAcademicYear(row.AcademicYear)
                && row.Term is "hk1" or "hk2")
            .GroupBy(row => new { row.AcademicYear, row.Term })
            .Select(group => new CanvasReportingPeriod(
                group.Key.AcademicYear,
                group.Key.Term,
                group.Min(row => row.Start),
                group.Max(row => row.End)))
            .ToList();
    }

    public static IQueryable<CanvasCourseScore> FilterScores(
        IQueryable<CanvasCourseScore> query,
        CanvasReportingPeriod period) =>
        query.Where(score => score.GradingPeriodId != null
            && score.GradingPeriodStartDate < period.End
            && score.GradingPeriodEndDate >= period.Start);

    /// <summary>
    /// Canvas emits zero-valued grading-period facts before some course gradebooks have a
    /// calculated grade. A zero is only reportable when Canvas also supplied points or a grade
    /// label; otherwise it represents unavailable data and must not affect averages.
    /// </summary>
    public static IQueryable<CanvasCourseScore> FilterUsableScores(
        IQueryable<CanvasCourseScore> query) =>
        query.Where(score => score.FinalScore != null
            && (score.FinalScore != 0
                || score.CurrentPoints != null && score.CurrentPoints != 0
                || score.CurrentGrade != null && score.CurrentGrade != ""
                || score.FinalGrade != null && score.FinalGrade != ""));

    public static IQueryable<CanvasCourseScore> FilterUsableCurrentScores(
        IQueryable<CanvasCourseScore> query) =>
        query.Where(score => score.CurrentScore != null
            && (score.CurrentScore != 0
                || score.CurrentPoints != null && score.CurrentPoints != 0
                || score.CurrentGrade != null && score.CurrentGrade != ""
                || score.FinalGrade != null && score.FinalGrade != ""));

    public static IQueryable<CanvasSubmission> FilterSubmissions(
        IQueryable<CanvasSubmission> query,
        CanvasReportingPeriod period) =>
        query.Where(submission =>
            (submission.CachedDueDate ?? submission.Assignment!.DueAt ?? submission.GradedAt
                ?? submission.SubmittedAt) >= period.Start
            && (submission.CachedDueDate ?? submission.Assignment!.DueAt ?? submission.GradedAt
                ?? submission.SubmittedAt) < period.End);

    private static string ResolveFallbackTerm(string academicYear, DateTime now)
    {
        var (hk1Start, hk1End) = DashboardPeriodNormalizer.PeriodRange(academicYear, "hk1");
        var (hk2Start, hk2End) = DashboardPeriodNormalizer.PeriodRange(academicYear, "hk2");
        if (now >= hk1Start && now < hk1End) return "hk1";
        if (now >= hk2Start && now < hk2End) return "hk2";
        return now < hk1Start ? "hk1" : "hk2";
    }
}
