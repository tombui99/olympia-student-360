using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class TrackCanvasReportCourseChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ReportChangesTracked",
                table: "CanvasSyncRuns",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PeriodConfigurationHash",
                table: "CanvasDashboardAggregateBuilds",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CanvasReportCourseChanges",
                columns: table => new
                {
                    CanvasCourseId = table.Column<long>(type: "bigint", nullable: false),
                    ChangedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanvasReportCourseChanges", x => x.CanvasCourseId);
                })
                .Annotation("MySQL:Charset", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CanvasReportCourseChanges");

            migrationBuilder.DropColumn(
                name: "ReportChangesTracked",
                table: "CanvasSyncRuns");

            migrationBuilder.DropColumn(
                name: "PeriodConfigurationHash",
                table: "CanvasDashboardAggregateBuilds");
        }
    }
}
