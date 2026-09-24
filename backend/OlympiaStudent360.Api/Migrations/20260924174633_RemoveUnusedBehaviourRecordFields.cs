using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedBehaviourRecordFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudentBehaviours_SourceFileName_SourceRecordId",
                table: "StudentBehaviours");

            migrationBuilder.DropColumn(
                name: "RecordedByName",
                table: "StudentBehaviours");

            migrationBuilder.DropColumn(
                name: "SourceDataIssues",
                table: "StudentBehaviours");

            migrationBuilder.DropColumn(
                name: "SourceRecordId",
                table: "StudentBehaviours");

            migrationBuilder.DropColumn(
                name: "StudentMatchStatus",
                table: "StudentBehaviours");

            migrationBuilder.CreateIndex(
                name: "IX_StudentBehaviours_SourceFileName_SourceRowNumber",
                table: "StudentBehaviours",
                columns: new[] { "SourceFileName", "SourceRowNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudentBehaviours_SourceFileName_SourceRowNumber",
                table: "StudentBehaviours");

            migrationBuilder.AddColumn<string>(
                name: "RecordedByName",
                table: "StudentBehaviours",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceDataIssues",
                table: "StudentBehaviours",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceRecordId",
                table: "StudentBehaviours",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentMatchStatus",
                table: "StudentBehaviours",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentBehaviours_SourceFileName_SourceRecordId",
                table: "StudentBehaviours",
                columns: new[] { "SourceFileName", "SourceRecordId" },
                unique: true);
        }
    }
}
