using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCustomFormActiveStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomForms_IsArchived_IsActive",
                table: "CustomForms");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "CustomForms");

            migrationBuilder.CreateIndex(
                name: "IX_CustomForms_IsArchived",
                table: "CustomForms",
                column: "IsArchived");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CustomForms_IsArchived",
                table: "CustomForms");

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "CustomForms",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_CustomForms_IsArchived_IsActive",
                table: "CustomForms",
                columns: new[] { "IsArchived", "IsActive" });
        }
    }
}
