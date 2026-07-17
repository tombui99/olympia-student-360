using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkspacePreferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChartOrdersJson",
                table: "DashboardPreferences");

            migrationBuilder.CreateTable(
                name: "WorkspacePreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CustomReportId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false),
                    AcademicYear = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Term = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    MeasurementCycle = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Campus = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    EducationLevel = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    GradeLevel = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Homeroom = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    StudentProfileId = table.Column<int>(type: "int", nullable: true),
                    Program = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Subject = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    ChartOrderJson = table.Column<string>(type: "longtext", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspacePreferences", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkspacePreferences_CustomReports_CustomReportId",
                        column: x => x.CustomReportId,
                        principalTable: "CustomReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspacePreferences_CustomReportId_UserId",
                table: "WorkspacePreferences",
                columns: new[] { "CustomReportId", "UserId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WorkspacePreferences");

            migrationBuilder.AddColumn<string>(
                name: "ChartOrdersJson",
                table: "DashboardPreferences",
                type: "longtext",
                nullable: false,
                defaultValue: "{}");
        }
    }
}
