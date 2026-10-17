using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;

namespace OlympiaStudent360.Api.Data;

public static class DatabaseMigrationRecovery
{
    private const string LearningReportsMigration =
        "20261008090830_AddConfigurableLearningReports";
    private const string MigrationLock = "olympia_student_360_schema_migration";

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
            await context.Database.MigrateAsync(cancellationToken);
        }
        finally
        {
            if (lockAcquired)
            {
                await ScalarAsync(
                    connection,
                    "SELECT RELEASE_LOCK(@lockName);",
                    CancellationToken.None,
                    ("@lockName", MigrationLock));
            }

            if (openedHere)
            {
                await context.Database.CloseConnectionAsync();
            }
        }
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
