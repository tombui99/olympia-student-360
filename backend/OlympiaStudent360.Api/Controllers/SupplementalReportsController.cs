using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/reports/supplemental")]
public sealed class SupplementalReportsController(AppDbContext db, UserAccessService access) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<IActionResult> Summary(
        [FromQuery] string moduleId, [FromQuery] string field,
        [FromQuery] string? academicYear = null, [FromQuery(Name = "term")] string? reportingTerm = null,
        [FromQuery] int? month = null, [FromQuery] string? weekStart = null)
    {
        if (!access.Current.CanAccessModule(moduleId)) return Forbid();
        if (!ValidField(moduleId, field)) return BadRequest();
        var dateError = CanvasActivityPeriodFilter.Validate(academicYear, month, weekStart);
        if (dateError is not null) return BadRequest(dateError);
        if (!await db.ImportedReportBuilds.AsNoTracking().AnyAsync())
            return StatusCode(503, new { Message = "Report aggregates are being prepared." });

        var query = access.ApplyStudentScope(db.ImportedReportAggregates.AsNoTracking()
            .Where(item => item.Source == moduleId), item => item.Student);
        if (!string.IsNullOrWhiteSpace(academicYear))
        {
            var year = DashboardPeriodNormalizer.AcademicYear(academicYear);
            var term = string.IsNullOrWhiteSpace(reportingTerm)
                ? "all" : DashboardPeriodNormalizer.Term(reportingTerm);
            if (term is not ("hk1" or "hk2")) term = "all";
            var yearValues = await db.AcademicPeriods.AsNoTracking().Select(item => item.AcademicYear)
                .Concat(db.ImportedReportAggregates.Where(item => item.Source == moduleId && item.AcademicYear != null)
                    .Select(item => item.AcademicYear!)).Distinct().ToListAsync();
            var yearAliases = yearValues.Where(value => DashboardPeriodNormalizer.AcademicYear(value)
                .Equals(year, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!yearAliases.Contains(year, StringComparer.OrdinalIgnoreCase)) yearAliases.Add(year);
            var (yearStart, yearEnd) = DashboardPeriodNormalizer.AcademicYearRange(year);
            if (moduleId == "student-mental-health")
                query = query.WhereMatchesAny(item => item.AcademicYear, yearAliases);
            else
                query = query.WhereMatchesAny(item => item.AcademicYear, yearAliases,
                    item => item.AcademicYear == null && item.EventDay >= yearStart && item.EventDay < yearEnd);
            if (term != "all")
            {
                var (start, end) = DashboardPeriodNormalizer.PeriodRange(year, term);
                if (moduleId == "student-mental-health")
                    query = query.Where(item => item.EventDay >= start && item.EventDay < end);
                else
                {
                    var termValues = await db.AcademicPeriods.AsNoTracking().Select(item => item.Term)
                        .Concat(db.ImportedReportAggregates.Where(item => item.Source == moduleId && item.Term != null)
                            .Select(item => item.Term!)).Distinct().ToListAsync();
                    var aliases = termValues.Where(value => DashboardPeriodNormalizer.Term(value)
                        .Equals(term, StringComparison.OrdinalIgnoreCase)).ToList();
                    if (!aliases.Contains(term, StringComparer.OrdinalIgnoreCase)) aliases.Add(term);
                    query = query.WhereMatchesAny(item => item.Term, aliases,
                        item => item.Term == null && item.EventDay >= start && item.EventDay < end);
                }
            }
        }

        query = ReportDateFilter.Apply(query, item => item.EventDay, ReportDateFilter.Range(academicYear, month, weekStart));
        var totals = await query.GroupBy(_ => 1).Select(group => new
        {
            Records = group.Sum(item => item.RecordCount),
            Students = group.Select(item => item.StudentKey).Distinct().Count(),
            Attention = group.Sum(item => item.AttentionCount)
        }).SingleOrDefaultAsync();
        var total = totals?.Records ?? 0;
        var labels = FieldExpression(field);
        var grouped = await query.GroupBy(labels).Select(group => new
        {
            Label = group.Key,
            Records = group.Sum(item => item.RecordCount),
            Students = group.Select(item => item.StudentKey).Distinct().Count()
        }).OrderByDescending(item => item.Records).ThenBy(item => item.Label)
            .Take(10).ToListAsync();
        var distribution = grouped.Select(item => new
        {
            Label = item.Label ?? "—",
            item.Records,
            item.Students,
            Share = total == 0 ? 0 : Math.Round((decimal)item.Records * 100 / total, 1)
        }).ToList();
        return Ok(new
        {
            TotalCount = total,
            DistinctStudents = totals?.Students ?? 0,
            AttentionCount = totals?.Attention ?? 0,
            Distribution = distribution
        });
    }

    private static bool ValidField(string module, string field) => (module, field) switch
    {
        ("student-behaviours", "behaviour_type" or "category") => true,
        ("student-projects", "status" or "subject") => true,
        ("student-mental-health", "risk_level" or "concern_type") => true,
        ("student-bus-attendance", "violation_type" or "trip_direction") => true,
        _ => false
    };

    private static Expression<Func<ImportedReportAggregate, string?>> FieldExpression(string field) => field switch
    {
        "behaviour_type" or "concern_type" or "violation_type" => row => row.Type,
        "category" => row => row.Category,
        "status" or "risk_level" or "trip_direction" => row => row.Status,
        "subject" => row => row.Subject,
        _ => row => null
    };
}
