using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Core.Services;

/// <summary>Rebuilds report measures after source writes, never in the request path.</summary>
public sealed class ImportedReportAggregateWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ImportedReportAggregateWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
                    acquire.CommandText = "SELECT GET_LOCK('olympia_imported_report_aggregates', 0)";
                    var lockResult = await acquire.ExecuteScalarAsync(stoppingToken);
                    if (Convert.ToInt32(lockResult) == 1)
                    {
                        try
                        {
                            var build = await db.ImportedReportBuilds.AsNoTracking()
                                .OrderByDescending(item => item.Id)
                                .Select(item => new { item.LastChangeId, item.CompletedAtUtc })
                                .FirstOrDefaultAsync(stoppingToken);
                            var latest = await db.ImportedReportChanges.AsNoTracking()
                                .OrderByDescending(item => item.Id)
                                .Select(item => new { item.Id, item.CreatedAt })
                                .FirstOrDefaultAsync(stoppingToken);
                            var now = DateTime.UtcNow;
                            var quiet = latest is null || latest.CreatedAt <= now.AddSeconds(-30);
                            var changed = latest is not null && latest.Id > (build?.LastChangeId ?? 0);
                            // Continuous imports may never have a 30-second quiet period. Keep
                            // reports boundedly fresh; a later pass catches writes made mid-build.
                            var maxWaitReached = build is not null && build.CompletedAtUtc <= now.AddMinutes(-5);
                            if (build is null || changed && (quiet || maxWaitReached) ||
                                build.CompletedAtUtc.Date < now.Date)
                            {
                                var count = await scope.ServiceProvider
                                    .GetRequiredService<ImportedReportAggregateService>()
                                    .RebuildAsync(stoppingToken);
                                logger.LogInformation("Rebuilt {Count} imported report aggregate rows", count);
                            }
                        }
                        finally
                        {
                            await ApplicationDatabase.ReleaseSessionLockAsync(connection, "olympia_imported_report_aggregates");
                        }
                    }
                }
                finally
                {
                    await db.Database.CloseConnectionAsync();
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Imported report aggregate rebuild failed; retrying");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }
}
