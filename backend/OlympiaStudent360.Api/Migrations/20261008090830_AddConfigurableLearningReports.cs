using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddConfigurableLearningReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CanvasAssignmentId",
                table: "CanvasDashboardMetricAggregates",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CanvasDashboardAggregateBuilds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    SchemaVersion = table.Column<int>(type: "int", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanvasDashboardAggregateBuilds", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ImportedReportAggregates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Source = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    StudentProfileId = table.Column<int>(type: "int", nullable: true),
                    StudentKey = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: false),
                    StudentName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    SISUserId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Grade = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Homeroom = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    CanvasGrade = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    Campus = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    EducationLevel = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Program = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Subject = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    MeasurementCycle = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    AcademicYear = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Term = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    EventDay = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    LatestEvent = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Type = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    Category = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Diagnosis = table.Column<string>(type: "text", nullable: true),
                    EnteredBy = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    ClassName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Symptoms = table.Column<string>(type: "text", nullable: true),
                    RecordCount = table.Column<int>(type: "int", nullable: false),
                    AttentionCount = table.Column<int>(type: "int", nullable: false),
                    Recent30Count = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportedReportAggregates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImportedReportAggregates_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ImportedReportBuilds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    LastChangeId = table.Column<int>(type: "int", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportedReportBuilds", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ImportedReportChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportedReportChanges", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "StudentRiskSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    StudentProfileId = table.Column<int>(type: "int", nullable: false),
                    AcademicYear = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Term = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    AsOfUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AverageCanvasScore = table.Column<decimal>(type: "decimal(7,2)", precision: 7, scale: 2, nullable: true),
                    BehaviourCount = table.Column<int>(type: "int", nullable: false),
                    PositiveBehaviourCount = table.Column<int>(type: "int", nullable: false),
                    ConcerningBehaviourCount = table.Column<int>(type: "int", nullable: false),
                    ProjectCount = table.Column<int>(type: "int", nullable: false),
                    MentalHealthCount = table.Column<int>(type: "int", nullable: false),
                    RiskConcerningCount = table.Column<int>(type: "int", nullable: false),
                    RiskBusCount = table.Column<int>(type: "int", nullable: false),
                    RiskLevelWithBus = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    RiskLevelWithoutBus = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    RiskFlagsWithBusJson = table.Column<string>(type: "text", nullable: false),
                    RiskFlagsWithoutBusJson = table.Column<string>(type: "text", nullable: false),
                    RiskFlagCountWithBus = table.Column<int>(type: "int", nullable: false),
                    RiskFlagCountWithoutBus = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StudentRiskSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StudentRiskSnapshots_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasDashboardMetricAggregates_CanvasAssignmentId",
                table: "CanvasDashboardMetricAggregates",
                column: "CanvasAssignmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ImportedReportAggregates_Source_AcademicYear_Term_EventDay",
                table: "ImportedReportAggregates",
                columns: new[] { "Source", "AcademicYear", "Term", "EventDay" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportedReportAggregates_Source_EventDay_StudentProfileId",
                table: "ImportedReportAggregates",
                columns: new[] { "Source", "EventDay", "StudentProfileId" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportedReportAggregates_Source_StudentKey",
                table: "ImportedReportAggregates",
                columns: new[] { "Source", "StudentKey" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportedReportAggregates_StudentProfileId",
                table: "ImportedReportAggregates",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_StudentRiskSnapshots_AcademicYear_Term_RiskLevelWithBus",
                table: "StudentRiskSnapshots",
                columns: new[] { "AcademicYear", "Term", "RiskLevelWithBus" });

            migrationBuilder.CreateIndex(
                name: "IX_StudentRiskSnapshots_AcademicYear_Term_StudentProfileId",
                table: "StudentRiskSnapshots",
                columns: new[] { "AcademicYear", "Term", "StudentProfileId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentRiskSnapshots_StudentProfileId",
                table: "StudentRiskSnapshots",
                column: "StudentProfileId");

            migrationBuilder.AddForeignKey(
                name: "FK_CanvasDashboardMetricAggregates_CanvasAssignments_CanvasAssi~",
                table: "CanvasDashboardMetricAggregates",
                column: "CanvasAssignmentId",
                principalTable: "CanvasAssignments",
                principalColumn: "CanvasAssignmentId",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CanvasDashboardMetricAggregates_CanvasAssignments_CanvasAssi~",
                table: "CanvasDashboardMetricAggregates");

            migrationBuilder.DropTable(
                name: "CanvasDashboardAggregateBuilds");

            migrationBuilder.DropTable(
                name: "ImportedReportAggregates");

            migrationBuilder.DropTable(
                name: "ImportedReportBuilds");

            migrationBuilder.DropTable(
                name: "ImportedReportChanges");

            migrationBuilder.DropTable(
                name: "StudentRiskSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_CanvasDashboardMetricAggregates_CanvasAssignmentId",
                table: "CanvasDashboardMetricAggregates");

            migrationBuilder.DropColumn(
                name: "CanvasAssignmentId",
                table: "CanvasDashboardMetricAggregates");

        }
    }
}
