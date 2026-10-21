using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class ClassifyGhkGroupsAsHs2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Update only the category on the published snapshot. Metric facts,
            // scores and the incremental aggregation cursor remain unchanged.
            migrationBuilder.Sql("""
                UPDATE `CanvasLeadershipReportAggregates`
                SET `AssessmentCategory` = 'HS2'
                WHERE `IsFinalCourseScore` = 0
                  AND UPPER(TRIM(`AssignmentGroupName`)) IN ('GHK1', 'GHK2')
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE `CanvasLeadershipReportAggregates`
                SET `AssessmentCategory` = NULL
                WHERE `IsFinalCourseScore` = 0
                  AND UPPER(TRIM(`AssignmentGroupName`)) IN ('GHK1', 'GHK2')
                """);
        }
    }
}
