using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class IndexTranscriptAssessmentEvidence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_CanvasSubmissions_TranscriptEvidence",
                table: "CanvasSubmissions",
                columns: new[] { "StudentProfileId", "CanvasCourseId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CanvasSubmissions_TranscriptEvidence",
                table: "CanvasSubmissions");
        }
    }
}
