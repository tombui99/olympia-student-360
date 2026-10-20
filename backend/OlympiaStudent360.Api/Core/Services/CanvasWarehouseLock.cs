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
        CancellationToken cancellationToken)
    {
        var heldLock = new CanvasWarehouseLock(db);
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            // MySQL named locks are session scoped. Reentrant acquisition on the same connection
            // lets a sync retain the lock when it invokes the post-completion aggregate builder.
            while (!heldLock._acquired)
            {
                await using var command = db.Database.GetDbConnection().CreateCommand();
                command.CommandText = "SELECT GET_LOCK('olympia_canvas_warehouse', 5)";
                heldLock._acquired = Convert.ToInt32(
                    await command.ExecuteScalarAsync(cancellationToken)) == 1;
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
