using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.SisSync;

/// <summary>Refreshes source-dependent views on the durable runner after ingestion is committed.</summary>
public sealed class SisReportRefresher
{
    public async Task RunAsync(AppDbContext db, CancellationToken ct)
    {
        var options = (DbContextOptions<AppDbContext>)db.GetService<IDbContextOptions>();
        await ApplicationDatabase.CreateRetryStrategy(db).ExecuteAsync(async token =>
        {
            await using var attempt = new AppDbContext(options);
            await attempt.MigrateWithRecoveryAsync(token);
            await using var held = await SisProjectionLease.AcquireAsync(attempt, token);
            var latest = await attempt.ImportedReportChanges.AsNoTracking().MaxAsync(x => (int?)x.Id, token) ?? 0;
            var build = await attempt.ImportedReportBuilds.AsNoTracking().OrderByDescending(x => x.Id).FirstOrDefaultAsync(token);
            if (build is null || build.LastChangeId < latest || build.CompletedAtUtc.Date < DateTime.UtcNow.Date)
            {
                var count = await new ImportedReportAggregateService(attempt, new CanvasReportingPeriodService(attempt)).RebuildAsync(token);
                Console.WriteLine($"Published {count} imported report aggregate rows after SIS sync.");
            }
            var mealBuild = await attempt.ErpMealReportBuilds.AsNoTracking().OrderByDescending(x => x.Id).FirstOrDefaultAsync(token);
            var lastMealRun = await attempt.ErpMealSyncRuns.AsNoTracking().MaxAsync(x => (int?)x.Id, token) ?? 0;
            var latestChange = await attempt.ImportedReportChanges.AsNoTracking().MaxAsync(x => (int?)x.Id, token) ?? 0;
            if (mealBuild is null || mealBuild.LastStudentChangeId < latestChange || mealBuild.LastSyncRunId < lastMealRun ||
                mealBuild.CompletedAtUtc.Date < DateTime.UtcNow.Date)
            {
                var count = await new ErpMealReportAggregateService(attempt).RebuildAsync(token);
                Console.WriteLine($"Published {count} ERP meal aggregate rows after SIS dimension updates.");
            }
            Console.WriteLine("SIS-dependent report views are current.");
        }, ct);
    }
}
