using System.Net;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OlympiaStudent360.Api.Data;
using OlympiaStudent360.Api.SisSync;

if (args.Contains("--help"))
{
    Console.WriteLine("SIS ETL: --incremental (default), --full, or --check-access (OAuth/scopes only; no database). Configure Sis__ClientId, Sis__ClientSecret and ConnectionStrings__DefaultConnection through protected secrets.");
    return 0;
}
try
{
    if (args.Length > 1 || args.Any(x => x is not ("--full" or "--incremental" or "--check-access")))
        throw new SisApiException("Choose one SIS mode: --incremental, --full, or --check-access.");
    var config = new ConfigurationBuilder().AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
        .AddEnvironmentVariables().Build();
    var options = SisEtlOptions.FromConfiguration(config);
    using var shutdown = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; shutdown.Cancel(); };
    using var handler = new HttpClientHandler
    {
        AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        AllowAutoRedirect = false // Never forward credentials to another origin.
    };
    using var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(90) };
    if (args.Contains("--check-access"))
    {
        await new SisApiClient(http, options).CheckScopesAsync(shutdown.Token);
        Console.WriteLine("SIS authentication and configured resource scopes verified. No database changes made.");
        return 0;
    }
    var connection = config.GetConnectionString("DefaultConnection");
    if (string.IsNullOrWhiteSpace(connection)) throw new SisApiException("ConnectionStrings:DefaultConnection is required.");
    await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseResilientMySql(connection).Options);
    await new SisSyncRunner().RunAsync(db, http, options, args.Contains("--full"), shutdown.Token);
    return 0;
}
catch (Exception ex)
{
    // Provider exceptions can contain PII or SQL parameters. Emit only controlled diagnostics.
    Console.Error.WriteLine(SisSyncRunner.SafeError(ex));
    return ex is OperationCanceledException ? 2 : 1;
}
