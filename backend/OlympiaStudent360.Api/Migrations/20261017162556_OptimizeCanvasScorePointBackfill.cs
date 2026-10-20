using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeCanvasScorePointBackfill : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CanvasSubmissions_ScorePointJoin",
                table: "CanvasSubmissions",
                columns: new[] { "CanvasAssignmentId", "StudentProfileId", "CanvasCourseId" });

            migrationBuilder.CreateIndex(
                name: "IX_CanvasLeadershipReportAggregates_ScorePointJoin",
                table: "CanvasLeadershipReportAggregates",
                columns: new[] { "StudentProfileId", "CanvasCourseId", "AcademicYear", "Term", "IsFinalCourseScore", "AcademicPeriodStart", "AssignmentGroupName" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CanvasSubmissions_ScorePointJoin",
                table: "CanvasSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_CanvasLeadershipReportAggregates_ScorePointJoin",
                table: "CanvasLeadershipReportAggregates");
        }
    }
}
