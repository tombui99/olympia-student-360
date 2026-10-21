using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Diagnostics;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.CanvasEtl;

/// <summary>
/// Runs the Canvas import inside a durable command-line process. This deliberately avoids routing
/// a multi-hour ETL through an HTTP request whose client, proxy, or hosting platform may time out.
/// </summary>
public sealed class CanvasDirectImportRunner
{
    public async Task<CanvasDirectImportResult> Run(
        AppDbContext db,
        HttpClient httpClient,
        CanvasDirectImportRequest request,
        CancellationToken cancellationToken)
    {
        var options = (DbContextOptions<AppDbContext>)db.GetService<IDbContextOptions>();
        var attempt = 0;
        var result = await ApplicationDatabase.CreateRetryStrategy(db).ExecuteAsync(async token =>
        {
            if (++attempt > 1)
                Console.Error.WriteLine($"Retrying Canvas import after a transient database failure (attempt {attempt}/4).");
            // Never reuse tracked entities, transactions, or a session whose named lock
            // may have been lost. Completed course upserts can safely be replayed; the
            // watermark advances only after the entire import succeeds.
            await using var attemptDb = new AppDbContext(options);
            try
            {
                return await RunAttempt(attemptDb, httpClient, request, token);
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine($"Canvas import attempt {attempt}/4 failed: {exception}");
                throw;
            }
        }, cancellationToken);

        // The completed import and watermark are durable before report materialization.
        // Retry report work independently so it cannot replay a successful Canvas import.
        Console.WriteLine("Canvas import committed; refreshing report aggregates.");
        try
        {
            await RefreshReports(db, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new InvalidOperationException(
                "Canvas data import completed, but report aggregation failed. Retry with --refresh-dashboard-aggregates-only; the committed import does not need to be repeated.", exception);
        }
        return result;
    }

    public static async Task<int?> RefreshReports(AppDbContext db, CancellationToken cancellationToken)
    {
        var options = (DbContextOptions<AppDbContext>)db.GetService<IDbContextOptions>();
        var elapsed = Stopwatch.StartNew();
        var count = await ApplicationDatabase.CreateRetryStrategy(db).ExecuteAsync(async token =>
        {
            await using var reportDb = new AppDbContext(options);
            return await new CanvasDashboardAggregateService(reportDb).RefreshIfNeededAsync(token);
        }, cancellationToken);
        Console.WriteLine(count.HasValue
            ? $"Canvas report refresh committed: {count.Value} metric rows rebuilt; elapsed {elapsed.Elapsed:hh\\:mm\\:ss}."
            : "Canvas reports already cover the latest completed import; no rebuild needed.");
        return count;
    }

    private static async Task<CanvasDirectImportResult> RunAttempt(
        AppDbContext db,
        HttpClient httpClient,
        CanvasDirectImportRequest request,
        CancellationToken cancellationToken)
    {
        Console.WriteLine("Checking Canvas database migrations.");
        await db.MigrateWithRecoveryAsync(cancellationToken);
        Console.WriteLine("Acquiring Canvas warehouse lock before importing.");
        await using var warehouseLock = await CanvasWarehouseLock.AcquireAsync(db, cancellationToken);
        Console.WriteLine("Canvas warehouse lock acquired; starting import.");

        var startedAtUtc = DateTime.UtcNow;
        var sourceKey = $"canvas:{request.BaseUri.Host.ToLowerInvariant()}:{request.AccountId.Trim().ToLowerInvariant()}";
        // Owning the warehouse lock proves no previous running attempt can still be
        // importing. A lost connection may have prevented it from recording failure.
        await db.CanvasSyncRuns.Where(item => item.SourceKey == sourceKey && item.Status == "running")
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.Status, "failed")
                .SetProperty(item => item.CompletedAtUtc, startedAtUtc)
                .SetProperty(item => item.UpdatedAt, startedAtUtc)
                .SetProperty(item => item.Error, "Previous Canvas attempt was interrupted before completion."),
                cancellationToken);
        var state = await db.CanvasSyncStates.SingleOrDefaultAsync(
            item => item.SourceKey == sourceKey, cancellationToken);

        var run = new CanvasSyncRun
        {
            SourceKey = sourceKey,
            Source = $"Canvas ({request.BaseUri.Host}, account {request.AccountId.Trim()})",
            Workflow = request.Mode,
            ReportChangesTracked = true,
            Status = "running",
            StartedAtUtc = startedAtUtc,
            WatermarkUtc = startedAtUtc,
            CreatedAt = startedAtUtc
        };
        db.CanvasSyncRuns.Add(run);

        if (request.Mode == "incremental" && state?.LastSuccessfulWatermarkUtc is null)
        {
            const string message =
                "No successful Canvas watermark exists. Run sync-all once before incremental sync.";
            run.Status = "failed";
            run.CompletedAtUtc = DateTime.UtcNow;
            run.Error = message;
            run.UpdatedAt = run.CompletedAtUtc;
            await db.SaveChangesAsync(CancellationToken.None);
            throw new InvalidOperationException(message);
        }

