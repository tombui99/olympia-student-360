using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Core.Services;

/// <summary>
/// Materializes the Canvas dashboard measures after an ETL run. Raw Canvas facts stay intact for
/// drill-down and audit; the overview endpoint reads narrow, indexed monthly rows.
/// </summary>
public sealed class CanvasDashboardAggregateService
{
    public const string FinalScoreAverage = "canvas-final-score-avg";
    public const string CurrentScoreAverage = "canvas-current-score-avg";
    public const string SubmissionScoreAverage = "canvas-submission-score-percent-avg";
    public const string AssignmentsSubmitted = "canvas-assignments-submitted";
    public const string AssignmentsGraded = "canvas-assignments-graded";
    public const string SubmittedAssignmentsGraded = "canvas-submitted-assignments-graded";
    public const string AssignmentsMissing = "canvas-assignments-missing";
    public const string AssignmentsLate = "canvas-assignments-late";

    private readonly AppDbContext _db;

    public CanvasDashboardAggregateService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int> RebuildAsync(CancellationToken cancellationToken)
    {
        var periods = await _db.AcademicPeriods.AsNoTracking()
            .Select(item => new ReportingPeriod(
                item.AcademicYear,
                item.Term,
                item.StartDate,
                item.EndDate))
            .ToListAsync(cancellationToken);
        var accumulators = new Dictionary<AggregateKey, AggregateValue>();

        await foreach (var item in _db.CanvasSubmissions.AsNoTracking()
            .Where(item => item.StudentProfileId != null)
            .Select(item => new
            {
                StudentProfileId = item.StudentProfileId!.Value,
                item.CanvasCourseId,
                CourseTerm = item.Assignment!.Course!.EnrollmentTermName,
                item.WorkflowState,
                item.Score,
                PointsPossible = item.Assignment.PointsPossible,
                item.SubmittedAt,
                item.GradedAt,
                DueAt = item.CachedDueDate ?? item.Assignment.DueAt,
                item.Late,
                item.Missing,
                item.Excused
            })
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            var filterDate = item.DueAt ?? item.GradedAt ?? item.SubmittedAt;
            var termDate = item.DueAt ?? item.SubmittedAt;
            var academicYear = AcademicYear(item.CourseTerm, filterDate);
            var term = Term(periods, academicYear, termDate);

            if (item.SubmittedAt is not null)
            {
                Add(accumulators, AssignmentsSubmitted, item.StudentProfileId, item.CanvasCourseId,
                    academicYear, term, item.SubmittedAt, filterDate, 1, 0);
            }
            if (item.WorkflowState == "graded")
            {
                var gradedDate = item.GradedAt ?? item.SubmittedAt;
                Add(accumulators, AssignmentsGraded, item.StudentProfileId, item.CanvasCourseId,
                    academicYear, term, gradedDate, filterDate, 1, 0);
                if (item.SubmittedAt is not null)
                {
                    Add(accumulators, SubmittedAssignmentsGraded, item.StudentProfileId,
                        item.CanvasCourseId, academicYear, term, item.SubmittedAt, filterDate, 1, 0);
                }
                if (item.Score is not null && item.PointsPossible > 0 && gradedDate is not null)
                {
                    Add(accumulators, SubmissionScoreAverage, item.StudentProfileId,
                        item.CanvasCourseId, academicYear, term, gradedDate, filterDate,
                        item.Score.Value / item.PointsPossible.Value * 100, 1);
                }
            }
            if (item.Missing && item.Excused != true)
            {
                Add(accumulators, AssignmentsMissing, item.StudentProfileId, item.CanvasCourseId,
                    academicYear, term, item.DueAt, filterDate, 1, 0);
            }
            if (item.Late)
            {
                Add(accumulators, AssignmentsLate, item.StudentProfileId, item.CanvasCourseId,
                    academicYear, term, item.SubmittedAt, filterDate, 1, 0);
            }
        }

        await foreach (var item in _db.CanvasCourseScores.AsNoTracking()
            .Where(item => item.StudentProfileId != null)
            .Select(item => new
            {
                StudentProfileId = item.StudentProfileId!.Value,
                CanvasCourseId = item.Enrollment!.CanvasCourseId,
                CourseEndAt = item.Enrollment!.Course!.EndAt,
                item.GradingPeriodTitle,
                item.GradingPeriodEndDate,
                item.CurrentScore,
                item.FinalScore,
                item.CurrentPoints,
                item.CurrentGrade,
                item.FinalGrade
            })
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            var reportingDate = item.GradingPeriodEndDate ?? item.CourseEndAt;
            // Score rows without a reporting date are still valid in an unfiltered overall view,
            // but intentionally have no academic-year or monthly bucket.
            var academicYear = reportingDate is null
                ? ""
                : DashboardPeriodNormalizer.AcademicYear(reportingDate.Value);
            var term = string.IsNullOrWhiteSpace(item.GradingPeriodTitle)
                ? ""
                : DashboardPeriodNormalizer.Term(item.GradingPeriodTitle);
            var hasGradeEvidence = item.CurrentPoints is not null and not 0
                || !string.IsNullOrWhiteSpace(item.CurrentGrade)
                || !string.IsNullOrWhiteSpace(item.FinalGrade);

            if (item.FinalScore is not null && (item.FinalScore != 0 || hasGradeEvidence))
            {
                Add(accumulators, FinalScoreAverage, item.StudentProfileId, item.CanvasCourseId,
                    academicYear, term, reportingDate, reportingDate, item.FinalScore.Value, 1);
            }
            if (item.CurrentScore is not null && (item.CurrentScore != 0 || hasGradeEvidence))
            {
                Add(accumulators, CurrentScoreAverage, item.StudentProfileId, item.CanvasCourseId,
                    academicYear, term, reportingDate, reportingDate, item.CurrentScore.Value, 1);
            }
        }

        var now = DateTime.UtcNow;
        var rows = accumulators.Select(item => new CanvasDashboardMetricAggregate
        {
            MetricKey = item.Key.MetricKey,
            StudentProfileId = item.Key.StudentProfileId,
            CanvasCourseId = item.Key.CanvasCourseId,
            AcademicYear = item.Key.AcademicYear,
            Term = item.Key.Term,
            PeriodStart = item.Key.PeriodStart,
            AcademicPeriodStart = item.Key.AcademicPeriodStart,
            ValueSum = item.Value.ValueSum,
            SampleCount = item.Value.SampleCount,
            CreatedAt = now
        }).ToArray();

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.CanvasDashboardMetricAggregates.ExecuteDeleteAsync(cancellationToken);
        foreach (var batch in rows.Chunk(1000))
        {
            _db.CanvasDashboardMetricAggregates.AddRange(batch);
            await _db.SaveChangesAsync(cancellationToken);
            _db.ChangeTracker.Clear();
        }
        await transaction.CommitAsync(cancellationToken);
        return rows.Length;
    }

