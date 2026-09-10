using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class ExpandCanvasWarehouseForSectionsAndGrades : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Attempt",
                table: "CanvasSubmissions",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "GraderId",
                table: "CanvasSubmissions",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LatePolicyStatus",
                table: "CanvasSubmissions",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PointsDeducted",
                table: "CanvasSubmissions",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PostedAt",
                table: "CanvasSubmissions",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CanvasCourseSectionId",
                table: "CanvasEnrollments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndAt",
                table: "CanvasEnrollments",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastActivityAt",
                table: "CanvasEnrollments",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAttendedAt",
                table: "CanvasEnrollments",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "LimitPrivilegesToCourseSection",
                table: "CanvasEnrollments",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "CanvasEnrollments",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RoleId",
                table: "CanvasEnrollments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SisAccountId",
                table: "CanvasEnrollments",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SisCourseId",
                table: "CanvasEnrollments",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SisSectionId",
                table: "CanvasEnrollments",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceCreatedAt",
                table: "CanvasEnrollments",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceUpdatedAt",
                table: "CanvasEnrollments",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartAt",
                table: "CanvasEnrollments",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "TotalActivityTimeSeconds",
                table: "CanvasEnrollments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentGrade",
                table: "CanvasCourseScores",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CurrentPoints",
                table: "CanvasCourseScores",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalGrade",
                table: "CanvasCourseScores",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnpostedCurrentGrade",
                table: "CanvasCourseScores",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "UnpostedCurrentPoints",
                table: "CanvasCourseScores",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "UnpostedCurrentScore",
                table: "CanvasCourseScores",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UnpostedFinalGrade",
                table: "CanvasCourseScores",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "UnpostedFinalScore",
                table: "CanvasCourseScores",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CanvasAccountId",
                table: "CanvasCourses",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CanvasGradeLevel",
                table: "CanvasCourses",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CanvasGradeSourceAccountId",
                table: "CanvasCourses",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "CanvasRootAccountId",
                table: "CanvasCourses",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockAt",
                table: "CanvasAssignments",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "OmitFromFinalGrade",
                table: "CanvasAssignments",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Position",
                table: "CanvasAssignments",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "Published",
                table: "CanvasAssignments",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UnlockAt",
                table: "CanvasAssignments",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CanvasAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CanvasAccountId = table.Column<long>(type: "bigint", nullable: false),
                    ParentAccountId = table.Column<long>(type: "bigint", nullable: true),
                    RootAccountId = table.Column<long>(type: "bigint", nullable: true),
                    SisAccountId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    Name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    WorkflowState = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    WarehouseCreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    WarehouseUpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanvasAccounts", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CanvasCourseSections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CanvasSectionId = table.Column<long>(type: "bigint", nullable: false),
                    CanvasCourseId = table.Column<long>(type: "bigint", nullable: false),
                    SisSectionId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    SisCourseId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    IntegrationId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    Name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    StartAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EndAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TotalStudents = table.Column<int>(type: "int", nullable: true),
                    RestrictEnrollmentsToSectionDates = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    WarehouseCreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    WarehouseUpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanvasCourseSections", x => x.Id);
                    table.UniqueConstraint("AK_CanvasCourseSections_CanvasSectionId", x => x.CanvasSectionId);
                    table.ForeignKey(
                        name: "FK_CanvasCourseSections_CanvasCourses_CanvasCourseId",
                        column: x => x.CanvasCourseId,
                        principalTable: "CanvasCourses",
                        principalColumn: "CanvasCourseId",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "CanvasUsers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    CanvasUserId = table.Column<long>(type: "bigint", nullable: false),
                    SisUserId = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    IntegrationId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    LoginId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    Name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    ShortName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    SortableName = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    Email = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    WarehouseCreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    WarehouseUpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanvasUsers", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasEnrollments_CanvasCourseSectionId",
                table: "CanvasEnrollments",
                column: "CanvasCourseSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasCourses_CanvasAccountId",
                table: "CanvasCourses",
                column: "CanvasAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasCourses_CanvasGradeLevel",
                table: "CanvasCourses",
                column: "CanvasGradeLevel");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasAccounts_CanvasAccountId",
                table: "CanvasAccounts",
                column: "CanvasAccountId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CanvasAccounts_ParentAccountId",
                table: "CanvasAccounts",
                column: "ParentAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasAccounts_SisAccountId",
                table: "CanvasAccounts",
                column: "SisAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasCourseSections_CanvasCourseId",
                table: "CanvasCourseSections",
                column: "CanvasCourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasCourseSections_CanvasSectionId",
                table: "CanvasCourseSections",
                column: "CanvasSectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CanvasCourseSections_SisSectionId",
                table: "CanvasCourseSections",
                column: "SisSectionId");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasUsers_CanvasUserId",
                table: "CanvasUsers",
                column: "CanvasUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CanvasUsers_SisUserId",
                table: "CanvasUsers",
                column: "SisUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CanvasEnrollments_CanvasCourseSections_CanvasCourseSectionId",
                table: "CanvasEnrollments",
                column: "CanvasCourseSectionId",
                principalTable: "CanvasCourseSections",
                principalColumn: "CanvasSectionId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CanvasEnrollments_CanvasCourseSections_CanvasCourseSectionId",
                table: "CanvasEnrollments");

            migrationBuilder.DropTable(
                name: "CanvasAccounts");

            migrationBuilder.DropTable(
                name: "CanvasCourseSections");

            migrationBuilder.DropTable(
                name: "CanvasUsers");

            migrationBuilder.DropIndex(
                name: "IX_CanvasEnrollments_CanvasCourseSectionId",
                table: "CanvasEnrollments");

            migrationBuilder.DropIndex(
                name: "IX_CanvasCourses_CanvasAccountId",
                table: "CanvasCourses");

            migrationBuilder.DropIndex(
                name: "IX_CanvasCourses_CanvasGradeLevel",
                table: "CanvasCourses");

            migrationBuilder.DropColumn(
                name: "Attempt",
                table: "CanvasSubmissions");

            migrationBuilder.DropColumn(
                name: "GraderId",
                table: "CanvasSubmissions");

            migrationBuilder.DropColumn(
                name: "LatePolicyStatus",
                table: "CanvasSubmissions");

            migrationBuilder.DropColumn(
                name: "PointsDeducted",
                table: "CanvasSubmissions");

            migrationBuilder.DropColumn(
                name: "PostedAt",
                table: "CanvasSubmissions");

            migrationBuilder.DropColumn(
                name: "CanvasCourseSectionId",
                table: "CanvasEnrollments");

            migrationBuilder.DropColumn(
                name: "EndAt",
                table: "CanvasEnrollments");

            migrationBuilder.DropColumn(
                name: "LastActivityAt",
                table: "CanvasEnrollments");

            migrationBuilder.DropColumn(
                name: "LastAttendedAt",
                table: "CanvasEnrollments");

            migrationBuilder.DropColumn(
                name: "LimitPrivilegesToCourseSection",
                table: "CanvasEnrollments");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "CanvasEnrollments");

            migrationBuilder.DropColumn(
                name: "RoleId",
                table: "CanvasEnrollments");

            migrationBuilder.DropColumn(
                name: "SisAccountId",
                table: "CanvasEnrollments");

            migrationBuilder.DropColumn(
                name: "SisCourseId",
                table: "CanvasEnrollments");

            migrationBuilder.DropColumn(
                name: "SisSectionId",
                table: "CanvasEnrollments");

            migrationBuilder.DropColumn(
                name: "SourceCreatedAt",
                table: "CanvasEnrollments");

            migrationBuilder.DropColumn(
                name: "SourceUpdatedAt",
                table: "CanvasEnrollments");

            migrationBuilder.DropColumn(
                name: "StartAt",
                table: "CanvasEnrollments");

            migrationBuilder.DropColumn(
                name: "TotalActivityTimeSeconds",
                table: "CanvasEnrollments");

            migrationBuilder.DropColumn(
                name: "CurrentGrade",
                table: "CanvasCourseScores");

            migrationBuilder.DropColumn(
                name: "CurrentPoints",
                table: "CanvasCourseScores");

            migrationBuilder.DropColumn(
                name: "FinalGrade",
                table: "CanvasCourseScores");

            migrationBuilder.DropColumn(
                name: "UnpostedCurrentGrade",
                table: "CanvasCourseScores");

            migrationBuilder.DropColumn(
                name: "UnpostedCurrentPoints",
                table: "CanvasCourseScores");

            migrationBuilder.DropColumn(
                name: "UnpostedCurrentScore",
                table: "CanvasCourseScores");

            migrationBuilder.DropColumn(
                name: "UnpostedFinalGrade",
                table: "CanvasCourseScores");

            migrationBuilder.DropColumn(
                name: "UnpostedFinalScore",
                table: "CanvasCourseScores");

            migrationBuilder.DropColumn(
                name: "CanvasAccountId",
                table: "CanvasCourses");

            migrationBuilder.DropColumn(
                name: "CanvasGradeLevel",
                table: "CanvasCourses");

            migrationBuilder.DropColumn(
                name: "CanvasGradeSourceAccountId",
                table: "CanvasCourses");

            migrationBuilder.DropColumn(
                name: "CanvasRootAccountId",
                table: "CanvasCourses");

            migrationBuilder.DropColumn(
                name: "LockAt",
                table: "CanvasAssignments");

            migrationBuilder.DropColumn(
                name: "OmitFromFinalGrade",
                table: "CanvasAssignments");

            migrationBuilder.DropColumn(
                name: "Position",
                table: "CanvasAssignments");

            migrationBuilder.DropColumn(
                name: "Published",
                table: "CanvasAssignments");

            migrationBuilder.DropColumn(
                name: "UnlockAt",
                table: "CanvasAssignments");
        }
    }
}
