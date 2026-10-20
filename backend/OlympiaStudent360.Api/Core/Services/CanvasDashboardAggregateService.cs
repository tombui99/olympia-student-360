using System.Text;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Core.Services;

/// <summary>
/// Materializes Canvas dashboard measures after a completed ETL run. Raw Canvas facts stay intact
/// for drill-down and audit; the overview endpoint reads narrow, indexed monthly rows.
/// </summary>
public sealed class CanvasDashboardAggregateService
{
    public const int ReportSchemaVersion = 5;
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

    public Task<int> RebuildAsync(CancellationToken cancellationToken) =>
        WithWarehouseAndRebuildLockAsync(
            () => RebuildCoreAsync(cancellationToken), cancellationToken);

    /// <summary>
    /// Processes the latest durably completed Canvas sync run, or upgrades an older aggregate
    /// schema. Called immediately after a run commits and retried by the API background worker.
    /// Readiness is checked under the warehouse and aggregate locks so parallel processes do
    /// not rebuild the same snapshot and no import can write raw facts while we read them.
    /// </summary>
    public Task<int?> RefreshIfNeededAsync(CancellationToken cancellationToken) =>
        WithWarehouseAndRebuildLockAsync(async () =>
        {
            var latestRunId = await LatestCompletedSyncRunIdAsync(cancellationToken);
            // Do not publish a snapshot of an empty or partially failed first import. An explicit
            // manual RebuildAsync remains available for a legacy warehouse without run records.
            if (latestRunId == 0)
                return (int?)null;
            if (await _db.CanvasDashboardAggregateBuilds.AsNoTracking()
                    .AnyAsync(item => item.SchemaVersion >= ReportSchemaVersion &&
                        item.LastCompletedSyncRunId >= latestRunId, cancellationToken))
                return (int?)null;
            // A v4 snapshot retains assignment-level metric lineage. Pivot it and attach
            // earned/possible points from the matching submissions inside MySQL rather than
            // rewriting the much larger dashboard metric table. Older snapshots are rebuilt
            // because their assignment-grain lineage may be incomplete.
            if (await _db.CanvasDashboardAggregateBuilds.AsNoTracking()
                    .AnyAsync(item => item.SchemaVersion >= 4 && item.SchemaVersion < ReportSchemaVersion &&
                        item.LastCompletedSyncRunId >= latestRunId, cancellationToken) &&
                await _db.CanvasDashboardMetricAggregates.AsNoTracking().AnyAsync(cancellationToken))
                return await BackfillLeadershipFromDashboardAsync(latestRunId, cancellationToken);
            return await RebuildCoreAsync(cancellationToken, latestRunId);
        }, cancellationToken);

    public Task<int?> BackfillIfNeededAsync(CancellationToken cancellationToken) =>
        RefreshIfNeededAsync(cancellationToken);

    /// <summary>
    /// Keep serving the last committed, compatible snapshot while a newer ETL run is being
    /// materialized. Freshness determines whether the worker rebuilds, not whether users can
    /// read reports. The replacement facts and build marker are published in one transaction.
    /// </summary>
    public static async Task<bool> HasReadableSnapshotAsync(
        AppDbContext db, CancellationToken cancellationToken = default)
    {
        return await db.CanvasDashboardAggregateBuilds.AsNoTracking()
            .AnyAsync(item => item.SchemaVersion >= ReportSchemaVersion, cancellationToken);
    }

    private async Task<T> WithWarehouseAndRebuildLockAsync<T>(
        Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        await using var warehouseLock = await CanvasWarehouseLock.AcquireAsync(_db, cancellationToken);
        return await WithRebuildLockAsync(operation, cancellationToken);
    }

    private async Task<int> LatestCompletedSyncRunIdAsync(CancellationToken cancellationToken) =>
        await _db.CanvasSyncRuns.AsNoTracking()
            .Where(item => item.Status == "completed")
            .MaxAsync(item => (int?)item.Id, cancellationToken) ?? 0;

