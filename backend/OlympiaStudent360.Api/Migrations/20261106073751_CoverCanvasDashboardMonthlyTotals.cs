using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class CoverCanvasDashboardMonthlyTotals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CanvasDashboardMetrics_MonthlyTotals",
                table: "CanvasDashboardMetricAggregates",
                columns: new[] { "MetricKey", "AcademicYear", "Term", "AcademicPeriodStart", "ValueSum", "SampleCount" });

            // The MySQL provider does not quote DROP INDEX identifiers; this legacy
            // generated name contains '~' and must be explicitly quoted.
            migrationBuilder.Sql("""
                DROP INDEX `IX_CanvasDashboardMetricAggregates_MetricKey_AcademicYear_Term_~`
                ON `CanvasDashboardMetricAggregates`;
                """)
                .Annotation("Olympia:DropIndexTable", "CanvasDashboardMetricAggregates")
                .Annotation("Olympia:DropIndexName", "IX_CanvasDashboardMetricAggregates_MetricKey_AcademicYear_Term_~");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CanvasDashboardMetricAggregates_MetricKey_AcademicYear_Term_~",
                table: "CanvasDashboardMetricAggregates",
                columns: new[] { "MetricKey", "AcademicYear", "Term", "AcademicPeriodStart" });

            migrationBuilder.DropIndex(
                name: "IX_CanvasDashboardMetrics_MonthlyTotals",
                table: "CanvasDashboardMetricAggregates");
        }
    }
}
