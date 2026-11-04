using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSchoolOnlineSourceOwnership : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsLocallyHidden",
                table: "StudentHealthVisits");

            migrationBuilder.DropColumn(
                name: "SisSourceValuesJson",
                table: "StudentHealthVisits");

            migrationBuilder.AddColumn<bool>(
                name: "ProjectsCurrentRoster",
                table: "SisSyncStates",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("""
                UPDATE `SisSyncStates` state
                SET state.`ProjectsCurrentRoster` = TRUE
                WHERE state.`Endpoint` = 'students' AND state.`LastSuccessfulWatermark` IS NOT NULL
                  AND EXISTS (SELECT 1 FROM `StudentProfiles` student
                    WHERE student.`SchoolOnlineUserId` IS NOT NULL
                      AND student.`SchoolOnlineSchoolYearId` = JSON_UNQUOTE(JSON_EXTRACT(state.`FiltersJson`, '$.school_year_id'))
                      AND (JSON_EXTRACT(state.`FiltersJson`, '$.school_id') IS NULL
                        OR student.`SchoolOnlineSchoolId` = JSON_UNQUOTE(JSON_EXTRACT(state.`FiltersJson`, '$.school_id'))));
                UPDATE `StudentProfiles`
                SET `EducationLevel` = CASE
                  WHEN `GradeLevel` IN ('1', '2', '3', '4') THEN 'Primary'
                  WHEN `GradeLevel` IN ('5', '6', '7', '8', '9') THEN 'LowerSecondary'
                  WHEN `GradeLevel` IN ('10', '11', '12') THEN 'UpperSecondary'
                  ELSE NULL END
                WHERE `SchoolOnlineUserId` IS NOT NULL;
                DELETE FROM `ImportedReportAggregates` WHERE `Source` = 'student-health-visits';
                INSERT INTO `ImportedReportChanges` (`CreatedAt`) VALUES (UTC_TIMESTAMP(6));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProjectsCurrentRoster",
                table: "SisSyncStates");

            migrationBuilder.AddColumn<bool>(
                name: "IsLocallyHidden",
                table: "StudentHealthVisits",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SisSourceValuesJson",
                table: "StudentHealthVisits",
                type: "json",
                nullable: true);
        }
    }
}
