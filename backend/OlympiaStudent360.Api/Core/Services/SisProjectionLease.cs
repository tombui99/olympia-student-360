using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Core.Services;

/// <summary>Prevents background report publishers from holding student FK locks during SIS writes.</summary>
public sealed class SisProjectionLease : IAsyncDisposable
{
    internal static readonly string[] ReportLocks =
        ["olympia_imported_report_aggregates", "olympia_erp_meal_report_aggregates"];
    private readonly AppDbContext db;
    private readonly CanvasWarehouseLock warehouse;
    private readonly List<string> acquired = [];

    private SisProjectionLease(AppDbContext db, CanvasWarehouseLock warehouse)
    { this.db = db; this.warehouse = warehouse; }

    public static async Task<SisProjectionLease> AcquireAsync(AppDbContext db, CancellationToken ct,
        TimeSpan? waitTimeout = null)
    {
        var timeout = waitTimeout ?? TimeSpan.FromMinutes(5);
        var warehouse = await CanvasWarehouseLock.AcquireAsync(db, ct, timeout);
        var lease = new SisProjectionLease(db, warehouse);
        var elapsed = Stopwatch.StartNew();
        var nextProgress = TimeSpan.Zero;
        try
        {
            foreach (var name in ReportLocks)
            {
                while (true)
                {
                    ct.ThrowIfCancellationRequested();
                    var remaining = timeout - elapsed.Elapsed;
                    if (remaining <= TimeSpan.Zero)
                        throw new TimeoutException($"Timed out waiting for report publisher lock {name} before SIS projection.");
                    await using var command = db.Database.GetDbConnection().CreateCommand();
                    command.CommandTimeout = 10;
                    command.CommandText = "SELECT GET_LOCK(@name, @seconds)";
                    var lockName = command.CreateParameter(); lockName.ParameterName = "@name"; lockName.Value = name;
                    var seconds = command.CreateParameter(); seconds.ParameterName = "@seconds";
                    seconds.Value = Math.Min(5, remaining.TotalSeconds);
                    command.Parameters.Add(lockName); command.Parameters.Add(seconds);
                    if (Convert.ToInt32(await command.ExecuteScalarAsync(ct)) == 1) { lease.acquired.Add(name); break; }
                    if (elapsed.Elapsed >= nextProgress)
                    {
                        Console.WriteLine($"Waiting for report publisher {name}; elapsed {elapsed.Elapsed.TotalSeconds:0}s.");
                        nextProgress = elapsed.Elapsed + TimeSpan.FromSeconds(30);
                    }
                }
            }
            return lease;
        }
        catch { await lease.DisposeAsync(); throw; }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            foreach (var name in acquired.AsEnumerable().Reverse())
                await ApplicationDatabase.ReleaseSessionLockAsync(db.Database.GetDbConnection(), name);
            acquired.Clear();
        }
        finally { await warehouse.DisposeAsync(); }
    }
}
