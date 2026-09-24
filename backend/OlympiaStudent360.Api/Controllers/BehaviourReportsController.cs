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
        [FromQuery(Name = "term")] string? reportingTerm = null)
    {
        var source = await Behaviours(academicYear, reportingTerm);
        var total = source.Count;
        var studentCount = source.Select(item => item.StudentKey).Distinct().Count();

        return Ok(new BehaviourReportSummaryDto
        {
            TotalRecords = total,
            StudentCount = studentCount,
            BehaviourTypeCount = source.Select(item => item.BehaviourType)
                .Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            PositiveCount = source.Count(item => item.Category == "Positive"),
            ConcerningCount = source.Count(item => item.Category == "Concerning"),
            OtherCount = source.Count(item => item.Category == "Other"),
            TopTypes = source.GroupBy(item => item.BehaviourType, StringComparer.OrdinalIgnoreCase)
                .Select(group => new BehaviourTypeSummaryDto
                {
                    BehaviourType = group.Key,
                    Category = DominantCategory(group),
                    RecordCount = group.Count(),
                    StudentCount = group.Select(item => item.StudentKey).Distinct().Count(),
                    SharePercentage = Percentage(group.Count(), total)
                })
                .OrderByDescending(item => item.RecordCount)
                .ThenBy(item => item.BehaviourType)
                .ToList(),
            ByGrade = source.GroupBy(item => Clean(item.Grade) ?? "__NO_GRADE__", StringComparer.OrdinalIgnoreCase)
                .Select(group => new BehaviourGradeSummaryDto
                {
                    Grade = group.Key,
                    RecordCount = group.Count(),
                    StudentCount = group.Select(item => item.StudentKey).Distinct().Count(),
                    PositiveCount = group.Count(item => item.Category == "Positive"),
                    ConcerningCount = group.Count(item => item.Category == "Concerning"),
                    OtherCount = group.Count(item => item.Category == "Other")
                })
                .OrderBy(item => GradeSort(item.Grade))
                .ThenBy(item => item.Grade)
                .ToList(),
            ByStudent = source.GroupBy(item => item.StudentKey)
                .Select(group =>
                {
                    var latest = group.OrderByDescending(item => item.IncidentDate).ThenByDescending(item => item.Id).First();
                    return new BehaviourStudentSummaryDto
                    {
                        Key = group.Key,
                        StudentProfileId = latest.StudentProfileId,
                        SISUserId = latest.SISUserId,
                        StudentName = latest.StudentName,
                        Grade = latest.Grade,
                        Homeroom = latest.Homeroom,
                        RecordCount = group.Count(),
                        PositiveCount = group.Count(item => item.Category == "Positive"),
                        ConcerningCount = group.Count(item => item.Category == "Concerning"),
                        OtherCount = group.Count(item => item.Category == "Other")
                    };
                })
                .OrderByDescending(item => item.RecordCount)
                .ThenBy(item => item.StudentName)
                .ToList()
        });
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
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        if (dimension is not ("all" or "category" or "type" or "grade" or "student")) return BadRequest();

        IEnumerable<BehaviourSource> source = await Behaviours(academicYear, reportingTerm);
        var filterValue = Clean(value);
        source = dimension switch
        {
            "category" when filterValue != null => source.Where(item =>
                item.Category.Equals(filterValue, StringComparison.OrdinalIgnoreCase)),
            "type" when filterValue != null => source.Where(item =>
                item.BehaviourType.Equals(filterValue, StringComparison.OrdinalIgnoreCase)),
            "grade" when filterValue == "__NO_GRADE__" => source.Where(item => Clean(item.Grade) == null),
            "grade" when filterValue != null => source.Where(item =>
                string.Equals(Clean(item.Grade), filterValue, StringComparison.OrdinalIgnoreCase)),
            "student" when studentProfileId.HasValue => source.Where(item =>
                item.StudentProfileId == studentProfileId.Value),
            "student" when !string.IsNullOrWhiteSpace(studentKey) => source.Where(item =>
                item.StudentKey.Equals(studentKey.Trim(), StringComparison.OrdinalIgnoreCase)),
            _ => source
        };

        var searchTerm = Clean(search);
        if (searchTerm?.Length > 100) searchTerm = searchTerm[..100];
        if (searchTerm != null)
        {
            source = source.Where(item =>
                Contains(item.StudentName, searchTerm) || Contains(item.SISUserId, searchTerm) ||
                Contains(item.Grade, searchTerm) || Contains(item.Homeroom, searchTerm) ||
                Contains(item.BehaviourType, searchTerm) || Contains(item.Notes, searchTerm) ||
                Contains(item.RecordedBy, searchTerm));
        }

        var ordered = source.OrderByDescending(item => item.IncidentDate).ThenByDescending(item => item.Id).ToList();
        var total = ordered.Count;
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling((double)total / pageSize);
        page = Math.Min(page, Math.Max(1, totalPages));
        return Ok(new BehaviourReportDetailsPageDto
        {
            Items = ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(item => new BehaviourReportDetailDto
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

    private async Task<List<BehaviourSource>> Behaviours(string? academicYear, string? reportingTerm)
    {
        var period = await ResolvePeriod(academicYear, reportingTerm);
        var query = userAccess.ApplyStudentScope(
            context.StudentBehaviours.AsNoTracking()
                .Include(item => item.Student)
                .Include(item => item.ImportHistory)
                .AsQueryable(),
            item => item.Student);
        if (period != null)
        {
            query = query.WhereMatchesAny(item => item.AcademicYear, period.YearAliases,
                item => item.AcademicYear == null && item.IncidentDate >= period.YearStart && item.IncidentDate < period.YearEnd);
            if (period.Term != "all")
            {
                query = query.WhereMatchesAny(item => item.Term, period.TermAliases,
                    item => item.Term == null && item.IncidentDate >= period.Start && item.IncidentDate < period.End);
            }
        }

        var records = await query.ToListAsync();
        return records.Select(item =>
        {
            var type = StudentClassificationService.CanonicalBehaviourTypeOrOriginal(item.BehaviourType).Trim();
            var name = Clean(item.Student?.FullName) ?? Clean(item.SourceStudentName) ?? "—";
            return new BehaviourSource(
                item.Id,
                item.StudentProfileId,
                Clean(item.Student?.SISUserId),
                name,
                Clean(item.Student?.GradeLevel) ?? Clean(item.Student?.CanvasGradeLevel),
                Clean(item.Student?.Homeroom),
                item.IncidentDate,
                type,
                StudentClassificationService.BehaviourCategory(item.BehaviourType, item.ClassificationCategory),
                Clean(item.Notes),
                Clean(item.Subject),
                Clean(item.Decision),
                Clean(item.ReporterName) ?? Clean(item.RecordedByEmail) ?? Clean(item.ImportHistory?.ImportedByName));
        }).ToList();
    }

    private async Task<BehaviourPeriod?> ResolvePeriod(string? academicYear, string? reportingTerm)
    {
        if (string.IsNullOrWhiteSpace(academicYear)) return null;
        var year = DashboardPeriodNormalizer.AcademicYear(academicYear);
        var term = string.IsNullOrWhiteSpace(reportingTerm) ? "all" : DashboardPeriodNormalizer.Term(reportingTerm);
        if (term is not ("hk1" or "hk2")) term = "all";

        var yearValues = await context.AcademicPeriods.AsNoTracking().Select(item => item.AcademicYear)
            .Concat(context.StudentBehaviours.Where(item => item.AcademicYear != null).Select(item => item.AcademicYear!))
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
                .Concat(context.StudentBehaviours.Where(item => item.Term != null).Select(item => item.Term!))
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

    private static string DominantCategory(IEnumerable<BehaviourSource> rows) => rows
        .GroupBy(item => item.Category)
        .OrderByDescending(group => group.Count())
        .ThenBy(group => group.Key)
        .Select(group => group.Key)
        .First();

    private static decimal Percentage(int numerator, int denominator) => denominator == 0
        ? 0
        : Math.Round((decimal)numerator * 100 / denominator, 1);

    private static int GradeSort(string grade)
    {
        if (grade == "__NO_GRADE__") return int.MaxValue;
        var match = Regex.Match(grade, @"\d+");
        return match.Success && int.TryParse(match.Value, out var value) ? value : int.MaxValue - 1;
    }

    private static bool Contains(string? value, string search) =>
        value?.Contains(search, StringComparison.OrdinalIgnoreCase) == true;

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
