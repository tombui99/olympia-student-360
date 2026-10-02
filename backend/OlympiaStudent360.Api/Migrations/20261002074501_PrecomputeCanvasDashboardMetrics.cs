using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class PrecomputeCanvasDashboardMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CanvasDashboardMetricAggregates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    MetricKey = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false),
                    StudentProfileId = table.Column<int>(type: "int", nullable: false),
                    CanvasCourseId = table.Column<long>(type: "bigint", nullable: false),
                    AcademicYear = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Term = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    AcademicPeriodStart = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ValueSum = table.Column<double>(type: "double", nullable: false),
                    SampleCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanvasDashboardMetricAggregates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CanvasDashboardMetricAggregates_CanvasCourses_CanvasCourseId",
                        column: x => x.CanvasCourseId,
                        principalTable: "CanvasCourses",
                        principalColumn: "CanvasCourseId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CanvasDashboardMetricAggregates_StudentProfiles_StudentProfi~",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasDashboardMetricAggregates_CanvasCourseId",
                table: "CanvasDashboardMetricAggregates",
                column: "CanvasCourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasDashboardMetricAggregates_MetricKey_AcademicYear_Term_~",
                table: "CanvasDashboardMetricAggregates",
                columns: new[] { "MetricKey", "AcademicYear", "Term", "AcademicPeriodStart" });

            migrationBuilder.CreateIndex(
                name: "IX_CanvasDashboardMetricAggregates_MetricKey_PeriodStart",
                table: "CanvasDashboardMetricAggregates",
                columns: new[] { "MetricKey", "PeriodStart" });

            migrationBuilder.CreateIndex(
                name: "IX_CanvasDashboardMetricAggregates_StudentProfileId",
                table: "CanvasDashboardMetricAggregates",
                column: "StudentProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CanvasDashboardMetricAggregates");
        }
    }
}
