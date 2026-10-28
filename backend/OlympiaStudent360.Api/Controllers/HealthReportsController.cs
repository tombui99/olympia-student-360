using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/reports/health-visits")]
[ApiController]
[Authorize]
public sealed class HealthReportsController(AppDbContext context, UserAccessService userAccess) : ControllerBase
{
    private sealed record HealthVisitSource(
        int Id,
        int? StudentProfileId,
        string? SISUserId,
        string StudentName,
        string? ClassName,
        DateTime ExamTime,
        string? Classification,
        string? Diagnosis,
        string? Symptoms,
        string? Treatment,
        string? Medication,
        string? Notes,
        bool NotificationSent,
        string? ExaminedBy,
        string? CustomFieldsJson);
    private sealed record HealthAggregateRow(int Year, int Month, string? Label,
        string? StudentKey, int? StudentProfileId, string? SISUserId, string? StudentName,
        int VisitCount, int StudentCount, DateTime LatestVisit, string? ClassName = null);

    [HttpGet("summary")]
    public async Task<ActionResult<HealthReportPageDto>> Summary(
        [FromQuery] string report = "diagnoses",
        [FromQuery] string groupBy = "month",
        [FromQuery] string? academicYear = null,
        [FromQuery(Name = "term")] string? reportingTerm = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool export = false,
        [FromQuery] int? month = null, [FromQuery] string? weekStart = null)
    {
        if (!KnownReport(report) || !KnownGrouping(groupBy)) return BadRequest();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, export ? 1000 : 100);
        var year = string.IsNullOrWhiteSpace(academicYear)
            ? DashboardPeriodNormalizer.AcademicYear(DateTime.UtcNow)
            : academicYear;
        var (start, end) = DashboardPeriodNormalizer.PeriodRange(year, reportingTerm);
        var dateError = CanvasActivityPeriodFilter.Validate(year, month, weekStart);
        if (dateError is not null) return BadRequest(dateError);
        if (ReportDateFilter.Range(year, month, weekStart) is { } activity)
        {
            start = start > activity.Start ? start : activity.Start;
            end = end < activity.End ? end : activity.End;
        }
        if (!await context.ImportedReportBuilds.AsNoTracking().AnyAsync())
            return StatusCode(503, new { Message = "Report aggregates are being prepared." });
        var searchTerm = Clean(search);
        if (searchTerm?.Length > 100) searchTerm = searchTerm[..100];
        if (report == "student-visits")
            return Ok(await VisitSummaryPage(start, end, groupBy, searchTerm, page, pageSize));
        var aggregateQuery = userAccess.ApplyStudentOrHomeroomScope(
            context.ImportedReportAggregates.AsNoTracking()
                .Where(item => item.Source == "student-health-visits" && item.EventDay >= start && item.EventDay < end),
            item => item.Student, item => item.ClassName);
        if (searchTerm != null)
            aggregateQuery = aggregateQuery.Where(item => item.StudentName.Contains(searchTerm) ||
                item.SISUserId != null && item.SISUserId.Contains(searchTerm) ||
                item.ClassName != null && item.ClassName.Contains(searchTerm) ||
                item.Diagnosis != null && item.Diagnosis.Contains(searchTerm) ||
                item.Symptoms != null && item.Symptoms.Contains(searchTerm) ||
                item.Category != null && item.Category.Contains(searchTerm));

