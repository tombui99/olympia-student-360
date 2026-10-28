using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/reports/behaviours")]
[ApiController]
[Authorize]
public sealed class BehaviourReportsController(AppDbContext context, UserAccessService userAccess) : ControllerBase
{
    private sealed record BehaviourSource(
        int Id,
        int? StudentProfileId,
        string? SISUserId,
        string StudentName,
        string? Grade,
        string? Homeroom,
        DateTime IncidentDate,
        string BehaviourType,
        string Category,
        string? Notes,
        string? Subject,
        string? Decision,
        string? RecordedBy)
    {
        public string StudentKey => StudentProfileId.HasValue
            ? $"id:{StudentProfileId.Value}"
            : $"source:{StudentName.Trim().ToUpperInvariant()}:{Grade?.Trim().ToUpperInvariant()}";
    }

    [HttpGet("summary")]
    public async Task<ActionResult<BehaviourReportSummaryDto>> Summary(
        [FromQuery] string? academicYear = null,
        [FromQuery(Name = "term")] string? reportingTerm = null,
        [FromQuery] int? month = null, [FromQuery] string? weekStart = null)
    {
        var dateError = CanvasActivityPeriodFilter.Validate(academicYear, month, weekStart);
        if (dateError is not null) return BadRequest(dateError);
        if (!await context.ImportedReportBuilds.AsNoTracking().AnyAsync())
            return StatusCode(503, new { Message = "Report aggregates are being prepared." });
        var source = await BehaviourAggregates(academicYear, reportingTerm, ReportDateFilter.Range(academicYear, month, weekStart));
        var total = source.Sum(item => item.RecordCount);
        var studentCount = source.Select(item => item.StudentKey).Distinct().Count();

        return Ok(new BehaviourReportSummaryDto
        {
            TotalRecords = total,
            StudentCount = studentCount,
            BehaviourTypeCount = source.Select(item => item.Type)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            PositiveCount = source.Where(item => item.Category == "Positive").Sum(item => item.RecordCount),
            ConcerningCount = source.Where(item => item.Category == "Concerning").Sum(item => item.RecordCount),
            OtherCount = source.Where(item => item.Category == "Other").Sum(item => item.RecordCount),
            TopTypes = source.GroupBy(item => item.Type ?? "—", StringComparer.OrdinalIgnoreCase)
                .Select(group => new BehaviourTypeSummaryDto
                {
                    BehaviourType = group.Key,
                    Category = group.GroupBy(item => item.Category).OrderByDescending(category => category.Sum(item => item.RecordCount))
                        .ThenBy(category => category.Key).First().Key ?? "Other",
                    RecordCount = group.Sum(item => item.RecordCount),
                    StudentCount = group.Select(item => item.StudentKey).Distinct().Count(),
                    SharePercentage = Percentage(group.Sum(item => item.RecordCount), total)
                })
                .OrderByDescending(item => item.RecordCount)
                .ThenBy(item => item.BehaviourType)
                .ToList(),
            ByGrade = source.GroupBy(item => Clean(item.Grade) ?? "__NO_GRADE__", StringComparer.OrdinalIgnoreCase)
                .Select(group => new BehaviourGradeSummaryDto
                {
                    Grade = group.Key,
                    RecordCount = group.Sum(item => item.RecordCount),
                    StudentCount = group.Select(item => item.StudentKey).Distinct().Count(),
                    PositiveCount = group.Where(item => item.Category == "Positive").Sum(item => item.RecordCount),
                    ConcerningCount = group.Where(item => item.Category == "Concerning").Sum(item => item.RecordCount),
                    OtherCount = group.Where(item => item.Category == "Other").Sum(item => item.RecordCount)
                })
                .OrderBy(item => GradeSort(item.Grade))
                .ThenBy(item => item.Grade)
                .ToList(),
            ByStudent = source.GroupBy(item => item.StudentKey)
                .Select(group =>
                {
                    var latest = group.OrderByDescending(item => item.LatestEvent).ThenByDescending(item => item.Id).First();
                    return new BehaviourStudentSummaryDto
                    {
                        Key = group.Key,
                        StudentProfileId = latest.StudentProfileId,
                        SISUserId = latest.SISUserId,
                        StudentName = latest.StudentName,
                        Grade = latest.Grade,
                        Homeroom = latest.Homeroom,
                        RecordCount = group.Sum(item => item.RecordCount),
                        PositiveCount = group.Where(item => item.Category == "Positive").Sum(item => item.RecordCount),
                        ConcerningCount = group.Where(item => item.Category == "Concerning").Sum(item => item.RecordCount),
                        OtherCount = group.Where(item => item.Category == "Other").Sum(item => item.RecordCount)
                    };
                })
                .OrderByDescending(item => item.RecordCount)
                .ThenBy(item => item.StudentName)
                .ToList()
        });
    }

