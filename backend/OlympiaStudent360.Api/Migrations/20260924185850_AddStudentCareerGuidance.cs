using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentCareerGuidance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StudentCareerGuidanceRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    StudentProfileId = table.Column<int>(type: "int", nullable: false),
                    RecordType = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    RecordDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AcademicYear = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Term = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Title = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    TargetCountries = table.Column<string>(type: "text", nullable: true),
                    TargetUniversities = table.Column<string>(type: "text", nullable: true),
                    TargetMajors = table.Column<string>(type: "text", nullable: true),
                    CareerInterests = table.Column<string>(type: "text", nullable: true),
                    ApplicationProgressPercent = table.Column<int>(type: "int", nullable: true),
                    PreparationProgressPercent = table.Column<int>(type: "int", nullable: true),
                    QualityRating = table.Column<int>(type: "int", nullable: true),
                    ActivitiesSummary = table.Column<string>(type: "text", nullable: true),
                    Issues = table.Column<string>(type: "text", nullable: true),
                    Solutions = table.Column<string>(type: "text", nullable: true),
                    StudentSupport = table.Column<string>(type: "text", nullable: true),
                    StudentFeedback = table.Column<string>(type: "text", nullable: true),
                    NextSteps = table.Column<string>(type: "text", nullable: true),
                    Recommendations = table.Column<string>(type: "text", nullable: true),
                    Proposals = table.Column<string>(type: "text", nullable: true),
                    InternshipCompany = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    InternshipRole = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    InternshipStartDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    InternshipEndDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    InternshipProgressPercent = table.Column<int>(type: "int", nullable: true),
                    InternshipQualityRating = table.Column<int>(type: "int", nullable: true),
                    StudentSelfAssessment = table.Column<string>(type: "text", nullable: true),
                    HostAssessment = table.Column<string>(type: "text", nullable: true),
                    FinalProduct = table.Column<string>(type: "text", nullable: true),
                    RecordedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false),
                    RecordedByName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true),
                    UpdatedByName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentCareerGuidanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentCareerGuidanceRecords_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCareerGuidanceRecords_AcademicYear_RecordType_Status",
                table: "StudentCareerGuidanceRecords",
                columns: new[] { "AcademicYear", "RecordType", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentCareerGuidanceRecords_RecordDate",
                table: "StudentCareerGuidanceRecords",
                column: "RecordDate");

            migrationBuilder.CreateIndex(
                name: "IX_StudentCareerGuidanceRecords_StudentProfileId",
                table: "StudentCareerGuidanceRecords",
                column: "StudentProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StudentCareerGuidanceRecords");
        }
    }
}