        // Month and year are the normal views. Group and page them in SQL so only one page
        // crosses the database boundary. Week remains a small in-memory fold over daily aggregates.
        if (groupBy != "week")
        {
            var totalVisits = await aggregateQuery.SumAsync(item => (int?)item.RecordCount) ?? 0;
            var monthSelector = groupBy == "year" ? 1 : 0;
            if (report == "diagnoses")
            {
                var grouped = aggregateQuery.GroupBy(item => new
                {
                    Year = item.EventDay.Year,
                    Month = monthSelector == 1 ? 1 : item.EventDay.Month,
                    Diagnosis = item.Diagnosis ?? "—"
                });
                var diagnosisTotal = await grouped.CountAsync();
                var diagnosisPages = diagnosisTotal == 0 ? 0 : (int)Math.Ceiling((double)diagnosisTotal / pageSize);
                page = Math.Min(page, Math.Max(1, diagnosisPages));
                var values = await grouped.OrderByDescending(group => group.Key.Year)
                    .ThenByDescending(group => group.Key.Month)
                    .ThenByDescending(group => group.Sum(item => item.RecordCount))
                    .ThenBy(group => group.Key.Diagnosis)
                    .Skip((page - 1) * pageSize).Take(pageSize)
                    .Select(group => new HealthAggregateRow(group.Key.Year, group.Key.Month,
                        group.Key.Diagnosis, null, null, null, null,
                        group.Sum(item => item.RecordCount),
                        group.Select(item => item.StudentKey).Distinct().Count(),
                        group.Max(item => item.LatestEvent)))
                    .ToListAsync();
                return Ok(HealthAggregatePage(values, groupBy, page, pageSize, diagnosisTotal,
                    diagnosisPages, totalVisits, "diagnosis"));
            }

            var studentGroups = aggregateQuery.GroupBy(item => new
            {
                Year = item.EventDay.Year,
                Month = monthSelector == 1 ? 1 : item.EventDay.Month,
                item.StudentKey, item.StudentProfileId, item.SISUserId,
                item.StudentName, item.ClassName
            });
            var studentTotal = await studentGroups.CountAsync();
            var studentPages = studentTotal == 0 ? 0 : (int)Math.Ceiling((double)studentTotal / pageSize);
            page = Math.Min(page, Math.Max(1, studentPages));
            var studentValues = await studentGroups.OrderByDescending(group => group.Key.Year)
                .ThenByDescending(group => group.Key.Month)
                .ThenByDescending(group => group.Sum(item => item.RecordCount))
                .ThenBy(group => group.Key.StudentName)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(group => new HealthAggregateRow(group.Key.Year, group.Key.Month,
                    null, group.Key.StudentKey, group.Key.StudentProfileId, group.Key.SISUserId,
                    group.Key.StudentName, group.Sum(item => item.RecordCount), 1,
                    group.Max(item => item.LatestEvent), group.Key.ClassName))
                .ToListAsync();
            return Ok(HealthAggregatePage(studentValues, groupBy, page, pageSize, studentTotal,
                studentPages, totalVisits, "student"));
        }

