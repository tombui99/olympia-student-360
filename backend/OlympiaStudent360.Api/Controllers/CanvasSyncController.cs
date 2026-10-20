using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;
using OlympiaStudent360.CanvasEtl;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/canvas-sync")]
[ApiController]
public sealed class CanvasSyncController : ControllerBase
{
    private const string SyncKeyHeader = "X-Olympia-Sync-Key";
    private const string CanvasTokenHeader = "X-Canvas-Access-Token";
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CanvasSyncController> _logger;

    public CanvasSyncController(
        AppDbContext db,
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<CanvasSyncController> logger)
    {
        _db = db;
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    [HttpPost]
    [AllowAnonymous]
    [RequestSizeLimit(16 * 1024)]
    public async Task<ActionResult<CanvasSyncResultDto>> Sync(
        CanvasSyncRequestDto request,
        CancellationToken cancellationToken)
    {
        var configuredSyncKey = _configuration["CanvasSync:ApiKey"];
        if (string.IsNullOrWhiteSpace(configuredSyncKey))
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                detail: "Canvas sync API authentication is not configured.");
        }

        if (!Request.Headers.TryGetValue(SyncKeyHeader, out var suppliedSyncKey) ||
            !FixedTimeEquals(configuredSyncKey, suppliedSyncKey.ToString()))
        {
            return Unauthorized();
        }

        if (!Request.Headers.TryGetValue(CanvasTokenHeader, out var suppliedCanvasToken) ||
            string.IsNullOrWhiteSpace(suppliedCanvasToken))
        {
            return BadRequest(new { Message = $"Required header {CanvasTokenHeader} is missing." });
        }

