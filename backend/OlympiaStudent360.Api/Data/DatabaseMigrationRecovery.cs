using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace OlympiaStudent360.Api.Data;

public static class DatabaseMigrationRecovery
{
    private const string LearningReportsMigration =
        "20261008090830_AddConfigurableLearningReports";
    private const string MigrationLock = "olympia_student_360_schema_migration";
    private const int MigrationCommandTimeoutSeconds = 600;

    private static readonly HashSet<string> ResumableCanvasMigrations =
    [
        "20261017145751_PrecomputeCanvasLeadershipReports",
        "20261017152023_TriggerCanvasReportAggregationOnCompletedSync",
        "20261017161256_PersistCanvasGroupScorePoints",
        "20261017162556_OptimizeCanvasScorePointBackfill",
        "20261020200719_TrackCanvasReportCourseChanges",
        "20261021093911_ClassifyGhkGroupsAsHs2"
    ];

    private static readonly string[] LearningReportTables =
    [
        "CanvasDashboardAggregateBuilds",
        "ImportedReportAggregates",
        "ImportedReportBuilds",
        "ImportedReportChanges",
        "StudentRiskSnapshots"
    ];

    public static async Task MigrateWithRecoveryAsync(
        this AppDbContext context,
        CancellationToken cancellationToken = default)
    {
        var connection = context.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
        {
            await context.Database.OpenConnectionAsync(cancellationToken);
        }

        var previousTimeout = context.Database.GetCommandTimeout();
        if (previousTimeout is null)
            context.Database.SetCommandTimeout(MigrationCommandTimeoutSeconds);
        var lockAcquired = false;
        try
        {
            lockAcquired = Convert.ToInt32(await ScalarAsync(
                connection,
                "SELECT GET_LOCK(@lockName, 60);",
                cancellationToken,
                ("@lockName", MigrationLock))) == 1;
            if (!lockAcquired)
            {
                throw new TimeoutException("Timed out waiting for the database migration lock.");
            }

            await RepairPartiallyAppliedLearningReportsMigrationAsync(connection, cancellationToken);
            await ResumeCanvasReportMigrationsAsync(context, connection, cancellationToken);
            await context.Database.MigrateAsync(cancellationToken);
        }
        finally
        {
            try
            {
                if (lockAcquired)
                    await ApplicationDatabase.ReleaseSessionLockAsync(connection, MigrationLock);
            }
            finally
            {
                context.Database.SetCommandTimeout(previousTimeout);
                if (openedHere)
                    await context.Database.CloseConnectionAsync();
            }
        }
    }

