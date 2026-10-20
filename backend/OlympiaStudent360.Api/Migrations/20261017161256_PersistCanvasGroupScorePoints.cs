using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class PersistCanvasGroupScorePoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "EarnedPoints",
                table: "CanvasLeadershipReportAggregates",
                type: "double",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "PossiblePoints",
                table: "CanvasLeadershipReportAggregates",
                type: "double",
                nullable: false,
                defaultValue: 0.0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EarnedPoints",
                table: "CanvasLeadershipReportAggregates");

            migrationBuilder.DropColumn(
                name: "PossiblePoints",
                table: "CanvasLeadershipReportAggregates");
        }
    }
}