        var sinceUtc = request.Mode switch
        {
            "incremental" => DateTime.SpecifyKind(
                    state!.LastSuccessfulWatermarkUtc!.Value, DateTimeKind.Utc)
                .AddMinutes(-request.OverlapMinutes),
            "reconcile" => startedAtUtc.AddDays(-request.ReconciliationDays!.Value),
            _ => (DateTime?)null
        };
        run.WindowStartUtc = sinceUtc;

        state ??= new CanvasSyncState { SourceKey = sourceKey, CreatedAt = startedAtUtc };
        if (state.Id == 0)
        {
            db.CanvasSyncStates.Add(state);
        }
        state.LastStartedAtUtc = startedAtUtc;
        state.LastRunMode = request.Mode;
        state.LastStatus = "running";
        state.LastError = null;
        state.UpdatedAt = startedAtUtc;
        await db.SaveChangesAsync(CancellationToken.None);

        var options = new CanvasEtlOptions
        {
            BaseUri = request.BaseUri,
            AccessToken = request.AccessToken,
            AccountId = request.AccountId.Trim(),
            PerPage = request.PerPage,
            MaxConcurrentRequests = request.MaxConcurrentRequests,
            AllowEmptyCourses = request.AllowEmptyCourses,
            AllowEmptyStudents = request.AllowEmptyStudents,
            IncludeAllCourses = request.IncludeAllCourses,
            IncludeCompletedCourses = request.Mode is "sync-all" or "reconcile",
            EnrollmentTermIds = request.EnrollmentTermIds,
            SinceUtc = sinceUtc is null ? null : new DateTimeOffset(sinceUtc.Value, TimeSpan.Zero)
        };
        var sync = new CanvasSyncService(db, new CanvasApiClient(httpClient, options), options);

        CanvasSyncSummary summary;
        DateTime completedAtUtc;
        try
        {
            summary = await sync.Run(request.ReplaceExisting, cancellationToken);
            completedAtUtc = DateTime.UtcNow;
            state = await db.CanvasSyncStates.SingleAsync(
                item => item.SourceKey == sourceKey, CancellationToken.None);
            state.LastSuccessfulWatermarkUtc = startedAtUtc;
            state.LastCompletedAtUtc = completedAtUtc;
            state.LastStatus = "completed";
            state.LastError = null;
            state.UpdatedAt = completedAtUtc;
            run = await db.CanvasSyncRuns.SingleAsync(item => item.Id == run.Id, CancellationToken.None);
            run.Status = "completed";
            run.CompletedAtUtc = completedAtUtc;
            run.Courses = summary.Courses;
            run.Assignments = summary.Assignments;
            run.Enrollments = summary.Enrollments;
            run.Scores = summary.Scores;
            run.Submissions = summary.Submissions;
            run.UpdatedAt = completedAtUtc;
            await db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            try
            {
                db.ChangeTracker.Clear();
                state = await db.CanvasSyncStates.SingleAsync(
                    item => item.SourceKey == sourceKey, CancellationToken.None);
                state.LastCompletedAtUtc = DateTime.UtcNow;
                state.LastStatus = exception is OperationCanceledException ? "cancelled" : "failed";
                state.LastError = Limit(exception.ToString(), 2000);
                state.UpdatedAt = DateTime.UtcNow;
                run = await db.CanvasSyncRuns.SingleAsync(item => item.Id == run.Id, CancellationToken.None);
                run.Status = state.LastStatus;
                run.CompletedAtUtc = state.LastCompletedAtUtc;
                run.Error = state.LastError;
                run.UpdatedAt = state.UpdatedAt;
                await db.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception historyException)
            {
                // A broken connection can also prevent recording the failure. Preserve
                // the original exception so the outer strategy can classify and retry it.
                Console.Error.WriteLine($"Could not record Canvas sync failure: {historyException}");
            }
            throw;
        }

        return new CanvasDirectImportResult(completedAtUtc, sinceUtc, startedAtUtc, summary);
    }

    private static string Limit(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

public sealed record CanvasDirectImportRequest(
    Uri BaseUri,
    string AccessToken,
    string AccountId,
    bool ReplaceExisting,
    bool AllowEmptyCourses,
    bool AllowEmptyStudents,
    bool IncludeAllCourses,
    IReadOnlyList<long> EnrollmentTermIds,
    string Mode,
    int OverlapMinutes,
    int? ReconciliationDays,
    int PerPage = 100,
    int MaxConcurrentRequests = 8);

public sealed record CanvasDirectImportResult(
    DateTime CompletedAtUtc,
    DateTime? WindowStartUtc,
    DateTime WatermarkUtc,
    CanvasSyncSummary Summary);
