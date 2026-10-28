using System.Linq.Expressions;

namespace OlympiaStudent360.Api.Core.Services;

public sealed record ReportDateRange(DateTime Start, DateTime End);

/// <summary>Filters dated report facts before aggregation, counting, and pagination.</summary>
public static class ReportDateFilter
{
    public static ReportDateRange? Range(string? academicYear, int? month, string? weekStart)
    {
        if (month is null && weekStart is null) return null;
        var (start, end) = DashboardPeriodNormalizer.AcademicYearRange(academicYear!);
        if (month is not null)
        {
            var year = start.Year + (month < DashboardPeriodNormalizer.SchoolYearStartMonth ? 1 : 0);
            start = new DateTime(year, month.Value, 1, 0, 0, 0, DateTimeKind.Utc);
            end = start.AddMonths(1);
        }
        if (CanvasActivityPeriodFilter.ParseWeekStart(weekStart) is { } week)
        {
            start = start > week ? start : week;
            end = end < week.AddDays(7) ? end : week.AddDays(7);
        }
        return new(start, end);
    }

    public static IQueryable<T> Apply<T>(IQueryable<T> query,
        Expression<Func<T, DateTime>> date, ReportDateRange? range)
    {
        if (range is null) return query;
        var bounds = Expression.AndAlso(
            Expression.GreaterThanOrEqual(date.Body, Expression.Constant(range.Start)),
            Expression.LessThan(date.Body, Expression.Constant(range.End)));
        return query.Where(Expression.Lambda<Func<T, bool>>(bounds, date.Parameters));
    }
}
