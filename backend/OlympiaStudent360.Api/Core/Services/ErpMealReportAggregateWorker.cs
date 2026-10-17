using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Core.Services;

public sealed class ErpMealReportAggregateWorker(
    IServiceScopeFactory scopeFactory,
    ApplicationInitializationState initialization,
    ILogger<ErpMealReportAggregateWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await initialization.WaitUntilReadyAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.OpenConnectionAsync(stoppingToken);
                try
                {
                    var connection = db.Database.GetDbConnection();
                    await using var acquire = connection.CreateCommand();
                    acquire.CommandText = "SELECT GET_LOCK('olympia_erp_meal_report_aggregates', 0)";
                    if (Convert.ToInt32(await acquire.ExecuteScalarAsync(stoppingToken)) == 1)
                    {
                        try
                        {
                            var build = await db.ErpMealReportBuilds.AsNoTracking()
                                .OrderByDescending(x => x.Id).FirstOrDefaultAsync(stoppingToken);
                            var lastRun = await db.ErpMealSyncRuns.AsNoTracking()
                                .MaxAsync(x => (int?)x.Id, stoppingToken) ?? 0;
                            var lastStudentChange = await db.ImportedReportChanges.AsNoTracking()
                                .MaxAsync(x => (int?)x.Id, stoppingToken) ?? 0;
                            var detailMissing = build is not null &&
                                await db.ErpMealDailyAggregates.AsNoTracking().AnyAsync(stoppingToken) &&
                                !await db.ErpMealReportDetails.AsNoTracking().AnyAsync(stoppingToken);
                            if (build is null || lastRun > build.LastSyncRunId ||
                                lastStudentChange > build.LastStudentChangeId ||
                                build.CompletedAtUtc.Date < DateTime.UtcNow.Date || detailMissing)
                            {
                                var count = await scope.ServiceProvider
                                    .GetRequiredService<ErpMealReportAggregateService>()
                                    .RebuildAsync(stoppingToken);
                                logger.LogInformation("Rebuilt {Count} ERP meal daily aggregates", count);
                            }
                        }
                        finally
                        {
                            await using var release = connection.CreateCommand();
                            release.CommandText = "SELECT RELEASE_LOCK('olympia_erp_meal_report_aggregates')";
                            await release.ExecuteScalarAsync(CancellationToken.None);
                        }
                    }
                }
                finally { await db.Database.CloseConnectionAsync(); }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogError(exception, "ERP meal aggregate rebuild failed; retrying");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
