using System.Security.Claims;
using System.Diagnostics;
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
    private readonly InFlightRequestCoalescer _requestCoalescer;
    private readonly ILogger<DashboardController> _logger;
    private readonly CanvasReportingPeriodService _canvasReportingPeriods;
    private readonly UserAccessService _userAccess;

    public DashboardController(
        AppDbContext context,
        IMemoryCache cache,
        InFlightRequestCoalescer requestCoalescer,
        ILogger<DashboardController> logger,
        CanvasReportingPeriodService canvasReportingPeriods,
        UserAccessService userAccess)
    {
        _context = context;
        _cache = cache;
        _requestCoalescer = requestCoalescer;
        _logger = logger;
        _canvasReportingPeriods = canvasReportingPeriods;
        _userAccess = userAccess;
    }

    [HttpGet("filter-options")]
    public async Task<ActionResult<DashboardFilterOptionsDto>> FilterOptions(
        [FromQuery] int? reportId, [FromQuery] bool periodOnly = false,
        [FromQuery] bool deferHeavy = false)
    {
        // The overview dashboard needs only school-year and term choices. Teachers without
        // workspace access must not be able to retrieve academic/student filter dimensions by
        // changing this query string directly.
        if (!_userAccess.Current.CanAccessWorkspace)
        {
            periodOnly = true;
            deferHeavy = false;
        }
        var optionScope = periodOnly ? "periods" : deferHeavy ? "deferred" : "full";
        var cacheKey = $"dashboard-filter-options:{reportId?.ToString() ?? "all"}:{optionScope}:{_userAccess.Current.CacheKey}";
        if (_cache.TryGetValue<DashboardFilterOptionsDto>(cacheKey, out var cached) && cached is not null)
        {
            return Ok(cached);
        }

        var canvasOnly = reportId.HasValue && await IsCanvasOnlyReport(reportId.Value);
        var (academicYears, terms) = await LoadFilterPeriods(canvasOnly);
        var scopedObservations = _userAccess.ApplyStudentScope(
            _context.DashboardMetricObservations.AsNoTracking(), item => item.Student);
        var scopedBehaviours = _userAccess.ApplyStudentScope(
            _context.StudentBehaviours.AsNoTracking(), item => item.Student);
        var scopedProjects = _userAccess.ApplyStudentScope(
            _context.StudentProjects.AsNoTracking(), item => item.Student);

        // The overview dashboard only has year and term selectors. Avoid making its initial
        // render wait for the much larger student/course option set used by report workspaces.
        if (periodOnly)
        {
            var periodResult = new DashboardFilterOptionsDto
            {
                AcademicYears = academicYears,
                Terms = terms
            };
            _cache.Set(cacheKey, periodResult, AppMemoryCache.Entry(TimeSpan.FromMinutes(5)));
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
                : await scopedObservations
                    .Select(item => new { item.Campus, item.EducationLevel, item.GradeLevel })
                    .Distinct()
                    .ToListAsync();
            var deferredImportedPrograms = canvasOnly
                ? []
                : await scopedObservations
                    .Where(item => item.Program != null).Select(item => item.Program!)
                    .Concat(scopedBehaviours.Where(item => item.Program != null)
                        .Select(item => item.Program!))
                    .Concat(scopedProjects.Where(item => item.Program != null)
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
            _cache.Set(cacheKey, deferredResult, AppMemoryCache.Entry(TimeSpan.FromMinutes(5)));
            return Ok(deferredResult);
        }

        // Scan each Canvas fact table once for reportable student/course membership. The previous
        // implementation independently rescanned the full submissions warehouse for students,
        // subjects and grades, which became expensive once the warehouse reached ~500k rows.
        var scopedCanvasScores = _userAccess.ApplyStudentScope(
            _context.CanvasCourseScores.AsNoTracking(), item => item.Student);
        var scopedCanvasSubmissions = _userAccess.ApplyStudentScope(
            _context.CanvasSubmissions.AsNoTracking(), item => item.Student);
        var canvasMemberships = await scopedCanvasScores
            .Where(item => item.StudentProfileId != null &&
                (item.FinalScore != null || item.CurrentScore != null) &&
                (item.GradingPeriodEndDate != null || item.Enrollment!.Course!.EndAt != null))
            .Select(item => new
            {
                StudentId = item.StudentProfileId!.Value,
                CourseId = item.Enrollment!.CanvasCourseId
            })
            .Concat(scopedCanvasSubmissions
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
            var importedStudentIds = await scopedBehaviours.Where(item => item.StudentProfileId.HasValue)
                .Select(item => item.StudentProfileId!.Value)
                .Concat(scopedProjects.Select(item => item.StudentProfileId))
                .Concat(_userAccess.ApplyStudentScope(_context.StudentMentalHealthRecords.AsNoTracking(), item => item.Student)
                    .Select(item => item.StudentProfileId))
                .Concat(_userAccess.ApplyStudentScope(_context.StudentBusAttendanceRecords.AsNoTracking(), item => item.Student)
                    .Select(item => item.StudentProfileId))
                .Concat(_userAccess.ApplyStudentScope(_context.StudentHealthVisits.AsNoTracking(), item => item.Student)
                    .Where(item => item.StudentProfileId != null)
                    .Select(item => item.StudentProfileId!.Value))
                .Concat(scopedObservations
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
            : reportableCanvasSubjects.Concat(await scopedProjects
                .Where(p => p.Subject != null).Select(p => p.Subject!)
                .Concat(scopedBehaviours.Where(b => b.Subject != null).Select(b => b.Subject!))
                .Concat(scopedObservations.Where(item => item.Subject != null)
                    .Select(item => item.Subject!))
                .Distinct()
                .ToListAsync());

        var observedCampuses = canvasOnly ? [] : await scopedObservations
            .Where(item => item.Campus != null).Select(item => item.Campus!).Distinct().ToListAsync();
        var observedEducationLevels = canvasOnly ? [] : await scopedObservations
            .Where(item => item.EducationLevel != null).Select(item => item.EducationLevel!).Distinct().ToListAsync();
        var observedGradeLevels = canvasOnly ? [] : await scopedObservations
            .Where(item => item.GradeLevel != null).Select(item => item.GradeLevel!).Distinct().ToListAsync();
        var observedHomerooms = canvasOnly ? [] : await scopedObservations
            .Where(item => item.Homeroom != null).Select(item => item.Homeroom!).Distinct().ToListAsync();
        var measurementCycles = canvasOnly ? [] : await scopedObservations
            .Where(item => item.MeasurementCycle != null).Select(item => item.MeasurementCycle!)
            .Concat(scopedBehaviours.Where(item => item.MeasurementCycle != null)
                .Select(item => item.MeasurementCycle!))
            .Concat(scopedProjects.Where(item => item.MeasurementCycle != null)
                .Select(item => item.MeasurementCycle!)).Distinct().ToListAsync();
        var importedPrograms = canvasOnly ? [] : await scopedObservations
            .Where(item => item.Program != null).Select(item => item.Program!)
            .Concat(scopedBehaviours.Where(item => item.Program != null)
                .Select(item => item.Program!))
            .Concat(scopedProjects.Where(item => item.Program != null)
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

        _cache.Set(cacheKey, result, AppMemoryCache.Entry(TimeSpan.FromMinutes(5)));
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
        var scopedObservations = _userAccess.ApplyStudentScope(
            _context.DashboardMetricObservations.AsNoTracking(), item => item.Student);
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
                values = values.Concat(scopedObservations
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
            var courses = _context.CanvasCourses.AsNoTracking().AsQueryable();
            if (_userAccess.Current.RestrictsStudents)
            {
                var allowedStudentIds = _userAccess.ApplyStudentScope(
                    _context.StudentProfiles.AsNoTracking()).Select(student => student.Id);
                courses = courses.Where(item => item.Enrollments.Any(enrollment =>
                    enrollment.StudentProfileId != null &&
                    allowedStudentIds.Contains(enrollment.StudentProfileId.Value)));
            }
            var values = courses
                .Where(item => item.Name != null &&
                    item.Enrollments.Any(enrollment => enrollment.StudentProfileId != null))
                .Select(item => item.Name!);
            if (!canvasOnly)
            {
                var scopedProjects = _userAccess.ApplyStudentScope(
                    _context.StudentProjects.AsNoTracking(), item => item.Student);
                var scopedBehaviours = _userAccess.ApplyStudentScope(
                    _context.StudentBehaviours.AsNoTracking(), item => item.Student);
                values = values.Concat(scopedProjects.Where(item => item.Subject != null)
                        .Select(item => item.Subject!))
                    .Concat(scopedBehaviours.Where(item => item.Subject != null)
                        .Select(item => item.Subject!))
                    .Concat(scopedObservations.Where(item => item.Subject != null)
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
        // Period options are dimensions, so read them from Canvas course/enrollment metadata.
        // Scanning the submissions fact table here made the dashboard wait on hundreds of
        // thousands of rows merely to populate two small dropdowns.
        var canvasCoursePeriods = await _context.CanvasCourses.AsNoTracking()
            .Where(item => item.Enrollments.Any(enrollment => enrollment.StudentProfileId != null))
            .Select(item => new
            {
                item.EnrollmentTermName,
                item.StartAt,
                item.EndAt
            })
            .Distinct()
            .ToListAsync();
        var canvasTerms = await _context.CanvasCourseScores.AsNoTracking()
            .Where(item => item.StudentProfileId != null && item.GradingPeriodTitle != null)
            .Select(item => item.GradingPeriodTitle!)
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
            : await _context.StudentHealthVisits.AsNoTracking()
                .Where(item => item.StudentProfileId != null)
                .Select(item => new { Year = item.ExamTime.Year, Month = item.ExamTime.Month })
                .Distinct()
                .ToListAsync();
        var academicYears = canvasCoursePeriods
            .Select(item => DashboardPeriodNormalizer.AcademicYear(
                item.EnrollmentTermName ?? "", item.EndAt ?? item.StartAt))
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
        var terms = canvasTerms.Concat(importedTerms)
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
        [FromQuery] string? program, [FromQuery] string? subject,
        [FromQuery] string? metricIds,
        [FromQuery] bool catalogOnly = false,
        CancellationToken cancellationToken = default)
    {
        // The workspace landing page only needs metric metadata for its chart picker. Avoid
        // scanning and materialising the Canvas fact tables until a report actually needs data.
        if (catalogOnly)
        {
            return Ok((await MetricCatalog(cancellationToken))
                .Where(metric => _userAccess.Current.CanAccessMetric(metric.Id, metric.Source)));
        }

        var startedAt = Stopwatch.GetTimestamp();
        var allRequestedMetricIds = ParseMetricIds(metricIds);
        var allStoredMetricDefinitions = await _context.DashboardMetrics.AsNoTracking()
            .Select(metric => new { metric.Key, metric.Source, metric.Domain })
            .ToListAsync(cancellationToken);
        var storedMetricDefinitions = allStoredMetricDefinitions
            .Where(metric => allRequestedMetricIds.Contains(metric.Key))
            .ToDictionary(metric => metric.Key, StringComparer.Ordinal);
        var deniedMetricIds = allRequestedMetricIds
            .Where(metricId =>
            {
                storedMetricDefinitions.TryGetValue(metricId, out var definition);
                return !_userAccess.Current.CanAccessMetric(
                    metricId, definition?.Source, definition?.Domain);
            })
            .ToHashSet(StringComparer.Ordinal);
        var requestedMetricIds = allRequestedMetricIds
            .Where(metricId => !deniedMetricIds.Contains(metricId))
            .ToHashSet(StringComparer.Ordinal);
        if ((allRequestedMetricIds.Count == 0 || requestedMetricIds.Count > 0 &&
             ImportedMetricCatalog().Any(metric => requestedMetricIds.Contains(metric.Id))) &&
            !await _context.ImportedReportBuilds.AsNoTracking().AnyAsync(cancellationToken))
            return StatusCode(503, new { Message = "Report aggregates are being prepared." });
        var requestedMetricCacheKey = allRequestedMetricIds.Count == 0
            ? "all"
            : string.Join(',', allRequestedMetricIds.Order(StringComparer.Ordinal));
        var cacheKey = DashboardCacheKey("metrics", year, term, cycle, campus, level, grade,
            homeroom, studentId?.ToString(), program, subject, requestedMetricCacheKey,
            _userAccess.Current.CacheKey);
        if (_cache.TryGetValue<List<DashboardMetricDto>>(cacheKey, out var cached) && cached is not null)
        {
            RecordMetricsTiming(startedAt, requestedMetricIds.Count, cached.Count, cacheHit: true);
            return Ok(cached);
        }

        var result = await _requestCoalescer.RunAsync(
            cacheKey,
            async computationToken =>
            {
                // Another request may have filled the cache while this request joined the queue.
                if (_cache.TryGetValue<List<DashboardMetricDto>>(cacheKey, out var coalescedCached) &&
                    coalescedCached is not null)
                {
                    return coalescedCached;
                }

        var yearAliases = await AcademicYearAliases(year);
        var termAliases = await TermAliases(term);
        var query = _context.DashboardMetricObservations.AsNoTracking()
            .Where(o => o.Metric.IsActive);
        query = _userAccess.ApplyStudentScope(query, item => item.Student);
        if (requestedMetricIds.Count > 0)
        {
            query = query.WhereMatchesAny(o => o.Metric.Key, requestedMetricIds);
        }
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

        // Aggregate in the database. The previous implementation materialised every matching
        // observation (and its Metric entity) before grouping, which made response time and API
        // memory usage proportional to the raw fact-table size.
        var rows = await query
            .GroupBy(o => new
            {
                o.Metric.Id,
                o.Metric.Key,
                o.Metric.Label,
                o.Metric.Domain,
                o.Metric.Source,
                o.Metric.Unit,
                o.PeriodStart
            })
            .Select(group => new
            {
                group.Key.Id,
                group.Key.Key,
                group.Key.Label,
                group.Key.Domain,
                group.Key.Source,
                group.Key.Unit,
                group.Key.PeriodStart,
                Value = group.Sum(item => item.Value),
                BaselineValue = group.Max(item => item.BaselineValue),
                KpiTarget = group.Max(item => item.KpiTarget),
                HasUnattributedObservation = group.Any(item => item.StudentProfileId == null)
            })
            .OrderBy(row => row.PeriodStart)
            .ToListAsync(computationToken);
        var computed = rows.GroupBy(o => new
        {
            o.Id,
            o.Key,
            o.Label,
            o.Domain,
            o.Source,
            o.Unit
        }).Select(group =>
        {
            var points = group.Select(row => new DashboardMetricPointDto
            {
                PeriodStart = row.PeriodStart, Value = row.Value
            }).OrderBy(point => point.PeriodStart).ToList();
            var latest = group.OrderByDescending(o => o.PeriodStart).First();
            return new DashboardMetricDto
            {
                Id = group.Key.Key, Label = group.Key.Label, Category = group.Key.Domain,
                Source = group.Key.Source, Dataset = group.Key.Source, Unit = group.Key.Unit,
                Value = points.LastOrDefault()?.Value,
                Trend = points.Count > 1 ? points[^1].Value - points[^2].Value : null,
                BaselineValue = latest.BaselineValue,
                KpiTarget = latest.KpiTarget,
                SupportsDrilldown = group.All(o => !o.HasUnattributedObservation),
                Series = points
            };
        }).ToList();

        computed.AddRange(await ImportedDataMetricsFromAggregates(
            year, term, cycle, campus, level, grade, homeroom, studentId, program, subject,
            yearAliases, termAliases, requestedMetricIds, computationToken));
        computed.AddRange(await CanvasDataMetrics(
            year, term, cycle, campus, level, grade, homeroom, studentId, program, subject,
            yearAliases, termAliases, requestedMetricIds, computationToken));
        computed = computed
            .Where(metric => _userAccess.Current.CanAccessMetric(metric.Id, metric.Source))
            .ToList();
        computed.AddRange(deniedMetricIds.Select(metricId => new DashboardMetricDto
        {
            Id = metricId,
            Label = LabelForMetric(metricId),
            Category = "Unavailable",
            Source = "Access policy",
            Dataset = "Access policy",
            UnavailableReason = "access_scope_updated",
            SupportsDrilldown = false
        }));
        computed = computed
            .OrderBy(metric => metric.Category)
            .ThenBy(metric => metric.Label)
            .ToList();
        _cache.Set(cacheKey, computed, AppMemoryCache.Entry(TimeSpan.FromMinutes(5)));
                return computed;
            },
            maxDuration: TimeSpan.FromSeconds(30),
            callerCancellationToken: cancellationToken);
        RecordMetricsTiming(startedAt, requestedMetricIds.Count, result.Count, cacheHit: false);
        return Ok(result);
    }

    private void RecordMetricsTiming(long startedAt, int requestedCount, int returnedCount, bool cacheHit)
    {
        var elapsed = Stopwatch.GetElapsedTime(startedAt);
        Response.Headers["Server-Timing"] = FormattableString.Invariant(
            $"dashboard-metrics;dur={elapsed.TotalMilliseconds:0.0}");
        _logger.LogInformation(
            "Dashboard metrics completed in {ElapsedMilliseconds:F1} ms (requested: {RequestedCount}, returned: {ReturnedCount}, cache hit: {CacheHit})",
            elapsed.TotalMilliseconds, requestedCount, returnedCount, cacheHit);
    }

    private async Task<List<DashboardMetricDto>> MetricCatalog(CancellationToken cancellationToken)
    {
        const string cacheKey = "dashboard:metric-catalog";
        if (_cache.TryGetValue<List<DashboardMetricDto>>(cacheKey, out var cached) && cached is not null)
        {
            return cached;
        }

        // This query only reads the small definition table. The EXISTS predicate prevents
        // inactive stored metrics with no observations from appearing in the picker.
        var result = await _context.DashboardMetrics.AsNoTracking()
            .Where(metric => metric.IsActive && metric.Observations.Any())
            .Select(metric => new DashboardMetricDto
            {
                Id = metric.Key,
                Label = metric.Label,
                Category = metric.Domain,
                Source = metric.Source,
                Dataset = metric.Source,
                Unit = metric.Unit,
                SupportsDrilldown = true
            })
            .ToListAsync(cancellationToken);

        result.AddRange(ImportedMetricCatalog());
        result.AddRange(CanvasMetricCatalog());
        result = result
            .OrderBy(metric => metric.Category)
            .ThenBy(metric => metric.Label)
            .ToList();

        _cache.Set(cacheKey, result, AppMemoryCache.Entry(TimeSpan.FromMinutes(5)));
        return result;
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
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(metricId))
        {
            return BadRequest("metricId is required.");
        }
        var storedMetricDefinition = await _context.DashboardMetrics.AsNoTracking()
            .Where(metric => metric.Key == metricId)
            .Select(metric => new { metric.Source, metric.Domain })
            .FirstOrDefaultAsync();
        if (!_userAccess.Current.CanAccessMetric(
                metricId, storedMetricDefinition?.Source, storedMetricDefinition?.Domain))
            return Forbid();
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
                .Select(item => new { item.StudentProfileId, item.IncidentDate, item.BehaviourType, item.ClassificationCategory,
                    item.Notes, RecordedByName = item.ImportHistory != null ? item.ImportHistory.ImportedByName : null,
                    item.Subject })
                .ToListAsync();
            samples.AddRange(rows
                .Where(item => item.StudentProfileId.HasValue)
                .Where(item => metricId switch
                {
                    "concern-behaviour-records" =>
                        StudentClassificationService.IsConcerningBehaviourType(item.BehaviourType, item.ClassificationCategory),
                    "commendation-records" =>
                        StudentClassificationService.IsCommendationBehaviourType(item.BehaviourType, item.ClassificationCategory),
                    _ => true
                })
                .Select(item => new MetricSample(item.StudentProfileId!.Value, item.IncidentDate, 1.0,
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
            var query = FilterStudentHealthVisits(campus, level, grade, homeroom, program, null)
                .Where(item => item.StudentProfileId != null && (studentId == null || item.StudentProfileId == studentId));
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
                 metricId == "canvas-submitted-assignments-graded" ||
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
                    "canvas-submitted-assignments-graded" => query.Where(item => item.SubmittedAt >= monthStart.Value && item.SubmittedAt < monthEnd!.Value),
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
                case "canvas-submitted-assignments-graded":
                    unit = "submissions";
                    samples.AddRange(rows.Where(r =>
                            string.Equals(r.WorkflowState, "graded", StringComparison.OrdinalIgnoreCase)
                            && r.StudentProfileId != null && r.SubmittedAt != null)
                        .Select(r => CanvasSample(r, r.SubmittedAt!.Value, 1.0)));
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
            var scoreQuery = FilterCanvasScores(campus, level, grade, homeroom, program, subject,
                    yearAliases, termAliases)
                .Where(item => studentId == null || item.StudentProfileId == studentId)
                .Where(item => !monthStart.HasValue ||
                    ((item.GradingPeriodEndDate ?? item.Enrollment!.Course!.EndAt) >= monthStart.Value &&
                     (item.GradingPeriodEndDate ?? item.Enrollment!.Course!.EndAt) < monthEnd!.Value));
            scoreQuery = metricId == "canvas-current-score-avg"
                ? CanvasReportingPeriodService.FilterUsableCurrentScores(scoreQuery)
                : CanvasReportingPeriodService.FilterUsableScores(scoreQuery);
            var rows = await scoreQuery
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
            query = _userAccess.ApplyStudentScope(query, item => item.Student);
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

        if (HasFilter(search) && perStudent.Count > 0)
        {
            var searchTerm = search!.Trim();
            var matchingStudentIds = await VisibleStudents()
                .Where(student =>
                    student.FullName.Contains(searchTerm) ||
                    student.SISUserId.Contains(searchTerm) ||
                    (student.GradeLevel != null && student.GradeLevel.Contains(searchTerm)) ||
                    (student.CanvasGradeLevel != null && student.CanvasGradeLevel.Contains(searchTerm)) ||
                    (student.Homeroom != null && student.Homeroom.Contains(searchTerm)))
                .Select(student => student.Id)
                .ToListAsync();
            var matchingStudentIdSet = matchingStudentIds.ToHashSet();
            perStudent = perStudent
                .Where(row => matchingStudentIdSet.Contains(row.Key))
                .ToDictionary(row => row.Key, row => row.Value);
        }

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
        var students = await VisibleStudents()
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
        "canvas-submitted-assignments-graded" => "Submitted assignments graded",
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
        query = _userAccess.ApplyStudentScope(query, item => item.Student);
        if (HasFilter(campus)) query = query.Where(item => item.Student != null && item.Student.Campus == campus);
        if (HasFilter(level)) query = query.Where(item => item.Student != null && item.Student.EducationLevel == level);
        if (HasFilter(grade)) query = query.Where(item =>
            item.Student != null && (item.Student.GradeLevel ?? item.Student.CanvasGradeLevel) == grade);
        if (HasFilter(homeroom)) query = query.Where(item => item.Student != null && item.Student.Homeroom == homeroom);
        if (HasFilter(program))
            query = query.Where(item => item.Program == program ||
                (item.Program == null && item.Student != null && item.Student.Program == program));
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
        query = _userAccess.ApplyStudentScope(query, item => item.Student);
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
        query = _userAccess.ApplyStudentScope(query, item => item.Student);
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

    private IQueryable<StudentHealthVisit> FilterStudentHealthVisits(
        string? campus, string? level, string? grade, string? homeroom, string? program, string? year)
    {
        var query = _context.StudentHealthVisits.AsNoTracking().Where(item => item.StudentProfileId != null);
        query = _userAccess.ApplyStudentScope(query, item => item.Student);
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
        query = _userAccess.ApplyStudentScope(query, item => item.Student);
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
        query = _userAccess.ApplyStudentScope(query, item => item.Student);
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
        query = _userAccess.ApplyStudentScope(query, item => item.Student);
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

    private async Task<List<DashboardMetricDto>> ImportedDataMetricsFromAggregates(
        string? year, string? term, string? cycle, string? campus, string? level, string? grade,
        string? homeroom, int? studentId, string? program, string? subject,
        IReadOnlyCollection<string> yearAliases, IReadOnlyCollection<string> termAliases,
        IReadOnlySet<string> requestedMetricIds, CancellationToken cancellationToken)
    {
        // A report that requests only Canvas metrics must not scan the imported aggregate table.
        // An empty request means "all metrics", so it intentionally continues below.
        if (requestedMetricIds.Count > 0 &&
            !ImportedMetricCatalog().Any(metric => requestedMetricIds.Contains(metric.Id)))
            return [];

        var query = _userAccess.ApplyStudentScope(
            _context.ImportedReportAggregates.AsNoTracking(), item => item.Student);
        if (studentId.HasValue) query = query.Where(item => item.StudentProfileId == studentId);
        if (HasFilter(campus)) query = query.Where(item => item.Campus == campus);
        if (HasFilter(level)) query = query.Where(item => item.EducationLevel == level);
        if (HasFilter(grade)) query = query.Where(item => item.Grade == grade);
        if (HasFilter(homeroom)) query = query.Where(item => item.Homeroom == homeroom);
        if (HasFilter(program)) query = query.Where(item => item.Program == program);
        if (HasFilter(subject)) query = query.Where(item => item.Subject == subject!.Trim());
        if (HasFilter(cycle)) query = query.Where(item => item.MeasurementCycle == cycle);
        query = query.Where(item => item.Source != "student-health-visits" || item.StudentProfileId != null);

        var hasYear = HasFilter(year);
        var hasTerm = HasFilter(term);
        if (hasYear || hasTerm)
        {
            var period = await ResolvePeriod(year, term, DateTime.UtcNow);
            if (hasYear)
            {
                // The MySQL provider cannot type-map a captured string array inside
                // this conditional expression. Expand aliases to scalar equality branches.
                query = query.WhereMatchesAny(item => item.AcademicYear, yearAliases,
                    item => item.Source == "student-health-visits" &&
                                item.EventDay >= period.Start && item.EventDay < period.End ||
                            item.Source != "student-health-visits" &&
                                item.Source != "student-mental-health" && item.AcademicYear == null &&
                                item.EventDay >= period.Start && item.EventDay < period.End);
            }
            if (hasTerm)
            {
                query = query.WhereMatchesAny(item => item.Term, termAliases,
                    item => (item.Source == "student-health-visits" ||
                             item.Source == "student-mental-health") &&
                                item.EventDay >= period.Start && item.EventDay < period.End ||
                            item.Source != "student-health-visits" &&
                                item.Source != "student-mental-health" && item.Term == null &&
                                item.EventDay >= period.Start && item.EventDay < period.End);
            }
        }

        // The database reduces daily/student facts to month and source dimensions before any
        // response assembly. No source record table is scanned in this request.
        var grouped = await query.GroupBy(item => new
            {
                item.Source, item.Type, item.Category, item.Status,
                Year = item.EventDay.Year, Month = item.EventDay.Month
            })
            .Select(group => new
            {
                group.Key.Source, group.Key.Type, group.Key.Category, group.Key.Status,
                group.Key.Year, group.Key.Month,
                Count = group.Sum(item => item.RecordCount)
            })
            .ToListAsync(cancellationToken);

        IEnumerable<(DateTime Day, int Count)> Select(string source, Func<string?, string?, string?, bool>? predicate = null) =>
            grouped.Where(row => row.Source == source && (predicate == null || predicate(row.Type, row.Category, row.Status)))
                .Select(row => (new DateTime(row.Year, row.Month, 1, 0, 0, 0, DateTimeKind.Utc), row.Count));

        List<DashboardMetricDto> metrics =
        [
            CountAggregateMetric("behaviour-records", "Behaviour records", "Compliance", "StudentBehaviours",
                Select("student-behaviours")),
            CountAggregateMetric("concern-behaviour-records", "Concerning behaviour records", "Compliance", "StudentBehaviours",
                Select("student-behaviours", (_, category, _) => category == "Concerning")),
            CountAggregateMetric("commendation-records", "Commendation records", "Achievement", "StudentBehaviours",
                Select("student-behaviours", (_, category, _) => category == "Positive")),
            CountAggregateMetric("project-records", "Student projects", "Achievement", "StudentProjects",
                Select("student-projects")),
            CountAggregateMetric("active-project-records", "Active student projects", "Achievement", "StudentProjects",
                Select("student-projects", (_, _, status) => !StudentDataEntryOptions.IsCompletedProjectStatus(status))),
            CountAggregateMetric("mental-health-records", "Mental health records", "Safety", "StudentMentalHealth",
                Select("student-mental-health")),
            CountAggregateMetric("high-risk-mental-health", "High-risk mental health records", "Safety", "StudentMentalHealth",
                Select("student-mental-health", (_, _, status) => string.Equals(status, "High", StringComparison.OrdinalIgnoreCase))),
            CountAggregateMetric("health-visit-records", "Health office visits", "Safety", "StudentHealthVisits",
                Select("student-health-visits")),
            CountAggregateMetric("bus-late-records", "Late bus check-ins", "Safety", "StudentBusAttendance",
                Select("student-bus-attendance", (type, _, _) => type == "Late")),
            CountAggregateMetric("bus-seatbelt-violations", "Bus seatbelt violations", "Safety", "StudentBusAttendance",
                Select("student-bus-attendance", (type, _, _) => type == "No seatbelt")),
            CountAggregateMetric("bus-late-to-school", "Late morning bus check-ins", "Safety", "StudentBusAttendance",
                Select("student-bus-attendance", (type, _, status) => type == "Late" && status == "To school")),
            CountAggregateMetric("bus-late-from-school", "Late afternoon bus check-ins", "Safety", "StudentBusAttendance",
                Select("student-bus-attendance", (type, _, status) => type == "Late" && status == "From school"))
        ];
        return metrics.Where(item => WantsMetric(requestedMetricIds, item.Id)).ToList();
    }

    private static DashboardMetricDto CountAggregateMetric(string id, string label, string category,
        string source, IEnumerable<(DateTime Day, int Count)> values)
    {
        var rows = values.ToList();
        var points = rows.GroupBy(item => item.Day)
            .OrderBy(group => group.Key)
            .Select(group => new DashboardMetricPointDto { PeriodStart = group.Key,
                Value = group.Sum(item => item.Count) }).ToList();
        return new DashboardMetricDto
        {
            Id = id, Label = label, Category = category, Source = source, Dataset = source,
            Unit = "records", Value = rows.Sum(item => item.Count),
            Trend = points.Count > 1 ? points[^1].Value - points[^2].Value : null,
            Series = points
        };
    }

    private static List<DashboardMetricDto> ImportedMetricCatalog() =>
    [
        CatalogMetric("behaviour-records", "Behaviour records", "Compliance", "StudentBehaviours", "records"),
        CatalogMetric("concern-behaviour-records", "Concerning behaviour records", "Compliance", "StudentBehaviours", "records"),
        CatalogMetric("commendation-records", "Commendation records", "Achievement", "StudentBehaviours", "records"),
        CatalogMetric("project-records", "Student projects", "Achievement", "StudentProjects", "records"),
        CatalogMetric("active-project-records", "Active student projects", "Achievement", "StudentProjects", "records"),
        CatalogMetric("mental-health-records", "Mental health records", "Safety", "StudentMentalHealth", "records"),
        CatalogMetric("high-risk-mental-health", "High-risk mental health records", "Safety", "StudentMentalHealth", "records"),
        CatalogMetric("health-visit-records", "Health office visits", "Safety", "StudentHealthVisits", "records"),
        CatalogMetric("bus-late-records", "Late bus check-ins", "Safety", "StudentBusAttendance", "records"),
        CatalogMetric("bus-seatbelt-violations", "Bus seatbelt violations", "Safety", "StudentBusAttendance", "records"),
        CatalogMetric("bus-late-to-school", "Late morning bus check-ins", "Safety", "StudentBusAttendance", "records"),
        CatalogMetric("bus-late-from-school", "Late afternoon bus check-ins", "Safety", "StudentBusAttendance", "records")
    ];

    /// <summary>
    /// Canvas grade and assignment metrics are served only from ETL-materialized monthly rows.
    /// A deployment before its first Canvas sync returns unavailable values instead of running
    /// an expensive raw warehouse calculation in a user request.
    /// </summary>
    private async Task<List<DashboardMetricDto>> CanvasDataMetrics(
        string? year, string? term, string? cycle, string? campus, string? level, string? grade,
        string? homeroom, int? studentId, string? program, string? subject,
        IReadOnlyCollection<string> yearAliases, IReadOnlyCollection<string> termAliases,
        IReadOnlySet<string> requestedMetricIds, CancellationToken cancellationToken)
    {
        // Avoid even touching the Canvas aggregate tables for imported-only report requests.
        // An empty request means "all metrics", so it intentionally continues below.
        if (requestedMetricIds.Count > 0 &&
            !CanvasMetricCatalog().Any(metric => requestedMetricIds.Contains(metric.Id)))
            return [];

        // A migration can be deployed before the first post-deploy Canvas run. The report must
        // remain unavailable until ETL materializes the summary table.
        if (await CanvasDashboardAggregateService.HasReadableSnapshotAsync(
                _context, cancellationToken))
        {
            return await CanvasAggregateMetrics(
                year, term, cycle, campus, level, grade, homeroom, studentId, program, subject,
                requestedMetricIds, cancellationToken);
        }

        return CanvasMetricCatalog()
            .Where(metric => WantsMetric(requestedMetricIds, metric.Id))
            .Select(metric =>
            {
                metric.Value = null;
                metric.UnavailableReason = "aggregate_unavailable";
                return metric;
            }).ToList();
    }

    private async Task<List<DashboardMetricDto>> CanvasAggregateMetrics(
        string? year, string? term, string? cycle, string? campus, string? level, string? grade,
        string? homeroom, int? studentId, string? program, string? subject,
        IReadOnlySet<string> requestedMetricIds, CancellationToken cancellationToken)
    {
        if (HasFilter(cycle)) return [];

        var catalog = CanvasMetricCatalog()
            .Where(metric => WantsMetric(requestedMetricIds, metric.Id))
            .ToDictionary(metric => metric.Id, StringComparer.Ordinal);
        if (catalog.Count == 0) return [];
        var requestedCanvasMetricIds = catalog.Keys.ToArray();

        var query = _context.CanvasDashboardMetricAggregates.AsNoTracking().AsQueryable();
        query = _userAccess.ApplyStudentScope(query, item => item.Student);
        query = query.WhereMatchesAny(item => item.MetricKey, requestedCanvasMetricIds);

        if (studentId.HasValue) query = query.Where(item => item.StudentProfileId == studentId.Value);
        if (HasFilter(campus)) query = query.Where(item => item.Student.Campus == campus);
        if (HasFilter(level)) query = query.Where(item => item.Student.EducationLevel == level);
        if (HasFilter(grade))
        {
            query = query.Where(item =>
                (item.Course.CanvasGradeLevel ?? item.Student.GradeLevel ??
                 item.Student.CanvasGradeLevel) == grade);
        }
        if (HasFilter(homeroom)) query = query.Where(item => item.Student.Homeroom == homeroom);
        if (HasFilter(program)) query = query.Where(item => item.Student.Program == program);
        if (HasFilter(subject))
        {
            var normalizedSubject = subject!.Trim();
            query = query.Where(item => item.Course.Name != null &&
                item.Course.Name.Trim() == normalizedSubject);
        }

        var hasYear = HasFilter(year);
        if (hasYear)
        {
            var academicYear = DashboardPeriodNormalizer.AcademicYear(year!);
            query = query.Where(item => item.AcademicYear == academicYear);
            // The raw implementation excludes an event outside the selected year unless a due
            // date supplies an in-year reporting month. Missing rows are the one exception: an
            // undated missing item still contributes to the total when its course term matches.
            query = query.Where(item =>
                item.MetricKey == CanvasDashboardAggregateService.AssignmentsMissing ||
                item.AcademicPeriodStart != null);
        }
        else
        {
            // These raw metrics historically require submitted_at for both their total and series.
            query = query.Where(item =>
                item.MetricKey != CanvasDashboardAggregateService.AssignmentsSubmitted &&
                item.MetricKey != CanvasDashboardAggregateService.SubmittedAssignmentsGraded &&
                item.MetricKey != CanvasDashboardAggregateService.AssignmentsLate ||
                item.PeriodStart != null);
        }
        if (HasFilter(term))
        {
            var normalizedTerm = DashboardPeriodNormalizer.Term(term!);
            query = query.Where(item => item.Term == normalizedTerm);
            if (!hasYear)
            {
                // Match the legacy raw path: a semester without an explicit school year means the
                // latest configured occurrence of that semester, not that semester in every year.
                var resolvedPeriod = await ResolvePeriod(null, term, DateTime.UtcNow);
                query = query.Where(item => item.AcademicYear == resolvedPeriod.Year);
            }
        }

        var grouped = await query
            .Select(item => new
            {
                item.MetricKey,
                ReportingMonth = hasYear ? item.AcademicPeriodStart : item.PeriodStart,
                item.ValueSum,
                item.SampleCount
            })
            .GroupBy(item => new { item.MetricKey, item.ReportingMonth })
            .Select(group => new
            {
                group.Key.MetricKey,
                group.Key.ReportingMonth,
                ValueSum = group.Sum(item => item.ValueSum),
                SampleCount = group.Sum(item => item.SampleCount)
            })
            .ToListAsync(cancellationToken);

        var averageMetrics = new HashSet<string>(StringComparer.Ordinal)
        {
            CanvasDashboardAggregateService.FinalScoreAverage,
            CanvasDashboardAggregateService.CurrentScoreAverage,
            CanvasDashboardAggregateService.SubmissionScoreAverage
        };
        var results = new List<DashboardMetricDto>(catalog.Count);
        foreach (var definition in catalog.Values)
        {
            var rows = grouped.Where(item => item.MetricKey == definition.Id).ToList();
            var isAverage = averageMetrics.Contains(definition.Id);
            var datedRows = rows.Where(item => item.ReportingMonth != null)
                .OrderBy(item => item.ReportingMonth)
                .ToList();
            definition.Series = datedRows.Select(item => new DashboardMetricPointDto
            {
                PeriodStart = item.ReportingMonth!.Value,
                Value = isAverage
                    ? item.SampleCount == 0
                        ? 0
                        : Math.Round((decimal)(item.ValueSum / item.SampleCount), 1)
                    : (decimal)item.ValueSum
            }).ToList();
            if (isAverage)
            {
                var sampleCount = rows.Sum(item => item.SampleCount);
                definition.Value = sampleCount == 0
                    ? null
                    : Math.Round((decimal)(rows.Sum(item => item.ValueSum) / sampleCount), 1);
            }
            else
            {
                definition.Value = (decimal)rows.Sum(item => item.ValueSum);
            }
            definition.Trend = definition.Series.Count > 1
                ? definition.Series[^1].Value - definition.Series[^2].Value
                : null;
            results.Add(definition);
        }
        return results;
    }

    private static List<DashboardMetricDto> CanvasMetricCatalog() =>
    [
        CatalogMetric(
            "canvas-final-score-avg", "Average Canvas final_score", "Academics", "Canvas", "%",
            "CanvasEnrollmentGrades",
            "Average of usable Canvas grades.final_score values. Empty zero placeholders without points or a grade label are excluded; explicit zero grades are preserved. The monthly series uses the Canvas grading-period end_date, falling back to the Canvas course end_at for an overall course grade.",
            "enrollment.grades.final_score", "grading_period.end_date", "course.end_at"),
        CatalogMetric(
            "canvas-current-score-avg", "Average Canvas current_score", "Academics", "Canvas", "%",
            "CanvasEnrollmentGrades",
            "Average of usable Canvas grades.current_score values. Empty zero placeholders without points or a grade label are excluded; explicit zero grades are preserved. The monthly series uses the Canvas grading-period end_date, falling back to the Canvas course end_at for an overall course grade.",
            "enrollment.grades.current_score", "grading_period.end_date", "course.end_at"),
        CatalogMetric(
            "canvas-submission-score-percent-avg", "Average Canvas submission score percentage", "Academics", "Canvas", "%",
            "CanvasSubmissionsAndAssignments",
            "Reporting calculation: average of Canvas submission.score / Canvas assignment.points_possible × 100 for submissions whose Canvas workflow_state is graded.",
            "submission.score", "submission.workflow_state", "submission.graded_at",
            "submission.submitted_at", "assignment.points_possible"),
        CatalogMetric(
            "canvas-assignments-submitted", "Canvas submissions with submitted_at", "Academics", "Canvas", "submissions",
            "CanvasSubmissionsAndAssignments",
            "Count of Canvas submission records whose submitted_at is non-null.",
            "submission.submitted_at"),
        CatalogMetric(
            "canvas-assignments-graded", "Canvas submissions with workflow_state=graded", "Academics", "Canvas", "submissions",
            "CanvasSubmissionsAndAssignments",
            "Count of Canvas submission records whose workflow_state is graded. Undated graded records contribute to the total but not the monthly series.",
            "submission.workflow_state", "submission.graded_at", "submission.submitted_at"),
        CatalogMetric(
            "canvas-submitted-assignments-graded", "Submitted Canvas activities graded", "Academics", "Canvas", "submissions",
            "CanvasSubmissionsAndAssignments",
            "Count of submitted Canvas activities whose workflow_state is graded. The submitted_at date is used so this metric can be compared directly with submitted activity counts.",
            "submission.workflow_state", "submission.submitted_at"),
        CatalogMetric(
            "canvas-assignments-missing", "Canvas submissions flagged missing", "Academics", "Canvas", "submissions",
            "CanvasSubmissionsAndAssignments",
            "Count of Canvas submission records where missing=true and excused is not true. Records without a Canvas due date contribute to the total but not the monthly series.",
            "submission.missing", "submission.excused", "submission.cached_due_date", "assignment.due_at"),
        CatalogMetric(
            "canvas-assignments-late", "Canvas submissions flagged late", "Academics", "Canvas", "submissions",
            "CanvasSubmissionsAndAssignments",
            "Count of Canvas submission records where late=true. Records without submitted_at contribute to the total but not the monthly series.",
            "submission.late", "submission.submitted_at")
    ];

    private static DashboardMetricDto CatalogMetric(
        string id, string label, string category, string source, string unit,
        string? dataset = null, string? definition = null, params string[] sourceFields) => new()
    {
        Id = id,
        Label = label,
        Category = category,
        Source = source,
        Dataset = dataset ?? source,
        Unit = unit,
        Definition = definition,
        SourceFields = sourceFields.ToList(),
        SupportsDrilldown = true
    };

    private static bool HasFilter(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !string.Equals(value, "all", StringComparison.OrdinalIgnoreCase);

    private static HashSet<string> ParseMetricIds(string? metricIds) =>
        string.IsNullOrWhiteSpace(metricIds)
            ? []
            : metricIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(id => id.Length <= 100)
                .Take(50)
                .ToHashSet(StringComparer.Ordinal);

    private static bool WantsMetric(IReadOnlySet<string> requestedMetricIds, string metricId) =>
        requestedMetricIds.Count == 0 || requestedMetricIds.Contains(metricId);

    private static bool WantsAnyMetric(IReadOnlySet<string> requestedMetricIds, params string[] metricIds) =>
        requestedMetricIds.Count == 0 || metricIds.Any(requestedMetricIds.Contains);

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
        var lastSuccessfulCanvasSyncAtUtc = await LastSuccessfulCanvasSyncAtUtc();
        var access = _userAccess.Current;

        if (!await _context.ImportedReportBuilds.AsNoTracking().AnyAsync())
            return StatusCode(503, new { Message = "Report aggregates are being prepared." });
        var students = await VisibleStudents().ToListAsync();
        var canvasPeriod = await _canvasReportingPeriods.ResolveAsync(
            period.Year, period.Term is "hk1" or "hk2" ? period.Term : null, now);
        var snapshots = await _userAccess.ApplyStudentScope(
                _context.StudentRiskSnapshots.AsNoTracking()
                    .Where(item => item.AcademicYear == canvasPeriod.AcademicYear && item.Term == canvasPeriod.Term),
                item => item.Student)
            .ToDictionaryAsync(item => item.StudentProfileId);
        var canvasAverages = snapshots.Where(item => item.Value.AverageCanvasScore.HasValue)
            .ToDictionary(item => item.Key, item => item.Value.AverageCanvasScore!.Value);
        var facts = _userAccess.ApplyStudentScope(_context.ImportedReportAggregates.AsNoTracking(),
            item => item.Student);
        var yearAliases = await AcademicYearAliases(period.Year);
        var termAliases = period.Term == "all" ? [] : await TermAliases(period.Term);
        var (yearStart, yearEnd) = DashboardPeriodNormalizer.AcademicYearRange(period.Year);
        var behaviours = facts.Where(item => item.Source == "student-behaviours")
            .WhereMatchesAny(item => item.AcademicYear, yearAliases,
                item => item.AcademicYear == null && item.EventDay >= yearStart && item.EventDay < yearEnd);
        var projects = facts.Where(item => item.Source == "student-projects")
            .WhereMatchesAny(item => item.AcademicYear, yearAliases,
                item => item.AcademicYear == null && item.EventDay >= period.Start && item.EventDay < period.End);
        if (period.Term != "all")
        {
            behaviours = behaviours.WhereMatchesAny(item => item.Term, termAliases,
                item => item.Term == null && item.EventDay >= period.Start && item.EventDay < period.End);
            projects = projects.WhereMatchesAny(item => item.Term, termAliases,
                item => item.Term == null && item.AcademicYear == null &&
                    item.EventDay >= period.Start && item.EventDay < period.End);
        }
        // Reduce daily persisted measures to the twelve chart points in SQL. The previous path
        // transferred every matching aggregate row and folded it in API memory.
        var behaviourRows = access.CanAccessAcademic
            ? await behaviours.GroupBy(item => new { item.EventDay.Year, item.EventDay.Month })
                .Select(group => new
                {
                    group.Key.Year,
                    group.Key.Month,
                    RecordCount = group.Sum(item => item.RecordCount)
                })
                .ToListAsync()
            : [];
        var behavioursLast30Days = access.CanAccessAcademic
            ? await facts.Where(item => item.Source == "student-behaviours")
                .SumAsync(item => (int?)item.Recent30Count) ?? 0
            : 0;
        var datedFacts = facts.Where(item => item.EventDay >= period.Start && item.EventDay < period.End);

        var overview = new DashboardOverviewDto
        {
            PeriodYear = period.Year,
            PeriodTerm = period.Term,
            LastSuccessfulCanvasSyncAtUtc = lastSuccessfulCanvasSyncAtUtc,
            TotalStudents = students.Count,
            CanAccessAcademic = access.CanAccessAcademic,
            CanAccessHealth = access.CanAccessHealth,
            CanAccessBus = access.CanAccessBus,
            TotalBehaviours = behaviourRows.Sum(item => item.RecordCount),
            BehavioursLast30Days = behavioursLast30Days,
            TotalProjects = access.CanAccessAcademic
                ? await projects.SumAsync(item => (int?)item.RecordCount) ?? 0 : 0,
            ActiveProjects = access.CanAccessAcademic
                ? await projects.SumAsync(item => (int?)item.AttentionCount) ?? 0 : 0,
            TotalHealthVisits = access.CanAccessHealth
                ? await datedFacts.Where(item => item.Source == "student-health-visits" && item.StudentProfileId != null)
                    .SumAsync(item => (int?)item.RecordCount) ?? 0 : 0,
            TotalMentalHealthRecords = access.CanAccessHealth
                ? await datedFacts.Where(item => item.Source == "student-mental-health")
                    .SumAsync(item => (int?)item.RecordCount) ?? 0 : 0,
            TotalBusRecords = access.CanAccessBus
                ? await datedFacts.Where(item => item.Source == "student-bus-attendance")
                    .SumAsync(item => (int?)item.RecordCount) ?? 0 : 0
        };

        var gradeBuckets = new Dictionary<string, DashboardGradeDto>();
        var gradeCanvasScores = new Dictionary<string, List<decimal>>();
        var classBuckets = new Dictionary<string, DashboardClassDto>();
        var classCanvasScores = new Dictionary<string, List<decimal>>();
        var attentionCandidates = new List<(StudentProfile Student, string RiskLevel, int SignalCount)>();

        foreach (var student in students)
        {
            snapshots.TryGetValue(student.Id, out var snapshot);
            var riskLevel = snapshot is null ? "None" : access.CanAccessBus
                ? snapshot.RiskLevelWithBus : snapshot.RiskLevelWithoutBus;
            var concernCount = snapshot?.RiskConcerningCount ?? 0;
            var riskBusViolationCount = access.CanAccessBus ? snapshot?.RiskBusCount ?? 0 : 0;
            if (access.CanAccessAcademic) switch (riskLevel)
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

            if (access.CanAccessAcademic && riskLevel != "None")
            {
                attentionCandidates.Add((student, riskLevel, concernCount + riskBusViolationCount));
            }

            var resolvedGrade = student.GradeLevel ?? student.CanvasGradeLevel;
            var gradeKey = string.IsNullOrWhiteSpace(resolvedGrade) ? "—" : resolvedGrade;
            if (!gradeBuckets.TryGetValue(gradeKey, out var bucket))
            {
                bucket = new DashboardGradeDto { Grade = gradeKey };
                gradeBuckets[gradeKey] = bucket;
            }

            bucket.Students++;
            var homeroomKey = string.IsNullOrWhiteSpace(student.Homeroom) ? "—" : student.Homeroom.Trim();
            var classKey = $"{gradeKey}\u001f{homeroomKey}";
            if (!classBuckets.TryGetValue(classKey, out var classBucket))
            {
                classBucket = new DashboardClassDto { Grade = gradeKey, Homeroom = homeroomKey };
                classBuckets[classKey] = classBucket;
            }
            classBucket.Students++;
            if (access.CanAccessAcademic && canvasAverages.TryGetValue(student.Id, out var canvasAverage))
            {
                if (!gradeCanvasScores.TryGetValue(gradeKey, out var scores))
                {
                    scores = [];
                    gradeCanvasScores[gradeKey] = scores;
                }
                scores.Add(canvasAverage);
                if (!classCanvasScores.TryGetValue(classKey, out var classScores))
                {
                    classScores = [];
                    classCanvasScores[classKey] = classScores;
                }
                classScores.Add(canvasAverage);
            }
            if (access.CanAccessAcademic && riskLevel != "None")
            {
                bucket.AtRisk++;
                classBucket.AtRisk++;
            }
        }

        overview.AtRiskTotal = overview.AtRiskHigh + overview.AtRiskMedium;
        foreach (var bucket in gradeBuckets)
        {
            if (gradeCanvasScores.TryGetValue(bucket.Key, out var scores) && scores.Count > 0)
            {
                bucket.Value.AverageCanvasScorePercentage = Math.Round(scores.Average(), 1);
            }
        }
        foreach (var bucket in classBuckets)
        {
            if (classCanvasScores.TryGetValue(bucket.Key, out var scores) && scores.Count > 0)
            {
                bucket.Value.AverageCanvasScorePercentage = Math.Round(scores.Average(), 1);
            }
        }
        overview.BehaviourTrend = access.CanAccessAcademic
            ? BuildAggregateTrend(behaviourRows.Select(item =>
                    (new DateTime(item.Year, item.Month, 1), item.RecordCount)),
                period.Start, period.End, now) : [];
        overview.GradeDistribution = gradeBuckets.Values
            .OrderBy(grade => grade.Grade, StringComparer.OrdinalIgnoreCase)
            .ToList();
        overview.ClassDistribution = classBuckets.Values
            .OrderBy(item => int.TryParse(item.Grade, out var number) ? number : int.MaxValue)
            .ThenBy(item => item.Grade, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Homeroom, StringComparer.OrdinalIgnoreCase)
            .ToList();
        overview.IncludedGradeLevels = gradeBuckets.Keys
            .Where(grade => int.TryParse(grade, out _))
            .OrderBy(grade => int.TryParse(grade, out var number) ? number : int.MaxValue)
            .ThenBy(grade => grade, StringComparer.OrdinalIgnoreCase)
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

    private async Task<DateTime?> LastSuccessfulCanvasSyncAtUtc()
    {
        var completedRunAt = await _context.CanvasSyncRuns.AsNoTracking()
            .Where(run => run.Status == "completed" && run.CompletedAtUtc != null)
            .OrderByDescending(run => run.CompletedAtUtc)
            .Select(run => run.CompletedAtUtc)
            .FirstOrDefaultAsync();
        var state = await _context.CanvasSyncStates.AsNoTracking()
            .Where(item => item.LastSuccessfulWatermarkUtc != null)
            .OrderByDescending(item => item.LastSuccessfulWatermarkUtc)
            .Select(item => new
            {
                item.LastSuccessfulWatermarkUtc,
                item.LastCompletedAtUtc,
                item.LastStatus
            })
            .FirstOrDefaultAsync();
        var successfulStateAt = state?.LastStatus == "completed"
            ? state.LastCompletedAtUtc ?? state.LastSuccessfulWatermarkUtc
            : state?.LastSuccessfulWatermarkUtc;

        if (completedRunAt is null) return successfulStateAt;
        if (successfulStateAt is null) return completedRunAt;
        return completedRunAt > successfulStateAt ? completedRunAt : successfulStateAt;
    }

    private async Task<(string Year, string Term, DateTime Start, DateTime End)> ResolvePeriod(
        string? year, string? term, DateTime now)
    {
        var normalizedYear = HasFilter(year) ? DashboardPeriodNormalizer.AcademicYear(year!) : null;
        var normalizedTerm = HasFilter(term) ? DashboardPeriodNormalizer.Term(term!) : null;
        var hasExplicitPeriod = normalizedYear is not null || normalizedTerm is not null;
        var configuredQuery = (await _context.AcademicPeriods.AsNoTracking().ToListAsync())
            .Where(period => normalizedYear == null || DashboardPeriodNormalizer.AcademicYear(period.AcademicYear)
                .Equals(normalizedYear, StringComparison.OrdinalIgnoreCase))
            .Where(period => normalizedTerm == null || DashboardPeriodNormalizer.Term(period.Term)
                .Equals(normalizedTerm, StringComparison.OrdinalIgnoreCase));
        if (!hasExplicitPeriod)
        {
            configuredQuery = configuredQuery.Where(period => period.StartDate <= now && now < period.EndDate);
        }
        var configured = configuredQuery
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
        IEnumerable<DateTime> behaviourDates, DateTime start, DateTime end, DateTime now)
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

        foreach (var incidentDate in behaviourDates)
        {
            if (index.TryGetValue((incidentDate.Year, incidentDate.Month), out var point))
            {
                point.Total++;
            }
        }

        return points;
    }

    private static List<DashboardTrendPointDto> BuildAggregateTrend(
        IEnumerable<(DateTime Day, int Count)> measures, DateTime start, DateTime end, DateTime now)
    {
        var points = new List<DashboardTrendPointDto>();
        var index = new Dictionary<(int Year, int Month), DashboardTrendPointDto>();
        var cursor = new DateTime(start.Year, start.Month, 1);
        var limit = end < now ? end : now;
        while (cursor <= limit && points.Count < 12)
        {
            var point = new DashboardTrendPointDto { Year = cursor.Year, Month = cursor.Month };
            points.Add(point);
            index[(cursor.Year, cursor.Month)] = point;
            cursor = cursor.AddMonths(1);
        }
        foreach (var measure in measures)
            if (index.TryGetValue((measure.Day.Year, measure.Day.Month), out var point))
                point.Total += measure.Count;
        return points;
    }

    private IQueryable<StudentProfile> VisibleStudents()
        => _userAccess.ApplyStudentScope(_context.StudentProfiles.AsNoTracking());

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
