using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class ScopeReportInvalidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Areas",
                table: "ImportedReportChanges",
                type: "int",
                nullable: false,
                defaultValue: 127);

            migrationBuilder.AddColumn<int>(
                name: "StudentProfileId",
                table: "ImportedReportChanges",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SchemaVersion",
                table: "ImportedReportBuilds",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SchemaVersion",
                table: "ErpMealReportBuilds",
                type: "int",
                nullable: false,
                defaultValue: 0);

            // Report row shapes/calculations are unchanged. Adopt valid existing snapshots;
            // only future invalidations use the narrower scopes. Pending legacy changes keep
            // their conservative All scope and will still be processed by both consumers.
            migrationBuilder.Sql("UPDATE ImportedReportBuilds SET SchemaVersion = 1");
            migrationBuilder.Sql("UPDATE ErpMealReportBuilds SET SchemaVersion = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Areas",
                table: "ImportedReportChanges");

            migrationBuilder.DropColumn(
                name: "StudentProfileId",
                table: "ImportedReportChanges");

            migrationBuilder.DropColumn(
                name: "SchemaVersion",
                table: "ImportedReportBuilds");

            migrationBuilder.DropColumn(
                name: "SchemaVersion",
                table: "ErpMealReportBuilds");
        }
    }
}