    private async Task<T> WithRebuildLockAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        await _db.Database.OpenConnectionAsync(cancellationToken);
        var connection = _db.Database.GetDbConnection();
        var acquired = false;
        try
        {
            // The short timed wait keeps shutdown cancellation responsive if another builder
            // holds this lock. The warehouse lock above separately excludes ETL writes.
            while (!acquired)
            {
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT GET_LOCK('olympia_canvas_report_aggregates', 5)";
                var result = await command.ExecuteScalarAsync(cancellationToken);
                acquired = Convert.ToInt32(result) == 1;
            }
            return await operation();
        }
        finally
        {
            try
            {
                if (acquired)
                {
                    await ApplicationDatabase.ReleaseSessionLockAsync(connection, "olympia_canvas_report_aggregates");
                }
            }
            finally
            {
                await _db.Database.CloseConnectionAsync();
            }
        }
    }

    private async Task<int> RebuildCoreAsync(
        CancellationToken cancellationToken,
        int? completedSyncRunId = null)
    {
        completedSyncRunId ??= await LatestCompletedSyncRunIdAsync(cancellationToken);
        var periods = await _db.AcademicPeriods.AsNoTracking()
            .Select(item => new ReportingPeriod(
                item.AcademicYear,
                item.Term,
                item.StartDate,
                item.EndDate))
            .ToListAsync(cancellationToken);
        var courseIds = await _db.CanvasCourses.AsNoTracking()
            .OrderBy(item => item.CanvasCourseId)
            .Select(item => item.CanvasCourseId).ToArrayAsync(cancellationToken);
        var rowCount = 0;
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.CanvasLeadershipReportAggregates.ExecuteDeleteAsync(cancellationToken);
        await _db.CanvasDashboardMetricAggregates.ExecuteDeleteAsync(cancellationToken);
        // Bound peak memory to one course instead of retaining millions of assignment
        // metric keys and a second full entity array in the API process. Keep publication
        // atomic so readers can continue using the previous committed snapshot.
        foreach (var courseId in courseIds)
        {
            var accumulators = new Dictionary<AggregateKey, AggregateValue>();

            await foreach (var item in _db.CanvasSubmissions.AsNoTracking()
                // Assignments.CourseId and Submissions.AssignmentId are indexed; the
                // denormalized Submissions.CourseId is not. Follow the assignment join
                // so each batch can seek its course instead of rescanning all facts.
                .Where(item => item.StudentProfileId != null && item.Assignment!.CanvasCourseId == courseId)
                .Select(item => new
                {
                    StudentProfileId = item.StudentProfileId!.Value,
                    item.CanvasCourseId,
                    item.CanvasAssignmentId,
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
                        item.CanvasAssignmentId, academicYear, term, item.SubmittedAt, filterDate, 1, 0);
                }
                if (item.WorkflowState == "graded")
                {
                    var gradedDate = item.GradedAt ?? item.SubmittedAt;
                    Add(accumulators, AssignmentsGraded, item.StudentProfileId, item.CanvasCourseId,
                        item.CanvasAssignmentId, academicYear, term, gradedDate, filterDate, 1, 0);
                    if (item.SubmittedAt is not null)
                    {
                        Add(accumulators, SubmittedAssignmentsGraded, item.StudentProfileId,
                            item.CanvasCourseId, item.CanvasAssignmentId, academicYear, term,
                            item.SubmittedAt, filterDate, 1, 0);
                    }
                    if (item.Score is not null && item.PointsPossible > 0 && gradedDate is not null)
                    {
                        Add(accumulators, SubmissionScoreAverage, item.StudentProfileId,
                            item.CanvasCourseId, item.CanvasAssignmentId, academicYear, term, gradedDate, filterDate,
                            item.Score.Value / item.PointsPossible.Value * 100, 1);
                    }
                }
                if (item.Missing && item.Excused != true)
                {
                    Add(accumulators, AssignmentsMissing, item.StudentProfileId, item.CanvasCourseId,
                        item.CanvasAssignmentId, academicYear, term, item.DueAt, filterDate, 1, 0);
                }
                if (item.Late)
                {
                    Add(accumulators, AssignmentsLate, item.StudentProfileId, item.CanvasCourseId,
                        item.CanvasAssignmentId, academicYear, term, item.SubmittedAt, filterDate, 1, 0);
                }
            }

            await foreach (var item in _db.CanvasCourseScores.AsNoTracking()
                .Where(item => item.StudentProfileId != null && item.Enrollment!.CanvasCourseId == courseId)
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
                        null, academicYear, term, reportingDate, reportingDate, item.FinalScore.Value, 1);
                }
                if (item.CurrentScore is not null && (item.CurrentScore != 0 || hasGradeEvidence))
                {
                    Add(accumulators, CurrentScoreAverage, item.StudentProfileId, item.CanvasCourseId,
                        null, academicYear, term, reportingDate, reportingDate, item.CurrentScore.Value, 1);
                }
            }

            var now = DateTime.UtcNow;
            var rows = accumulators.Select(item => new CanvasDashboardMetricAggregate
            {
                MetricKey = item.Key.MetricKey,
                StudentProfileId = item.Key.StudentProfileId,
                CanvasCourseId = item.Key.CanvasCourseId,
                CanvasAssignmentId = item.Key.CanvasAssignmentId,
                AcademicYear = item.Key.AcademicYear,
                Term = item.Key.Term,
                PeriodStart = item.Key.PeriodStart,
                AcademicPeriodStart = item.Key.AcademicPeriodStart,
                ValueSum = item.Value.ValueSum,
                SampleCount = item.Value.SampleCount,
                CreatedAt = now
            });

            foreach (var batch in rows.Chunk(1000))
            {
                _db.CanvasDashboardMetricAggregates.AddRange(batch);
                await _db.SaveChangesAsync(cancellationToken);
                _db.ChangeTracker.Clear();
                rowCount += batch.Length;
            }
        }
        await PopulateLeadershipFromDashboardFactsAsync(cancellationToken);
        await PopulateLeadershipScorePointsAsync(cancellationToken);
        await _db.CanvasDashboardAggregateBuilds.ExecuteDeleteAsync(cancellationToken);
        _db.CanvasDashboardAggregateBuilds.Add(new CanvasDashboardAggregateBuild
        {
            SchemaVersion = ReportSchemaVersion,
            CompletedAtUtc = DateTime.UtcNow,
            LastCompletedSyncRunId = completedSyncRunId.Value
        });
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return rowCount;
    }

    private async Task<int> BackfillLeadershipFromDashboardAsync(
        int completedSyncRunId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await _db.CanvasLeadershipReportAggregates.ExecuteDeleteAsync(cancellationToken);
        var count = await PopulateLeadershipFromDashboardFactsAsync(cancellationToken);
        await PopulateLeadershipScorePointsAsync(cancellationToken);
        await _db.CanvasDashboardAggregateBuilds.ExecuteDeleteAsync(cancellationToken);
        _db.CanvasDashboardAggregateBuilds.Add(new CanvasDashboardAggregateBuild
        {
            SchemaVersion = ReportSchemaVersion,
            CompletedAtUtc = DateTime.UtcNow,
            LastCompletedSyncRunId = completedSyncRunId
        });
        await _db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return count;
    }

    /// <summary>
    /// A single database-side pivot over the already materialized metric facts. This runs after
    /// a normal ETL rebuild and upgrades v2 installations without transferring a million metric
    /// rows to the API process or rewriting the dashboard aggregate table.
    /// </summary>
    private async Task<int> PopulateLeadershipFromDashboardFactsAsync(CancellationToken cancellationToken)
    {
        var count = await _db.Database.ExecuteSqlRawAsync($"""
            INSERT INTO `CanvasLeadershipReportAggregates`
                (`StudentProfileId`, `CanvasCourseId`, `AcademicYear`, `Term`,
                 `AcademicPeriodStart`, `IsFinalCourseScore`, `AssignmentGroupName`,
                 `AssessmentCategory`, `Submitted`, `Missing`, `Late`, `Graded`,
                 `ScoreSum`, `ScoreCount`, `EarnedPoints`, `PossiblePoints`, `CreatedAt`)
            SELECT source.`StudentProfileId`, source.`CanvasCourseId`,
                   source.`AcademicYear`, source.`Term`, source.`AcademicPeriodStart`,
                   CASE WHEN source.`MetricKey` = '{FinalScoreAverage}' THEN 1 ELSE 0 END,
                   CASE WHEN source.`MetricKey` = '{FinalScoreAverage}'
                        THEN NULL ELSE assignment.`AssignmentGroupName` END,
                   NULL,
                   CAST(SUM(CASE WHEN source.`MetricKey` = '{AssignmentsSubmitted}'
                        THEN source.`ValueSum` ELSE 0 END) AS SIGNED),
                   CAST(SUM(CASE WHEN source.`MetricKey` = '{AssignmentsMissing}'
                        THEN source.`ValueSum` ELSE 0 END) AS SIGNED),
                   CAST(SUM(CASE WHEN source.`MetricKey` = '{AssignmentsLate}'
                        THEN source.`ValueSum` ELSE 0 END) AS SIGNED),
                   CAST(SUM(CASE WHEN source.`MetricKey` = '{SubmittedAssignmentsGraded}'
                        THEN source.`ValueSum` ELSE 0 END) AS SIGNED),
                   SUM(CASE WHEN source.`MetricKey` IN ('{SubmissionScoreAverage}', '{FinalScoreAverage}')
                        THEN source.`ValueSum` ELSE 0 END),
                   SUM(CASE WHEN source.`MetricKey` IN ('{SubmissionScoreAverage}', '{FinalScoreAverage}')
                        THEN source.`SampleCount` ELSE 0 END),
                   0, 0, UTC_TIMESTAMP(6)
            FROM `CanvasDashboardMetricAggregates` AS source
            LEFT JOIN `CanvasAssignments` AS assignment
                ON assignment.`CanvasAssignmentId` = source.`CanvasAssignmentId`
            WHERE source.`MetricKey` IN ('{AssignmentsSubmitted}', '{AssignmentsMissing}',
                                         '{AssignmentsLate}', '{SubmittedAssignmentsGraded}',
                                         '{SubmissionScoreAverage}', '{FinalScoreAverage}')
            GROUP BY source.`StudentProfileId`, source.`CanvasCourseId`,
                     source.`AcademicYear`, source.`Term`, source.`AcademicPeriodStart`,
                     CASE WHEN source.`MetricKey` = '{FinalScoreAverage}' THEN 1 ELSE 0 END,
                     CASE WHEN source.`MetricKey` = '{FinalScoreAverage}'
                          THEN NULL ELSE assignment.`AssignmentGroupName` END
            """, cancellationToken);

        var groupNames = await _db.CanvasAssignments.AsNoTracking()
            .Where(item => item.AssignmentGroupName != null && item.AssignmentGroupName != "")
            .Select(item => item.AssignmentGroupName!)
            .Distinct().ToListAsync(cancellationToken);
        var categories = groupNames
            .Select(name => new { Name = name, Category = CanvasReportFilters.CategoryForGroup(name) })
            .Where(item => item.Category is not null)
            .ToList();
        if (categories.Count > 0)
        {
            var sql = new StringBuilder("UPDATE `CanvasLeadershipReportAggregates` SET `AssessmentCategory` = CASE");
            var parameters = new List<object>();
            foreach (var item in categories)
            {
                sql.Append(" WHEN CAST(`AssignmentGroupName` AS BINARY) = CAST({")
                    .Append(parameters.Count).Append("} AS BINARY) THEN {")
                    .Append(parameters.Count + 1).Append('}');
                parameters.Add(item.Name);
                parameters.Add(item.Category!);
            }
            sql.Append(" ELSE NULL END WHERE `IsFinalCourseScore` = 0");
            await _db.Database.ExecuteSqlRawAsync(sql.ToString(), parameters, cancellationToken);
        }
        return count;
    }

    /// <summary>
    /// Reuses the persisted submission-score fact's reporting year, term and month, then attaches
    /// the original earned/possible points to the narrower group aggregate. This also upgrades a
    /// v4 snapshot without rerunning the entire dashboard build. Only scored, graded, published,
    /// included and non-excused assignments contribute to a Canvas group score estimate.
    /// </summary>
    private Task<int> PopulateLeadershipScorePointsAsync(CancellationToken cancellationToken) =>
        _db.Database.ExecuteSqlRawAsync($"""
            UPDATE `CanvasLeadershipReportAggregates` AS leadership
            INNER JOIN (
                SELECT metric.`StudentProfileId`, metric.`CanvasCourseId`, metric.`AcademicYear`,
                       metric.`Term`, metric.`AcademicPeriodStart`, assignment.`AssignmentGroupName`,
                       SUM(submission.`Score`) AS `Earned`,
                       SUM(assignment.`PointsPossible`) AS `Possible`
                FROM `CanvasDashboardMetricAggregates` AS metric
                INNER JOIN `CanvasAssignments` AS assignment
                    ON assignment.`CanvasAssignmentId` = metric.`CanvasAssignmentId`
                INNER JOIN `CanvasSubmissions` AS submission
                    ON submission.`CanvasAssignmentId` = metric.`CanvasAssignmentId`
                   AND submission.`StudentProfileId` = metric.`StudentProfileId`
                   AND submission.`CanvasCourseId` = metric.`CanvasCourseId`
                WHERE metric.`MetricKey` = '{SubmissionScoreAverage}'
                  AND assignment.`Published` = 1
                  AND assignment.`OmitFromFinalGrade` = 0
                  AND assignment.`PointsPossible` > 0
                  AND submission.`WorkflowState` = 'graded'
                  AND submission.`Score` IS NOT NULL
                  AND (submission.`Excused` IS NULL OR submission.`Excused` = 0)
                GROUP BY metric.`StudentProfileId`, metric.`CanvasCourseId`, metric.`AcademicYear`,
                         metric.`Term`, metric.`AcademicPeriodStart`, assignment.`AssignmentGroupName`
            ) AS points
                ON points.`StudentProfileId` = leadership.`StudentProfileId`
               AND points.`CanvasCourseId` = leadership.`CanvasCourseId`
               AND points.`AcademicYear` = leadership.`AcademicYear`
               AND points.`Term` = leadership.`Term`
               AND points.`AcademicPeriodStart` <=> leadership.`AcademicPeriodStart`
               AND points.`AssignmentGroupName` <=> leadership.`AssignmentGroupName`
            SET leadership.`EarnedPoints` = points.`Earned`,
                leadership.`PossiblePoints` = points.`Possible`
            WHERE leadership.`IsFinalCourseScore` = 0
            """, cancellationToken);

    private static void Add(
        IDictionary<AggregateKey, AggregateValue> values,
        string metricKey,
        int studentProfileId,
        long canvasCourseId,
        long? canvasAssignmentId,
        string academicYear,
        string term,
        DateTime? eventDate,
        DateTime? filterDate,
        double value,
        int sampleCount)
    {
        var periodStart = Month(eventDate);
        var academicPeriodStart = AcademicPeriod(eventDate, filterDate, academicYear);
        var key = new AggregateKey(metricKey, studentProfileId, canvasCourseId, canvasAssignmentId,
            academicYear, term, periodStart, academicPeriodStart);
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
        long? CanvasAssignmentId,
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
