using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OlympiaStudent360.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadershipActivityReportingDate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AcademicActivityDate",
                table: "CanvasLeadershipReportAggregates",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CanvasLeadershipReports_ActivityDate",
                table: "CanvasLeadershipReportAggregates",
                columns: new[] { "AcademicYear", "IsFinalCourseScore", "Term", "AcademicActivityDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CanvasLeadershipReports_ActivityDate",
                table: "CanvasLeadershipReportAggregates");

            migrationBuilder.DropColumn(
                name: "AcademicActivityDate",
                table: "CanvasLeadershipReportAggregates");
        }
    }
}
