using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using OlympiaStudent360.Api.Data;

#nullable disable

namespace OlympiaStudent360.Api.Migrations;

/// <summary>
/// Corrects the reporting fallback calendar. Canvas grading-period dates are stored separately
/// and remain authoritative for Canvas grades; these rows scope dated non-Canvas records.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260910130000_CorrectVietnamAcademicCalendar")]
public partial class CorrectVietnamAcademicCalendar : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE `AcademicPeriods`
            SET
                `StartDate` = STR_TO_DATE(
                    CONCAT(SUBSTRING_INDEX(`AcademicYear`, '-', 1), '-09-01'), '%Y-%m-%d'),
                `EndDate` = STR_TO_DATE(
                    CONCAT(CAST(SUBSTRING_INDEX(`AcademicYear`, '-', 1) AS UNSIGNED) + 1, '-01-19'), '%Y-%m-%d'),
                `UpdatedAt` = UTC_TIMESTAMP(6)
            WHERE LOWER(`Term`) = 'hk1'
              AND `AcademicYear` REGEXP '^[0-9]{4}-[0-9]{4}$';

            UPDATE `AcademicPeriods`
            SET
                `StartDate` = STR_TO_DATE(
                    CONCAT(CAST(SUBSTRING_INDEX(`AcademicYear`, '-', 1) AS UNSIGNED) + 1, '-01-19'), '%Y-%m-%d'),
                `EndDate` = STR_TO_DATE(
                    CONCAT(CAST(SUBSTRING_INDEX(`AcademicYear`, '-', 1) AS UNSIGNED) + 1, '-06-01'), '%Y-%m-%d'),
                `UpdatedAt` = UTC_TIMESTAMP(6)
            WHERE LOWER(`Term`) = 'hk2'
              AND `AcademicYear` REGEXP '^[0-9]{4}-[0-9]{4}$';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            UPDATE `AcademicPeriods`
            SET
                `StartDate` = STR_TO_DATE(
                    CONCAT(SUBSTRING_INDEX(`AcademicYear`, '-', 1), '-08-01'), '%Y-%m-%d'),
                `EndDate` = STR_TO_DATE(
                    CONCAT(CAST(SUBSTRING_INDEX(`AcademicYear`, '-', 1) AS UNSIGNED) + 1, '-01-01'), '%Y-%m-%d'),
                `UpdatedAt` = UTC_TIMESTAMP(6)
            WHERE LOWER(`Term`) = 'hk1'
              AND `AcademicYear` REGEXP '^[0-9]{4}-[0-9]{4}$';

            UPDATE `AcademicPeriods`
            SET
                `StartDate` = STR_TO_DATE(
                    CONCAT(CAST(SUBSTRING_INDEX(`AcademicYear`, '-', 1) AS UNSIGNED) + 1, '-01-01'), '%Y-%m-%d'),
                `EndDate` = STR_TO_DATE(
                    CONCAT(CAST(SUBSTRING_INDEX(`AcademicYear`, '-', 1) AS UNSIGNED) + 1, '-07-01'), '%Y-%m-%d'),
                `UpdatedAt` = UTC_TIMESTAMP(6)
            WHERE LOWER(`Term`) = 'hk2'
              AND `AcademicYear` REGEXP '^[0-9]{4}-[0-9]{4}$';
            """);
    }
}
