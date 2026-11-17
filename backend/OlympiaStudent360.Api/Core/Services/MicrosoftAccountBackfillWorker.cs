using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Core.Services;

public sealed class MicrosoftAccountBackfillWorker(
    IServiceScopeFactory scopes, ILogger<MicrosoftAccountBackfillWorker> logger) : BackgroundService
{
    private const string LockName = "olympia_microsoft_link_backfill";

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15));
        do
        {
            try
            {
                await BackfillAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception error)
            {
                logger.LogWarning("Microsoft account backfill failed ({ErrorType}) and will retry.", error.GetType().Name);
            }
        } while (await timer.WaitForNextTickAsync(ct));
    }

    internal async Task BackfillAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.OpenConnectionAsync(ct);
        var connection = db.Database.GetDbConnection();
        await using var acquire = connection.CreateCommand();
        acquire.CommandText = "SELECT GET_LOCK('olympia_microsoft_link_backfill', 0)";
        if (Convert.ToInt32(await acquire.ExecuteScalarAsync(ct)) != 1) return;

        try
        {
            var accounts = await db.Users.AsNoTracking()
                .Where(user => user.EmailConfirmed && user.Email != null && user.Email != "" && !user.MicrosoftSignInBlocked &&
                    !db.UserLogins.Any(login => login.UserId == user.Id && login.LoginProvider == MicrosoftSignIn.Scheme))
                .OrderBy(user => user.Id).Select(user => user.Id).ToListAsync(ct);
            var provisioning = scope.ServiceProvider.GetRequiredService<MicrosoftAccountProvisioning>();
            var linked = 0;
            var checkedAccounts = 0;
            foreach (var id in accounts)
            {
                db.ChangeTracker.Clear();
                var user = await db.Users.SingleOrDefaultAsync(user => user.Id == id, ct);
                if (user is null) continue;
                var status = await provisioning.LinkSavedEmailAsync(user, ct);
                checkedAccounts++;
                if (status == "microsoft_linked") linked++;
                if (status is "microsoft_directory_permission" or "microsoft_directory_unavailable")
                {
                    logger.LogWarning("Microsoft account backfill paused ({Code}) after linking {Linked} accounts.", status, linked);
                    return;
                }
            }
            if (checkedAccounts > 0)
                logger.LogInformation("Microsoft account backfill linked {Linked} of {Checked} candidate accounts.", linked, checkedAccounts);
        }
        finally
        {
            await ApplicationDatabase.ReleaseSessionLockAsync(connection, LockName);
        }
    }
}
