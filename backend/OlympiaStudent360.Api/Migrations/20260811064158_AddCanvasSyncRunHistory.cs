using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddCanvasSyncRunHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CanvasSyncRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    SourceKey = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Source = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Workflow = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    StartedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    WindowStartUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    WatermarkUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Error = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    Courses = table.Column<int>(type: "int", nullable: true),
                    Assignments = table.Column<int>(type: "int", nullable: true),
                    Enrollments = table.Column<int>(type: "int", nullable: true),
                    Scores = table.Column<int>(type: "int", nullable: true),
                    Submissions = table.Column<int>(type: "int", nullable: true),
                    WarehouseCreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    WarehouseUpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanvasSyncRuns", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasSyncRuns_SourceKey_StartedAtUtc",
                table: "CanvasSyncRuns",
                columns: new[] { "SourceKey", "StartedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CanvasSyncRuns_StartedAtUtc",
                table: "CanvasSyncRuns",
                column: "StartedAtUtc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CanvasSyncRuns");
        }
    }
}