    private async Task<List<ImportedReportAggregate>> BehaviourAggregates(string? academicYear, string? reportingTerm, ReportDateRange? activity)
    {
        var period = await ResolvePeriod(academicYear, reportingTerm);
        var query = userAccess.ApplyStudentScope(
            context.ImportedReportAggregates.AsNoTracking().Where(item => item.Source == "student-behaviours"),
            item => item.Student);
        if (period != null)
        {
            query = query.WhereMatchesAny(item => item.AcademicYear, period.YearAliases,
                item => item.AcademicYear == null && item.EventDay >= period.YearStart && item.EventDay < period.YearEnd);
            if (period.Term != "all")
                query = query.WhereMatchesAny(item => item.Term, period.TermAliases,
                    item => item.Term == null && item.EventDay >= period.Start && item.EventDay < period.End);
        }
        query = ReportDateFilter.Apply(query, item => item.EventDay, activity);
        // Only bring back the narrow measures required by this summary. The fact
        // table also carries long health text fields that must not cross the wire.
        return await query.Select(item => new ImportedReportAggregate
        {
            Id = item.Id,
            StudentProfileId = item.StudentProfileId,
            StudentKey = item.StudentKey,
            SISUserId = item.SISUserId,
            StudentName = item.StudentName,
            Grade = item.Grade,
            Homeroom = item.Homeroom,
            Type = item.Type,
            Category = item.Category,
            LatestEvent = item.LatestEvent,
            RecordCount = item.RecordCount
        }).ToListAsync();
    }

