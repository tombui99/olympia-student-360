namespace OlympiaStudent360.Api.Core.Services;

/// <summary>
/// Backfills a newly introduced report schema and retries aggregation for completed Canvas
/// imports if their post-completion refresh was interrupted or failed.
/// </summary>
public sealed class CanvasReportBackfillWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    IHostEnvironment environment,
    ILogger<CanvasReportBackfillWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // App Runner throttles CPU when no HTTP requests are active. A background
        // rebuild can then hold a warehouse lock and transaction indefinitely.
        // The durable Canvas ETL already materializes reports after each import.
        if (!configuration.GetValue("Canvas:EnableApiReportBackfill", !environment.IsProduction()))
        {
            logger.LogInformation("Canvas API report backfill is disabled; use the Canvas ETL or --rebuild-dashboard-aggregates-only to materialize reports.");
            return;
        }
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var count = await scope.ServiceProvider.GetRequiredService<CanvasDashboardAggregateService>()
                    .RefreshIfNeededAsync(stoppingToken);
                if (count.HasValue)
                    logger.LogInformation("Canvas report aggregation completed with {Count} metric rows", count.Value);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Canvas report aggregation failed; retrying in one minute");
            }

            try { await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); }
            catch (OperationCanceledException) { return; }
        }
    }
}
