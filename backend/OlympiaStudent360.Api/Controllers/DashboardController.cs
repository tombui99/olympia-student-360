using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;
using System.Text.Json;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class DashboardController : ControllerBase
{
    private sealed record MetricSample(
        int StudentId, DateTime? Date, double Value, Dictionary<string, string?> Fields);

    private sealed record CanvasMetricRow(
        int? StudentProfileId, string? WorkflowState, bool Late, bool Missing, bool? Excused,
        double? Score, string? Grade, double? PointsPossible, DateTime? SubmittedAt,
        DateTime? GradedAt, DateTime? DueAt, string? CourseName, string? AssignmentTitle);

    private static Dictionary<string, string?> DetailFields(params (string Key, string? Value)[] fields) =>
        fields.Where(field => !string.IsNullOrWhiteSpace(field.Value))
            .ToDictionary(field => field.Key, field => field.Value);

    private static MetricSample CanvasSample(CanvasMetricRow row, DateTime date, double value) =>
        new(row.StudentProfileId!.Value, date, value,
            DetailFields(("Canvas assignment", row.AssignmentTitle), ("Canvas course name", row.CourseName),
                ("Trạng thái", row.WorkflowState), ("Điểm", row.Score?.ToString("0.##")),
                ("Điểm tối đa", row.PointsPossible?.ToString("0.##")), ("Xếp loại", row.Grade),
                ("Hạn nộp", row.DueAt?.ToString("dd/MM/yyyy HH:mm"))));

    private readonly AppDbContext _context;
    private readonly IMemoryCache _cache;

    public DashboardController(AppDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    [HttpGet("filter-options")]
    public async Task<ActionResult<DashboardFilterOptionsDto>> FilterOptions(
        [FromQuery] int? reportId, [FromQuery] bool periodOnly = false,
        [FromQuery] bool deferHeavy = false)
    {
        var optionScope = periodOnly ? "periods" : deferHeavy ? "deferred" : "full";
        var cacheKey = $"dashboard-filter-options:{reportId?.ToString() ?? "all"}:{optionScope}";
        if (_cache.TryGetValue<DashboardFilterOptionsDto>(cacheKey, out var cached) && cached is not null)
        {
            return Ok(cached);
        }

        var canvasOnly = reportId.HasValue && await IsCanvasOnlyReport(reportId.Value);
        var (academicYears, terms) = await LoadFilterPeriods(canvasOnly);

        // The overview dashboard only has year and term selectors. Avoid making its initial
        // render wait for the much larger student/course option set used by report workspaces.
        if (periodOnly)
        {
            var periodResult = new DashboardFilterOptionsDto
            {
                AcademicYears = academicYears,
                Terms = terms
            };
            _cache.Set(cacheKey, periodResult, TimeSpan.FromMinutes(5));
            return Ok(periodResult);
        }

        if (deferHeavy)
        {
            var studentDimensions = await VisibleStudents()
                .Select(item => new
                {
                    item.Campus,
                    item.EducationLevel,
                    GradeLevel = item.GradeLevel ?? item.CanvasGradeLevel,
                    item.Program
                })
                .Distinct()
                .ToListAsync();
            var canvasGradeLevels = await _context.CanvasCourses.AsNoTracking()
                .Where(item => item.CanvasGradeLevel != null)
                .Select(item => item.CanvasGradeLevel!)
                .Distinct()
                .ToListAsync();
            var observedDimensions = canvasOnly
                ? []
                : await _context.DashboardMetricObservations.AsNoTracking()
                    .Select(item => new { item.Campus, item.EducationLevel, item.GradeLevel })
                    .Distinct()
                    .ToListAsync();
            var deferredImportedPrograms = canvasOnly
                ? []
                : await _context.DashboardMetricObservations
                    .Where(item => item.Program != null).Select(item => item.Program!)
                    .Concat(_context.StudentBehaviours.Where(item => item.Program != null)
                        .Select(item => item.Program!))
                    .Concat(_context.StudentProjects.Where(item => item.Program != null)
                        .Select(item => item.Program!))
                    .Distinct()
                    .ToListAsync();
            var deferredResult = new DashboardFilterOptionsDto
            {
                AcademicYears = academicYears,
                Terms = terms,
                Campuses = CleanOptions(studentDimensions.Where(item => item.Campus != null)
                    .Select(item => item.Campus!).Concat(observedDimensions
                        .Where(item => item.Campus != null).Select(item => item.Campus!))),
                EducationLevels = CleanOptions(studentDimensions.Where(item => item.EducationLevel != null)
                    .Select(item => item.EducationLevel!).Concat(observedDimensions
                        .Where(item => item.EducationLevel != null).Select(item => item.EducationLevel!))),
                GradeLevels = GradeOptions(studentDimensions.Where(item => item.GradeLevel != null)
                    .Select(item => item.GradeLevel!).Concat(canvasGradeLevels).Concat(observedDimensions
                        .Where(item => item.GradeLevel != null).Select(item => item.GradeLevel!))),
                Programs = CleanOptions(studentDimensions.Where(item => item.Program != null)
                    .Select(item => item.Program!).Concat(deferredImportedPrograms))
            };
            _cache.Set(cacheKey, deferredResult, TimeSpan.FromMinutes(5));
            return Ok(deferredResult);
        }

        // Scan each Canvas fact table once for reportable student/course membership. The previous
        // implementation independently rescanned the full submissions warehouse for students,
        // subjects and grades, which became expensive once the warehouse reached ~500k rows.
        var canvasMemberships = await _context.CanvasCourseScores.AsNoTracking()
            .Where(item => item.StudentProfileId != null &&
                (item.FinalScore != null || item.CurrentScore != null) &&
                (item.GradingPeriodEndDate != null || item.Enrollment!.Course!.EndAt != null))
            .Select(item => new
            {
                StudentId = item.StudentProfileId!.Value,
                CourseId = item.Enrollment!.CanvasCourseId
            })
            .Concat(_context.CanvasSubmissions.AsNoTracking()
                .Where(item => item.StudentProfileId != null &&
                    (item.SubmittedAt != null ||
                     (item.WorkflowState != null && item.WorkflowState.ToLower() == "graded" &&
                      (item.GradedAt != null || item.SubmittedAt != null)) ||
                     (item.Missing && item.Excused != true &&
                      (item.CachedDueDate != null || item.Assignment!.DueAt != null))))
                .Select(item => new
                {
                    StudentId = item.StudentProfileId!.Value,
                    CourseId = item.CanvasCourseId
                }))
            .Distinct()
            .ToListAsync();

        var dataStudentIds = canvasMemberships.Select(item => item.StudentId).Distinct().ToList();
        if (!canvasOnly)
        {
            var importedStudentIds = await _context.StudentBehaviours.Select(item => item.StudentProfileId)
                .Concat(_context.StudentProjects.Select(item => item.StudentProfileId))
                .Concat(_context.StudentMentalHealthRecords.Select(item => item.StudentProfileId))
                .Concat(_context.StudentBusAttendanceRecords.Select(item => item.StudentProfileId))
                .Concat(_context.SISHealthVisits.Where(item => item.SISUserId != null)
                    .Select(item => item.Student!.Id))
                .Concat(_context.DashboardMetricObservations
                    .Where(item => item.StudentProfileId != null).Select(item => item.StudentProfileId!.Value))
                .Distinct()
                .ToListAsync();
            dataStudentIds = dataStudentIds.Concat(importedStudentIds).Distinct().ToList();
        }

        // Load the selected student dimension once, then derive all student-owned options in
        // memory. This replaces six nearly identical StudentProfiles queries and round trips.
        var students = await VisibleStudents()
            .Where(item => dataStudentIds.Contains(item.Id))
            .OrderBy(item => item.FullName)
            .Select(item => new DashboardStudentOptionDto
            {
                Id = item.Id,
                SISUserId = item.SISUserId,
                FullName = item.FullName,
                Campus = item.Campus,
                EducationLevel = item.EducationLevel,
                GradeLevel = item.GradeLevel ?? item.CanvasGradeLevel,
                Homeroom = item.Homeroom,
                Program = item.Program
            })
            .ToListAsync();

        var reportableCourseIds = canvasMemberships.Select(item => item.CourseId).Distinct().ToList();
        var canvasCourses = await _context.CanvasCourses.AsNoTracking()
            .Where(item => reportableCourseIds.Contains(item.CanvasCourseId))
            .Select(item => new { item.Name, item.CanvasGradeLevel })
            .ToListAsync();

        // Non-period options are sourced from the real Canvas and imported data, not the legacy
        // demo metrics. Measurement cycles come from sources that actually store them.
        var reportableCanvasSubjects = canvasCourses.Where(item => item.Name != null).Select(item => item.Name!);
        var reportableCanvasGradeLevels = canvasCourses.Where(item => item.CanvasGradeLevel != null)
            .Select(item => item.CanvasGradeLevel!);
        var subjectValues = canvasOnly
            ? reportableCanvasSubjects.ToList()
            : reportableCanvasSubjects.Concat(await _context.StudentProjects
                .Where(p => p.Subject != null).Select(p => p.Subject!)
                .Concat(_context.StudentBehaviours.Where(b => b.Subject != null).Select(b => b.Subject!))
                .Concat(_context.DashboardMetricObservations.Where(item => item.Subject != null)
                    .Select(item => item.Subject!))
                .Distinct()
                .ToListAsync());

        var observedCampuses = canvasOnly ? [] : await _context.DashboardMetricObservations
            .Where(item => item.Campus != null).Select(item => item.Campus!).Distinct().ToListAsync();
        var observedEducationLevels = canvasOnly ? [] : await _context.DashboardMetricObservations
            .Where(item => item.EducationLevel != null).Select(item => item.EducationLevel!).Distinct().ToListAsync();
        var observedGradeLevels = canvasOnly ? [] : await _context.DashboardMetricObservations
            .Where(item => item.GradeLevel != null).Select(item => item.GradeLevel!).Distinct().ToListAsync();
        var observedHomerooms = canvasOnly ? [] : await _context.DashboardMetricObservations
            .Where(item => item.Homeroom != null).Select(item => item.Homeroom!).Distinct().ToListAsync();
        var measurementCycles = canvasOnly ? [] : await _context.DashboardMetricObservations
            .Where(item => item.MeasurementCycle != null).Select(item => item.MeasurementCycle!)
            .Concat(_context.StudentBehaviours.Where(item => item.MeasurementCycle != null)
                .Select(item => item.MeasurementCycle!))
            .Concat(_context.StudentProjects.Where(item => item.MeasurementCycle != null)
                .Select(item => item.MeasurementCycle!)).Distinct().ToListAsync();
        var importedPrograms = canvasOnly ? [] : await _context.DashboardMetricObservations
            .Where(item => item.Program != null).Select(item => item.Program!)
            .Concat(_context.StudentBehaviours.Where(item => item.Program != null)
                .Select(item => item.Program!))
            .Concat(_context.StudentProjects.Where(item => item.Program != null)
                .Select(item => item.Program!)).Distinct().ToListAsync();

        var result = new DashboardFilterOptionsDto
        {
            AcademicYears = academicYears,
            Terms = terms,
            Campuses = CleanOptions(students.Where(item => item.Campus != null).Select(item => item.Campus!)
                .Concat(observedCampuses)),
            EducationLevels = CleanOptions(students.Where(item => item.EducationLevel != null)
                .Select(item => item.EducationLevel!).Concat(observedEducationLevels)),
            GradeLevels = GradeOptions(students.Where(item => item.GradeLevel != null)
                .Select(item => item.GradeLevel!).Concat(reportableCanvasGradeLevels)
                .Concat(observedGradeLevels)),
            Homerooms = CleanOptions(students.Where(item => item.Homeroom != null)
                .Select(item => item.Homeroom!).Concat(observedHomerooms)),
            Students = students,
            MeasurementCycles = CleanOptions(measurementCycles),
            Programs = CleanOptions(students.Where(item => item.Program != null).Select(item => item.Program!)
                .Concat(importedPrograms)),
            Subjects = CleanOptions(subjectValues)
        };

        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
        return Ok(result);
    }

    [HttpGet("filter-option-search")]
    public async Task<ActionResult<List<DashboardFilterSearchOptionDto>>> FilterOptionSearch(
        [FromQuery] string field, [FromQuery] string? query, [FromQuery] string? selectedValue,
        [FromQuery] int? reportId, [FromQuery] string? campus, [FromQuery] string? level,
        [FromQuery] string? grade, [FromQuery] string? homeroom)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return BadRequest("A filter field is required.");
        }

        var search = Clean(query);
        var selected = Clean(selectedValue);
        if (search is null && selected is null)
        {
            return Ok(new List<DashboardFilterSearchOptionDto>());
        }

        var canvasOnly = reportId.HasValue && await IsCanvasOnlyReport(reportId.Value);
        var students = VisibleStudents();
        if (canvasOnly)
        {
            students = students.Where(item => item.CanvasUserId != null);
        }

        if (field.Equals("homeroom", StringComparison.OrdinalIgnoreCase))
        {
            if (HasFilter(campus)) students = students.Where(item => item.Campus == campus);
            if (HasFilter(level)) students = students.Where(item => item.EducationLevel == level);
            if (HasFilter(grade)) students = students.Where(item =>
                (item.GradeLevel ?? item.CanvasGradeLevel) == grade);
            if (HasFilter(homeroom)) students = students.Where(item => item.Homeroom == homeroom);

            var values = students.Where(item => item.Homeroom != null).Select(item => item.Homeroom!);
            if (!canvasOnly && !HasFilter(campus) && !HasFilter(level) && !HasFilter(grade))
            {
                values = values.Concat(_context.DashboardMetricObservations
                    .Where(item => item.Homeroom != null).Select(item => item.Homeroom!));
            }
            values = selected is not null
                ? values.Where(value => value == selected)
                : values.Where(value => value.Contains(search!));
            var results = await values.Distinct().OrderBy(value => value).Take(20)
                .Select(value => new DashboardFilterSearchOptionDto { Value = value, Label = value })
                .ToListAsync(HttpContext.RequestAborted);
            return Ok(results);
        }

        if (field.Equals("student", StringComparison.OrdinalIgnoreCase))
        {
            if (HasFilter(campus)) students = students.Where(item => item.Campus == campus);
            if (HasFilter(level)) students = students.Where(item => item.EducationLevel == level);
            if (HasFilter(grade)) students = students.Where(item =>
                (item.GradeLevel ?? item.CanvasGradeLevel) == grade);
            if (HasFilter(homeroom)) students = students.Where(item => item.Homeroom == homeroom);

            if (selected is not null)
            {
                if (!int.TryParse(selected, out var studentId))
                {
                    return Ok(new List<DashboardFilterSearchOptionDto>());
                }
                students = students.Where(item => item.Id == studentId);
            }
            else
            {
                students = students.Where(item => item.FullName.Contains(search!) ||
                    item.SISUserId.Contains(search!));
            }

            var rows = await students.OrderBy(item => item.FullName).ThenBy(item => item.SISUserId).Take(20)
                .Select(item => new { item.Id, item.FullName, item.SISUserId })
                .ToListAsync(HttpContext.RequestAborted);
            var results = rows.Select(item => new DashboardFilterSearchOptionDto
            {
                Value = item.Id.ToString(),
                Label = $"{item.FullName} ({item.SISUserId})"
            }).ToList();
            return Ok(results);
        }

        if (field.Equals("subject", StringComparison.OrdinalIgnoreCase))
        {
            var values = _context.CanvasCourses.AsNoTracking()
                .Where(item => item.Name != null &&
                    item.Enrollments.Any(enrollment => enrollment.StudentProfileId != null))
                .Select(item => item.Name!);
            if (!canvasOnly)
            {
                values = values.Concat(_context.StudentProjects.Where(item => item.Subject != null)
                        .Select(item => item.Subject!))
                    .Concat(_context.StudentBehaviours.Where(item => item.Subject != null)
                        .Select(item => item.Subject!))
                    .Concat(_context.DashboardMetricObservations.Where(item => item.Subject != null)
                        .Select(item => item.Subject!));
            }
            values = selected is not null
                ? values.Where(value => value == selected)
                : values.Where(value => value.Contains(search!));
            var results = await values.Distinct().OrderBy(value => value).Take(20)
                .Select(value => new DashboardFilterSearchOptionDto { Value = value, Label = value })
                .ToListAsync(HttpContext.RequestAborted);
            return Ok(results);
        }

        return BadRequest("Only homeroom, student and subject support lazy option search.");
    }

    private async Task<(List<string> AcademicYears, List<string> Terms)> LoadFilterPeriods(bool canvasOnly)
    {
        // Collapse fact timestamps in SQL. Selecting the exact timestamp here previously returned
        // almost one row per submission before .NET reduced them to academic years.
        var canvasPeriods = await _context.CanvasCourseScores.AsNoTracking()
            .Where(item => item.StudentProfileId != null &&
                (item.FinalScore != null || item.CurrentScore != null) &&
                (item.GradingPeriodEndDate != null || item.Enrollment!.Course!.EndAt != null))
            .Select(item => new
            {
                Year = (item.GradingPeriodEndDate ?? item.Enrollment!.Course!.EndAt)!.Value.Year,
                Month = (item.GradingPeriodEndDate ?? item.Enrollment!.Course!.EndAt)!.Value.Month,
                Term = item.GradingPeriodTitle
            })
            .Concat(_context.CanvasSubmissions.AsNoTracking()
                .Where(item => item.StudentProfileId != null &&
                    (item.SubmittedAt != null ||
                     (item.WorkflowState != null && item.WorkflowState.ToLower() == "graded" &&
                      (item.GradedAt != null || item.SubmittedAt != null)) ||
                     (item.Missing && item.Excused != true &&
                      (item.CachedDueDate != null || item.Assignment!.DueAt != null))))
                .Select(item => new
                {
                    Year = (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.GradedAt ?? item.SubmittedAt)!
                        .Value.Year,
                    Month = (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.GradedAt ?? item.SubmittedAt)!
                        .Value.Month,
                    Term = (string?)null
                }))
            .Distinct()
            .ToListAsync();

        var importedAcademicYears = canvasOnly
            ? []
            : await _context.DashboardMetricObservations
                .Where(item => item.AcademicYear != null).Select(item => item.AcademicYear!)
                .Concat(_context.StudentBehaviours.Where(item => item.AcademicYear != null)
                    .Select(item => item.AcademicYear!))
                .Concat(_context.StudentProjects.Where(item => item.AcademicYear != null)
                    .Select(item => item.AcademicYear!))
                .Concat(_context.StudentMentalHealthRecords.Select(item => item.AcademicYear))
                .Concat(_context.StudentBusAttendanceRecords.Where(item => item.AcademicYear != null)
                    .Select(item => item.AcademicYear!))
                .Distinct()
                .ToListAsync();
        var healthVisitPeriods = canvasOnly
            ? []
            : await _context.SISHealthVisits.AsNoTracking()
                .Where(item => item.SISUserId != null)
                .Select(item => new { Year = item.ExamTime.Year, Month = item.ExamTime.Month })
                .Distinct()
                .ToListAsync();
        var academicYears = canvasPeriods
            .Select(item => DashboardPeriodNormalizer.AcademicYear(new DateTime(item.Year, item.Month, 1)))
            .Concat(importedAcademicYears.Select(DashboardPeriodNormalizer.AcademicYear))
            .Concat(healthVisitPeriods.Select(item =>
                DashboardPeriodNormalizer.AcademicYear(new DateTime(item.Year, item.Month, 1))))
            .Where(DashboardPeriodNormalizer.IsAcademicYear)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(DashboardPeriodNormalizer.AcademicYearStart)
            .ThenByDescending(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var importedTerms = canvasOnly
            ? []
            : await _context.AcademicPeriods.AsNoTracking().Select(item => item.Term)
                .Concat(_context.DashboardMetricObservations.Where(item => item.Term != null)
                    .Select(item => item.Term!))
                .Concat(_context.StudentBehaviours.Where(item => item.Term != null)
                    .Select(item => item.Term!))
                .Concat(_context.StudentProjects.Where(item => item.Term != null)
                    .Select(item => item.Term!))
                .Concat(_context.StudentBusAttendanceRecords.Where(item => item.Term != null)
                    .Select(item => item.Term!))
                .Distinct()
                .ToListAsync();
        var terms = canvasPeriods.Where(item => item.Term != null).Select(item => item.Term!)
            .Concat(importedTerms)
            .Select(DashboardPeriodNormalizer.Term)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return (academicYears, terms);
    }

    private async Task<bool> IsCanvasOnlyReport(int reportId)
    {
        var definition = await _context.CustomReports.AsNoTracking()
            .Where(item => item.Id == reportId)
            .Select(item => item.DefinitionJson)
            .FirstOrDefaultAsync();
        if (string.IsNullOrWhiteSpace(definition)) return false;

        try
        {
            using var document = JsonDocument.Parse(definition);
            if (!document.RootElement.TryGetProperty("charts", out var charts) ||
                charts.ValueKind != JsonValueKind.Array || charts.GetArrayLength() == 0)
            {
                return false;
            }

            foreach (var chart in charts.EnumerateArray())
            {
                if (chart.TryGetProperty("explorer", out var explorer) &&
                    explorer.ValueKind == JsonValueKind.Object &&
                    explorer.TryGetProperty("dataset", out var dataset))
                {
                    if (dataset.GetString() is not ("submissions" or "scores" or "assignments" or "courses"))
                        return false;
                    continue;
                }

                if (!chart.TryGetProperty("metrics", out var metrics) || metrics.ValueKind != JsonValueKind.Array ||
                    !metrics.EnumerateArray().Any() ||
                    metrics.EnumerateArray().Any(metric =>
                        !metric.GetString()!.StartsWith("canvas-", StringComparison.Ordinal)))
                {
                    return false;
                }
            }
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    [HttpGet("preference")]
    public async Task<ActionResult<DashboardPreferenceDto>> Preference()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var preference = await _context.DashboardPreferences.AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId);
        return Ok(preference == null ? new DashboardPreferenceDto() : ToPreferenceDto(preference));
    }

    [HttpPut("preference")]
    public async Task<ActionResult<DashboardPreferenceDto>> SavePreference(DashboardPreferenceDto model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var preference = await _context.DashboardPreferences.FirstOrDefaultAsync(item => item.UserId == userId);
        if (preference == null)
        {
            preference = new DashboardPreference { UserId = userId };
            _context.DashboardPreferences.Add(preference);
        }
        preference.AcademicYear = NormalizeAcademicYear(model.AcademicYear);
        preference.Term = NormalizeTerm(model.Term);
        preference.MeasurementCycle = Clean(model.MeasurementCycle);
        preference.Campus = Clean(model.Campus);
        preference.EducationLevel = Clean(model.EducationLevel);
        preference.GradeLevel = Clean(model.GradeLevel);
        preference.Homeroom = Clean(model.Homeroom);
        preference.StudentProfileId = model.StudentProfileId;
        preference.Program = Clean(model.Program);
        preference.Subject = Clean(model.Subject);
        preference.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(ToPreferenceDto(preference));
    }

    [HttpGet("metrics")]
    public async Task<ActionResult<List<DashboardMetricDto>>> Metrics(
        [FromQuery] string? year, [FromQuery] string? term, [FromQuery] string? cycle,
        [FromQuery] string? campus, [FromQuery] string? level, [FromQuery] string? grade,
        [FromQuery] string? homeroom, [FromQuery] int? studentId,
        [FromQuery] string? program, [FromQuery] string? subject)
    {
        var cacheKey = DashboardCacheKey("metrics", year, term, cycle, campus, level, grade,
            homeroom, studentId?.ToString(), program, subject);
        if (_cache.TryGetValue<List<DashboardMetricDto>>(cacheKey, out var cached) && cached is not null)
        {
            return Ok(cached);
        }

        var yearAliases = await AcademicYearAliases(year);
        var termAliases = await TermAliases(term);
        var query = _context.DashboardMetricObservations.AsNoTracking()
            .Include(o => o.Metric).Where(o => o.Metric.IsActive);
        query = Apply(query, o => o.AcademicYear, yearAliases);
        query = Apply(query, o => o.Term, termAliases);
        query = Apply(query, o => o.MeasurementCycle, cycle);
        query = Apply(query, o => o.Campus, campus);
        query = Apply(query, o => o.EducationLevel, level);
        query = Apply(query, o => o.GradeLevel, grade);
        query = Apply(query, o => o.Homeroom, homeroom);
        query = Apply(query, o => o.Program, program);
        query = Apply(query, o => o.Subject, subject);
        if (studentId.HasValue) query = query.Where(o => o.StudentProfileId == studentId);

        var rows = await query.OrderBy(o => o.PeriodStart).ToListAsync();
        var result = rows.GroupBy(o => new
        {
            o.Metric.Id,
            o.Metric.Key,
            o.Metric.Label,
            o.Metric.Domain,
            o.Metric.Source,
            o.Metric.Unit
        }).Select(group =>
        {
            var points = group.GroupBy(o => o.PeriodStart).Select(period => new DashboardMetricPointDto
            {
                PeriodStart = period.Key, Value = period.Sum(item => item.Value)
            }).OrderBy(point => point.PeriodStart).ToList();
            return new DashboardMetricDto
            {
                Id = group.Key.Key, Label = group.Key.Label, Category = group.Key.Domain,
                Source = group.Key.Source, Dataset = group.Key.Source, Unit = group.Key.Unit,
                Value = points.LastOrDefault()?.Value,
                Trend = points.Count > 1 ? points[^1].Value - points[^2].Value : null,
                BaselineValue = group.OrderByDescending(o => o.PeriodStart).First().BaselineValue,
                KpiTarget = group.OrderByDescending(o => o.PeriodStart).First().KpiTarget,
                SupportsDrilldown = group.All(o => o.StudentProfileId != null),
                Series = points
            };
        }).ToList();

        result.AddRange(await ImportedDataMetrics(
            year, term, cycle, campus, level, grade, homeroom, studentId, program, subject,
            yearAliases, termAliases));
        result.AddRange(await CanvasDataMetrics(
            year, term, cycle, campus, level, grade, homeroom, studentId, program, subject,
            yearAliases, termAliases));
        result = result
            .OrderBy(metric => metric.Category)
            .ThenBy(metric => metric.Label)
            .ToList();
        _cache.Set(cacheKey, result, TimeSpan.FromMinutes(5));
        return Ok(result);
    }

    /// <summary>
    /// Per-student breakdown of a single metric, so the workspace drill-down shows the
    /// students who actually drive the clicked metric (and their attributed value),
    /// rather than a generic activity ranking. Honours the same dashboard filters, plus
    /// the optional clicked month (<paramref name="periodStart"/>).
    /// </summary>
    [HttpGet("metric-drilldown")]
    public async Task<ActionResult<MetricDrilldownDto>> MetricDrilldown(
        [FromQuery] string metricId,
        [FromQuery] string? year, [FromQuery] string? term, [FromQuery] string? cycle,
        [FromQuery] string? campus, [FromQuery] string? level, [FromQuery] string? grade,
        [FromQuery] string? homeroom, [FromQuery] int? studentId,
        [FromQuery] string? program, [FromQuery] string? subject,
        [FromQuery] DateTime? periodStart,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(metricId))
        {
            return BadRequest("metricId is required.");
        }
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        // Optional month window matching the clicked chart column.
        DateTime? monthStart = periodStart.HasValue
            ? new DateTime(periodStart.Value.Year, periodStart.Value.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            : null;
        var monthEnd = monthStart?.AddMonths(1);
        bool InMonth(DateTime? date) => monthStart == null ||
            (date.HasValue && date.Value >= monthStart && date.Value < monthEnd);

        var hasYear = HasFilter(year);
        var hasTerm = HasFilter(term);
        (string Year, string Term, DateTime Start, DateTime End)? selectedPeriod = null;
        if (hasYear || hasTerm)
        {
            selectedPeriod = await ResolvePeriod(year, term, DateTime.UtcNow);
        }
        var yearAliases = await AcademicYearAliases(year);
        var termAliases = await TermAliases(term);

        var samples = new List<MetricSample>();
        var aggregation = "count";
        var unit = "records";

        // ---- Behaviour, project and health metrics (imported warehouse data) ----
        if (metricId is "behaviour-records" or "concern-behaviour-records" or "commendation-records")
        {
            var query = FilterBehaviours(campus, level, grade, homeroom, program, subject,
                    yearAliases, termAliases)
                .Where(item => studentId == null || item.StudentProfileId == studentId);
            if (HasFilter(cycle)) query = query.Where(item => item.MeasurementCycle == cycle);
            if (hasYear && selectedPeriod.HasValue)
                query = query.WhereMatchesAny(item => item.AcademicYear, yearAliases,
                    item => item.AcademicYear == null && item.IncidentDate >= selectedPeriod.Value.Start &&
                        item.IncidentDate < selectedPeriod.Value.End);
            if (hasTerm && selectedPeriod.HasValue)
                query = query.WhereMatchesAny(item => item.Term, termAliases,
                    item => item.Term == null && item.IncidentDate >= selectedPeriod.Value.Start &&
                        item.IncidentDate < selectedPeriod.Value.End);
            if (monthStart.HasValue)
                query = query.Where(item => item.IncidentDate >= monthStart.Value && item.IncidentDate < monthEnd!.Value);
            var rows = await query
                .Select(item => new { item.StudentProfileId, item.IncidentDate, item.BehaviourType,
                    item.Notes, item.RecordedByName, item.Subject })
                .ToListAsync();
            samples.AddRange(rows
                .Where(item => metricId switch
                {
                    "concern-behaviour-records" =>
                        StudentClassificationService.IsConcerningBehaviourType(item.BehaviourType),
                    "commendation-records" =>
                        StudentClassificationService.IsCommendationBehaviourType(item.BehaviourType),
                    _ => true
                })
                .Select(item => new MetricSample(item.StudentProfileId, item.IncidentDate, 1.0,
                    DetailFields(("Loại hành vi", StudentClassificationService.CanonicalBehaviourTypeOrOriginal(item.BehaviourType)), ("Ghi chú", item.Notes),
                        ("Môn học", item.Subject), ("Ghi nhận bởi", item.RecordedByName)))));
        }
        else if (metricId is "project-records" or "active-project-records")
        {
            var query = FilterProjects(campus, level, grade, homeroom, program, subject,
                    yearAliases, termAliases)
                .Where(item => studentId == null || item.StudentProfileId == studentId);
            if (HasFilter(cycle)) query = query.Where(item => item.MeasurementCycle == cycle);
            if (hasYear && selectedPeriod.HasValue)
                query = query.WhereMatchesAny(item => item.AcademicYear, yearAliases,
                    item => item.AcademicYear == null &&
                        (item.ActivityDate ?? item.CreatedAt) >= selectedPeriod.Value.Start &&
                        (item.ActivityDate ?? item.CreatedAt) < selectedPeriod.Value.End);
            if (hasTerm && selectedPeriod.HasValue)
                query = query.WhereMatchesAny(item => item.Term, termAliases,
                    item => item.Term == null &&
                        (item.ActivityDate ?? item.CreatedAt) >= selectedPeriod.Value.Start &&
                        (item.ActivityDate ?? item.CreatedAt) < selectedPeriod.Value.End);
            if (monthStart.HasValue)
                query = query.Where(item => (item.ActivityDate ?? item.CreatedAt) >= monthStart.Value &&
                    (item.ActivityDate ?? item.CreatedAt) < monthEnd!.Value);
            var rows = await query
                .Select(item => new { item.StudentProfileId, item.ActivityDate, item.CreatedAt, item.Status,
                    item.ProjectName, item.Subject, item.TeacherNotes })
                .ToListAsync();
            samples.AddRange(rows
                .Where(item => metricId != "active-project-records" ||
                    !StudentDataEntryOptions.IsCompletedProjectStatus(item.Status))
                .Select(item => new MetricSample(item.StudentProfileId, item.ActivityDate ?? item.CreatedAt, 1.0,
                    DetailFields(("Dự án", item.ProjectName), ("Trạng thái", item.Status),
                        ("Môn học", item.Subject), ("Ghi chú", item.TeacherNotes)))));
        }
        else if (metricId is "mental-health-records" or "high-risk-mental-health")
        {
            // Mental health records carry no subject/cycle dimension.
            if (HasFilter(subject) || HasFilter(cycle))
            {
                return Ok(EmptyDrilldown(metricId));
            }
            var query = FilterMentalHealth(campus, level, grade, homeroom, program, null)
                .Where(item => studentId == null || item.StudentProfileId == studentId);
            if (hasYear) query = query.WhereMatchesAny(item => item.AcademicYear, yearAliases);
            if (hasTerm && selectedPeriod.HasValue)
                query = query.Where(item => item.RecordDate >= selectedPeriod.Value.Start &&
                    item.RecordDate < selectedPeriod.Value.End);
            if (monthStart.HasValue)
                query = query.Where(item => item.RecordDate >= monthStart.Value && item.RecordDate < monthEnd!.Value);
            var rows = await query
                .Select(item => new { item.StudentProfileId, item.RecordDate, item.RiskLevel,
                    item.ConcernType, item.Notes, item.RecordedByName })
                .ToListAsync();
            samples.AddRange(rows
                .Where(item => metricId != "high-risk-mental-health"
                    || string.Equals(item.RiskLevel, "High", StringComparison.OrdinalIgnoreCase))
                .Select(item => new MetricSample(item.StudentProfileId, item.RecordDate, 1.0,
                    DetailFields(("Loại vấn đề", item.ConcernType), ("Mức độ rủi ro", item.RiskLevel),
                        ("Ghi chú", item.Notes), ("Ghi nhận bởi", item.RecordedByName)))));
        }
        else if (metricId == "health-visit-records")
        {
            // Health clinic visits (from SIS) carry no subject/cycle/year dimension of their own.
            if (HasFilter(subject) || HasFilter(cycle))
            {
                return Ok(EmptyDrilldown(metricId));
            }
            var query = FilterSISHealthVisits(campus, level, grade, homeroom, program, null)
                .Where(item => item.SISUserId != null && (studentId == null || item.Student!.Id == studentId));
            if ((hasYear || hasTerm) && selectedPeriod.HasValue)
                query = query.Where(item => item.ExamTime >= selectedPeriod.Value.Start &&
                    item.ExamTime < selectedPeriod.Value.End);
            if (monthStart.HasValue)
                query = query.Where(item => item.ExamTime >= monthStart.Value && item.ExamTime < monthEnd!.Value);
            var rows = await query
                .Select(item => new { StudentProfileId = item.Student!.Id, item.ExamTime, item.Symptoms,
                    item.Diagnosis, item.Classification, item.Treatment, item.Medication, item.ExaminedBy })
                .ToListAsync();
            samples.AddRange(rows.Select(item => new MetricSample(item.StudentProfileId, item.ExamTime, 1.0,
                DetailFields(("Triệu chứng", item.Symptoms), ("Chẩn đoán", item.Diagnosis),
                    ("Phân loại", item.Classification), ("Xử trí", item.Treatment),
                    ("Thuốc", item.Medication), ("Khám bởi", item.ExaminedBy)))));
        }
        else if (metricId is "bus-late-records" or "bus-seatbelt-violations" or
                 "bus-late-to-school" or "bus-late-from-school")
        {
            if (HasFilter(subject) || HasFilter(cycle))
            {
                return Ok(EmptyDrilldown(metricId));
            }
            var query = FilterBusAttendance(campus, level, grade, homeroom, program,
                    yearAliases, termAliases)
                .Where(item => studentId == null || item.StudentProfileId == studentId);
            if (hasYear && selectedPeriod.HasValue)
                query = query.WhereMatchesAny(item => item.AcademicYear, yearAliases,
                    item => item.AcademicYear == null && item.AttendanceDate >= selectedPeriod.Value.Start &&
                        item.AttendanceDate < selectedPeriod.Value.End);
            if (hasTerm && selectedPeriod.HasValue)
                query = query.WhereMatchesAny(item => item.Term, termAliases,
                    item => item.Term == null && item.AttendanceDate >= selectedPeriod.Value.Start &&
                        item.AttendanceDate < selectedPeriod.Value.End);
            if (monthStart.HasValue)
                query = query.Where(item => item.AttendanceDate >= monthStart.Value && item.AttendanceDate < monthEnd!.Value);
            var rows = await query
                .Select(item => new { item.StudentProfileId, item.AttendanceDate, item.ViolationType,
                    item.TripDirection, item.BusCode, item.RouteName, item.Notes, item.RecordedByName })
                .ToListAsync();
            samples.AddRange(rows.Where(item => metricId switch
                {
                    "bus-seatbelt-violations" => item.ViolationType == "No seatbelt",
                    "bus-late-to-school" => item.ViolationType == "Late" && item.TripDirection == "To school",
                    "bus-late-from-school" => item.ViolationType == "Late" && item.TripDirection == "From school",
                    _ => item.ViolationType == "Late"
                })
                .Select(item => new MetricSample(item.StudentProfileId, item.AttendanceDate, 1.0,
                    DetailFields(("Vi phạm", item.ViolationType), ("Chiều đi", item.TripDirection),
                        ("Mã xe", item.BusCode), ("Tuyến", item.RouteName), ("Ghi chú", item.Notes),
                        ("Ghi nhận bởi", item.RecordedByName)))));
        }
        // ---- Canvas assignment/submission metrics ----
        else if (metricId.StartsWith("canvas-assignment", StringComparison.Ordinal) ||
                 metricId == "canvas-submission-score-percent-avg")
        {
            if (HasFilter(cycle))
            {
                return Ok(EmptyDrilldown(metricId));
            }
            var query = FilterCanvasSubmissions(campus, level, grade, homeroom, program, subject,
                    yearAliases, hasTerm ? selectedPeriod : null)
                .Where(item => studentId == null || item.StudentProfileId == studentId);
            if (monthStart.HasValue)
            {
                query = metricId switch
                {
                    "canvas-assignments-submitted" => query.Where(item => item.SubmittedAt >= monthStart.Value && item.SubmittedAt < monthEnd!.Value),
                    "canvas-assignments-missing" => query.Where(item =>
                        (item.CachedDueDate ?? item.Assignment!.DueAt) >= monthStart.Value &&
                        (item.CachedDueDate ?? item.Assignment!.DueAt) < monthEnd!.Value),
                    "canvas-assignments-late" => query.Where(item => item.SubmittedAt >= monthStart.Value && item.SubmittedAt < monthEnd!.Value),
                    _ => query.Where(item => (item.GradedAt ?? item.SubmittedAt) >= monthStart.Value &&
                        (item.GradedAt ?? item.SubmittedAt) < monthEnd!.Value)
                };
            }
            var rows = await query
                .Select(item => new CanvasMetricRow(
                    item.StudentProfileId, item.WorkflowState, item.Late, item.Missing, item.Excused,
                    item.Score, item.Grade, item.Assignment!.PointsPossible, item.SubmittedAt, item.GradedAt,
                    item.CachedDueDate ?? item.Assignment.DueAt,
                    item.Assignment.Course!.Name, item.Assignment.Name
                ))
                .ToListAsync();

            switch (metricId)
            {
                case "canvas-assignments-submitted":
                    unit = "submissions";
                    samples.AddRange(rows.Where(r => r.SubmittedAt != null && r.StudentProfileId != null)
                        .Select(r => CanvasSample(r, r.SubmittedAt!.Value, 1.0)));
                    break;
                case "canvas-assignments-graded":
                    unit = "submissions";
                    samples.AddRange(rows.Where(r =>
                            string.Equals(r.WorkflowState, "graded", StringComparison.OrdinalIgnoreCase)
                            && r.StudentProfileId != null && (r.GradedAt != null || r.SubmittedAt != null))
                        .Select(r => CanvasSample(r, (r.GradedAt ?? r.SubmittedAt)!.Value, 1.0)));
                    break;
                case "canvas-assignments-missing":
                    unit = "assignments";
                    samples.AddRange(rows.Where(r => r.Missing && r.Excused != true && r.DueAt != null && r.StudentProfileId != null)
                        .Select(r => CanvasSample(r, r.DueAt!.Value, 1.0)));
                    break;
                case "canvas-assignments-late":
                    unit = "submissions";
                    samples.AddRange(rows.Where(r => r.Late && r.SubmittedAt != null && r.StudentProfileId != null)
                        .Select(r => CanvasSample(r, r.SubmittedAt!.Value, 1.0)));
                    break;
                case "canvas-assignment-score-avg": // Backward-compatible alias for saved reports.
                case "canvas-submission-score-percent-avg":
                    aggregation = "average";
                    unit = "%";
                    samples.AddRange(rows.Where(r =>
                            string.Equals(r.WorkflowState, "graded", StringComparison.OrdinalIgnoreCase)
                            && r.Score != null && r.PointsPossible is > 0 && r.StudentProfileId != null)
                        .Where(r => r.GradedAt != null || r.SubmittedAt != null)
                        .Select(r => CanvasSample(r, (r.GradedAt ?? r.SubmittedAt)!.Value,
                            r.Score!.Value / r.PointsPossible!.Value * 100)));
                    break;
                default:
                    return Ok(EmptyDrilldown(metricId));
            }
        }
        // ---- Canvas course grade ----
        else if (metricId is "canvas-course-grade-avg" or "canvas-final-score-avg" or
                 "canvas-current-score-avg")
        {
            if (HasFilter(cycle))
            {
                return Ok(EmptyDrilldown(metricId));
            }
            aggregation = "average";
            unit = "%";
            var rows = await FilterCanvasScores(campus, level, grade, homeroom, program, subject,
                    yearAliases, termAliases)
                .Where(item => studentId == null || item.StudentProfileId == studentId)
                .Where(item => !monthStart.HasValue ||
                    ((item.GradingPeriodEndDate ?? item.Enrollment!.Course!.EndAt) >= monthStart.Value &&
                     (item.GradingPeriodEndDate ?? item.Enrollment!.Course!.EndAt) < monthEnd!.Value))
                .Select(item => new { item.StudentProfileId, item.FinalScore, item.CurrentScore,
                    Date = item.GradingPeriodEndDate ?? item.Enrollment!.Course!.EndAt,
                    CourseName = item.Enrollment!.Course!.Name,
                    item.GradingPeriodTitle })
                .ToListAsync();
            samples.AddRange(rows
                .Where(r => r.StudentProfileId != null &&
                    (metricId == "canvas-current-score-avg" ? r.CurrentScore : r.FinalScore) != null)
                .Select(r => new MetricSample(r.StudentProfileId!.Value, r.Date,
                    (metricId == "canvas-current-score-avg" ? r.CurrentScore : r.FinalScore)!.Value,
                    DetailFields(("Canvas course name", r.CourseName),
                        ("Canvas grading_period.title", r.GradingPeriodTitle),
                        ("Canvas grades.current_score", r.CurrentScore?.ToString("0.##")),
                        ("Canvas grades.final_score", r.FinalScore?.ToString("0.##"))))));
        }
        // ---- Seeded observation-based metrics (SIS / Base ERP / warehouse) ----
        else
        {
            var query = _context.DashboardMetricObservations.AsNoTracking()
                .Include(o => o.Metric)
                .Where(o => o.Metric.Key == metricId && o.StudentProfileId != null);
            query = Apply(query, o => o.AcademicYear, yearAliases);
            query = Apply(query, o => o.Term, termAliases);
            query = Apply(query, o => o.MeasurementCycle, cycle);
            query = Apply(query, o => o.Campus, campus);
            query = Apply(query, o => o.EducationLevel, level);
            query = Apply(query, o => o.GradeLevel, grade);
            query = Apply(query, o => o.Homeroom, homeroom);
            query = Apply(query, o => o.Program, program);
            query = Apply(query, o => o.Subject, subject);
            if (studentId.HasValue) query = query.Where(o => o.StudentProfileId == studentId);
            if (monthStart.HasValue)
                query = query.Where(o => o.PeriodStart >= monthStart.Value && o.PeriodStart < monthEnd!.Value);

            var metricRow = await _context.DashboardMetrics.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Key == metricId);
            unit = metricRow?.Unit ?? "";
            var rows = await query
                .Select(o => new { StudentId = o.StudentProfileId!.Value, o.PeriodStart, o.Value })
                .ToListAsync();
            samples.AddRange(rows.Select(r => new MetricSample(r.StudentId, r.PeriodStart,
                (double)r.Value, DetailFields(("Nguồn", "DashboardMetricObservation")))));
        }

        // Restrict to the clicked month, group per student, then attach student details.
        var scoped = samples.Where(sample => InMonth(sample.Date)).ToList();
        var perStudent = scoped
            .GroupBy(sample => sample.StudentId)
            .Select(group => new
            {
                StudentId = group.Key,
                Value = aggregation == "average"
                    ? Math.Round((decimal)group.Average(item => item.Value), 1)
                    : Math.Round((decimal)group.Sum(item => item.Value), 0)
            })
            .ToDictionary(row => row.StudentId, row => row.Value);

        var totalCount = perStudent.Count;
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);
        page = Math.Min(page, Math.Max(1, totalPages));
        var studentIds = perStudent
            .OrderByDescending(row => row.Value)
            .ThenBy(row => row.Key)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => row.Key)
            .ToList();
        var rank = studentIds.Select((id, index) => new { id, index }).ToDictionary(item => item.id, item => item.index);
        var students = await _context.StudentProfiles.AsNoTracking()
            .Where(student => studentIds.Contains(student.Id))
            .Select(student => new
            {
                student.Id,
                student.SISUserId,
                student.FullName,
                GradeLevel = student.GradeLevel ?? student.CanvasGradeLevel,
                student.Homeroom
            })
            .ToListAsync();

        var recordsByStudent = scoped
            .Where(sample => studentIds.Contains(sample.StudentId))
            .GroupBy(sample => sample.StudentId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(sample => sample.Date)
                    .Take(10)
                    .Select(sample => new DrilldownRecordDto
                    {
                        Date = sample.Date,
                        Value = Math.Round((decimal)sample.Value, aggregation == "average" ? 1 : 0),
                        Fields = sample.Fields
                    })
                    .ToList());
        var recordCounts = scoped.GroupBy(sample => sample.StudentId)
            .ToDictionary(group => group.Key, group => group.Count());

        var result = new MetricDrilldownDto
        {
            MetricId = metricId,
            Label = LabelForMetric(metricId),
            Unit = unit,
            Aggregation = aggregation,
            AggregateValue = scoped.Count == 0
                ? null
                : aggregation == "average"
                    ? Math.Round((decimal)scoped.Average(sample => sample.Value), 1)
                    : Math.Round((decimal)scoped.Sum(sample => sample.Value), 0),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            Students = students
                .Select(student => new MetricDrilldownRowDto
                {
                    StudentId = student.Id,
                    SISUserId = student.SISUserId,
                    FullName = student.FullName,
                    GradeLevel = student.GradeLevel,
                    Homeroom = student.Homeroom,
                    Value = perStudent[student.Id],
                    RecordCount = recordCounts[student.Id],
                    Records = recordsByStudent.GetValueOrDefault(student.Id, [])
                })
                .OrderBy(row => rank[row.StudentId])
                .ToList()
        };
        return Ok(result);
    }

    private MetricDrilldownDto EmptyDrilldown(string metricId) => new()
    {
        MetricId = metricId,
        Label = LabelForMetric(metricId),
        Aggregation = "count",
        Students = []
    };

    private static string LabelForMetric(string metricId) => metricId switch
    {
        "behaviour-records" => "Behaviour records",
        "concern-behaviour-records" => "Concerning behaviour records",
        "commendation-records" => "Commendation records",
        "project-records" => "Student projects",
        "active-project-records" => "Active student projects",
        "mental-health-records" => "Mental health records",
        "high-risk-mental-health" => "High-risk mental health records",
        "health-visit-records" => "Health office visits",
        "bus-late-records" => "Late bus check-ins",
        "bus-seatbelt-violations" => "Bus seatbelt violations",
        "bus-late-to-school" => "Late morning bus check-ins",
        "bus-late-from-school" => "Late afternoon bus check-ins",
        "canvas-course-grade-avg" => "Average Canvas final_score",
        "canvas-final-score-avg" => "Average Canvas final_score",
        "canvas-current-score-avg" => "Average Canvas current_score",
        "canvas-assignment-score-avg" => "Average Canvas submission score percentage",
        "canvas-submission-score-percent-avg" => "Average Canvas submission score percentage",
        "canvas-assignments-submitted" => "Assignments submitted",
        "canvas-assignments-graded" => "Assignments graded",
        "canvas-assignments-missing" => "Missing assignments",
        "canvas-assignments-late" => "Late submissions",
        _ => metricId
    };

    private IQueryable<StudentBehaviour> FilterBehaviours(
        string? campus, string? level, string? grade, string? homeroom,
        string? program, string? subject, IReadOnlyCollection<string> yearAliases,
        IReadOnlyCollection<string> termAliases)
    {
        var query = _context.StudentBehaviours.AsNoTracking().AsQueryable();
        if (HasFilter(campus)) query = query.Where(item => item.Student.Campus == campus);
        if (HasFilter(level)) query = query.Where(item => item.Student.EducationLevel == level);
        if (HasFilter(grade)) query = query.Where(item =>
            (item.Student.GradeLevel ?? item.Student.CanvasGradeLevel) == grade);
        if (HasFilter(homeroom)) query = query.Where(item => item.Student.Homeroom == homeroom);
        if (HasFilter(program))
            query = query.Where(item => item.Program == program ||
                (item.Program == null && item.Student.Program == program));
        if (HasFilter(subject)) query = query.Where(item => item.Subject != null &&
            item.Subject.Trim() == subject!.Trim());
        if (yearAliases.Count > 0)
            query = query.WhereMatchesAny(item => item.AcademicYear, yearAliases,
                item => item.AcademicYear == null);
        if (termAliases.Count > 0)
            query = query.WhereMatchesAny(item => item.Term, termAliases,
                item => item.Term == null);
        return query;
    }

    private IQueryable<StudentProject> FilterProjects(
        string? campus, string? level, string? grade, string? homeroom,
        string? program, string? subject, IReadOnlyCollection<string> yearAliases,
        IReadOnlyCollection<string> termAliases)
    {
        var query = _context.StudentProjects.AsNoTracking().AsQueryable();
        if (HasFilter(campus)) query = query.Where(item => item.Student.Campus == campus);
        if (HasFilter(level)) query = query.Where(item => item.Student.EducationLevel == level);
        if (HasFilter(grade)) query = query.Where(item =>
            (item.Student.GradeLevel ?? item.Student.CanvasGradeLevel) == grade);
        if (HasFilter(homeroom)) query = query.Where(item => item.Student.Homeroom == homeroom);
        if (HasFilter(program))
            query = query.Where(item => item.Program == program ||
                (item.Program == null && item.Student.Program == program));
        if (HasFilter(subject)) query = query.Where(item => item.Subject != null &&
            item.Subject.Trim() == subject!.Trim());
        if (yearAliases.Count > 0)
            query = query.WhereMatchesAny(item => item.AcademicYear, yearAliases,
                item => item.AcademicYear == null);
        if (termAliases.Count > 0)
            query = query.WhereMatchesAny(item => item.Term, termAliases,
                item => item.Term == null);
        return query;
    }

    private IQueryable<StudentMentalHealthRecord> FilterMentalHealth(
        string? campus, string? level, string? grade, string? homeroom, string? program, string? year)
    {
        var query = _context.StudentMentalHealthRecords.AsNoTracking().AsQueryable();
        if (HasFilter(campus)) query = query.Where(item => item.Student.Campus == campus);
        if (HasFilter(level)) query = query.Where(item => item.Student.EducationLevel == level);
        if (HasFilter(grade)) query = query.Where(item =>
            (item.Student.GradeLevel ?? item.Student.CanvasGradeLevel) == grade);
        if (HasFilter(homeroom)) query = query.Where(item => item.Student.Homeroom == homeroom);
        if (HasFilter(program)) query = query.Where(item => item.Student.Program == program);
        if (HasFilter(year))
        {
            var normalizedYear = DashboardPeriodNormalizer.AcademicYear(year!);
            query = query.Where(item => item.AcademicYear == normalizedYear || item.AcademicYear == year);
        }
        return query;
    }

    private IQueryable<SISHealthVisit> FilterSISHealthVisits(
        string? campus, string? level, string? grade, string? homeroom, string? program, string? year)
    {
        var query = _context.SISHealthVisits.AsNoTracking().Where(item => item.SISUserId != null);
        if (HasFilter(campus)) query = query.Where(item => item.Student!.Campus == campus);
        if (HasFilter(level)) query = query.Where(item => item.Student!.EducationLevel == level);
        if (HasFilter(grade)) query = query.Where(item =>
            (item.Student!.GradeLevel ?? item.Student.CanvasGradeLevel) == grade);
        if (HasFilter(homeroom)) query = query.Where(item => item.Student!.Homeroom == homeroom);
        if (HasFilter(program)) query = query.Where(item => item.Student!.Program == program);
        // Health visits store an exam timestamp; scope to the selected September-based school year.
        if (HasFilter(year) && TryAcademicYearRange(year!, out var start, out var end))
        {
            query = query.Where(item => item.ExamTime >= start && item.ExamTime < end);
        }
        return query;
    }

    private IQueryable<StudentBusAttendanceRecord> FilterBusAttendance(
        string? campus, string? level, string? grade, string? homeroom,
        string? program, IReadOnlyCollection<string> yearAliases,
        IReadOnlyCollection<string> termAliases)
    {
        var query = _context.StudentBusAttendanceRecords.AsNoTracking().AsQueryable();
        if (HasFilter(campus)) query = query.Where(item => item.Student.Campus == campus);
        if (HasFilter(level)) query = query.Where(item => item.Student.EducationLevel == level);
        if (HasFilter(grade)) query = query.Where(item =>
            (item.Student.GradeLevel ?? item.Student.CanvasGradeLevel) == grade);
        if (HasFilter(homeroom)) query = query.Where(item => item.Student.Homeroom == homeroom);
        if (HasFilter(program)) query = query.Where(item => item.Student.Program == program);
        if (yearAliases.Count > 0)
            query = query.WhereMatchesAny(item => item.AcademicYear, yearAliases,
                item => item.AcademicYear == null);
        if (termAliases.Count > 0)
            query = query.WhereMatchesAny(item => item.Term, termAliases,
                item => item.Term == null);
        return query;
    }

    private static bool TryAcademicYearRange(string academicYear, out DateTime start, out DateTime end)
    {
        if (!DashboardPeriodNormalizer.IsAcademicYear(academicYear))
        {
            start = default;
            end = default;
            return false;
        }

        (start, end) = DashboardPeriodNormalizer.AcademicYearRange(academicYear);
        return true;
    }

    private IQueryable<CanvasSubmission> FilterCanvasSubmissions(
        string? campus, string? level, string? grade, string? homeroom,
        string? program, string? subject, IReadOnlyCollection<string> yearAliases,
        (string Year, string Term, DateTime Start, DateTime End)? period)
    {
        var query = _context.CanvasSubmissions.AsNoTracking().Where(item => item.StudentProfileId != null);
        if (HasFilter(campus)) query = query.Where(item => item.Student!.Campus == campus);
        if (HasFilter(level)) query = query.Where(item => item.Student!.EducationLevel == level);
        if (HasFilter(grade)) query = query.Where(item =>
            (item.Assignment!.Course!.CanvasGradeLevel ?? item.Student!.GradeLevel ??
             item.Student.CanvasGradeLevel) == grade);
        if (HasFilter(homeroom)) query = query.Where(item => item.Student!.Homeroom == homeroom);
        if (HasFilter(program)) query = query.Where(item => item.Student!.Program == program);
        if (HasFilter(subject)) query = query.Where(item => item.Assignment!.Course!.Name != null &&
            item.Assignment.Course.Name.Trim() == subject!.Trim());
        if (yearAliases.Count > 0)
        {
            var academicYear = DashboardPeriodNormalizer.AcademicYear(yearAliases.First());
            var (yearStart, yearEnd) = DashboardPeriodNormalizer.AcademicYearRange(academicYear);
            query = query.WhereMatchesAny(item => item.Assignment!.Course!.EnrollmentTermName,
                    yearAliases, item =>
                        (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.GradedAt ?? item.SubmittedAt) >= yearStart &&
                        (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.GradedAt ?? item.SubmittedAt) < yearEnd)
                .Where(item =>
                    (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.GradedAt ?? item.SubmittedAt) == null ||
                    (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.GradedAt ?? item.SubmittedAt) >= yearStart &&
                    (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.GradedAt ?? item.SubmittedAt) < yearEnd);
        }
        if (period.HasValue)
            query = query.Where(item =>
                (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.SubmittedAt) >= period.Value.Start &&
                (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.SubmittedAt) < period.Value.End);
        return query;
    }

    private IQueryable<CanvasCourseScore> FilterCanvasScores(
        string? campus, string? level, string? grade, string? homeroom,
        string? program, string? subject, IReadOnlyCollection<string> yearAliases,
        IReadOnlyCollection<string> termAliases)
    {
        var query = _context.CanvasCourseScores.AsNoTracking()
            .Where(item => item.StudentProfileId != null);
        if (HasFilter(campus)) query = query.Where(item => item.Student!.Campus == campus);
        if (HasFilter(level)) query = query.Where(item => item.Student!.EducationLevel == level);
        if (HasFilter(grade)) query = query.Where(item =>
            (item.Enrollment!.Course!.CanvasGradeLevel ?? item.Student!.GradeLevel ??
             item.Student.CanvasGradeLevel) == grade);
        if (HasFilter(homeroom)) query = query.Where(item => item.Student!.Homeroom == homeroom);
        if (HasFilter(program)) query = query.Where(item => item.Student!.Program == program);
        if (HasFilter(subject)) query = query.Where(item => item.Enrollment!.Course!.Name != null &&
            item.Enrollment.Course.Name.Trim() == subject!.Trim());
        if (yearAliases.Count > 0)
        {
            var academicYear = DashboardPeriodNormalizer.AcademicYear(yearAliases.First());
            var (yearStart, yearEnd) = DashboardPeriodNormalizer.AcademicYearRange(academicYear);
            query = query.WhereMatchesAny(item => item.Enrollment!.Course!.EnrollmentTermName,
                yearAliases, item => (item.GradingPeriodEndDate ?? item.Enrollment!.Course!.EndAt) >= yearStart &&
                    (item.GradingPeriodEndDate ?? item.Enrollment!.Course!.EndAt) < yearEnd)
                .Where(item => (item.GradingPeriodEndDate ?? item.Enrollment!.Course!.EndAt) >= yearStart &&
                    (item.GradingPeriodEndDate ?? item.Enrollment!.Course!.EndAt) < yearEnd);
        }
        if (termAliases.Count > 0)
            query = query.WhereMatchesAny(item => item.GradingPeriodTitle, termAliases);
        return query;
    }

    private async Task<List<DashboardMetricDto>> ImportedDataMetrics(
        string? year, string? term, string? cycle, string? campus, string? level, string? grade,
        string? homeroom, int? studentId, string? program, string? subject,
        IReadOnlyCollection<string> yearAliases, IReadOnlyCollection<string> termAliases)
    {
        var behaviours = _context.StudentBehaviours.AsNoTracking().AsQueryable();
        var projects = _context.StudentProjects.AsNoTracking().AsQueryable();
        var mentalHealth = _context.StudentMentalHealthRecords.AsNoTracking().AsQueryable();
        var healthVisits = _context.SISHealthVisits.AsNoTracking().Where(item => item.SISUserId != null);
        var busRecords = _context.StudentBusAttendanceRecords.AsNoTracking().AsQueryable();

        if (studentId.HasValue)
        {
            behaviours = behaviours.Where(item => item.StudentProfileId == studentId.Value);
            projects = projects.Where(item => item.StudentProfileId == studentId.Value);
            mentalHealth = mentalHealth.Where(item => item.StudentProfileId == studentId.Value);
            healthVisits = healthVisits.Where(item => item.Student!.Id == studentId.Value);
            busRecords = busRecords.Where(item => item.StudentProfileId == studentId.Value);
        }
        if (HasFilter(campus))
        {
            behaviours = behaviours.Where(item => item.Student.Campus == campus);
            projects = projects.Where(item => item.Student.Campus == campus);
            mentalHealth = mentalHealth.Where(item => item.Student.Campus == campus);
            healthVisits = healthVisits.Where(item => item.Student!.Campus == campus);
            busRecords = busRecords.Where(item => item.Student.Campus == campus);
        }
        if (HasFilter(level))
        {
            behaviours = behaviours.Where(item => item.Student.EducationLevel == level);
            projects = projects.Where(item => item.Student.EducationLevel == level);
            mentalHealth = mentalHealth.Where(item => item.Student.EducationLevel == level);
            healthVisits = healthVisits.Where(item => item.Student!.EducationLevel == level);
            busRecords = busRecords.Where(item => item.Student.EducationLevel == level);
        }
        if (HasFilter(grade))
        {
            behaviours = behaviours.Where(item =>
                (item.Student.GradeLevel ?? item.Student.CanvasGradeLevel) == grade);
            projects = projects.Where(item =>
                (item.Student.GradeLevel ?? item.Student.CanvasGradeLevel) == grade);
            mentalHealth = mentalHealth.Where(item =>
                (item.Student.GradeLevel ?? item.Student.CanvasGradeLevel) == grade);
            healthVisits = healthVisits.Where(item =>
                (item.Student!.GradeLevel ?? item.Student.CanvasGradeLevel) == grade);
            busRecords = busRecords.Where(item =>
                (item.Student.GradeLevel ?? item.Student.CanvasGradeLevel) == grade);
        }
        if (HasFilter(homeroom))
        {
            behaviours = behaviours.Where(item => item.Student.Homeroom == homeroom);
            projects = projects.Where(item => item.Student.Homeroom == homeroom);
            mentalHealth = mentalHealth.Where(item => item.Student.Homeroom == homeroom);
            healthVisits = healthVisits.Where(item => item.Student!.Homeroom == homeroom);
            busRecords = busRecords.Where(item => item.Student.Homeroom == homeroom);
        }
        if (HasFilter(program))
        {
            behaviours = behaviours.Where(item => item.Program == program ||
                (item.Program == null && item.Student.Program == program));
            projects = projects.Where(item => item.Program == program ||
                (item.Program == null && item.Student.Program == program));
            mentalHealth = mentalHealth.Where(item => item.Student.Program == program);
            healthVisits = healthVisits.Where(item => item.Student!.Program == program);
            busRecords = busRecords.Where(item => item.Student.Program == program);
        }
        if (HasFilter(subject))
        {
            behaviours = behaviours.Where(item => item.Subject != null && item.Subject.Trim() == subject!.Trim());
            projects = projects.Where(item => item.Subject != null && item.Subject.Trim() == subject!.Trim());
            // Mental health and clinic visits have no subject dimension.
            mentalHealth = mentalHealth.Where(_ => false);
            healthVisits = healthVisits.Where(_ => false);
            busRecords = busRecords.Where(_ => false);
        }
        if (HasFilter(cycle))
        {
            behaviours = behaviours.Where(item => item.MeasurementCycle == cycle);
            projects = projects.Where(item => item.MeasurementCycle == cycle);
            // These are event records and do not use a measurement cycle.
            mentalHealth = mentalHealth.Where(_ => false);
            healthVisits = healthVisits.Where(_ => false);
            busRecords = busRecords.Where(_ => false);
        }

        var hasYear = HasFilter(year);
        var hasTerm = HasFilter(term);
        (string Year, string Term, DateTime Start, DateTime End)? period = null;
        if (hasYear || hasTerm)
        {
            period = await ResolvePeriod(year, term, DateTime.UtcNow);
        }
        if (hasYear && period.HasValue)
        {
            var start = period.Value.Start;
            var end = period.Value.End;
            behaviours = behaviours.WhereMatchesAny(item => item.AcademicYear, yearAliases,
                item => item.AcademicYear == null && item.IncidentDate >= start && item.IncidentDate < end);
            projects = projects.WhereMatchesAny(item => item.AcademicYear, yearAliases,
                item => item.AcademicYear == null && (item.ActivityDate ?? item.CreatedAt) >= start &&
                    (item.ActivityDate ?? item.CreatedAt) < end);
            mentalHealth = mentalHealth.WhereMatchesAny(item => item.AcademicYear, yearAliases);
            healthVisits = healthVisits.Where(item => item.ExamTime >= start && item.ExamTime < end);
            busRecords = busRecords.WhereMatchesAny(item => item.AcademicYear, yearAliases,
                item => item.AcademicYear == null && item.AttendanceDate >= start && item.AttendanceDate < end);
        }
        if (hasTerm && period.HasValue)
        {
            var start = period.Value.Start;
            var end = period.Value.End;
            behaviours = behaviours.WhereMatchesAny(item => item.Term, termAliases,
                item => item.Term == null && item.IncidentDate >= start && item.IncidentDate < end);
            projects = projects.WhereMatchesAny(item => item.Term, termAliases,
                item => item.Term == null && (item.ActivityDate ?? item.CreatedAt) >= start &&
                    (item.ActivityDate ?? item.CreatedAt) < end);
            mentalHealth = mentalHealth.Where(item => item.RecordDate >= start && item.RecordDate < end);
            healthVisits = healthVisits.Where(item => item.ExamTime >= start && item.ExamTime < end);
            busRecords = busRecords.WhereMatchesAny(item => item.Term, termAliases,
                item => item.Term == null && item.AttendanceDate >= start && item.AttendanceDate < end);
        }

        var behaviourRows = await behaviours
            .Select(item => new { Date = item.IncidentDate, Type = item.BehaviourType })
            .ToListAsync();
        var projectRows = await projects
            .Select(item => new { Date = item.ActivityDate ?? item.CreatedAt, item.Status })
            .ToListAsync();
        var mentalHealthRows = await mentalHealth
            .Select(item => new { Date = item.RecordDate, item.RiskLevel })
            .ToListAsync();
        var healthVisitRows = await healthVisits
            .Select(item => new { Date = item.ExamTime })
            .ToListAsync();
        var busRows = await busRecords
            .Select(item => new { Date = item.AttendanceDate, item.ViolationType, item.TripDirection })
            .ToListAsync();

        return
        [
            CountMetric("behaviour-records", "Behaviour records", "Compliance", "StudentBehaviours",
                behaviourRows.Select(item => item.Date)),
            CountMetric("concern-behaviour-records", "Concerning behaviour records", "Compliance", "StudentBehaviours",
                behaviourRows.Where(item => StudentClassificationService.IsConcerningBehaviourType(item.Type))
                    .Select(item => item.Date)),
            CountMetric("commendation-records", "Commendation records", "Achievement", "StudentBehaviours",
                behaviourRows.Where(item => StudentClassificationService.IsCommendationBehaviourType(item.Type))
                    .Select(item => item.Date)),
            CountMetric("project-records", "Student projects", "Achievement", "StudentProjects",
                projectRows.Select(item => item.Date)),
            CountMetric("active-project-records", "Active student projects", "Achievement", "StudentProjects",
                projectRows.Where(item => !StudentDataEntryOptions.IsCompletedProjectStatus(item.Status))
                    .Select(item => item.Date)),
            CountMetric("mental-health-records", "Mental health records", "Safety", "StudentMentalHealth",
                mentalHealthRows.Select(item => item.Date)),
            CountMetric("high-risk-mental-health", "High-risk mental health records", "Safety", "StudentMentalHealth",
                mentalHealthRows.Where(item => string.Equals(item.RiskLevel, "High", StringComparison.OrdinalIgnoreCase))
                    .Select(item => item.Date)),
            CountMetric("health-visit-records", "Health office visits", "Safety", "SISHealthVisits",
                healthVisitRows.Select(item => item.Date)),
            CountMetric("bus-late-records", "Late bus check-ins", "Safety", "StudentBusAttendance",
                busRows.Where(item => item.ViolationType == "Late").Select(item => item.Date)),
            CountMetric("bus-seatbelt-violations", "Bus seatbelt violations", "Safety", "StudentBusAttendance",
                busRows.Where(item => item.ViolationType == "No seatbelt").Select(item => item.Date)),
            CountMetric("bus-late-to-school", "Late morning bus check-ins", "Safety", "StudentBusAttendance",
                busRows.Where(item => item.ViolationType == "Late" && item.TripDirection == "To school")
                    .Select(item => item.Date)),
            CountMetric("bus-late-from-school", "Late afternoon bus check-ins", "Safety", "StudentBusAttendance",
                busRows.Where(item => item.ViolationType == "Late" && item.TripDirection == "From school")
                    .Select(item => item.Date))
        ];
    }

    /// <summary>
    /// Metrics derived live from the Canvas warehouse tables (grades and assignments).
    /// These replace the retired Canvas "assumption" demo metrics. They honour the same
    /// visibility and dashboard filters as the other imported sources.
    /// </summary>
    private async Task<List<DashboardMetricDto>> CanvasDataMetrics(
        string? year, string? term, string? cycle, string? campus, string? level, string? grade,
        string? homeroom, int? studentId, string? program, string? subject,
        IReadOnlyCollection<string> yearAliases, IReadOnlyCollection<string> termAliases)
    {
        // Canvas records carry no measurement cycle, so a cycle-scoped report has no Canvas rows.
        if (HasFilter(cycle))
        {
            return [];
        }

        var submissions = _context.CanvasSubmissions.AsNoTracking()
            .Where(item => item.StudentProfileId != null).AsQueryable();
        var scores = _context.CanvasCourseScores.AsNoTracking()
            .Where(item => item.StudentProfileId != null).AsQueryable();

        if (studentId.HasValue)
        {
            submissions = submissions.Where(item => item.StudentProfileId == studentId.Value);
            scores = scores.Where(item => item.StudentProfileId == studentId.Value);
        }
        if (HasFilter(campus))
        {
            submissions = submissions.Where(item => item.Student!.Campus == campus);
            scores = scores.Where(item => item.Student!.Campus == campus);
        }
        if (HasFilter(level))
        {
            submissions = submissions.Where(item => item.Student!.EducationLevel == level);
            scores = scores.Where(item => item.Student!.EducationLevel == level);
        }
        if (HasFilter(grade))
        {
            submissions = submissions.Where(item =>
                (item.Assignment!.Course!.CanvasGradeLevel ?? item.Student!.GradeLevel ??
                 item.Student.CanvasGradeLevel) == grade);
            scores = scores.Where(item =>
                (item.Enrollment!.Course!.CanvasGradeLevel ?? item.Student!.GradeLevel ??
                 item.Student.CanvasGradeLevel) == grade);
        }
        if (HasFilter(homeroom))
        {
            submissions = submissions.Where(item => item.Student!.Homeroom == homeroom);
            scores = scores.Where(item => item.Student!.Homeroom == homeroom);
        }
        if (HasFilter(program))
        {
            submissions = submissions.Where(item => item.Student!.Program == program);
            scores = scores.Where(item => item.Student!.Program == program);
        }
        if (HasFilter(subject))
        {
            submissions = submissions.Where(item => item.Assignment!.Course!.Name != null &&
                item.Assignment.Course.Name.Trim() == subject!.Trim());
            scores = scores.Where(item => item.Enrollment!.Course!.Name != null &&
                item.Enrollment.Course.Name.Trim() == subject!.Trim());
        }
        if (HasFilter(year))
        {
            var academicYear = DashboardPeriodNormalizer.AcademicYear(year!);
            var (yearStart, yearEnd) = DashboardPeriodNormalizer.AcademicYearRange(academicYear);
            submissions = submissions.WhereMatchesAny(
                    item => item.Assignment!.Course!.EnrollmentTermName, yearAliases, item =>
                        (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.GradedAt ?? item.SubmittedAt) >= yearStart &&
                        (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.GradedAt ?? item.SubmittedAt) < yearEnd)
                .Where(item =>
                    (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.GradedAt ?? item.SubmittedAt) == null ||
                    (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.GradedAt ?? item.SubmittedAt) >= yearStart &&
                    (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.GradedAt ?? item.SubmittedAt) < yearEnd);
            scores = scores.WhereMatchesAny(item => item.Enrollment!.Course!.EnrollmentTermName,
                yearAliases, item => (item.GradingPeriodEndDate ?? item.Enrollment!.Course!.EndAt) >= yearStart &&
                    (item.GradingPeriodEndDate ?? item.Enrollment!.Course!.EndAt) < yearEnd)
                .Where(item => (item.GradingPeriodEndDate ?? item.Enrollment!.Course!.EndAt) >= yearStart &&
                    (item.GradingPeriodEndDate ?? item.Enrollment!.Course!.EndAt) < yearEnd);
        }
        if (HasFilter(term))
        {
            var period = await ResolvePeriod(year, term, DateTime.UtcNow);
            submissions = submissions.Where(item =>
                (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.SubmittedAt) >= period.Start &&
                (item.CachedDueDate ?? item.Assignment!.DueAt ?? item.SubmittedAt) < period.End);
            scores = scores.WhereMatchesAny(item => item.GradingPeriodTitle, termAliases);
        }

        var submissionRows = await submissions.Select(item => new
        {
            item.WorkflowState, item.Late, item.Missing, item.Excused,
            item.Score, PointsPossible = item.Assignment!.PointsPossible,
            item.SubmittedAt, item.GradedAt,
            DueAt = item.CachedDueDate ?? item.Assignment.DueAt
        }).ToListAsync();
        var scoreRows = await scores.Select(item => new
        {
            item.FinalScore, item.CurrentScore,
            Date = item.GradingPeriodEndDate ?? item.Enrollment!.Course!.EndAt
        }).ToListAsync();

        (DateTime Start, DateTime End)? selectedYearRange = null;
        if (HasFilter(year))
        {
            selectedYearRange = DashboardPeriodNormalizer.AcademicYearRange(
                DashboardPeriodNormalizer.AcademicYear(year!));
        }
        DateTime? ReportingDate(DateTime? eventDate, DateTime? dueDate)
        {
            if (!selectedYearRange.HasValue) return eventDate;
            var range = selectedYearRange.Value;
            if (eventDate >= range.Start && eventDate < range.End) return eventDate;
            if (dueDate >= range.Start && dueDate < range.End) return dueDate;
            return null;
        }

        // Use each submission's most meaningful timeline date for the monthly series.
        var gradedSubs = submissionRows
            .Where(item => string.Equals(item.WorkflowState, "graded", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var finalScorePoints = scoreRows
            .Where(item => item.FinalScore != null && item.Date != null)
            .Select(item => (Date: item.Date!.Value, Value: item.FinalScore!.Value));
        var currentScorePoints = scoreRows
            .Where(item => item.CurrentScore != null && item.Date != null)
            .Select(item => (Date: item.Date!.Value, Value: item.CurrentScore!.Value));

        var assignmentScorePoints = gradedSubs
            .Where(item => item.Score != null && item.PointsPossible is > 0 &&
                (item.GradedAt != null || item.SubmittedAt != null))
            .Select(item => (Item: item, Date: ReportingDate(item.GradedAt ?? item.SubmittedAt, item.DueAt)))
            .Where(item => item.Date != null)
            .Select(item => (Date: item.Date!.Value,
                Value: item.Item.Score!.Value / item.Item.PointsPossible!.Value * 100));

        var finalScoreMetric = DocumentMetric(AverageMetric(
                "canvas-final-score-avg", "Average Canvas final_score", "Academics", "Canvas", "%",
                finalScorePoints, "CanvasEnrollmentGrades"),
            "Average of non-null Canvas grades.final_score values; zero is preserved. The monthly series uses the Canvas grading-period end_date, falling back to the Canvas course end_at for an overall course grade.",
            "enrollment.grades.final_score", "grading_period.end_date", "course.end_at");
        var allFinalScores = scoreRows.Where(item => item.FinalScore != null)
            .Select(item => item.FinalScore!.Value).ToList();
        finalScoreMetric.Value = allFinalScores.Count == 0
            ? null
            : Math.Round((decimal)allFinalScores.Average(), 1);

        var currentScoreMetric = DocumentMetric(AverageMetric(
                "canvas-current-score-avg", "Average Canvas current_score", "Academics", "Canvas", "%",
                currentScorePoints, "CanvasEnrollmentGrades"),
            "Average of non-null Canvas grades.current_score values; zero is preserved. The monthly series uses the Canvas grading-period end_date, falling back to the Canvas course end_at for an overall course grade.",
            "enrollment.grades.current_score", "grading_period.end_date", "course.end_at");
        var allCurrentScores = scoreRows.Where(item => item.CurrentScore != null)
            .Select(item => item.CurrentScore!.Value).ToList();
        currentScoreMetric.Value = allCurrentScores.Count == 0
            ? null
            : Math.Round((decimal)allCurrentScores.Average(), 1);

        var submissionScoreMetric = DocumentMetric(AverageMetric(
                "canvas-submission-score-percent-avg", "Average Canvas submission score percentage",
                "Academics", "Canvas", "%", assignmentScorePoints, "CanvasSubmissionsAndAssignments"),
            "Reporting calculation: average of Canvas submission.score / Canvas assignment.points_possible × 100 for submissions whose Canvas workflow_state is graded.",
            "submission.score", "submission.workflow_state", "submission.graded_at",
            "submission.submitted_at", "assignment.points_possible");

        var submittedMetric = DocumentMetric(CountMetric(
                "canvas-assignments-submitted", "Canvas submissions with submitted_at", "Academics", "Canvas",
                submissionRows.Where(item => item.SubmittedAt != null)
                    .Select(item => ReportingDate(item.SubmittedAt, item.DueAt))
                    .Where(date => date != null).Select(date => date!.Value),
                "submissions", "CanvasSubmissionsAndAssignments"),
            "Count of Canvas submission records whose submitted_at is non-null.",
            "submission.submitted_at");
        var datedGradedRows = gradedSubs
            .Select(item => new { Item = item, Date = ReportingDate(item.GradedAt ?? item.SubmittedAt, item.DueAt) })
            .Where(item => item.Date != null)
            .ToList();
        var gradedMetric = DocumentMetric(CountMetric(
                "canvas-assignments-graded", "Canvas submissions with workflow_state=graded", "Academics", "Canvas",
                datedGradedRows.Select(item => item.Date!.Value),
                "submissions", "CanvasSubmissionsAndAssignments"),
            "Count of Canvas submission records whose workflow_state is graded. Undated graded records contribute to the total but not the monthly series.",
            "submission.workflow_state", "submission.graded_at", "submission.submitted_at");
        gradedMetric.Value = selectedYearRange.HasValue ? datedGradedRows.Count : gradedSubs.Count;

        var missingRows = submissionRows.Where(item => item.Missing && item.Excused != true).ToList();
        var missingMetric = DocumentMetric(CountMetric(
                "canvas-assignments-missing", "Canvas submissions flagged missing", "Academics", "Canvas",
                missingRows.Where(item => item.DueAt != null).Select(item => item.DueAt!.Value),
                "submissions", "CanvasSubmissionsAndAssignments"),
            "Count of Canvas submission records where missing=true and excused is not true. Records without a Canvas due date contribute to the total but not the monthly series.",
            "submission.missing", "submission.excused", "submission.cached_due_date", "assignment.due_at");
        missingMetric.Value = missingRows.Count;

        var lateRows = submissionRows.Where(item => item.Late)
            .Select(item => new { Item = item, Date = ReportingDate(item.SubmittedAt, item.DueAt) })
            .Where(item => !selectedYearRange.HasValue || item.Date != null)
            .ToList();
        var lateMetric = DocumentMetric(CountMetric(
                "canvas-assignments-late", "Canvas submissions flagged late", "Academics", "Canvas",
                lateRows.Where(item => item.Date != null).Select(item => item.Date!.Value),
                "submissions", "CanvasSubmissionsAndAssignments"),
            "Count of Canvas submission records where late=true. Records without submitted_at contribute to the total but not the monthly series.",
            "submission.late", "submission.submitted_at");
        lateMetric.Value = lateRows.Count;

        return
        [
            finalScoreMetric,
            currentScoreMetric,
            submissionScoreMetric,
            submittedMetric,
            gradedMetric,
            missingMetric,
            lateMetric
        ];
    }

    private static DashboardMetricDto DocumentMetric(
        DashboardMetricDto metric, string definition, params string[] sourceFields)
    {
        metric.Definition = definition;
        metric.SourceFields = sourceFields.ToList();
        return metric;
    }

    private static DashboardMetricDto AverageMetric(
        string id, string label, string category, string source, string unit,
        IEnumerable<(DateTime Date, double Value)> samples, string? dataset = null)
    {
        var values = samples.ToList();
        var points = values
            .GroupBy(sample => new DateTime(sample.Date.Year, sample.Date.Month, 1, 0, 0, 0, DateTimeKind.Utc))
            .OrderBy(group => group.Key)
            .Select(group => new DashboardMetricPointDto
            {
                PeriodStart = group.Key,
                Value = Math.Round((decimal)group.Average(sample => sample.Value), 1)
            })
            .ToList();

        return new DashboardMetricDto
        {
            Id = id,
            Label = label,
            Category = category,
            Source = source,
            Dataset = dataset ?? source,
            Unit = unit,
            Value = values.Count == 0 ? null : Math.Round((decimal)values.Average(sample => sample.Value), 1),
            Trend = points.Count > 1 ? points[^1].Value - points[^2].Value : null,
            Series = points
        };
    }

    private static DashboardMetricDto CountMetric(
        string id, string label, string category, string source, IEnumerable<DateTime> dates,
        string unit = "records", string? dataset = null)
    {
        var values = dates.ToList();
        var points = values
            .GroupBy(date => new DateTime(date.Year, date.Month, 1, 0, 0, 0, DateTimeKind.Utc))
            .OrderBy(group => group.Key)
            .Select(group => new DashboardMetricPointDto
            {
                PeriodStart = group.Key,
                Value = group.Count()
            })
            .ToList();

        return new DashboardMetricDto
        {
            Id = id,
            Label = label,
            Category = category,
            Source = source,
            Dataset = dataset ?? source,
            Unit = unit,
            Value = values.Count,
            Trend = points.Count > 1 ? points[^1].Value - points[^2].Value : null,
            Series = points
        };
    }

    private static bool HasFilter(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !string.Equals(value, "all", StringComparison.OrdinalIgnoreCase);

    private static string DashboardCacheKey(string operation, params string?[] values) =>
        $"dashboard:{operation}:" + string.Join('|', values.Select(value =>
        {
            var normalized = value?.Trim().ToLowerInvariant() ?? "";
            return $"{normalized.Length}:{normalized}";
        }));

    [HttpGet("overview")]
    public async Task<ActionResult<DashboardOverviewDto>> Overview(
        [FromQuery] string? year,
        [FromQuery] string? term)
    {
        var now = DateTime.UtcNow;
        var period = await ResolvePeriod(year, term, now);

        var students = await VisibleStudents()
            .Include(student => student.Behaviours)
            .Include(student => student.Projects)
            .Include(student => student.BusAttendanceRecords)
            .AsSplitQuery()
            .ToListAsync();
        var settings = await ClassificationSettings();
        var studentIds = students.Select(student => student.Id).ToList();
        var canvasScores = await _context.CanvasCourseScores.AsNoTracking()
            .Where(score => score.StudentProfileId != null
                && studentIds.Contains(score.StudentProfileId.Value)
                && score.FinalScore != null)
            .Select(score => new
            {
                StudentId = score.StudentProfileId!.Value,
                Value = (decimal)score.FinalScore!.Value
            })
            .ToListAsync();
        var canvasAverages = canvasScores
            .GroupBy(score => score.StudentId)
            .ToDictionary(group => group.Key, group => Math.Round(group.Average(score => score.Value), 1));

        var overview = new DashboardOverviewDto
        {
            PeriodYear = period.Year,
            PeriodTerm = period.Term,
            TotalStudents = students.Count,
            TotalProjects = students.Sum(student => student.Projects.Count(project => InPeriod(project, period))),
            ActiveProjects = students.Sum(student => student.Projects
                .Count(project => InPeriod(project, period) && !StudentDataEntryOptions.IsCompletedProjectStatus(project.Status)))
        };

        var since = now.AddDays(-30);
        var gradeBuckets = new Dictionary<string, DashboardGradeDto>();
        var riskSince = now.AddDays(-settings.RiskWindowDays);
        var commendationSince = now.AddDays(-settings.CommendationWindowDays);
        var attentionCandidates = new List<(StudentProfile Student, string RiskLevel, int SignalCount)>();

        foreach (var student in students)
        {
            var periodBehaviours = student.Behaviours.Where(behaviour => InPeriod(behaviour, period)).ToList();
            var concernCount = student.Behaviours.Count(behaviour => behaviour.IncidentDate >= riskSince
                && StudentClassificationService.IsConcerningBehaviour(behaviour));
            var riskBusViolationCount = student.BusAttendanceRecords.Count(record => record.AttendanceDate >= riskSince);
            var risk = StudentClassificationService.ClassifyRisk(
                new StudentSignalSummary(
                    concernCount,
                    student.Behaviours.Count(behaviour => behaviour.IncidentDate >= commendationSince
                        && StudentClassificationService.IsCommendationBehaviour(behaviour)),
                    canvasAverages.GetValueOrDefault(student.Id),
                    riskBusViolationCount,
                    student.BusAttendanceRecords.Count(record => record.AttendanceDate >= commendationSince)),
                settings);
            switch (risk.Level)
            {
                case "High":
                    overview.AtRiskHigh++;
                    break;
                case "Medium":
                    overview.AtRiskMedium++;
                    break;
                default:
                    overview.OnTrack++;
                    break;
            }

            if (risk.Level != "None")
            {
                attentionCandidates.Add((student, risk.Level, concernCount + riskBusViolationCount));
            }

            foreach (var behaviour in student.Behaviours)
            {
                // Behaviour activity is scoped to the selected reporting period.
                if (InPeriod(behaviour, period))
                {
                    overview.TotalBehaviours++;
                }

                if (behaviour.IncidentDate >= since)
                {
                    overview.BehavioursLast30Days++;
                }
            }

            var gradeKey = string.IsNullOrWhiteSpace(student.GradeLevel) ? "—" : student.GradeLevel!;
            if (!gradeBuckets.TryGetValue(gradeKey, out var bucket))
            {
                bucket = new DashboardGradeDto { Grade = gradeKey };
                gradeBuckets[gradeKey] = bucket;
            }

            bucket.Students++;
            if (risk.Level != "None")
            {
                bucket.AtRisk++;
            }
        }

        overview.AtRiskTotal = overview.AtRiskHigh + overview.AtRiskMedium;
        overview.BehaviourTrend = BuildTrend(students, period.Start, period.End, now);
        overview.GradeDistribution = gradeBuckets.Values
            .OrderBy(grade => grade.Grade, StringComparer.OrdinalIgnoreCase)
            .ToList();
        overview.TopAtRisk = attentionCandidates
            .OrderBy(candidate => candidate.RiskLevel == "High" ? 0 : 1)
            .ThenByDescending(candidate => candidate.SignalCount)
            .ThenBy(candidate => candidate.Student.FullName, StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .Select(candidate => new DashboardAttentionStudentDto
            {
                Id = candidate.Student.Id,
                SISUserId = candidate.Student.SISUserId,
                FullName = candidate.Student.FullName,
                GradeLevel = candidate.Student.GradeLevel ?? candidate.Student.CanvasGradeLevel,
                Homeroom = candidate.Student.Homeroom,
                RiskLevel = candidate.RiskLevel
            })
            .ToList();

        return Ok(overview);
    }

    private async Task<(string Year, string Term, DateTime Start, DateTime End)> ResolvePeriod(
        string? year, string? term, DateTime now)
    {
        var normalizedYear = HasFilter(year) ? DashboardPeriodNormalizer.AcademicYear(year!) : null;
        var normalizedTerm = HasFilter(term) ? DashboardPeriodNormalizer.Term(term!) : null;
        var configured = (await _context.AcademicPeriods.AsNoTracking().ToListAsync())
            .Where(period => normalizedYear == null || DashboardPeriodNormalizer.AcademicYear(period.AcademicYear)
                .Equals(normalizedYear, StringComparison.OrdinalIgnoreCase))
            .Where(period => normalizedTerm == null || DashboardPeriodNormalizer.Term(period.Term)
                .Equals(normalizedTerm, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(period => period.IsCurrent)
            .ThenByDescending(period => period.StartDate)
            .ToList();
        if (configured.Count > 0)
        {
            var selected = configured[0];
            var selectedYear = DashboardPeriodNormalizer.AcademicYear(selected.AcademicYear);
            var selectedYearPeriods = normalizedTerm == null
                ? configured.Where(period => DashboardPeriodNormalizer.AcademicYear(period.AcademicYear)
                    .Equals(selectedYear, StringComparison.OrdinalIgnoreCase)).ToList()
                : [selected];
            return (selectedYear, normalizedTerm ?? "all",
                selectedYearPeriods.Min(period => period.StartDate),
                selectedYearPeriods.Max(period => period.EndDate));
        }

        // The reporting school year begins in September. Default to the year containing "now".
        var startYear = now.Month >= DashboardPeriodNormalizer.SchoolYearStartMonth
            ? now.Year
            : now.Year - 1;
        if (!string.IsNullOrWhiteSpace(year))
        {
            var parts = year.Split('-');
            if (parts.Length > 0 && int.TryParse(parts[0], out var parsed))
            {
                startYear = parsed;
            }
        }

        normalizedTerm = normalizedTerm is "hk1" or "hk2" ? normalizedTerm : "all";

        var canonicalYear = $"{startYear}-{startYear + 1}";
        var (start, end) = DashboardPeriodNormalizer.PeriodRange(canonicalYear, normalizedTerm);

        return (canonicalYear, normalizedTerm, start, end);
    }

    private static bool InPeriod(StudentBehaviour item, (string Year, string Term, DateTime Start, DateTime End) period)
    {
        if (!string.IsNullOrWhiteSpace(item.AcademicYear))
            return DashboardPeriodNormalizer.AcademicYear(item.AcademicYear) == period.Year &&
                (period.Term == "all" || (!string.IsNullOrWhiteSpace(item.Term) &&
                    DashboardPeriodNormalizer.Term(item.Term) == period.Term));
        return item.IncidentDate >= period.Start && item.IncidentDate < period.End;
    }

    private static bool InPeriod(StudentProject item, (string Year, string Term, DateTime Start, DateTime End) period)
    {
        if (!string.IsNullOrWhiteSpace(item.AcademicYear))
            return DashboardPeriodNormalizer.AcademicYear(item.AcademicYear) == period.Year &&
                (period.Term == "all" || (!string.IsNullOrWhiteSpace(item.Term) &&
                    DashboardPeriodNormalizer.Term(item.Term) == period.Term));
        var date = item.ActivityDate ?? item.CreatedAt;
        return date >= period.Start && date < period.End;
    }

    private async Task<StudentClassificationSettingsDto> ClassificationSettings()
    {
        var setting = await _context.StudentClassificationSettings.AsNoTracking()
            .OrderBy(item => item.Id)
            .FirstOrDefaultAsync();
        return setting == null
            ? new StudentClassificationSettingsDto()
            : new StudentClassificationSettingsDto
            {
                RiskWindowDays = setting.RiskWindowDays,
                HighRiskBehaviourCount = setting.HighRiskBehaviourCount,
                HighRiskBusViolationCount = setting.HighRiskBusViolationCount,
                HighRiskCanvasAverageBelow = setting.HighRiskCanvasAverageBelow,
                CommendationWindowDays = setting.CommendationWindowDays,
                CommendationPositiveBehaviourCount = setting.CommendationPositiveBehaviourCount,
                CommendationCanvasAverageAtLeast = setting.CommendationCanvasAverageAtLeast,
                CommendationMaxBusViolations = setting.CommendationMaxBusViolations
            };
    }

    private static List<DashboardTrendPointDto> BuildTrend(
        List<StudentProfile> students, DateTime start, DateTime end, DateTime now)
    {
        var points = new List<DashboardTrendPointDto>();
        var index = new Dictionary<(int Year, int Month), DashboardTrendPointDto>();

        // Show elapsed months of the period only (never future months).
        var cursor = new DateTime(start.Year, start.Month, 1);
        var limit = end < now ? end : now;
        while (cursor <= limit && points.Count < 12)
        {
            var point = new DashboardTrendPointDto { Year = cursor.Year, Month = cursor.Month };
            points.Add(point);
            index[(cursor.Year, cursor.Month)] = point;
            cursor = cursor.AddMonths(1);
        }

        foreach (var behaviour in students.SelectMany(student => student.Behaviours))
        {
            if (index.TryGetValue((behaviour.IncidentDate.Year, behaviour.IncidentDate.Month), out var point))
            {
                point.Total++;
            }
        }

        return points;
    }

    private IQueryable<StudentProfile> VisibleStudents()
        => _context.StudentProfiles.AsNoTracking();

    private static List<string> CleanOptions(IEnumerable<string> values) =>
        values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<string> GradeOptions(IEnumerable<string> values) =>
        values.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => int.TryParse(value, out var grade) ? grade : int.MaxValue)
            .ThenBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private IQueryable<string> AcademicYearValues() =>
        _context.AcademicPeriods.AsNoTracking().Select(item => item.AcademicYear)
            .Concat(_context.DashboardMetricObservations.Where(item => item.AcademicYear != null)
                .Select(item => item.AcademicYear!))
            .Concat(_context.CanvasCourses.Where(item => item.EnrollmentTermName != null)
                .Select(item => item.EnrollmentTermName!))
            .Concat(_context.StudentBehaviours.Where(item => item.AcademicYear != null)
                .Select(item => item.AcademicYear!))
            .Concat(_context.StudentProjects.Where(item => item.AcademicYear != null)
                .Select(item => item.AcademicYear!))
            .Concat(_context.StudentMentalHealthRecords.Select(item => item.AcademicYear))
            .Concat(_context.StudentBusAttendanceRecords.Where(item => item.AcademicYear != null)
                .Select(item => item.AcademicYear!));

    private IQueryable<string> TermValues() =>
        _context.AcademicPeriods.AsNoTracking().Select(item => item.Term)
            .Concat(_context.DashboardMetricObservations.Where(item => item.Term != null)
                .Select(item => item.Term!))
            .Concat(_context.CanvasCourseScores.Where(item => item.GradingPeriodTitle != null)
                .Select(item => item.GradingPeriodTitle!))
            .Concat(_context.StudentBehaviours.Where(item => item.Term != null)
                .Select(item => item.Term!))
            .Concat(_context.StudentProjects.Where(item => item.Term != null)
                .Select(item => item.Term!))
            .Concat(_context.StudentBusAttendanceRecords.Where(item => item.Term != null)
                .Select(item => item.Term!));

    private async Task<List<string>> AcademicYearAliases(string? selected)
    {
        if (!HasFilter(selected)) return [];
        var normalized = DashboardPeriodNormalizer.AcademicYear(selected!);
        var aliases = (await AcademicYearValues().Distinct().ToListAsync())
            .Where(value => DashboardPeriodNormalizer.AcademicYear(value)
                .Equals(normalized, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (aliases.Count == 0) aliases.Add(selected!);
        return aliases;
    }

    private async Task<List<string>> TermAliases(string? selected)
    {
        if (!HasFilter(selected)) return [];
        var normalized = DashboardPeriodNormalizer.Term(selected!);
        var aliases = (await TermValues().Distinct().ToListAsync())
            .Where(value => DashboardPeriodNormalizer.Term(value)
                .Equals(normalized, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (aliases.Count == 0) aliases.Add(selected!);
        return aliases;
    }

    private static IQueryable<DashboardMetricObservation> Apply(
        IQueryable<DashboardMetricObservation> query,
        System.Linq.Expressions.Expression<Func<DashboardMetricObservation, string?>> selector,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "all") return query;
        return query.Where(BuildEquality(selector, value));
    }

    private static IQueryable<DashboardMetricObservation> Apply(
        IQueryable<DashboardMetricObservation> query,
        System.Linq.Expressions.Expression<Func<DashboardMetricObservation, string?>> selector,
        IReadOnlyCollection<string> values)
    {
        if (values.Count == 0) return query;
        System.Linq.Expressions.Expression? body = null;
        foreach (var value in values)
        {
            var equality = System.Linq.Expressions.Expression.Equal(selector.Body,
                System.Linq.Expressions.Expression.Constant(value, typeof(string)));
            body = body == null ? equality : System.Linq.Expressions.Expression.OrElse(body, equality);
        }
        var predicate = System.Linq.Expressions.Expression.Lambda<Func<DashboardMetricObservation, bool>>(
            body!, selector.Parameters);
        return query.Where(predicate);
    }

    private static System.Linq.Expressions.Expression<Func<DashboardMetricObservation, bool>> BuildEquality(
        System.Linq.Expressions.Expression<Func<DashboardMetricObservation, string?>> selector, string value)
    {
        var equals = System.Linq.Expressions.Expression.Equal(selector.Body,
            System.Linq.Expressions.Expression.Constant(value, typeof(string)));
        return System.Linq.Expressions.Expression.Lambda<Func<DashboardMetricObservation, bool>>(equals, selector.Parameters);
    }

    private static DashboardPreferenceDto ToPreferenceDto(DashboardPreference item) => new()
    {
        AcademicYear = NormalizeAcademicYear(item.AcademicYear), Term = NormalizeTerm(item.Term),
        MeasurementCycle = item.MeasurementCycle,
        Campus = item.Campus, EducationLevel = item.EducationLevel, GradeLevel = item.GradeLevel,
        Homeroom = item.Homeroom, StudentProfileId = item.StudentProfileId,
        Program = item.Program, Subject = item.Subject
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeAcademicYear(string? value) =>
        Clean(value) is { } cleaned ? DashboardPeriodNormalizer.AcademicYear(cleaned) : null;

    private static string? NormalizeTerm(string? value) =>
        Clean(value) is { } cleaned ? DashboardPeriodNormalizer.Term(cleaned) : null;
}