    [HttpGet("details")]
    public async Task<ActionResult<BehaviourReportDetailsPageDto>> Details(
        [FromQuery] string? academicYear = null,
        [FromQuery(Name = "term")] string? reportingTerm = null,
        [FromQuery] string dimension = "all",
        [FromQuery] string? value = null,
        [FromQuery] int? studentProfileId = null,
        [FromQuery] string? studentKey = null,
        [FromQuery] string? search = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? month = null, [FromQuery] string? weekStart = null)
    {
        var dateError = CanvasActivityPeriodFilter.Validate(academicYear, month, weekStart);
        if (dateError is not null) return BadRequest(dateError);
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        if (dimension is not ("all" or "category" or "type" or "grade" or "student")) return BadRequest();

        var period = await ResolvePeriod(academicYear, reportingTerm);
        var query = userAccess.ApplyStudentScope(context.StudentBehaviours.AsNoTracking()
            .Include(item => item.Student).Include(item => item.ImportHistory), item => item.Student);
        if (period != null)
        {
            query = query.WhereMatchesAny(item => item.AcademicYear, period.YearAliases,
                item => item.AcademicYear == null && item.IncidentDate >= period.YearStart && item.IncidentDate < period.YearEnd);
            if (period.Term != "all")
                query = query.WhereMatchesAny(item => item.Term, period.TermAliases,
                    item => item.Term == null && item.IncidentDate >= period.Start && item.IncidentDate < period.End);
        }
        query = ReportDateFilter.Apply(query, item => item.IncidentDate, ReportDateFilter.Range(academicYear, month, weekStart));
        var filterValue = Clean(value);
        if (dimension == "type" && filterValue != null)
        {
            var rawTypes = await context.StudentBehaviours.AsNoTracking()
                .Select(item => item.BehaviourType).Distinct().ToListAsync();
            var matchingTypes = rawTypes.Where(type =>
                StudentClassificationService.CanonicalBehaviourTypeOrOriginal(type)
                    .Equals(filterValue, StringComparison.OrdinalIgnoreCase)).ToArray();
            query = query.Where(item => matchingTypes.Contains(item.BehaviourType));
        }
        if (dimension == "grade" && filterValue == "__NO_GRADE__")
            query = query.Where(item => item.Student == null ||
                (item.Student.GradeLevel ?? item.Student.CanvasGradeLevel) == null ||
                (item.Student.GradeLevel ?? item.Student.CanvasGradeLevel) == "");
        else if (dimension == "grade" && filterValue != null)
            query = query.Where(item => item.Student != null &&
                (item.Student.GradeLevel ?? item.Student.CanvasGradeLevel) == filterValue);
        if (dimension == "student")
        {
            if (studentProfileId.HasValue) query = query.Where(item => item.StudentProfileId == studentProfileId.Value);
            else if (!string.IsNullOrWhiteSpace(studentKey))
            {
                var key = studentKey.Trim();
                if (key.StartsWith("id:", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(key[3..], out var parsedId))
                    query = query.Where(item => item.StudentProfileId == parsedId);
                else if (key.StartsWith("source:", StringComparison.OrdinalIgnoreCase) && key.LastIndexOf(':') > 7)
                {
                    var sourceName = key[7..key.LastIndexOf(':')];
                    query = query.Where(item => item.StudentProfileId == null &&
                        item.SourceStudentName != null && item.SourceStudentName.ToUpper() == sourceName);
                }
                else query = query.Where(item => false);
            }
        }
        if (dimension == "category" && filterValue != null)
        {
            var rawTypes = await context.StudentBehaviours.AsNoTracking()
                .Where(item => item.ClassificationCategory == null)
                .Select(item => item.BehaviourType).Distinct().ToListAsync();
            var matchingTypes = rawTypes.Where(type => StudentClassificationService.BehaviourCategory(type) == filterValue)
                .ToArray();
            query = filterValue == "Other"
                ? query.Where(item => item.ClassificationCategory != "Positive" &&
                    item.ClassificationCategory != "Concerning" &&
                    (item.ClassificationCategory != null || matchingTypes.Contains(item.BehaviourType)))
                : query.Where(item => item.ClassificationCategory == filterValue ||
                    item.ClassificationCategory == null && matchingTypes.Contains(item.BehaviourType));
        }

        var searchTerm = Clean(search);
        if (searchTerm?.Length > 100) searchTerm = searchTerm[..100];
        if (searchTerm != null)
        {
            query = query.Where(item =>
                item.Student != null && (item.Student.FullName.Contains(searchTerm) ||
                    item.Student.SISUserId.Contains(searchTerm) ||
                    item.Student.GradeLevel != null && item.Student.GradeLevel.Contains(searchTerm) ||
                    item.Student.CanvasGradeLevel != null && item.Student.CanvasGradeLevel.Contains(searchTerm) ||
                    item.Student.Homeroom != null && item.Student.Homeroom.Contains(searchTerm)) ||
                item.SourceStudentName != null && item.SourceStudentName.Contains(searchTerm) ||
                item.BehaviourType.Contains(searchTerm) ||
                item.Notes != null && item.Notes.Contains(searchTerm) ||
                item.ReporterName != null && item.ReporterName.Contains(searchTerm) ||
                item.RecordedByEmail != null && item.RecordedByEmail.Contains(searchTerm) ||
                item.ImportHistory != null && item.ImportHistory.ImportedByName.Contains(searchTerm));
        }

        var total = await query.CountAsync();
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling((double)total / pageSize);
        page = Math.Min(page, Math.Max(1, totalPages));
        var ordered = await query.OrderByDescending(item => item.IncidentDate).ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new BehaviourReportDetailsPageDto
        {
            Items = ordered.Select(ToSource).Select(item => new BehaviourReportDetailDto
            {
                Id = item.Id,
                StudentProfileId = item.StudentProfileId,
                SISUserId = item.SISUserId,
                StudentName = item.StudentName,
                Grade = item.Grade,
                Homeroom = item.Homeroom,
                IncidentDate = item.IncidentDate,
                BehaviourType = item.BehaviourType,
                Category = item.Category,
                Notes = item.Notes,
                Subject = item.Subject,
                Decision = item.Decision,
                RecordedBy = item.RecordedBy
            }).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            TotalPages = totalPages
        });
    }

