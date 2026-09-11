using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

var options = Arguments.Parse(args);
if (options is null)
{
    Arguments.PrintUsage();
    return 2;
}

try
{
    var reader = new SisRosterWorkbookReader();
    var workbooks = new List<SisRosterWorkbook>();
    foreach (var workbookPath in options.WorkbookPaths)
    {
        Console.WriteLine($"Reading SIS workbook: {workbookPath}");
        await using var stream = File.OpenRead(workbookPath);
        workbooks.Add(reader.Read(stream));
    }
    var workbook = SisRosterWorkbooks.Combine(workbooks);
    var grades = workbook.Students.GroupBy(student => student.GradeLevel)
        .OrderBy(group => int.Parse(group.Key))
        .Select(group => $"Grade {group.Key}: {group.Count():N0}");
    Console.WriteLine($"Validated {workbook.Students.Count:N0} active students ({string.Join(", ", grades)}).");
    var sourceFileName = string.Join(" + ", options.WorkbookPaths.Select(Path.GetFileName));

    if (options.SqlOutputPath is not null)
    {
        var script = new SisRosterSqlScriptGenerator().Generate(workbook, sourceFileName);
        var outputDirectory = Path.GetDirectoryName(Path.GetFullPath(options.SqlOutputPath));
        if (outputDirectory is not null) Directory.CreateDirectory(outputDirectory);
        await File.WriteAllTextAsync(options.SqlOutputPath, script);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                options.SqlOutputPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        Console.WriteLine($"Generated RDS Workbench script: {Path.GetFullPath(options.SqlOutputPath)}");
    }

    if (options.ImportLocal)
    {
        var configuration = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .AddUserSecrets<Arguments>()
            .Build();
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");

        var dbOptions = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySQL(connectionString)
            .Options;
        await using var db = new AppDbContext(dbOptions);
        Console.WriteLine("Applying database migrations...");
        await db.Database.MigrateAsync();
        Console.WriteLine("Replacing the local student roster and pruning Canvas facts for inactive students...");
        var result = await new SisRosterImportService(db).Import(
            workbook,
            sourceFileName,
            new SisRosterImportActor("local-cli", "local-cli", "Local SIS import"));
        Console.WriteLine(
            $"Imported {result.StudentsImported:N0} students ({result.StudentsAdded:N0} added, " +
            $"{result.StudentsUpdated:N0} updated, {result.StudentsRemoved:N0} removed), " +
            $"{result.ParentsImported:N0} parents, {result.CanvasLinkedStudents:N0} Canvas links; " +
            $"removed {result.CanvasFactsRemoved:N0} inactive Canvas fact rows.");
    }

    return 0;
}
catch (SisRosterValidationException exception)
{
    Console.Error.WriteLine("SIS workbook validation failed:");
    foreach (var error in exception.Errors) Console.Error.WriteLine($"- {error}");
    return 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"SIS roster import failed: {exception.Message}");
    return 1;
}

internal sealed record Arguments(IReadOnlyList<string> WorkbookPaths, bool ImportLocal, string? SqlOutputPath)
{
    public static Arguments? Parse(string[] args)
    {
        var workbooks = new List<string>();
        string? sqlOutput = null;
        var importLocal = false;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--file" when index + 1 < args.Length:
                    workbooks.Add(args[++index]);
                    break;
                case "--sql-output" when index + 1 < args.Length:
                    sqlOutput = args[++index];
                    break;
                case "--import-local":
                    importLocal = true;
                    break;
                default:
                    return null;
            }
        }

        return workbooks.Count > 0 && workbooks.All(File.Exists) &&
            (importLocal || !string.IsNullOrWhiteSpace(sqlOutput))
            ? new Arguments(workbooks.Select(Path.GetFullPath).ToArray(), importLocal,
                string.IsNullOrWhiteSpace(sqlOutput) ? null : Path.GetFullPath(sqlOutput))
            : null;
    }

    public static void PrintUsage() => Console.Error.WriteLine(
        "Usage: dotnet run --project backend/OlympiaStudent360.SisImport -- " +
        "--file <sis.xlsx> [--file <another-sis.xlsx>] [--import-local] [--sql-output <rds-import.sql>]");
}
