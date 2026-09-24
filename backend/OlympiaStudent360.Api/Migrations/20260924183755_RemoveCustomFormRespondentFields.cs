using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCustomFormRespondentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RespondentEmail",
                table: "CustomFormResponses");

            migrationBuilder.DropColumn(
                name: "RespondentName",
                table: "CustomFormResponses");

            migrationBuilder.DropColumn(
                name: "RespondentPhone",
                table: "CustomFormResponses");

            migrationBuilder.DropColumn(
                name: "RespondentRelationship",
                table: "CustomFormResponses");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RespondentEmail",
                table: "CustomFormResponses",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RespondentName",
                table: "CustomFormResponses",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RespondentPhone",
                table: "CustomFormResponses",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "RespondentRelationship",
                table: "CustomFormResponses",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");
        }
    }
}
