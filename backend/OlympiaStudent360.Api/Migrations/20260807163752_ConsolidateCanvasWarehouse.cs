using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateCanvasWarehouse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CanvasSubmissions_AcademicYear_Term",
                table: "CanvasSubmissions");

            migrationBuilder.DropIndex(
                name: "IX_CanvasCourseScores_AcademicYear_Term",
                table: "CanvasCourseScores");

            migrationBuilder.DropColumn(
                name: "AcademicYear",
                table: "CanvasSubmissions");

            migrationBuilder.DropColumn(
                name: "PointsPossible",
                table: "CanvasSubmissions");

            migrationBuilder.DropColumn(
                name: "SisUserId",
                table: "CanvasSubmissions");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "CanvasSubmissions");

            migrationBuilder.DropColumn(
                name: "Term",
                table: "CanvasSubmissions");

            migrationBuilder.DropColumn(
                name: "WorkflowState",
                table: "CanvasEnrollments");

            migrationBuilder.DropColumn(
                name: "AcademicYear",
                table: "CanvasCourseScores");

            migrationBuilder.DropColumn(
                name: "CourseName",
                table: "CanvasCourseScores");

            migrationBuilder.DropColumn(
                name: "CourseScore",
                table: "CanvasCourseScores");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "CanvasCourseScores");

            migrationBuilder.DropColumn(
                name: "Term",
                table: "CanvasCourseScores");

            migrationBuilder.DropColumn(
                name: "AcademicYear",
                table: "CanvasCourses");

            migrationBuilder.DropColumn(
                name: "Subject",
                table: "CanvasCourses");

            migrationBuilder.DropColumn(
                name: "Term",
                table: "CanvasCourses");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "CanvasAssignments");

            migrationBuilder.RenameColumn(
                name: "StudentCode",
                table: "StudentProfiles",
                newName: "SISUserId");

            migrationBuilder.RenameIndex(
                name: "IX_StudentProfiles_StudentCode",
                table: "StudentProfiles",
                newName: "IX_StudentProfiles_SISUserId");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "CanvasSubmissions",
                newName: "WarehouseUpdatedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "CanvasSubmissions",
                newName: "WarehouseCreatedAt");

            migrationBuilder.RenameColumn(
                name: "DueAt",
                table: "CanvasSubmissions",
                newName: "CachedDueDate");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "CanvasEnrollments",
                newName: "WarehouseUpdatedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "CanvasEnrollments",
                newName: "WarehouseCreatedAt");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "CanvasCourseScores",
                newName: "WarehouseUpdatedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "CanvasCourseScores",
                newName: "WarehouseCreatedAt");

            migrationBuilder.RenameColumn(
                name: "OverrideScore",
                table: "CanvasCourseScores",
                newName: "GradingPeriodWeight");

            migrationBuilder.RenameColumn(
                name: "CanvasScoreId",
                table: "CanvasCourseScores",
                newName: "WarehouseScoreFactId");

            migrationBuilder.RenameIndex(
                name: "IX_CanvasCourseScores_CanvasScoreId",
                table: "CanvasCourseScores",
                newName: "IX_CanvasCourseScores_WarehouseScoreFactId");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "CanvasCourses",
                newName: "WarehouseUpdatedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "CanvasCourses",
                newName: "WarehouseCreatedAt");

            migrationBuilder.RenameColumn(
                name: "TermName",
                table: "CanvasCourses",
                newName: "EnrollmentTermName");

            migrationBuilder.RenameColumn(
                name: "SisSourceId",
                table: "CanvasCourses",
                newName: "SisCourseId");

            migrationBuilder.RenameColumn(
                name: "UpdatedAt",
                table: "CanvasAssignments",
                newName: "WarehouseUpdatedAt");

            migrationBuilder.RenameColumn(
                name: "CreatedAt",
                table: "CanvasAssignments",
                newName: "WarehouseCreatedAt");

            migrationBuilder.AddColumn<long>(
                name: "CanvasUserId",
                table: "StudentProfiles",
                type: "bigint",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "WorkflowState",
                table: "CanvasSubmissions",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<bool>(
                name: "Excused",
                table: "CanvasSubmissions",
                type: "tinyint(1)",
                nullable: true,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "CanvasEnrollments",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<string>(
                name: "EnrollmentState",
                table: "CanvasEnrollments",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EnrollmentOverrideScore",
                table: "CanvasCourseScores",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "GradesOverrideScore",
                table: "CanvasCourseScores",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GradingPeriodEndDate",
                table: "CanvasCourseScores",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "GradingPeriodStartDate",
                table: "CanvasCourseScores",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "WorkflowState",
                table: "CanvasCourses",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "CanvasCourses",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255);

            migrationBuilder.AddColumn<DateTime>(
                name: "EndAt",
                table: "CanvasCourses",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "StartAt",
                table: "CanvasCourses",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "WorkflowState",
                table: "CanvasAssignments",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<long>(
                name: "AssignmentGroupId",
                table: "CanvasAssignments",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "CanvasAssignments",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CanvasSyncStates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    SourceKey = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    LastSuccessfulWatermarkUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastStartedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastCompletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LastRunMode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    LastStatus = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    LastError = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    WarehouseCreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    WarehouseUpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CanvasSyncStates", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_StudentProfiles_CanvasUserId",
                table: "StudentProfiles",
                column: "CanvasUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CanvasAssignments_AssignmentGroupId",
                table: "CanvasAssignments",
                column: "AssignmentGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasSyncStates_SourceKey",
                table: "CanvasSyncStates",
                column: "SourceKey",
                unique: true);

            // Required columns need temporary values while upgrading existing rows. Remove
            // those migration-only defaults afterward so the database matches the EF model
            // and does not invent source values for future Canvas records.
            migrationBuilder.Sql("ALTER TABLE `CanvasAssignments` ALTER COLUMN `WarehouseCreatedAt` DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE `CanvasCourses` ALTER COLUMN `WarehouseCreatedAt` DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE `CanvasCourseScores` ALTER COLUMN `WarehouseScoreFactId` DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE `CanvasCourseScores` ALTER COLUMN `WarehouseCreatedAt` DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE `CanvasEnrollments` ALTER COLUMN `WarehouseCreatedAt` DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE `CanvasSubmissions` ALTER COLUMN `WarehouseCreatedAt` DROP DEFAULT;");
            migrationBuilder.Sql("ALTER TABLE `StudentProfiles` ALTER COLUMN `SISUserId` DROP DEFAULT;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CanvasSyncStates");

            migrationBuilder.DropIndex(
                name: "IX_StudentProfiles_CanvasUserId",
                table: "StudentProfiles");

            migrationBuilder.DropIndex(
                name: "IX_CanvasAssignments_AssignmentGroupId",
                table: "CanvasAssignments");

            migrationBuilder.DropColumn(
                name: "CanvasUserId",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "EnrollmentState",
                table: "CanvasEnrollments");

            migrationBuilder.DropColumn(
                name: "EnrollmentOverrideScore",
                table: "CanvasCourseScores");

            migrationBuilder.DropColumn(
                name: "GradesOverrideScore",
                table: "CanvasCourseScores");

            migrationBuilder.DropColumn(
                name: "GradingPeriodEndDate",
                table: "CanvasCourseScores");

            migrationBuilder.DropColumn(
                name: "GradingPeriodStartDate",
                table: "CanvasCourseScores");

            migrationBuilder.DropColumn(
                name: "EndAt",
                table: "CanvasCourses");

            migrationBuilder.DropColumn(
                name: "StartAt",
                table: "CanvasCourses");

            migrationBuilder.DropColumn(
                name: "AssignmentGroupId",
                table: "CanvasAssignments");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "CanvasAssignments");

            migrationBuilder.RenameColumn(
                name: "SISUserId",
                table: "StudentProfiles",
                newName: "StudentCode");

            migrationBuilder.RenameIndex(
                name: "IX_StudentProfiles_SISUserId",
                table: "StudentProfiles",
                newName: "IX_StudentProfiles_StudentCode");

            migrationBuilder.RenameColumn(
                name: "WarehouseUpdatedAt",
                table: "CanvasSubmissions",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "WarehouseCreatedAt",
                table: "CanvasSubmissions",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "CachedDueDate",
                table: "CanvasSubmissions",
                newName: "DueAt");

            migrationBuilder.RenameColumn(
                name: "WarehouseUpdatedAt",
                table: "CanvasEnrollments",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "WarehouseCreatedAt",
                table: "CanvasEnrollments",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "WarehouseUpdatedAt",
                table: "CanvasCourseScores",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "WarehouseCreatedAt",
                table: "CanvasCourseScores",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "WarehouseScoreFactId",
                table: "CanvasCourseScores",
                newName: "CanvasScoreId");

            migrationBuilder.RenameColumn(
                name: "GradingPeriodWeight",
                table: "CanvasCourseScores",
                newName: "OverrideScore");

            migrationBuilder.RenameIndex(
                name: "IX_CanvasCourseScores_WarehouseScoreFactId",
                table: "CanvasCourseScores",
                newName: "IX_CanvasCourseScores_CanvasScoreId");

            migrationBuilder.RenameColumn(
                name: "WarehouseUpdatedAt",
                table: "CanvasCourses",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "WarehouseCreatedAt",
                table: "CanvasCourses",
                newName: "CreatedAt");

            migrationBuilder.RenameColumn(
                name: "SisCourseId",
                table: "CanvasCourses",
                newName: "SisSourceId");

            migrationBuilder.RenameColumn(
                name: "EnrollmentTermName",
                table: "CanvasCourses",
                newName: "TermName");

            migrationBuilder.RenameColumn(
                name: "WarehouseUpdatedAt",
                table: "CanvasAssignments",
                newName: "UpdatedAt");

            migrationBuilder.RenameColumn(
                name: "WarehouseCreatedAt",
                table: "CanvasAssignments",
                newName: "CreatedAt");

            migrationBuilder.AlterColumn<string>(
                name: "WorkflowState",
                table: "CanvasSubmissions",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<bool>(
                name: "Excused",
                table: "CanvasSubmissions",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "tinyint(1)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcademicYear",
                table: "CanvasSubmissions",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PointsPossible",
                table: "CanvasSubmissions",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SisUserId",
                table: "CanvasSubmissions",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "CanvasSubmissions",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Term",
                table: "CanvasSubmissions",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                table: "CanvasEnrollments",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WorkflowState",
                table: "CanvasEnrollments",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "AcademicYear",
                table: "CanvasCourseScores",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CourseName",
                table: "CanvasCourseScores",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CourseScore",
                table: "CanvasCourseScores",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "CanvasCourseScores",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Term",
                table: "CanvasCourseScores",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "WorkflowState",
                table: "CanvasCourses",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "CanvasCourses",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(255)",
                oldMaxLength: 255,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AcademicYear",
                table: "CanvasCourses",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Subject",
                table: "CanvasCourses",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Term",
                table: "CanvasCourses",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "WorkflowState",
                table: "CanvasAssignments",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(50)",
                oldMaxLength: 50,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "CanvasAssignments",
                type: "varchar(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasSubmissions_AcademicYear_Term",
                table: "CanvasSubmissions",
                columns: new[] { "AcademicYear", "Term" });

            migrationBuilder.CreateIndex(
                name: "IX_CanvasCourseScores_AcademicYear_Term",
                table: "CanvasCourseScores",
                columns: new[] { "AcademicYear", "Term" });
        }
    }
}
