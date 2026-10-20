using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Core.Services;

/// <summary>
/// Serializes Canvas warehouse imports and report materialization across API and CLI processes.
/// The lock belongs to the DbContext's MySQL session and stays held while that connection is open.
/// </summary>
public sealed class CanvasWarehouseLock : IAsyncDisposable
{
    private const string LockName = "olympia_canvas_warehouse";
    private readonly AppDbContext _db;
    private bool _acquired;

    private CanvasWarehouseLock(AppDbContext db) => _db = db;

    public static async Task<CanvasWarehouseLock> AcquireAsync(
        AppDbContext db,
        CancellationToken cancellationToken,
        TimeSpan? waitTimeout = null)
    {
        var timeout = waitTimeout ?? TimeSpan.FromMinutes(5);
        if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(waitTimeout));
        var elapsed = Stopwatch.StartNew();
        var nextProgress = TimeSpan.Zero;
        var heldLock = new CanvasWarehouseLock(db);
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            // MySQL named locks are session scoped. Reentrant acquisition on the same connection
            // lets a sync retain the lock when it invokes the post-completion aggregate builder.
            while (!heldLock._acquired)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var remaining = timeout - elapsed.Elapsed;
                if (remaining <= TimeSpan.Zero)
                    throw new TimeoutException($"Timed out after {timeout.TotalSeconds:0} seconds waiting for the Canvas warehouse lock. Check the session holding olympia_canvas_warehouse.");
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandTimeout = 10;
                command.CommandText = "SELECT GET_LOCK('olympia_canvas_warehouse', @waitSeconds)";
                var waitSeconds = command.CreateParameter();
                waitSeconds.ParameterName = "@waitSeconds";
                waitSeconds.Value = Math.Min(5, remaining.TotalSeconds);
                command.Parameters.Add(waitSeconds);
                heldLock._acquired = Convert.ToInt32(
                    await command.ExecuteScalarAsync(cancellationToken)) == 1;
                if (!heldLock._acquired && elapsed.Elapsed >= nextProgress)
                {
                    command.Parameters.Clear();
                    command.CommandText = "SELECT IS_USED_LOCK('olympia_canvas_warehouse')";
                    var owner = await command.ExecuteScalarAsync(cancellationToken);
                    Console.WriteLine($"Waiting for Canvas warehouse lock held by MySQL session {owner}; elapsed {elapsed.Elapsed.TotalSeconds:0}s, limit {timeout.TotalSeconds:0}s.");
                    nextProgress = elapsed.Elapsed + TimeSpan.FromSeconds(30);
                }
            }
            return heldLock;
        }
        catch
        {
            await db.Database.CloseConnectionAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_acquired)
            {
                await ApplicationDatabase.ReleaseSessionLockAsync(_db.Database.GetDbConnection(), LockName);
                _acquired = false;
            }
        }
        finally
        {
            await _db.Database.CloseConnectionAsync();
        }
    }
}
