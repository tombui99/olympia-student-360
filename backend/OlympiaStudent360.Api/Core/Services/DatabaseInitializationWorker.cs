using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Core.Services;

/// <summary>Runs migrations and seed work after Kestrel begins starting.</summary>
public sealed class DatabaseInitializationWorker(
    IServiceProvider services,
    IConfiguration configuration,
    ApplicationInitializationState initialization,
    IHostApplicationLifetime lifetime,
    IHostEnvironment environment,
    ILogger<DatabaseInitializationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Always yield so long database work can never delay the HTTP listener.
        await Task.Yield();
        try
        {
            await DbInitializer.Seed(services, configuration, stoppingToken);
            initialization.MarkReady();
            logger.LogInformation("Database migration and initialization completed");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            initialization.MarkFailed();
            logger.LogCritical(exception, "Database migration or initialization failed");
            if (environment.IsProduction()) lifetime.StopApplication();
        }
    }
}
