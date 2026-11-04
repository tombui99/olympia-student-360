using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSisHealthVisitSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLocallyHidden",
                table: "StudentHealthVisits",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SisIsDeleted",
                table: "StudentHealthVisits",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SisSourceKey",
                table: "StudentHealthVisits",
                type: "varchar(64)",
                maxLength: 64,
                nullable: true,
                collation: "utf8mb4_bin");

            migrationBuilder.AddColumn<string>(
                name: "SisSourceValuesJson",
                table: "StudentHealthVisits",
                type: "json",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SisVisitId",
                table: "StudentHealthVisits",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                collation: "utf8mb4_bin");

            migrationBuilder.CreateIndex(
                name: "IX_StudentHealthVisits_SisSourceKey_SisVisitId",
                table: "StudentHealthVisits",
                columns: new[] { "SisSourceKey", "SisVisitId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StudentHealthVisits_SisSourceKey_SisVisitId",
                table: "StudentHealthVisits");

            migrationBuilder.DropColumn(
                name: "IsLocallyHidden",
                table: "StudentHealthVisits");

            migrationBuilder.DropColumn(
                name: "SisIsDeleted",
                table: "StudentHealthVisits");

            migrationBuilder.DropColumn(
                name: "SisSourceKey",
                table: "StudentHealthVisits");

            migrationBuilder.DropColumn(
                name: "SisSourceValuesJson",
                table: "StudentHealthVisits");

            migrationBuilder.DropColumn(
                name: "SisVisitId",
                table: "StudentHealthVisits");
        }
    }
}
