using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCanvasActivityReportingDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AcademicActivityDate",
                table: "CanvasDashboardMetricAggregates",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CanvasDashboardMetrics_ActivityDate",
                table: "CanvasDashboardMetricAggregates",
                columns: new[] { "AcademicYear", "Term", "AcademicActivityDate", "MetricKey" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CanvasDashboardMetrics_ActivityDate",
                table: "CanvasDashboardMetricAggregates");

            migrationBuilder.DropColumn(
                name: "AcademicActivityDate",
                table: "CanvasDashboardMetricAggregates");
        }
    }
}