    private static async Task ResumeCanvasReportMigrationsAsync(
        AppDbContext context, DbConnection connection, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "__EFMigrationsHistory", cancellationToken)) return;
        var applied = (await context.Database.GetAppliedMigrationsAsync(cancellationToken)).ToHashSet();
        var assembly = context.GetService<IMigrationsAssembly>();
        var generator = context.GetService<IMigrationsSqlGenerator>();
        var history = context.GetService<IHistoryRepository>();

        foreach (var (id, migrationType) in assembly.Migrations.OrderBy(item => item.Key))
        {
            if (applied.Contains(id)) continue;
            // Never apply a report migration ahead of an older pending schema change.
            // Fresh databases and all other migrations use EF's normal migrator.
            if (!ResumableCanvasMigrations.Contains(id)) break;
            Console.WriteLine($"Applying/resuming Canvas report migration {id}.");
            var migration = assembly.CreateMigration(migrationType, context.Database.ProviderName!);
            var model = context.GetService<IModelRuntimeInitializer>().Initialize(migration.TargetModel);
            foreach (var operation in migration.UpOperations)
            {
                var alreadyApplied = operation switch
                {
                    CreateTableOperation table => await CreatedTableExistsAsync(connection, table, cancellationToken),
                    AddColumnOperation column => await ColumnExistsAsync(connection, column.Table, column.Name, cancellationToken),
                    CreateIndexOperation index => await CreatedIndexExistsAsync(connection, index, cancellationToken),
                    // This migration's cursor UPDATE is idempotent and must finish
                    // even when ADD COLUMN committed before the process was killed.
                    SqlOperation => false,
                    _ => throw new InvalidOperationException($"Unsupported recovery operation in {id}: {operation.GetType().Name}")
                };
                if (alreadyApplied) continue;
                foreach (var command in generator.Generate([operation], model))
                    await context.Database.ExecuteSqlRawAsync(command.CommandText, cancellationToken);
            }
            // MySQL DDL commits independently of the EF history row. Record the
            // migration only after all tables, columns, indexes, and data updates exist.
            await context.Database.ExecuteSqlRawAsync(
                history.GetInsertScript(new HistoryRow(id, ProductInfo.GetVersion())), cancellationToken);
        }
    }

    private static async Task<bool> CreatedTableExistsAsync(
        DbConnection connection, CreateTableOperation table, CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, table.Name, cancellationToken)) return false;
        foreach (var column in table.Columns)
        {
            if (!await ColumnExistsAsync(connection, table.Name, column.Name, cancellationToken))
                throw new InvalidOperationException($"Existing report table {table.Name} is missing column {column.Name}; refusing to overwrite it.");
        }
        return true;
    }

    private static async Task<bool> CreatedIndexExistsAsync(
        DbConnection connection, CreateIndexOperation index, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, """
            SELECT `COLUMN_NAME` FROM `information_schema`.`STATISTICS`
            WHERE `TABLE_SCHEMA` = DATABASE() AND `TABLE_NAME` = @table AND `INDEX_NAME` = @index
            ORDER BY `SEQ_IN_INDEX`
            """, ("@table", index.Table), ("@index", index.Name));
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var columns = new List<string>();
        while (await reader.ReadAsync(cancellationToken)) columns.Add(reader.GetString(0));
        if (columns.Count == 0) return false;
        if (!columns.SequenceEqual(index.Columns, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Existing index {index.Name} has unexpected columns; refusing to overwrite it.");
        return true;
    }

    private static async Task RepairPartiallyAppliedLearningReportsMigrationAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(connection, "__EFMigrationsHistory", cancellationToken))
        {
            return;
        }

        var migrationRecorded = Convert.ToInt32(await ScalarAsync(
            connection,
            "SELECT COUNT(*) FROM `__EFMigrationsHistory` WHERE `MigrationId` = @migrationId;",
            cancellationToken,
            ("@migrationId", LearningReportsMigration))) > 0;
        if (migrationRecorded)
        {
            return;
        }

        var assignmentColumnExists = await ColumnExistsAsync(
            connection,
            "CanvasDashboardMetricAggregates",
            "CanvasAssignmentId",
            cancellationToken);
        var partialTableExists = false;
        foreach (var table in LearningReportTables)
        {
            if (await TableExistsAsync(connection, table, cancellationToken))
            {
                partialTableExists = true;
                break;
            }
        }

        if (!assignmentColumnExists && !partialTableExists)
        {
            return;
        }

        Console.WriteLine(
            $"Repairing partially applied EF migration {LearningReportsMigration} before retrying it.");

        if (assignmentColumnExists)
        {
            var foreignKeys = await ReadNamesAsync(
                connection,
                """
                SELECT `CONSTRAINT_NAME`
                FROM `information_schema`.`KEY_COLUMN_USAGE`
                WHERE `TABLE_SCHEMA` = DATABASE()
                  AND `TABLE_NAME` = 'CanvasDashboardMetricAggregates'
                  AND `COLUMN_NAME` = 'CanvasAssignmentId'
                  AND `REFERENCED_TABLE_NAME` IS NOT NULL;
                """,
                cancellationToken);
            foreach (var foreignKey in foreignKeys)
            {
                await ExecuteAsync(
                    connection,
                    $"ALTER TABLE `CanvasDashboardMetricAggregates` DROP FOREIGN KEY `{EscapeIdentifier(foreignKey)}`;",
                    cancellationToken);
            }
        }

        foreach (var table in LearningReportTables)
        {
            await ExecuteAsync(
                connection,
                $"DROP TABLE IF EXISTS `{EscapeIdentifier(table)}`;",
                cancellationToken);
        }

        if (assignmentColumnExists)
        {
            await ExecuteAsync(
                connection,
                "ALTER TABLE `CanvasDashboardMetricAggregates` DROP COLUMN `CanvasAssignmentId`;",
                cancellationToken);
        }
    }

    private static async Task<bool> TableExistsAsync(
        DbConnection connection,
        string table,
        CancellationToken cancellationToken) =>
        Convert.ToInt32(await ScalarAsync(
            connection,
            """
            SELECT COUNT(*)
            FROM `information_schema`.`TABLES`
            WHERE `TABLE_SCHEMA` = DATABASE() AND `TABLE_NAME` = @table;
            """,
            cancellationToken,
            ("@table", table))) > 0;

    private static async Task<bool> ColumnExistsAsync(
        DbConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken) =>
        Convert.ToInt32(await ScalarAsync(
            connection,
            """
            SELECT COUNT(*)
            FROM `information_schema`.`COLUMNS`
            WHERE `TABLE_SCHEMA` = DATABASE()
              AND `TABLE_NAME` = @table
              AND `COLUMN_NAME` = @column;
            """,
            cancellationToken,
            ("@table", table),
            ("@column", column))) > 0;

    private static async Task<object?> ScalarAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, object Value)[] parameters)
    {
        await using var command = CreateCommand(connection, sql, parameters);
        return await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task ExecuteAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, sql, []);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<string>> ReadNamesAsync(
        DbConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(connection, sql, []);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var names = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    private static DbCommand CreateCommand(
        DbConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandTimeout = MigrationCommandTimeoutSeconds;
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        return command;
    }

    private static string EscapeIdentifier(string value) => value.Replace("`", "``");
}
