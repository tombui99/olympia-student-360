using System.Net.Http.Json;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using OlympiaStudent360.CanvasEtl;

try
{
    var replaceExisting = args.Contains("--replace", StringComparer.OrdinalIgnoreCase);
    var syncAll = args.Contains("--sync-all", StringComparer.OrdinalIgnoreCase);
    var incremental = args.Contains("--incremental", StringComparer.OrdinalIgnoreCase);
    var reconciliationArgument = args.SingleOrDefault(arg =>
        arg.StartsWith("--reconcile-days=", StringComparison.OrdinalIgnoreCase));
    var reconciliationDays = reconciliationArgument is null
        ? (int?)null
        : ParseReconciliationDays(reconciliationArgument);
    var unexpected = args.Where(arg =>
        !arg.Equals("--replace", StringComparison.OrdinalIgnoreCase) &&
        !arg.Equals("--sync-all", StringComparison.OrdinalIgnoreCase) &&
        !arg.Equals("--incremental", StringComparison.OrdinalIgnoreCase) &&
        !arg.StartsWith("--reconcile-days=", StringComparison.OrdinalIgnoreCase)).ToArray();
    if (unexpected.Length > 0)
    {
        throw new InvalidOperationException($"Unknown arguments: {string.Join(", ", unexpected)}");
    }
    var selectedModes = (syncAll ? 1 : 0) + (incremental ? 1 : 0) + (reconciliationDays is null ? 0 : 1);
    if (selectedModes > 1)
    {
        throw new InvalidOperationException(
            "Use exactly one Canvas sync mode: --sync-all, --incremental, or --reconcile-days=N.");
    }
    if (selectedModes == 0)
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
    var canvasOptions = CanvasEtlOptions.FromConfiguration(configuration);
    var selectedTermIds = syncAll
        ? canvasOptions.SyncAllEnrollmentTermIds
        : reconciliationDays is not null
            ? canvasOptions.SyncAllEnrollmentTermIds.Concat(canvasOptions.EnrollmentTermIds).Distinct().ToArray()
            : canvasOptions.EnrollmentTermIds;
    if (syncAll && selectedTermIds.Count == 0)
    {
        throw new InvalidOperationException(
            "--sync-all requires Canvas:SyncAllEnrollmentTermIds so nonacademic Canvas courses are not imported.");
    }
    if (!syncAll && selectedTermIds.Count == 0)
    {
        throw new InvalidOperationException(
            "Incremental and reconciliation modes require configured Canvas enrollment term ids.");
    }
    var backendBaseUri = RequiredHttpsOrLoopbackUri(configuration, "Backend:BaseUrl");
    var syncApiKey = Required(configuration, "Backend:SyncApiKey");

    using var shutdown = new CancellationTokenSource();
    Console.CancelKeyPress += (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        shutdown.Cancel();
    };

    using var httpClient = new HttpClient
    {
        BaseAddress = backendBaseUri,
        Timeout = syncAll ? TimeSpan.FromHours(8) : TimeSpan.FromHours(2)
    };
    httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
        "OlympiaStudent360-CanvasETL/1.0 (+https://theolympiaschools.edu.vn)");
    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Olympia-Sync-Key", syncApiKey);
    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Canvas-Access-Token", canvasOptions.AccessToken);

    Console.WriteLine($"Triggering Canvas sync through {backendBaseUri}api/canvas-sync.");
    using var response = await httpClient.PostAsJsonAsync(
        "api/canvas-sync",
        new CanvasSyncRequest(
            canvasOptions.BaseUri.GetLeftPart(UriPartial.Authority),
            canvasOptions.AccountId,
            replaceExisting,
            canvasOptions.AllowEmptyCourses,
            canvasOptions.AllowEmptyStudents,
            false,
            selectedTermIds,
            syncAll ? "sync-all" : reconciliationDays is not null ? "reconcile" : "incremental",
            canvasOptions.WatermarkOverlapMinutes,
            reconciliationDays),
        shutdown.Token);
    var responseBody = await response.Content.ReadAsStringAsync(shutdown.Token);
    if (!response.IsSuccessStatusCode)
    {
        throw new HttpRequestException(
            $"Backend Canvas sync endpoint returned HTTP {(int)response.StatusCode}: {Trim(responseBody, 1000)}");
    }

    Console.WriteLine($"Backend Canvas sync completed: {responseBody}");
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

static Uri RequiredHttpsOrLoopbackUri(IConfiguration configuration, string name)
{
    var value = Required(configuration, name).TrimEnd('/') + "/";
    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
        (uri.Scheme != Uri.UriSchemeHttps && !uri.IsLoopback))
    {
        throw new InvalidOperationException($"{name} must use HTTPS, except for loopback testing.");
    }

    return uri;
}

static string Trim(string value, int maxLength) =>
    value.Length <= maxLength ? value : value[..maxLength] + "...";

static int ParseReconciliationDays(string argument)
{
    var value = argument[(argument.IndexOf('=') + 1)..];
    return int.TryParse(value, out var days) && days is >= 1 and <= 90
        ? days
        : throw new InvalidOperationException("--reconcile-days must be from 1 through 90.");
}

internal sealed record CanvasSyncRequest(
    string BaseUrl,
    string AccountId,
    bool ReplaceExisting,
    bool AllowEmptyCourses,
    bool AllowEmptyStudents,
    bool IncludeAllCourses,
    IReadOnlyList<long> EnrollmentTermIds,
    string Mode,
    int OverlapMinutes,
    int? ReconciliationDays);
