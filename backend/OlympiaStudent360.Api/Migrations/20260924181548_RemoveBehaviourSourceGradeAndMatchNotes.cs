using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveBehaviourSourceGradeAndMatchNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceGradeLevel",
                table: "StudentBehaviours");

            migrationBuilder.DropColumn(
                name: "StudentMatchNotes",
                table: "StudentBehaviours");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceGradeLevel",
                table: "StudentBehaviours",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StudentMatchNotes",
                table: "StudentBehaviours",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true);
        }
    }
}