        var source = await aggregateQuery.Select(item => new ImportedReportAggregate
        {
            StudentProfileId = item.StudentProfileId,
            StudentKey = item.StudentKey,
            SISUserId = item.SISUserId,
            StudentName = item.StudentName,
            ClassName = item.ClassName,
            EventDay = item.EventDay,
            LatestEvent = item.LatestEvent,
            Diagnosis = item.Diagnosis,
            Symptoms = item.Symptoms,
            Category = item.Category,
            RecordCount = item.RecordCount
        }).ToListAsync();
        var rows = report switch
        {
            "diagnoses" => AggregatedDiagnosisRows(source, groupBy),
            "frequent-students" => AggregatedFrequentStudentRows(source, groupBy),
            _ => []
        };
        var total = rows.Count;
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling((double)total / pageSize);
        page = Math.Min(page, Math.Max(1, totalPages));
        return Ok(new HealthReportPageDto
        {
            Items = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            TotalPages = totalPages,
            TotalVisits = source.Sum(item => item.RecordCount)
        });
    }

    private static List<HealthReportRowDto> AggregatedDiagnosisRows(
        List<ImportedReportAggregate> source, string groupBy) =>
        source.GroupBy(item => new { Period = PeriodBounds(item.EventDay, groupBy).Start,
                Diagnosis = (item.Diagnosis ?? "—").ToUpperInvariant() })
            .Select(group =>
            {
                var latest = group.OrderByDescending(item => item.LatestEvent).First();
                var bounds = PeriodBounds(latest.EventDay, groupBy);
                return new HealthReportRowDto
                {
                    Key = $"diagnosis:{bounds.Start:yyyyMMdd}:{group.Key.Diagnosis}",
                    PeriodStart = bounds.Start, PeriodEnd = bounds.End,
                    StudentName = latest.Diagnosis ?? "—", Diagnosis = latest.Diagnosis ?? "—",
                    VisitCount = group.Sum(item => item.RecordCount),
                    StudentCount = group.Select(item => item.StudentKey).Distinct().Count(),
                    LatestVisit = group.Max(item => item.LatestEvent)
                };
            })
            .OrderByDescending(item => item.PeriodStart).ThenByDescending(item => item.VisitCount)
            .ThenBy(item => item.Diagnosis).ToList();

    private static List<HealthReportRowDto> AggregatedFrequentStudentRows(
        List<ImportedReportAggregate> source, string groupBy) =>
        source.GroupBy(item => new { Period = PeriodBounds(item.EventDay, groupBy).Start, item.StudentKey })
            .Select(group =>
            {
                var latest = group.OrderByDescending(item => item.LatestEvent).First();
                var bounds = PeriodBounds(latest.EventDay, groupBy);
                return new HealthReportRowDto
                {
                    Key = $"student:{bounds.Start:yyyyMMdd}:{group.Key.StudentKey}",
                    PeriodStart = bounds.Start, PeriodEnd = bounds.End,
                    StudentProfileId = latest.StudentProfileId, SISUserId = latest.SISUserId,
                    StudentName = latest.StudentName, ClassName = latest.ClassName,
                    VisitCount = group.Sum(item => item.RecordCount), StudentCount = 1,
                    LatestVisit = group.Max(item => item.LatestEvent)
                };
            })
            .OrderByDescending(item => item.PeriodStart).ThenByDescending(item => item.VisitCount)
            .ThenBy(item => item.StudentName).ToList();

    private static HealthReportPageDto HealthAggregatePage(List<HealthAggregateRow> values,
        string groupBy, int page, int pageSize, int total, int totalPages, int totalVisits,
        string kind) => new()
        {
            Items = values.Select(item =>
            {
                var start = new DateTime(item.Year, item.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var end = groupBy == "year" ? start.AddYears(1) : start.AddMonths(1);
                return new HealthReportRowDto
                {
                    Key = kind == "diagnosis"
                        ? $"diagnosis:{start:yyyyMMdd}:{item.Label}"
                        : $"student:{start:yyyyMMdd}:{item.StudentKey}",
                    PeriodStart = start,
                    PeriodEnd = end,
                    StudentProfileId = item.StudentProfileId,
                    SISUserId = item.SISUserId,
                    StudentName = kind == "diagnosis" ? item.Label ?? "—" : item.StudentName ?? "—",
                    ClassName = item.ClassName,
                    Diagnosis = kind == "diagnosis" ? item.Label ?? "—" : null,
                    VisitCount = item.VisitCount,
                    StudentCount = item.StudentCount,
                    LatestVisit = item.LatestVisit
                };
            }).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            TotalPages = totalPages,
            TotalVisits = totalVisits
        };

    private async Task<HealthReportPageDto> VisitSummaryPage(DateTime start, DateTime end,
        string groupBy, string? search, int page, int pageSize)
    {
        var query = userAccess.ApplyStudentOrHomeroomScope(
            context.StudentHealthVisits.AsNoTracking()
                .Where(item => item.ExamTime >= start && item.ExamTime < end),
            item => item.Student, item => item.ClassName);
        if (search != null)
            query = query.Where(item => item.FullName.Contains(search) ||
                item.SISUserId != null && item.SISUserId.Contains(search) ||
                item.ClassName != null && item.ClassName.Contains(search) ||
                item.Diagnosis != null && item.Diagnosis.Contains(search) ||
                item.Classification != null && item.Classification.Contains(search) ||
                item.Symptoms != null && item.Symptoms.Contains(search) ||
                item.Notes != null && item.Notes.Contains(search));
        var total = await query.CountAsync();
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling((double)total / pageSize);
        page = Math.Min(page, Math.Max(1, totalPages));
        var visits = await query.OrderByDescending(item => item.ExamTime).ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new HealthVisitSource(item.Id, item.StudentProfileId, item.SISUserId,
                item.FullName, item.ClassName, item.ExamTime, item.Classification, item.Diagnosis,
                item.Symptoms, item.Treatment, item.Medication, item.Notes,
                item.NotificationSent, item.ExaminedBy, item.CustomFieldsJson)).ToListAsync();
        return new HealthReportPageDto
        {
            Items = VisitRows(visits, groupBy), Page = page, PageSize = pageSize,
            TotalCount = total, TotalPages = totalPages, TotalVisits = total
        };
    }

    [HttpGet("details")]
    public async Task<ActionResult<HealthVisitReportDetailsPageDto>> Details(
        [FromQuery] DateTime periodStart,
        [FromQuery] DateTime periodEnd,
        [FromQuery] int? visitId = null,
        [FromQuery] string? diagnosis = null,
        [FromQuery] int? studentProfileId = null,
        [FromQuery] string? sisUserId = null,
        [FromQuery] string? studentName = null,
        [FromQuery] string? className = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? academicYear = null, [FromQuery(Name = "term")] string? reportingTerm = null,
        [FromQuery] int? month = null, [FromQuery] string? weekStart = null)
    {
        var dateError = CanvasActivityPeriodFilter.Validate(academicYear, month, weekStart);
        if (dateError is not null) return BadRequest(dateError);
        if (periodEnd <= periodStart || periodEnd - periodStart > TimeSpan.FromDays(370)) return BadRequest();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var start = ToUtc(periodStart);
        var end = ToUtc(periodEnd);
        if (!string.IsNullOrWhiteSpace(academicYear))
        {
            var (scopeStart, scopeEnd) = DashboardPeriodNormalizer.PeriodRange(academicYear, reportingTerm);
            start = start > scopeStart ? start : scopeStart;
            end = end < scopeEnd ? end : scopeEnd;
        }
        if (ReportDateFilter.Range(academicYear, month, weekStart) is { } activity)
        {
            start = start > activity.Start ? start : activity.Start;
            end = end < activity.End ? end : activity.End;
        }
        var query = userAccess.ApplyStudentOrHomeroomScope(
            context.StudentHealthVisits.AsNoTracking()
                .Where(item => item.ExamTime >= start && item.ExamTime < end),
            item => item.Student, item => item.ClassName);
        if (visitId.HasValue) query = query.Where(item => item.Id == visitId.Value);
        if (!string.IsNullOrWhiteSpace(diagnosis))
        {
            var label = diagnosis.Trim();
            query = query.Where(item => (item.Diagnosis != null && item.Diagnosis.Trim() != ""
                    ? item.Diagnosis.Trim()
                    : item.Classification != null && item.Classification.Trim() != ""
                        ? item.Classification.Trim()
                        : item.Symptoms != null && item.Symptoms.Trim() != ""
                            ? item.Symptoms.Trim() : "—") == label);
        }
        if (studentProfileId.HasValue)
            query = query.Where(item => item.StudentProfileId == studentProfileId.Value);
        else if (!string.IsNullOrWhiteSpace(sisUserId))
            query = query.Where(item => item.SISUserId == sisUserId.Trim());
        else if (!string.IsNullOrWhiteSpace(studentName))
            query = query.Where(item => item.FullName == studentName.Trim()
                && (string.IsNullOrWhiteSpace(className) || item.ClassName == className.Trim()));

        var total = await query.CountAsync();
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling((double)total / pageSize);
        page = Math.Min(page, Math.Max(1, totalPages));
        var source = await query.OrderByDescending(item => item.ExamTime).ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new HealthVisitSource(item.Id, item.StudentProfileId, item.SISUserId,
                item.FullName, item.ClassName, item.ExamTime, item.Classification, item.Diagnosis,
                item.Symptoms, item.Treatment, item.Medication, item.Notes,
                item.NotificationSent, item.ExaminedBy, item.CustomFieldsJson)).ToListAsync();
        return Ok(new HealthVisitReportDetailsPageDto
        {
            Items = source.Select(Detail).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            TotalPages = totalPages
        });
    }

    private static List<HealthReportRowDto> VisitRows(List<HealthVisitSource> source, string groupBy) =>
        source.OrderByDescending(item => item.ExamTime).ThenByDescending(item => item.Id)
            .Select(item =>
            {
                var bounds = PeriodBounds(item.ExamTime, groupBy);
                return new HealthReportRowDto
                {
                    Key = $"visit:{item.Id}",
                    PeriodStart = bounds.Start,
                    PeriodEnd = bounds.End,
                    VisitId = item.Id,
                    StudentProfileId = item.StudentProfileId,
                    SISUserId = item.SISUserId,
                    StudentName = item.StudentName,
                    ClassName = item.ClassName,
                    Diagnosis = DiagnosisLabel(item),
                    Classification = Clean(item.Classification),
                    VisitCount = 1,
                    StudentCount = 1,
                    LatestVisit = item.ExamTime
                };
            }).ToList();

    private static HealthVisitReportDetailDto Detail(HealthVisitSource item) => new()
    {
        Id = item.Id,
        StudentProfileId = item.StudentProfileId,
        SISUserId = item.SISUserId,
        StudentName = item.StudentName,
        ClassName = item.ClassName,
        ExamTime = item.ExamTime,
        Classification = item.Classification,
        Diagnosis = item.Diagnosis,
        Symptoms = item.Symptoms,
        Treatment = item.Treatment,
        Medication = item.Medication,
        Notes = item.Notes,
        NotificationSent = item.NotificationSent,
        ExaminedBy = item.ExaminedBy,
        CustomFields = ImportCustomFieldService.Parse(item.CustomFieldsJson)
    };

    private static (DateTime Start, DateTime End) PeriodBounds(DateTime value, string groupBy)
    {
        var date = ToUtc(value).Date;
        if (groupBy == "week")
        {
            var daysSinceMonday = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
            var start = date.AddDays(-daysSinceMonday);
            return (start, start.AddDays(7));
        }
        if (groupBy == "year")
        {
            var start = new DateTime(date.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (start, start.AddYears(1));
        }
        var month = new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return (month, month.AddMonths(1));
    }

    private static string DiagnosisLabel(HealthVisitSource item) =>
        Clean(item.Diagnosis) ?? Clean(item.Classification) ?? Clean(item.Symptoms) ?? "—";

    private static bool Contains(string? value, string term) =>
        value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;

    private static bool KnownReport(string value) => value is "diagnoses" or "frequent-students" or "student-visits";
    private static bool KnownGrouping(string value) => value is "week" or "month" or "year";
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DateTime ToUtc(DateTime value) => value.Kind == DateTimeKind.Utc
        ? value
        : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
