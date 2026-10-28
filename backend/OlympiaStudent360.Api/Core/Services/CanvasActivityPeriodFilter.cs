using System.Globalization;
using OlympiaStudent360.Api.Core.Entities;

namespace OlympiaStudent360.Api.Core.Services;

/// <summary>Calendar month and Monday–Sunday week filters over persisted Canvas activity facts.</summary>
public static class CanvasActivityPeriodFilter
{
    public static DateTime? ParseWeekStart(string? value) =>
        DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var date)
        && date.DayOfWeek == DayOfWeek.Monday ? date : null;

    public static string? Validate(string? academicYear, int? month, string? weekStart)
    {
        if (month is < 1 or > 12) return "Month must be between 1 and 12.";
        if (month is null && weekStart is null) return null;
        if (string.IsNullOrWhiteSpace(academicYear) ||
            !DashboardPeriodNormalizer.IsAcademicYear(DashboardPeriodNormalizer.AcademicYear(academicYear)))
            return "Select a valid academic year for activity filters.";
        if (weekStart is null) return null;
        var start = ParseWeekStart(weekStart);
        if (start is null) return "Week start must be a Monday in yyyy-MM-dd format.";
        var (yearStart, yearEnd) = DashboardPeriodNormalizer.AcademicYearRange(academicYear);
        if (start >= yearEnd || start.Value.AddDays(7) <= yearStart)
            return "Select a week overlapping the academic year.";
        return null;
    }

    public static IQueryable<CanvasDashboardMetricAggregate> Apply(
        IQueryable<CanvasDashboardMetricAggregate> facts, int? month, string? weekStart,
        bool preserveCourseScores = false)
    {
        if (month is not null)
            facts = facts.Where(item => preserveCourseScores && item.CanvasAssignmentId == null ||
                item.AcademicPeriodStart != null && item.AcademicPeriodStart.Value.Month == month);
        if (ParseWeekStart(weekStart) is { } start)
        {
            var end = start.AddDays(7);
            facts = facts.Where(item => preserveCourseScores && item.CanvasAssignmentId == null ||
                item.AcademicActivityDate >= start && item.AcademicActivityDate < end);
        }
        return facts;
    }

    public static IQueryable<CanvasLeadershipReportAggregate> Apply(
        IQueryable<CanvasLeadershipReportAggregate> facts, int? month, string? weekStart)
    {
        if (month is not null)
            facts = facts.Where(item => item.AcademicPeriodStart != null && item.AcademicPeriodStart.Value.Month == month);
        if (ParseWeekStart(weekStart) is { } start)
        {
            var end = start.AddDays(7);
            facts = facts.Where(item => item.AcademicActivityDate >= start && item.AcademicActivityDate < end);
        }
        return facts;
    }
}
