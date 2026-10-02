using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OlympiaStudent360.Api.Data;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.CanvasEtl;

try
{
    var replaceExisting = args.Contains("--replace", StringComparer.OrdinalIgnoreCase);
    var syncAll = args.Contains("--sync-all", StringComparer.OrdinalIgnoreCase);
    var incremental = args.Contains("--incremental", StringComparer.OrdinalIgnoreCase);
    var rebuildAggregatesOnly = args.Contains(
        "--rebuild-dashboard-aggregates-only", StringComparer.OrdinalIgnoreCase);
    var reconciliationArgument = args.SingleOrDefault(arg =>
        arg.StartsWith("--reconcile-days=", StringComparison.OrdinalIgnoreCase));
    var reconciliationDays = reconciliationArgument is null
        ? (int?)null
        : ParseReconciliationDays(reconciliationArgument);
    var unexpected = args.Where(arg =>
        !arg.Equals("--replace", StringComparison.OrdinalIgnoreCase) &&
        !arg.Equals("--sync-all", StringComparison.OrdinalIgnoreCase) &&
        !arg.Equals("--incremental", StringComparison.OrdinalIgnoreCase) &&
        !arg.Equals("--rebuild-dashboard-aggregates-only", StringComparison.OrdinalIgnoreCase) &&
        !arg.StartsWith("--reconcile-days=", StringComparison.OrdinalIgnoreCase)).ToArray();
    if (unexpected.Length > 0)
    {
        throw new InvalidOperationException($"Unknown arguments: {string.Join(", ", unexpected)}");
    }

    var selectedModes = (syncAll ? 1 : 0) + (incremental ? 1 : 0) +
        (reconciliationDays is null ? 0 : 1);
    if (selectedModes > 1)
    {
        throw new InvalidOperationException(
            "Use exactly one Canvas sync mode: --sync-all, --incremental, or --reconcile-days=N.");
    }
    if (rebuildAggregatesOnly && (selectedModes > 0 || replaceExisting))
    {
        throw new InvalidOperationException(
            "--rebuild-dashboard-aggregates-only cannot be combined with a Canvas sync mode or --replace.");
    }
    if (selectedModes == 0 && !rebuildAggregatesOnly)
    {
        incremental = true;
    }
    if (replaceExisting && !syncAll)
    {
        throw new InvalidOperationException(
            "--replace is allowed only with --sync-all because incremental responses are partial.");
    }

    var configuration = new ConfigurationBuilder()
        .AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
        .AddEnvironmentVariables()
        .Build();
    var connectionString = Required(configuration, "ConnectionStrings:DefaultConnection");
    var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
        .UseMySQL(connectionString)
        .Options;
    if (rebuildAggregatesOnly)
    {
        await using var aggregateDb = new AppDbContext(dbOptions);
        await aggregateDb.Database.MigrateAsync();
        var rowCount = await new CanvasDashboardAggregateService(aggregateDb)
            .RebuildAsync(CancellationToken.None);
        Console.WriteLine($"Rebuilt {rowCount} pre-calculated Canvas dashboard metric rows.");
        return 0;
    }

    var canvasOptions = CanvasEtlOptions.FromConfiguration(configuration);
    var selectedTermIds = syncAll
        ? canvasOptions.SyncAllEnrollmentTermIds
        : reconciliationDays is not null
            ? canvasOptions.SyncAllEnrollmentTermIds.Concat(canvasOptions.EnrollmentTermIds).Distinct().ToArray()
            : canvasOptions.EnrollmentTermIds;
    if (syncAll && selectedTermIds.Count == 0)
    {
        throw new InvalidOperationException(
            "--sync-all requires Canvas:SyncAllEnrollmentTermIds so the import stays inside explicit Canvas terms.");
    }
    if (!syncAll && selectedTermIds.Count == 0)
    {
        throw new InvalidOperationException(
            "Incremental and reconciliation modes require configured Canvas enrollment term ids.");
    }

    using var shutdown = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        shutdown.Cancel();
    };

    await using var db = new AppDbContext(dbOptions);
    using var httpClient = new HttpClient();
    var mode = syncAll ? "sync-all" : reconciliationDays is not null ? "reconcile" : "incremental";
    Console.WriteLine(
        $"Starting direct Canvas-to-MySQL {mode} for term ids {string.Join(",", selectedTermIds)}. " +
        "No backend HTTP request is involved.");

    var result = await new CanvasDirectImportRunner().Run(
        db,
        httpClient,
        new CanvasDirectImportRequest(
            canvasOptions.BaseUri,
            canvasOptions.AccessToken,
            canvasOptions.AccountId,
            replaceExisting,
            canvasOptions.AllowEmptyCourses,
            canvasOptions.AllowEmptyStudents,
            syncAll || canvasOptions.IncludeAllCourses,
            selectedTermIds,
            mode,
            canvasOptions.WatermarkOverlapMinutes,
            reconciliationDays,
            canvasOptions.PerPage),
        shutdown.Token);

    Console.WriteLine(
        $"Canvas import completed at {result.CompletedAtUtc:O}: " +
        JsonSerializer.Serialize(result.Summary));
    return 0;
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Canvas sync was cancelled.");
    return 2;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Canvas sync failed: {exception.Message}");
    return 1;
}

static string Required(IConfiguration configuration, string name)
{
    var value = configuration[name];
    return !string.IsNullOrWhiteSpace(value)
        ? value
        : throw new InvalidOperationException($"Required configuration value {name} is not configured.");
}

static int ParseReconciliationDays(string argument)
{
    var value = argument[(argument.IndexOf('=') + 1)..];
    return int.TryParse(value, out var days) && days is >= 1 and <= 90
        ? days
        : throw new InvalidOperationException("--reconcile-days must be from 1 through 90.");
}
