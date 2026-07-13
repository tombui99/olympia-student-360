using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAcademicDashboardDimensions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AcademicYear",
                table: "StudentProjects",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActivityDate",
                table: "StudentProjects",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeasurementCycle",
                table: "StudentProjects",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Program",
                table: "StudentProjects",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Term",
                table: "StudentProjects",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Campus",
                table: "StudentProfiles",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EducationLevel",
                table: "StudentProfiles",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Program",
                table: "StudentProfiles",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcademicYear",
                table: "StudentBehaviours",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeasurementCycle",
                table: "StudentBehaviours",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Program",
                table: "StudentBehaviours",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "StudentBehaviours",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Term",
                table: "StudentBehaviours",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AcademicPeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    AcademicYear = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Term = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsCurrent = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcademicPeriods", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DashboardMetrics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Key = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Label = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Domain = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Source = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Unit = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardMetrics", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DashboardPreferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
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
                    ChartOrdersJson = table.Column<string>(type: "longtext", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardPreferences", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "DashboardMetricObservations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    DashboardMetricId = table.Column<int>(type: "int", nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Value = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    BaselineValue = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    KpiTarget = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
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
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DashboardMetricObservations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DashboardMetricObservations_DashboardMetrics_DashboardMetric~",
                        column: x => x.DashboardMetricId,
                        principalTable: "DashboardMetrics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DashboardMetricObservations_StudentProfiles_StudentProfileId",
                        column: x => x.StudentProfileId,
                        principalTable: "StudentProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AcademicPeriods_AcademicYear_Term",
                table: "AcademicPeriods",
                columns: new[] { "AcademicYear", "Term" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AcademicPeriods_IsCurrent",
                table: "AcademicPeriods",
                column: "IsCurrent");

            migrationBuilder.CreateIndex(
                name: "IX_DashboardMetricObservations_AcademicYear_Term_MeasurementCyc~",
                table: "DashboardMetricObservations",
                columns: new[] { "AcademicYear", "Term", "MeasurementCycle" });

            migrationBuilder.CreateIndex(
                name: "IX_DashboardMetricObservations_DashboardMetricId_PeriodStart",
                table: "DashboardMetricObservations",
                columns: new[] { "DashboardMetricId", "PeriodStart" });

            migrationBuilder.CreateIndex(
                name: "IX_DashboardMetricObservations_StudentProfileId",
                table: "DashboardMetricObservations",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_DashboardMetrics_Key",
                table: "DashboardMetrics",
                column: "Key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DashboardPreferences_UserId",
                table: "DashboardPreferences",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcademicPeriods");

            migrationBuilder.DropTable(
                name: "DashboardMetricObservations");

            migrationBuilder.DropTable(
                name: "DashboardPreferences");

            migrationBuilder.DropTable(
                name: "DashboardMetrics");

            migrationBuilder.DropColumn(
                name: "AcademicYear",
                table: "StudentProjects");

            migrationBuilder.DropColumn(
                name: "ActivityDate",
                table: "StudentProjects");

            migrationBuilder.DropColumn(
                name: "MeasurementCycle",
                table: "StudentProjects");

            migrationBuilder.DropColumn(
                name: "Program",
                table: "StudentProjects");

            migrationBuilder.DropColumn(
                name: "Term",
                table: "StudentProjects");

            migrationBuilder.DropColumn(
                name: "Campus",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "EducationLevel",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "Program",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "AcademicYear",
                table: "StudentBehaviours");

            migrationBuilder.DropColumn(
                name: "MeasurementCycle",
                table: "StudentBehaviours");

            migrationBuilder.DropColumn(
                name: "Program",
                table: "StudentBehaviours");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "StudentBehaviours");

            migrationBuilder.DropColumn(
                name: "Term",
                table: "StudentBehaviours");
        }
    }
}
