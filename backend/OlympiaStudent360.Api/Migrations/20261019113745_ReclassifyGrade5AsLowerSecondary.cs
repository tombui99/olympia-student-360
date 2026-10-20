using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class ReclassifyGrade5AsLowerSecondary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Before this release, Primary represented Grade 5. Preserve those teachers' access
            // under the new Grade 5-9 Lower Secondary definition without creating duplicate rows.
            migrationBuilder.Sql(
                """
                DELETE primary_access
                FROM `TeacherSchoolLevelAccesses` AS primary_access
                INNER JOIN `TeacherSchoolLevelAccesses` AS lower_access
                    ON lower_access.`UserId` = primary_access.`UserId`
                    AND lower_access.`SchoolLevel` = 'LowerSecondary'
                WHERE primary_access.`SchoolLevel` = 'Primary';

                UPDATE `TeacherSchoolLevelAccesses`
                SET `SchoolLevel` = 'LowerSecondary',
                    `UpdatedAt` = UTC_TIMESTAMP()
                WHERE `SchoolLevel` = 'Primary';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This semantic data move is intentionally not reversed. Once new Primary grants
            // (Grades 1-4) exist, they cannot be distinguished safely from legacy Grade 5 grants.
        }
    }
}
