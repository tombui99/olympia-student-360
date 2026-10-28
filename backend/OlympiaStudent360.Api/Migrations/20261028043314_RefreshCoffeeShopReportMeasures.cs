using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class RefreshCoffeeShopReportMeasures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rebuild existing product-prefix totals using the school's all-item definition.
            // Source imports are preserved; the background worker republishes the measures.
            migrationBuilder.Sql("DELETE FROM `ErpMealReportBuilds`;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM `ErpMealReportBuilds`;");
        }
    }
}
