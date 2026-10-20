using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class PrecomputeCanvasLeadershipReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CanvasLeadershipReportAggregates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    StudentProfileId = table.Column<int>(type: "int", nullable: false),
                    CanvasCourseId = table.Column<long>(type: "bigint", nullable: false),
                    AcademicYear = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Term = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    AcademicPeriodStart = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    IsFinalCourseScore = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    AssignmentGroupName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    AssessmentCategory = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true),
                    Submitted = table.Column<int>(type: "int", nullable: false),
                    Missing = table.Column<int>(type: "int", nullable: false),
                    Late = table.Column<int>(type: "int", nullable: false),
                    Graded = table.Column<int>(type: "int", nullable: false),
                    ScoreSum = table.Column<double>(type: "double", nullable: false),
                    ScoreCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanvasLeadershipReportAggregates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CanvasLeadershipReportAggregates_CanvasCourses_CanvasCourseId",
                        column: x => x.CanvasCourseId,
                        principalTable: "CanvasCourses",
                        principalColumn: "CanvasCourseId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CanvasLeadershipReportAggregates_StudentProfiles_StudentProf~",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasLeadershipReportAggregates_AcademicYear_AssessmentCate~",
                table: "CanvasLeadershipReportAggregates",
                columns: new[] { "AcademicYear", "AssessmentCategory" });

            migrationBuilder.CreateIndex(
                name: "IX_CanvasLeadershipReportAggregates_AcademicYear_IsFinalCours~",
                table: "CanvasLeadershipReportAggregates",
                columns: new[] { "AcademicYear", "IsFinalCourseScore", "Term", "AcademicPeriodStart" });

            migrationBuilder.CreateIndex(
                name: "IX_CanvasLeadershipReportAggregates_CanvasCourseId",
                table: "CanvasLeadershipReportAggregates",
                column: "CanvasCourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasLeadershipReportAggregates_StudentProfileId",
                table: "CanvasLeadershipReportAggregates",
                column: "StudentProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CanvasLeadershipReportAggregates");
        }
    }
}
