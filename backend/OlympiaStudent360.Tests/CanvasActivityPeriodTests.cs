using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;
using Xunit;

namespace OlympiaStudent360.Tests;

public sealed class CanvasActivityPeriodTests
{
    [Theory]
    [InlineData(null, null, true)]
    [InlineData(10, "2026-10-26", true)]
    [InlineData(9, "2026-08-31", true)] // The first school-year week overlaps August.
    [InlineData(8, "2027-08-30", true)]
    [InlineData(10, "2026-10-27", false)]
    [InlineData(10, "2026-02-30", false)]
    [InlineData(10, "2026-10-26T00:00:00Z", false)]
    [InlineData(10, "", false)]
    [InlineData(13, null, false)]
    [InlineData(0, null, false)]
    [InlineData(null, "2026-08-24", false)]
    [InlineData(null, "2027-09-06", false)]
    public void ValidatesCalendarWeeksWithinTheSchoolYear(int? month, string? week, bool valid)
    {
        Assert.Equal(valid, CanvasActivityPeriodFilter.Validate("2026-2027", month, week) is null);
    }

    [Fact]
    public void WeeklyFiltersUseInclusiveMondayExclusiveNextMondayAndIntersectMonth()
    {
        var facts = new[]
        {
            Fact(1, "2026-10-25"), Fact(2, "2026-10-26"), Fact(3, "2026-10-31"),
            Fact(4, "2026-11-01"), Fact(5, "2026-11-02"), Fact(6, null)
        }.AsQueryable();
        Assert.Equal(new[] { 2, 3, 4 }, CanvasActivityPeriodFilter.Apply(facts, null, "2026-10-26").Select(item => item.Id));
        Assert.Equal(new[] { 2, 3 }, CanvasActivityPeriodFilter.Apply(facts, 10, "2026-10-26").Select(item => item.Id));
        Assert.Equal(new[] { 4 }, CanvasActivityPeriodFilter.Apply(facts, 11, "2026-10-26").Select(item => item.Id));
        Assert.Equal(6, CanvasActivityPeriodFilter.Apply(facts, null, null).Count());
    }

    [Fact]
    public void DiligenceActivityFiltersPreserveSemesterCourseScores()
    {
        var courseScore = Fact(3, null);
        courseScore.CanvasAssignmentId = null;
        var facts = new[] { Fact(1, "2026-10-26"), Fact(2, "2026-10-19"), courseScore }.AsQueryable();
        Assert.Equal(new[] { 1, 3 }, CanvasActivityPeriodFilter.Apply(facts, 10, "2026-10-26", true).Select(item => item.Id));
    }

    [Fact]
    public void ReportingDayRetainsExistingAcademicMonthFallbackWithoutInventingDates()
    {
        var eventDate = new DateTime(2027, 9, 2, 15, 30, 0);
        var dueDate = new DateTime(2027, 8, 31, 20, 0, 0);
        Assert.Equal(dueDate.Date, CanvasDashboardAggregateService.AcademicActivityDate(eventDate, dueDate, "2026-2027"));
        Assert.Equal(dueDate.Date, CanvasDashboardAggregateService.AcademicActivityDate(dueDate, eventDate, "2026-2027"));
        Assert.Null(CanvasDashboardAggregateService.AcademicActivityDate(null, null, "2026-2027"));
        Assert.Null(CanvasDashboardAggregateService.AcademicActivityDate(eventDate, null, "2026-2027"));
    }

    [Fact]
    public void ActivityFilterTranslatesToMySqlBeforeAggregation()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseResilientMySql("Server=localhost;Database=unused;User=test;Password=test").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var sql = CanvasActivityPeriodFilter.Apply(db.CanvasDashboardMetricAggregates, 10, "2026-10-26", true)
            .GroupBy(item => item.StudentProfileId).Select(group => group.Sum(item => item.ValueSum)).ToQueryString();
        Assert.Contains("AcademicActivityDate", sql);
        Assert.Matches(@"(?i)(MONTH\(|EXTRACT\(MONTH\s+FROM)", sql);
        Assert.Contains("IS NULL", sql);
        Assert.Contains("GROUP BY", sql);
    }

    private static CanvasDashboardMetricAggregate Fact(int id, string? date)
    {
        var day = date is null ? (DateTime?)null : DateTime.ParseExact(date, "yyyy-MM-dd", null);
        return new CanvasDashboardMetricAggregate
        {
            Id = id, CanvasAssignmentId = id, AcademicActivityDate = day,
            AcademicPeriodStart = day is null ? null : new DateTime(day.Value.Year, day.Value.Month, 1)
        };
    }
}
