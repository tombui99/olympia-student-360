using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthoritativeSisStudentRoster : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ForeignName",
                table: "StudentProfiles",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HomeroomCode",
                table: "StudentProfiles",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SISLastImportedAtUtc",
                table: "StudentProfiles",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SISStatus",
                table: "StudentProfiles",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SISStudentCode",
                table: "StudentProfiles",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "BirthYear",
                table: "SISStudentParents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StudentProfileId",
                table: "SISStudentParents",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "SISStudentParents",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "StudentProfileId",
                table: "SISHealthVisits",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE `SISStudentParents` parent
                INNER JOIN `StudentProfiles` student ON student.`SISUserId` = parent.`SISUserId`
                SET parent.`StudentProfileId` = student.`Id`
                """);

            migrationBuilder.Sql("""
                UPDATE `SISHealthVisits` visit
                INNER JOIN `StudentProfiles` student ON student.`SISUserId` = visit.`SISUserId`
                SET visit.`StudentProfileId` = student.`Id`
                WHERE visit.`SISUserId` IS NOT NULL
                """);

            migrationBuilder.AlterColumn<int>(
                name: "StudentProfileId",
                table: "SISStudentParents",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.DropForeignKey(
                name: "FK_SISHealthVisits_StudentProfiles_SISUserId",
                table: "SISHealthVisits");

            migrationBuilder.DropForeignKey(
                name: "FK_SISStudentParents_StudentProfiles_SISUserId",
                table: "SISStudentParents");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_StudentProfiles_SISUserId",
                table: "StudentProfiles");

            migrationBuilder.DropIndex(
                name: "IX_SISStudentParents_SISUserId",
                table: "SISStudentParents");

            // A previous Canvas reconciliation could create a second profile for the same
            // school username after the legacy alternate key had been removed by an
            // interrupted migration. Merge those rows before enforcing the new unique index.
            migrationBuilder.Sql("""
                DROP TEMPORARY TABLE IF EXISTS `_student_profile_dedup`;
                CREATE TEMPORARY TABLE `_student_profile_dedup` AS
                SELECT ranked.`Id` AS `DuplicateId`, ranked.`KeeperId`
                FROM (
                    SELECT profile.`Id`,
                           FIRST_VALUE(profile.`Id`) OVER (
                               PARTITION BY profile.`SISUserId`
                               ORDER BY (profile.`CanvasUserId` IS NOT NULL) DESC,
                                        (profile.`SISStudentCode` IS NOT NULL) DESC,
                                        profile.`Id` DESC
                           ) AS `KeeperId`,
                           COUNT(*) OVER (PARTITION BY profile.`SISUserId`) AS `ProfileCount`
                    FROM `StudentProfiles` profile
                ) ranked
                WHERE ranked.`ProfileCount` > 1 AND ranked.`Id` <> ranked.`KeeperId`;

                UPDATE `CanvasEnrollments` target INNER JOIN `_student_profile_dedup` merge_row
                    ON merge_row.`DuplicateId` = target.`StudentProfileId`
                SET target.`StudentProfileId` = merge_row.`KeeperId`;
                UPDATE `CanvasCourseScores` target INNER JOIN `_student_profile_dedup` merge_row
                    ON merge_row.`DuplicateId` = target.`StudentProfileId`
                SET target.`StudentProfileId` = merge_row.`KeeperId`;
                UPDATE `CanvasSubmissions` target INNER JOIN `_student_profile_dedup` merge_row
                    ON merge_row.`DuplicateId` = target.`StudentProfileId`
                SET target.`StudentProfileId` = merge_row.`KeeperId`;
                UPDATE `CustomFormAccessSessions` target INNER JOIN `_student_profile_dedup` merge_row
                    ON merge_row.`DuplicateId` = target.`StudentProfileId`
                SET target.`StudentProfileId` = merge_row.`KeeperId`;
                UPDATE `CustomFormResponses` target INNER JOIN `_student_profile_dedup` merge_row
                    ON merge_row.`DuplicateId` = target.`StudentProfileId`
                SET target.`StudentProfileId` = merge_row.`KeeperId`;
                UPDATE `DashboardMetricObservations` target INNER JOIN `_student_profile_dedup` merge_row
                    ON merge_row.`DuplicateId` = target.`StudentProfileId`
                SET target.`StudentProfileId` = merge_row.`KeeperId`;
                UPDATE `StudentBehaviours` target INNER JOIN `_student_profile_dedup` merge_row
                    ON merge_row.`DuplicateId` = target.`StudentProfileId`
                SET target.`StudentProfileId` = merge_row.`KeeperId`;
                UPDATE `StudentBusAttendanceRecords` target INNER JOIN `_student_profile_dedup` merge_row
                    ON merge_row.`DuplicateId` = target.`StudentProfileId`
                SET target.`StudentProfileId` = merge_row.`KeeperId`;
                UPDATE `StudentMentalHealthRecords` target INNER JOIN `_student_profile_dedup` merge_row
                    ON merge_row.`DuplicateId` = target.`StudentProfileId`
                SET target.`StudentProfileId` = merge_row.`KeeperId`;
                UPDATE `StudentProjects` target INNER JOIN `_student_profile_dedup` merge_row
                    ON merge_row.`DuplicateId` = target.`StudentProfileId`
                SET target.`StudentProfileId` = merge_row.`KeeperId`;
                UPDATE `WorkspacePreferences` target INNER JOIN `_student_profile_dedup` merge_row
                    ON merge_row.`DuplicateId` = target.`StudentProfileId`
                SET target.`StudentProfileId` = merge_row.`KeeperId`;
                UPDATE `SISHealthVisits` target INNER JOIN `_student_profile_dedup` merge_row
                    ON merge_row.`DuplicateId` = target.`StudentProfileId`
                SET target.`StudentProfileId` = merge_row.`KeeperId`;
                UPDATE `SISStudentParents` target INNER JOIN `_student_profile_dedup` merge_row
                    ON merge_row.`DuplicateId` = target.`StudentProfileId`
                SET target.`StudentProfileId` = merge_row.`KeeperId`;

                DELETE duplicate_profile
                FROM `StudentProfiles` duplicate_profile
                INNER JOIN `_student_profile_dedup` merge_row
                    ON merge_row.`DuplicateId` = duplicate_profile.`Id`;
                DROP TEMPORARY TABLE `_student_profile_dedup`;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_StudentProfiles_SISStudentCode",
                table: "StudentProfiles",
                column: "SISStudentCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StudentProfiles_SISUserId",
                table: "StudentProfiles",
                column: "SISUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SISStudentParents_StudentProfileId",
                table: "SISStudentParents",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_SISStudentParents_StudentProfileId_Relationship",
                table: "SISStudentParents",
                columns: new[] { "StudentProfileId", "Relationship" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SISHealthVisits_StudentProfileId",
                table: "SISHealthVisits",
                column: "StudentProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_CanvasSubmissions_CanvasUserId",
                table: "CanvasSubmissions",
                column: "CanvasUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_SISHealthVisits_StudentProfiles_StudentProfileId",
                table: "SISHealthVisits",
                column: "StudentProfileId",
                principalTable: "StudentProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SISStudentParents_StudentProfiles_StudentProfileId",
                table: "SISStudentParents",
                column: "StudentProfileId",
                principalTable: "StudentProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_SISHealthVisits_StudentProfiles_StudentProfileId",
                table: "SISHealthVisits");

            migrationBuilder.DropForeignKey(
                name: "FK_SISStudentParents_StudentProfiles_StudentProfileId",
                table: "SISStudentParents");

            migrationBuilder.DropIndex(
                name: "IX_StudentProfiles_SISStudentCode",
                table: "StudentProfiles");

            migrationBuilder.DropIndex(
                name: "IX_StudentProfiles_SISUserId",
                table: "StudentProfiles");

            migrationBuilder.DropIndex(
                name: "IX_SISStudentParents_StudentProfileId",
                table: "SISStudentParents");

            migrationBuilder.DropIndex(
                name: "IX_SISStudentParents_StudentProfileId_Relationship",
                table: "SISStudentParents");

            migrationBuilder.DropIndex(
                name: "IX_SISHealthVisits_StudentProfileId",
                table: "SISHealthVisits");

            migrationBuilder.DropIndex(
                name: "IX_CanvasSubmissions_CanvasUserId",
                table: "CanvasSubmissions");

            migrationBuilder.DropColumn(
                name: "ForeignName",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "HomeroomCode",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "SISLastImportedAtUtc",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "SISStatus",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "SISStudentCode",
                table: "StudentProfiles");

            migrationBuilder.DropColumn(
                name: "BirthYear",
                table: "SISStudentParents");

            migrationBuilder.DropColumn(
                name: "StudentProfileId",
                table: "SISStudentParents");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "SISStudentParents");

            migrationBuilder.DropColumn(
                name: "StudentProfileId",
                table: "SISHealthVisits");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_StudentProfiles_SISUserId",
                table: "StudentProfiles",
                column: "SISUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SISStudentParents_SISUserId",
                table: "SISStudentParents",
                column: "SISUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_SISHealthVisits_StudentProfiles_SISUserId",
                table: "SISHealthVisits",
                column: "SISUserId",
                principalTable: "StudentProfiles",
                principalColumn: "SISUserId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_SISStudentParents_StudentProfiles_SISUserId",
                table: "SISStudentParents",
                column: "SISUserId",
                principalTable: "StudentProfiles",
                principalColumn: "SISUserId",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