    private static void Add(
        IDictionary<AggregateKey, AggregateValue> values,
        string metricKey,
        int studentProfileId,
        long canvasCourseId,
        string academicYear,
        string term,
        DateTime? eventDate,
        DateTime? filterDate,
        double value,
        int sampleCount)
    {
        var periodStart = Month(eventDate);
        var academicPeriodStart = AcademicPeriod(eventDate, filterDate, academicYear);
        var key = new AggregateKey(metricKey, studentProfileId, canvasCourseId, academicYear,
            term, periodStart, academicPeriodStart);
        if (!values.TryGetValue(key, out var aggregate))
        {
            aggregate = new AggregateValue();
            values.Add(key, aggregate);
        }
        aggregate.ValueSum += value;
        aggregate.SampleCount += sampleCount;
    }

    private static string AcademicYear(string? sourceTerm, DateTime? filterDate)
    {
        if (filterDate is not null)
        {
            return DashboardPeriodNormalizer.AcademicYear(filterDate.Value);
        }
        if (!string.IsNullOrWhiteSpace(sourceTerm))
        {
            var normalized = DashboardPeriodNormalizer.AcademicYear(sourceTerm);
            if (DashboardPeriodNormalizer.IsAcademicYear(normalized)) return normalized;
        }
        return "";
    }

    private static string Term(
        IReadOnlyCollection<ReportingPeriod> periods,
        string academicYear,
        DateTime? date)
    {
        if (date is null || string.IsNullOrWhiteSpace(academicYear)) return "";
        var configured = periods
            .Where(period =>
                DashboardPeriodNormalizer.AcademicYear(period.AcademicYear) == academicYear &&
                period.Start <= date && date < period.End)
            .OrderByDescending(period => period.Start)
            .Select(period => DashboardPeriodNormalizer.Term(period.Term))
            .FirstOrDefault(term => term is "hk1" or "hk2");
        if (configured is not null) return configured;

        foreach (var fallback in new[] { "hk1", "hk2" })
        {
            var (start, end) = DashboardPeriodNormalizer.PeriodRange(academicYear, fallback);
            if (start <= date && date < end) return fallback;
        }
        return "";
    }

    private static DateTime? AcademicPeriod(
        DateTime? eventDate,
        DateTime? filterDate,
        string academicYear)
    {
        if (string.IsNullOrWhiteSpace(academicYear)) return null;
        if (eventDate is not null &&
            DashboardPeriodNormalizer.AcademicYear(eventDate.Value) == academicYear)
        {
            return Month(eventDate);
        }
        return filterDate is not null &&
               DashboardPeriodNormalizer.AcademicYear(filterDate.Value) == academicYear
            ? Month(filterDate)
            : null;
    }

    private static DateTime? Month(DateTime? value) => value is null
        ? null
        : new DateTime(value.Value.Year, value.Value.Month, 1, 0, 0, 0, DateTimeKind.Utc);

    private sealed record ReportingPeriod(string AcademicYear, string Term, DateTime Start, DateTime End);

    private readonly record struct AggregateKey(
        string MetricKey,
        int StudentProfileId,
        long CanvasCourseId,
        string AcademicYear,
        string Term,
        DateTime? PeriodStart,
        DateTime? AcademicPeriodStart);

    private sealed class AggregateValue
    {
        public double ValueSum { get; set; }
        public int SampleCount { get; set; }
    }
}
