using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigurableStudentRecordFieldOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentRecordFieldOptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ModuleId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    FieldKey = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Value = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    OptionGroup = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentRecordFieldOptions", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRecordFieldOptions_ModuleId_FieldKey_SortOrder",
                table: "StudentRecordFieldOptions",
                columns: new[] { "ModuleId", "FieldKey", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentRecordFieldOptions_ModuleId_FieldKey_Value",
                table: "StudentRecordFieldOptions",
                columns: new[] { "ModuleId", "FieldKey", "Value" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentRecordFieldOptions");
        }
    }
}