    private static BehaviourSource ToSource(StudentBehaviour item)
    {
        var type = StudentClassificationService.CanonicalBehaviourTypeOrOriginal(item.BehaviourType).Trim();
        var name = Clean(item.Student?.FullName) ?? Clean(item.SourceStudentName) ?? "—";
        return new BehaviourSource(item.Id, item.StudentProfileId, Clean(item.Student?.SISUserId),
            name, Clean(item.Student?.GradeLevel) ?? Clean(item.Student?.CanvasGradeLevel),
            Clean(item.Student?.Homeroom), item.IncidentDate, type,
            StudentClassificationService.BehaviourCategory(item.BehaviourType, item.ClassificationCategory),
            Clean(item.Notes), Clean(item.Subject), Clean(item.Decision),
            Clean(item.ReporterName) ?? Clean(item.RecordedByEmail) ?? Clean(item.ImportHistory?.ImportedByName));
    }

    private async Task<BehaviourPeriod?> ResolvePeriod(string? academicYear, string? reportingTerm)
    {
        if (string.IsNullOrWhiteSpace(academicYear)) return null;
        var year = DashboardPeriodNormalizer.AcademicYear(academicYear);
        var term = string.IsNullOrWhiteSpace(reportingTerm) ? "all" : DashboardPeriodNormalizer.Term(reportingTerm);
        if (term is not ("hk1" or "hk2")) term = "all";

        var yearValues = await context.AcademicPeriods.AsNoTracking().Select(item => item.AcademicYear)
            .Concat(context.ImportedReportAggregates.Where(item => item.Source == "student-behaviours" && item.AcademicYear != null)
                .Select(item => item.AcademicYear!))
            .Distinct().ToListAsync();
        var yearAliases = yearValues.Where(value => DashboardPeriodNormalizer.AcademicYear(value)
                .Equals(year, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (yearAliases.Count == 0) yearAliases.Add(academicYear.Trim());
        if (!yearAliases.Contains(year, StringComparer.OrdinalIgnoreCase)) yearAliases.Add(year);

        var termAliases = new List<string>();
        if (term != "all")
        {
            var termValues = await context.AcademicPeriods.AsNoTracking().Select(item => item.Term)
                .Concat(context.ImportedReportAggregates.Where(item => item.Source == "student-behaviours" && item.Term != null)
                    .Select(item => item.Term!))
                .Distinct().ToListAsync();
            termAliases = termValues.Where(value => DashboardPeriodNormalizer.Term(value)
                    .Equals(term, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (termAliases.Count == 0 && reportingTerm != null) termAliases.Add(reportingTerm.Trim());
            if (!termAliases.Contains(term, StringComparer.OrdinalIgnoreCase)) termAliases.Add(term);
        }

        var (yearStart, yearEnd) = DashboardPeriodNormalizer.AcademicYearRange(year);
        var (start, end) = DashboardPeriodNormalizer.PeriodRange(year, term);
        return new BehaviourPeriod(yearAliases.ToArray(), termAliases.ToArray(), term, yearStart, yearEnd, start, end);
    }

    private static decimal Percentage(int numerator, int denominator) => denominator == 0
        ? 0
        : Math.Round((decimal)numerator * 100 / denominator, 1);

    private static int GradeSort(string grade)
    {
        if (grade == "__NO_GRADE__") return int.MaxValue;
        var match = Regex.Match(grade, @"\d+");
        return match.Success && int.TryParse(match.Value, out var value) ? value : int.MaxValue - 1;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private sealed record BehaviourPeriod(
        string[] YearAliases,
        string[] TermAliases,
        string Term,
        DateTime YearStart,
        DateTime YearEnd,
        DateTime Start,
        DateTime End);
}
