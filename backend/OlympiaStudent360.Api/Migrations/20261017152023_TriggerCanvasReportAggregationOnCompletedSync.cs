using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class TriggerCanvasReportAggregationOnCompletedSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LastCompletedSyncRunId",
                table: "CanvasDashboardAggregateBuilds",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Prior ETL versions built the snapshot before recording each successful run as
            // completed. Existing current-version builds therefore already include these runs.
            migrationBuilder.Sql("""
                UPDATE `CanvasDashboardAggregateBuilds`
                SET `LastCompletedSyncRunId` = COALESCE(
                    (SELECT MAX(`Id`) FROM `CanvasSyncRuns` WHERE `Status` = 'completed'), 0);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastCompletedSyncRunId",
                table: "CanvasDashboardAggregateBuilds");
        }
    }
}
