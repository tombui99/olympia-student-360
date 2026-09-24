using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
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

    [HttpGet("summary")]
    public async Task<ActionResult<HealthReportPageDto>> Summary(
        [FromQuery] string report = "diagnoses",
        [FromQuery] string groupBy = "month",
        [FromQuery] string? academicYear = null,
        [FromQuery(Name = "term")] string? reportingTerm = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (!KnownReport(report) || !KnownGrouping(groupBy)) return BadRequest();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var year = string.IsNullOrWhiteSpace(academicYear)
            ? DashboardPeriodNormalizer.AcademicYear(DateTime.UtcNow)
            : academicYear;
        var (start, end) = DashboardPeriodNormalizer.PeriodRange(year, reportingTerm);
        var source = await HealthVisits(start, end);
        var searchTerm = Clean(search);
        if (searchTerm?.Length > 100) searchTerm = searchTerm[..100];
        if (searchTerm != null) source = source.Where(item => Matches(item, searchTerm)).ToList();

        var rows = report switch
        {
            "diagnoses" => DiagnosisRows(source, groupBy),
            "frequent-students" => FrequentStudentRows(source, groupBy),
            "student-visits" => VisitRows(source, groupBy),
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
            TotalVisits = source.Count
        });
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
        [FromQuery] int pageSize = 20)
    {
        if (periodEnd <= periodStart || periodEnd - periodStart > TimeSpan.FromDays(370)) return BadRequest();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var source = await HealthVisits(ToUtc(periodStart), ToUtc(periodEnd));
        if (visitId.HasValue) source = source.Where(item => item.Id == visitId.Value).ToList();
        if (!string.IsNullOrWhiteSpace(diagnosis))
            source = source.Where(item => string.Equals(DiagnosisLabel(item), diagnosis.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        if (studentProfileId.HasValue)
            source = source.Where(item => item.StudentProfileId == studentProfileId.Value).ToList();
        else if (!string.IsNullOrWhiteSpace(sisUserId))
            source = source.Where(item => string.Equals(item.SISUserId, sisUserId.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        else if (!string.IsNullOrWhiteSpace(studentName))
            source = source.Where(item => string.Equals(item.StudentName, studentName.Trim(), StringComparison.OrdinalIgnoreCase)
                && (string.IsNullOrWhiteSpace(className) || string.Equals(item.ClassName, className.Trim(), StringComparison.OrdinalIgnoreCase))).ToList();

        source = source.OrderByDescending(item => item.ExamTime).ThenByDescending(item => item.Id).ToList();
        var total = source.Count;
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling((double)total / pageSize);
        page = Math.Min(page, Math.Max(1, totalPages));
        return Ok(new HealthVisitReportDetailsPageDto
        {
            Items = source.Skip((page - 1) * pageSize).Take(pageSize).Select(Detail).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            TotalPages = totalPages
        });
    }

    private async Task<List<HealthVisitSource>> HealthVisits(DateTime start, DateTime end)
    {
        var query = context.StudentHealthVisits.AsNoTracking()
            .Where(item => item.ExamTime >= start && item.ExamTime < end);
        query = userAccess.ApplyHomeroomScope(query,
            item => item.Student != null ? item.Student.Homeroom : item.ClassName);
        return await query
            .Select(item => new HealthVisitSource(
                item.Id, item.StudentProfileId, item.SISUserId, item.FullName, item.ClassName,
                item.ExamTime, item.Classification, item.Diagnosis, item.Symptoms,
                item.Treatment, item.Medication, item.Notes, item.NotificationSent,
                item.ExaminedBy, item.CustomFieldsJson))
            .ToListAsync();
    }

    private static List<HealthReportRowDto> DiagnosisRows(List<HealthVisitSource> source, string groupBy) =>
        source.GroupBy(item => new
            {
                Period = PeriodBounds(item.ExamTime, groupBy).Start,
                Diagnosis = DiagnosisLabel(item).ToUpperInvariant()
            })
            .Select(group =>
            {
                var first = group.First();
                var bounds = PeriodBounds(first.ExamTime, groupBy);
                return new HealthReportRowDto
                {
                    Key = $"diagnosis:{bounds.Start:yyyyMMdd}:{group.Key.Diagnosis}",
                    PeriodStart = bounds.Start,
                    PeriodEnd = bounds.End,
                    StudentName = DiagnosisLabel(first),
                    Diagnosis = DiagnosisLabel(first),
                    VisitCount = group.Count(),
                    StudentCount = group.Select(StudentIdentity).Distinct().Count(),
                    LatestVisit = group.Max(item => item.ExamTime)
                };
            })
            .OrderByDescending(item => item.PeriodStart)
            .ThenByDescending(item => item.VisitCount)
            .ThenBy(item => item.Diagnosis)
            .ToList();

    private static List<HealthReportRowDto> FrequentStudentRows(List<HealthVisitSource> source, string groupBy) =>
        source.GroupBy(item => new
            {
                Period = PeriodBounds(item.ExamTime, groupBy).Start,
                Student = StudentIdentity(item)
            })
            .Select(group =>
            {
                var latest = group.OrderByDescending(item => item.ExamTime).ThenByDescending(item => item.Id).First();
                var bounds = PeriodBounds(latest.ExamTime, groupBy);
                return new HealthReportRowDto
                {
                    Key = $"student:{bounds.Start:yyyyMMdd}:{group.Key.Student}",
                    PeriodStart = bounds.Start,
                    PeriodEnd = bounds.End,
                    StudentProfileId = latest.StudentProfileId,
                    SISUserId = latest.SISUserId,
                    StudentName = latest.StudentName,
                    ClassName = latest.ClassName,
                    VisitCount = group.Count(),
                    StudentCount = 1,
                    LatestVisit = latest.ExamTime
                };
            })
            .OrderByDescending(item => item.PeriodStart)
            .ThenByDescending(item => item.VisitCount)
            .ThenBy(item => item.StudentName)
            .ToList();

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

    private static string StudentIdentity(HealthVisitSource item) => item.StudentProfileId.HasValue
        ? $"profile:{item.StudentProfileId.Value}"
        : !string.IsNullOrWhiteSpace(item.SISUserId)
            ? $"sis:{item.SISUserId.Trim().ToUpperInvariant()}"
            : $"name:{item.StudentName.Trim().ToUpperInvariant()}:{item.ClassName?.Trim().ToUpperInvariant()}";

    private static bool Matches(HealthVisitSource item, string term) =>
        Contains(item.StudentName, term) || Contains(item.SISUserId, term) || Contains(item.ClassName, term) ||
        Contains(item.Diagnosis, term) || Contains(item.Classification, term) || Contains(item.Symptoms, term) ||
        Contains(item.Notes, term);

    private static bool Contains(string? value, string term) =>
        value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;

    private static bool KnownReport(string value) => value is "diagnoses" or "frequent-students" or "student-visits";
    private static bool KnownGrouping(string value) => value is "week" or "month" or "year";
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static DateTime ToUtc(DateTime value) => value.Kind == DateTimeKind.Utc
        ? value
        : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