        var baseUrl = request.BaseUrl?.TrimEnd('/') + "/";
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttps && !baseUri.IsLoopback))
        {
            return BadRequest(new { Message = "baseUrl must be HTTPS, except for loopback testing." });
        }

        if (string.IsNullOrWhiteSpace(request.AccountId))
        {
            return BadRequest(new { Message = "accountId is required." });
        }
        if (request.EnrollmentTermIds?.Any(id => id <= 0) == true)
        {
            return BadRequest(new { Message = "enrollmentTermIds must contain only positive ids." });
        }
        var mode = request.Mode?.Trim().ToLowerInvariant();
        if (mode is not ("sync-all" or "incremental" or "reconcile"))
        {
            return BadRequest(new { Message = "mode must be sync-all, incremental, or reconcile." });
        }
        if ((request.EnrollmentTermIds?.Count ?? 0) == 0)
        {
            return BadRequest(new
            {
                Message = "Canvas sync requires enrollmentTermIds to avoid importing nonacademic courses."
            });
        }
        if (request.OverlapMinutes is < 0 or > 1440)
        {
            return BadRequest(new { Message = "overlapMinutes must be from 0 through 1440." });
        }
        if (mode == "reconcile" && request.ReconciliationDays is not (>= 1 and <= 90))
        {
            return BadRequest(new { Message = "reconciliationDays must be from 1 through 90." });
        }
        if (mode != "reconcile" && request.ReconciliationDays is not null)
        {
            return BadRequest(new { Message = "reconciliationDays is valid only in reconcile mode." });
        }
        if (request.ReplaceExisting && mode != "sync-all")
        {
            return BadRequest(new { Message = "replaceExisting is valid only in sync-all mode." });
        }

        await _db.MigrateWithRecoveryAsync(cancellationToken);
        await using var warehouseLock = await CanvasWarehouseLock.AcquireAsync(_db, cancellationToken);
        var candidateWatermarkUtc = DateTime.UtcNow;
        var sourceKey = $"canvas:{baseUri.Host.ToLowerInvariant()}:{request.AccountId.Trim().ToLowerInvariant()}";
        var state = await _db.CanvasSyncStates.SingleOrDefaultAsync(
            item => item.SourceKey == sourceKey, cancellationToken);

        var run = new CanvasSyncRun
        {
            SourceKey = sourceKey,
            Source = $"Canvas ({baseUri.Host}, account {request.AccountId.Trim()})",
            Workflow = mode,
            Status = "running",
            StartedAtUtc = candidateWatermarkUtc,
            WatermarkUtc = candidateWatermarkUtc,
            CreatedAt = candidateWatermarkUtc
        };
        _db.CanvasSyncRuns.Add(run);

        if (mode == "incremental" && state?.LastSuccessfulWatermarkUtc is null)
        {
            const string message =
                "No successful Canvas watermark exists. Run sync-all once before incremental sync.";
            run.Status = "failed";
            run.CompletedAtUtc = DateTime.UtcNow;
            run.Error = message;
            run.UpdatedAt = run.CompletedAtUtc;
            await _db.SaveChangesAsync(CancellationToken.None);
            return Conflict(new
            {
                Message = message
            });
        }

        var sinceUtc = mode switch
        {
            "incremental" => DateTime.SpecifyKind(
                state!.LastSuccessfulWatermarkUtc!.Value, DateTimeKind.Utc)
                .AddMinutes(-request.OverlapMinutes),
            "reconcile" => candidateWatermarkUtc.AddDays(-request.ReconciliationDays!.Value),
            _ => (DateTime?)null
        };
        run.WindowStartUtc = sinceUtc;

        state ??= new CanvasSyncState { SourceKey = sourceKey, CreatedAt = candidateWatermarkUtc };
        if (state.Id == 0)
        {
            _db.CanvasSyncStates.Add(state);
        }
        state.LastStartedAtUtc = candidateWatermarkUtc;
        state.LastRunMode = mode;
        state.LastStatus = "running";
        state.LastError = null;
        state.UpdatedAt = candidateWatermarkUtc;

        // Persist the audit row independently of client disconnects so every accepted run is visible.
        await _db.SaveChangesAsync(CancellationToken.None);

        var options = new CanvasEtlOptions
        {
            BaseUri = baseUri,
            AccessToken = suppliedCanvasToken.ToString(),
            AccountId = request.AccountId.Trim(),
            PerPage = 100,
            AllowEmptyCourses = request.AllowEmptyCourses,
            AllowEmptyStudents = request.AllowEmptyStudents,
            IncludeAllCourses = request.IncludeAllCourses,
            IncludeCompletedCourses = mode is "sync-all" or "reconcile",
            EnrollmentTermIds = request.EnrollmentTermIds ?? [],
            SinceUtc = sinceUtc is null ? null : new DateTimeOffset(sinceUtc.Value, TimeSpan.Zero)
        };
        using var canvasClient = _httpClientFactory.CreateClient();
        var sync = new CanvasSyncService(_db, new CanvasApiClient(canvasClient, options), options);

        CanvasSyncSummary summary;
        DateTime completedAtUtc;
        try
        {
            summary = await sync.Run(request.ReplaceExisting, cancellationToken);
            completedAtUtc = DateTime.UtcNow;
            state = await _db.CanvasSyncStates.SingleAsync(
                item => item.SourceKey == sourceKey, CancellationToken.None);
            state.LastSuccessfulWatermarkUtc = candidateWatermarkUtc;
            state.LastCompletedAtUtc = completedAtUtc;
            state.LastStatus = "completed";
            state.LastError = null;
            state.UpdatedAt = completedAtUtc;
            run = await _db.CanvasSyncRuns.SingleAsync(item => item.Id == run.Id, CancellationToken.None);
            run.Status = "completed";
            run.CompletedAtUtc = completedAtUtc;
            run.Courses = summary.Courses;
            run.Assignments = summary.Assignments;
            run.Enrollments = summary.Enrollments;
            run.Scores = summary.Scores;
            run.Submissions = summary.Submissions;
            run.UpdatedAt = completedAtUtc;
            await _db.SaveChangesAsync(CancellationToken.None);
        }
        catch (Exception exception)
        {
            _db.ChangeTracker.Clear();
            state = await _db.CanvasSyncStates.SingleAsync(
                item => item.SourceKey == sourceKey, CancellationToken.None);
            state.LastCompletedAtUtc = DateTime.UtcNow;
            state.LastStatus = exception is OperationCanceledException ? "cancelled" : "failed";
            state.LastError = Limit(exception.Message, 2000);
            state.UpdatedAt = DateTime.UtcNow;
            run = await _db.CanvasSyncRuns.SingleAsync(
                item => item.Id == run.Id, CancellationToken.None);
            run.Status = exception is OperationCanceledException ? "cancelled" : "failed";
            run.CompletedAtUtc = state.LastCompletedAtUtc;
            run.Error = Limit(exception.Message, 2000);
            run.UpdatedAt = state.UpdatedAt;
            await _db.SaveChangesAsync(CancellationToken.None);
            throw;
        }

        // The import's completed status is committed before materialization. If this optional
        // follow-on step fails, the durable completed run remains available to the retry worker.
        try
        {
            var count = await new CanvasDashboardAggregateService(_db)
                .RefreshIfNeededAsync(CancellationToken.None);
            if (count.HasValue)
                _logger.LogInformation("Rebuilt {Count} Canvas dashboard metric rows", count.Value);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "Canvas import completed, but report aggregation is pending retry for run {RunId}",
                run.Id);
        }

        return Ok(new CanvasSyncResultDto(
            "completed", completedAtUtc, sinceUtc, candidateWatermarkUtc, summary));
    }

    private static bool FixedTimeEquals(string expected, string supplied)
    {
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        return expectedBytes.Length == suppliedBytes.Length &&
            CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }

    private static string Limit(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];
}

public sealed class CanvasSyncRequestDto
{
    public string? BaseUrl { get; init; }
    public string? AccountId { get; init; }
    public bool ReplaceExisting { get; init; }
    public bool AllowEmptyCourses { get; init; }
    public bool AllowEmptyStudents { get; init; }
    public bool IncludeAllCourses { get; init; }
    public IReadOnlyList<long>? EnrollmentTermIds { get; init; }
    public string? Mode { get; init; }
    public int OverlapMinutes { get; init; } = 120;
    public int? ReconciliationDays { get; init; }
}

public sealed record CanvasSyncResultDto(
    string Status,
    DateTime CompletedAtUtc,
    DateTime? WindowStartUtc,
    DateTime WatermarkUtc,
    CanvasSyncSummary Summary);
