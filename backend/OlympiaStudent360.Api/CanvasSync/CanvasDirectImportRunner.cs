using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;
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
        await db.MigrateWithRecoveryAsync(cancellationToken);

        var startedAtUtc = DateTime.UtcNow;
        var sourceKey = $"canvas:{request.BaseUri.Host.ToLowerInvariant()}:{request.AccountId.Trim().ToLowerInvariant()}";
        var state = await db.CanvasSyncStates.SingleOrDefaultAsync(
            item => item.SourceKey == sourceKey, cancellationToken);

        var run = new CanvasSyncRun
        {
            SourceKey = sourceKey,
            Source = $"Canvas ({request.BaseUri.Host}, account {request.AccountId.Trim()})",
            Workflow = request.Mode,
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
            AllowEmptyCourses = request.AllowEmptyCourses,
            AllowEmptyStudents = request.AllowEmptyStudents,
            IncludeAllCourses = request.IncludeAllCourses,
            IncludeCompletedCourses = request.Mode is "sync-all" or "reconcile",
            EnrollmentTermIds = request.EnrollmentTermIds,
            SinceUtc = sinceUtc is null ? null : new DateTimeOffset(sinceUtc.Value, TimeSpan.Zero)
        };
        var sync = new CanvasSyncService(db, new CanvasApiClient(httpClient, options), options);

        try
        {
            var summary = await sync.Run(request.ReplaceExisting, cancellationToken);
            var completedAtUtc = DateTime.UtcNow;
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
            return new CanvasDirectImportResult(completedAtUtc, sinceUtc, startedAtUtc, summary);
        }
        catch (Exception exception)
        {
            db.ChangeTracker.Clear();
            state = await db.CanvasSyncStates.SingleAsync(
                item => item.SourceKey == sourceKey, CancellationToken.None);
            state.LastCompletedAtUtc = DateTime.UtcNow;
            state.LastStatus = exception is OperationCanceledException ? "cancelled" : "failed";
            state.LastError = Limit(exception.Message, 2000);
            state.UpdatedAt = DateTime.UtcNow;
            run = await db.CanvasSyncRuns.SingleAsync(item => item.Id == run.Id, CancellationToken.None);
            run.Status = state.LastStatus;
            run.CompletedAtUtc = state.LastCompletedAtUtc;
            run.Error = state.LastError;
            run.UpdatedAt = state.UpdatedAt;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
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
    int PerPage = 100);

public sealed record CanvasDirectImportResult(
    DateTime CompletedAtUtc,
    DateTime? WindowStartUtc,
    DateTime WatermarkUtc,
    CanvasSyncSummary Summary);
