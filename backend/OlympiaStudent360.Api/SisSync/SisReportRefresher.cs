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
            var imported = await new ImportedReportAggregateService(attempt, new CanvasReportingPeriodService(attempt))
                .RefreshIfNeededAsync(token);
            var meals = await new ErpMealReportAggregateService(attempt).RefreshIfNeededAsync(token);
            Console.WriteLine($"SIS-dependent report refresh: {imported} imported rows, {meals} meal rows published; unchanged areas skipped.");
            Console.WriteLine("SIS-dependent report views are current.");
        }, ct);
    }
}
