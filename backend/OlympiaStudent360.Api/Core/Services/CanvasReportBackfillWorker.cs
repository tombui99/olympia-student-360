using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Core.Services;

/// <summary>
/// Builds a newly introduced Canvas report aggregate from existing warehouse data after
/// deployment. Normal Canvas ETL continues to rebuild it after each successful sync.
/// </summary>
public sealed class CanvasReportBackfillWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<CanvasReportBackfillWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                if (await db.CanvasDashboardAggregateBuilds.AsNoTracking()
                        .AnyAsync(item => item.SchemaVersion >= CanvasDashboardAggregateService.ReportSchemaVersion,
                            stoppingToken))
                    return;

                logger.LogInformation("Backfilling Canvas report aggregates from existing warehouse data");
                var count = await scope.ServiceProvider.GetRequiredService<CanvasDashboardAggregateService>()
                    .BackfillIfNeededAsync(stoppingToken);
                if (count.HasValue)
                    logger.LogInformation("Canvas report aggregate backfill completed with {Count} rows", count.Value);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Canvas report aggregate backfill failed; retrying in one minute");
                try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
                catch (OperationCanceledException) { return; }
            }
        }
    }
}
