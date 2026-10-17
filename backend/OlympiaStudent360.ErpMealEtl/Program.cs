using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;
using OlympiaStudent360.ErpMealEtl;

try
{
    var options = args.Select(arg => arg.Split('=', 2))
        .ToDictionary(parts => parts[0], parts => parts.Length == 2 ? parts[1] : "", StringComparer.OrdinalIgnoreCase);
    var allowed = new[] { "--canteen", "--missing-ticket", "--missing-order", "--dry-run", "--relink-only", "--rebuild-aggregates-only" };
    if (options.Keys.Any(key => !allowed.Contains(key, StringComparer.OrdinalIgnoreCase)) ||
        options.Count == 0 || options.Keys.All(key => key == "--dry-run") ||
        options.ContainsKey("--relink-only") && options.Count != 1 ||
        options.ContainsKey("--rebuild-aggregates-only") && options.Count != 1)
        throw new ArgumentException("Provide export paths, optionally with --dry-run, or use --relink-only / --rebuild-aggregates-only.");

    var purchases = options.TryGetValue("--canteen", out var canteenPath)
        ? ExcelMealExportReader.ReadPurchases(canteenPath) : null;
    var missingTicket = options.TryGetValue("--missing-ticket", out var ticketPath)
        ? ExcelMealExportReader.ReadStatuses(ticketPath) : null;
    var missingOrder = options.TryGetValue("--missing-order", out var orderPath)
        ? ExcelMealExportReader.ReadStatuses(orderPath) : null;
    Console.WriteLine($"Validated ERP export rows: canteen={purchases?.Count ?? 0}, missing-ticket={missingTicket?.Count ?? 0}, missing-order={missingOrder?.Count ?? 0}.");
    if (options.ContainsKey("--dry-run")) return 0;

    var config = new ConfigurationBuilder().AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true)
        .AddEnvironmentVariables().Build();
    var connection = config["ConnectionStrings:DefaultConnection"];
    if (string.IsNullOrWhiteSpace(connection))
        throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required to import.");
    await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseMySQL(connection).Options);
    await db.Database.MigrateAsync();
    var warehouse = new ErpMealWarehouseService(db);
    if (options.ContainsKey("--relink-only"))
    {
        Console.WriteLine($"Relinked {await warehouse.RelinkUnmatchedAsync()} previously unlinked rows.");
        return 0;
    }
    if (options.ContainsKey("--rebuild-aggregates-only"))
    {
        await RebuildAggregatesAsync(db);
        return 0;
    }
    if (purchases is not null)
        Print(await warehouse.ImportPurchasesAsync(Path.GetFileName(canteenPath!), purchases));
    if (missingTicket is not null)
        Print(await warehouse.ImportStatusesAsync(ErpMealFeeds.MissingTicket, Path.GetFileName(ticketPath!), missingTicket));
    if (missingOrder is not null)
        Print(await warehouse.ImportStatusesAsync(ErpMealFeeds.MissingOrder, Path.GetFileName(orderPath!), missingOrder));
    await RebuildAggregatesAsync(db);
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"ERP meal import failed: {ex.Message}");
    return 1;
}

static void Print(OlympiaStudent360.Api.Core.Entities.ErpMealSyncRun run) =>
    Console.WriteLine($"{run.Feed}: {run.InsertedRows} inserted, {run.UpdatedRows} updated, " +
                      $"{run.UnmatchedRows} unmatched, {run.ConflictRows} conflicts.");

static async Task RebuildAggregatesAsync(AppDbContext db)
{
    await db.Database.OpenConnectionAsync();
    try
    {
        var connection = db.Database.GetDbConnection();
        await using var acquire = connection.CreateCommand();
        acquire.CommandText = "SELECT GET_LOCK('olympia_erp_meal_report_aggregates', 60)";
        if (Convert.ToInt32(await acquire.ExecuteScalarAsync()) != 1)
            throw new TimeoutException("Timed out waiting to rebuild ERP meal report aggregates.");
        try
        {
            var count = await new ErpMealReportAggregateService(db).RebuildAsync();
            Console.WriteLine($"Rebuilt {count} ERP meal daily aggregates.");
        }
        finally
        {
            await using var release = connection.CreateCommand();
            release.CommandText = "SELECT RELEASE_LOCK('olympia_erp_meal_report_aggregates')";
            await release.ExecuteScalarAsync();
        }
    }
    finally
    {
        await db.Database.CloseConnectionAsync();
    }
}
