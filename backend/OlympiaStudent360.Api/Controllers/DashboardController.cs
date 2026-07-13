using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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
    private readonly AppDbContext _context;

    public DashboardController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("filter-options")]
    public async Task<ActionResult<DashboardFilterOptionsDto>> FilterOptions()
    {
        var students = VisibleStudents();
        var observations = _context.DashboardMetricObservations.AsNoTracking();
        if (!User.IsInRole("Admin") && !User.IsInRole("Principal"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            observations = observations.Where(o => o.StudentProfileId == null || o.Student!.TeacherUserId == userId);
        }
        var periods = _context.AcademicPeriods.AsNoTracking();

        var result = new DashboardFilterOptionsDto
        {
            AcademicYears = await periods.Select(p => p.AcademicYear)
                .Concat(observations.Where(o => o.AcademicYear != null).Select(o => o.AcademicYear!))
                .Distinct().OrderByDescending(value => value).ToListAsync(),
            Terms = await periods.Select(p => p.Term)
                .Concat(observations.Where(o => o.Term != null).Select(o => o.Term!))
                .Distinct().OrderBy(value => value).ToListAsync(),
            MeasurementCycles = await observations.Where(o => o.MeasurementCycle != null)
                .Select(o => o.MeasurementCycle!).Distinct().OrderBy(value => value).ToListAsync(),
            Campuses = await students.Where(s => s.Campus != null).Select(s => s.Campus!)
                .Concat(observations.Where(o => o.Campus != null).Select(o => o.Campus!))
                .Distinct().OrderBy(value => value).ToListAsync(),
            EducationLevels = await students.Where(s => s.EducationLevel != null).Select(s => s.EducationLevel!)
                .Concat(observations.Where(o => o.EducationLevel != null).Select(o => o.EducationLevel!))
                .Distinct().OrderBy(value => value).ToListAsync(),
            GradeLevels = await students.Where(s => s.GradeLevel != null).Select(s => s.GradeLevel!)
                .Distinct().OrderBy(value => value).ToListAsync(),
            Homerooms = await students.Where(s => s.Homeroom != null).Select(s => s.Homeroom!)
                .Distinct().OrderBy(value => value).ToListAsync(),
            Students = await students.OrderBy(s => s.FullName).Select(s => new DashboardStudentOptionDto
            {
                Id = s.Id, StudentCode = s.StudentCode, FullName = s.FullName,
                GradeLevel = s.GradeLevel, Homeroom = s.Homeroom
            }).ToListAsync(),
            Programs = await students.Where(s => s.Program != null).Select(s => s.Program!)
                .Concat(observations.Where(o => o.Program != null).Select(o => o.Program!))
                .Distinct().OrderBy(value => value).ToListAsync(),
            Subjects = await observations.Where(o => o.Subject != null).Select(o => o.Subject!)
                .Concat(_context.StudentProjects.Where(p => p.Subject != null).Select(p => p.Subject!))
                .Distinct().OrderBy(value => value).ToListAsync()
        };
        return Ok(result);
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
        preference.AcademicYear = Clean(model.AcademicYear);
        preference.Term = Clean(model.Term);
        preference.MeasurementCycle = Clean(model.MeasurementCycle);
        preference.Campus = Clean(model.Campus);
        preference.EducationLevel = Clean(model.EducationLevel);
        preference.GradeLevel = Clean(model.GradeLevel);
        preference.Homeroom = Clean(model.Homeroom);
        preference.StudentProfileId = model.StudentProfileId;
        preference.Program = Clean(model.Program);
        preference.Subject = Clean(model.Subject);
        preference.ChartOrdersJson = JsonSerializer.Serialize(model.ChartOrders ?? []);
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
        var query = _context.DashboardMetricObservations.AsNoTracking()
            .Include(o => o.Metric).Where(o => o.Metric.IsActive);
        if (!User.IsInRole("Admin") && !User.IsInRole("Principal"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            query = query.Where(o => o.StudentProfileId == null || o.Student!.TeacherUserId == userId);
        }
        query = Apply(query, o => o.AcademicYear, year);
        query = Apply(query, o => o.Term, term);
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
                Source = group.Key.Source, Unit = group.Key.Unit,
                Value = points.LastOrDefault()?.Value,
                Trend = points.Count > 1 ? points[^1].Value - points[^2].Value : null,
                BaselineValue = group.OrderByDescending(o => o.PeriodStart).First().BaselineValue,
                KpiTarget = group.OrderByDescending(o => o.PeriodStart).First().KpiTarget,
                Series = points
            };
        }).OrderBy(metric => metric.Category).ThenBy(metric => metric.Label).ToList();
        return Ok(result);
    }

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
            .ToListAsync();

        var overview = new DashboardOverviewDto
        {
            PeriodYear = period.Year,
            PeriodTerm = period.Term,
            TotalStudents = students.Count,
            TotalProjects = students.Sum(student => student.Projects.Count(project => InPeriod(project, period))),
            ActiveProjects = students.Sum(student => student.Projects
                .Count(project => InPeriod(project, period) && !string.Equals(project.Status, "Completed", StringComparison.OrdinalIgnoreCase)))
        };

        var since = now.AddDays(-30);
        var gradeBuckets = new Dictionary<string, DashboardGradeDto>();

        foreach (var student in students)
        {
            var risk = StudentClassificationService.ClassifyRisk(student);
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

        return Ok(overview);
    }

    private async Task<(string Year, string Term, DateTime Start, DateTime End)> ResolvePeriod(
        string? year, string? term, DateTime now)
    {
        var storedPeriods = _context.AcademicPeriods.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(year)) storedPeriods = storedPeriods.Where(p => p.AcademicYear == year);
        if (!string.IsNullOrWhiteSpace(term) && term != "all") storedPeriods = storedPeriods.Where(p => p.Term == term);
        var configured = await storedPeriods.OrderByDescending(p => p.IsCurrent).ThenBy(p => p.StartDate).ToListAsync();
        if (configured.Count > 0)
        {
            return (configured[0].AcademicYear, string.IsNullOrWhiteSpace(term) ? "all" : term,
                configured.Min(p => p.StartDate), configured.Max(p => p.EndDate));
        }

        // School year runs August–July. Default to the year that contains "now".
        var startYear = now.Month >= 8 ? now.Year : now.Year - 1;
        if (!string.IsNullOrWhiteSpace(year))
        {
            var parts = year.Split('-');
            if (parts.Length > 0 && int.TryParse(parts[0], out var parsed))
            {
                startYear = parsed;
            }
        }

        var normalizedTerm = term switch
        {
            "hk1" => "hk1",
            "hk2" => "hk2",
            _ => "all"
        };

        var (start, end) = normalizedTerm switch
        {
            "hk1" => (new DateTime(startYear, 8, 1), new DateTime(startYear + 1, 1, 1)),
            "hk2" => (new DateTime(startYear + 1, 1, 1), new DateTime(startYear + 1, 7, 1)),
            _ => (new DateTime(startYear, 8, 1), new DateTime(startYear + 1, 7, 1))
        };

        return ($"{startYear}-{startYear + 1}", normalizedTerm, start, end);
    }

    private static bool InPeriod(StudentBehaviour item, (string Year, string Term, DateTime Start, DateTime End) period)
    {
        if (!string.IsNullOrWhiteSpace(item.AcademicYear))
            return item.AcademicYear == period.Year && (period.Term == "all" || item.Term == period.Term);
        return item.IncidentDate >= period.Start && item.IncidentDate < period.End;
    }

    private static bool InPeriod(StudentProject item, (string Year, string Term, DateTime Start, DateTime End) period)
    {
        if (!string.IsNullOrWhiteSpace(item.AcademicYear))
            return item.AcademicYear == period.Year && (period.Term == "all" || item.Term == period.Term);
        var date = item.ActivityDate ?? item.CreatedAt;
        return date >= period.Start && date < period.End;
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
    {
        var query = _context.StudentProfiles.AsNoTracking();

        if (User.IsInRole("Admin") || User.IsInRole("Principal"))
        {
            return query;
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return query.Where(student => student.TeacherUserId == userId);
    }

    private static IQueryable<DashboardMetricObservation> Apply(
        IQueryable<DashboardMetricObservation> query,
        System.Linq.Expressions.Expression<Func<DashboardMetricObservation, string?>> selector,
        string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value == "all") return query;
        return query.Where(BuildEquality(selector, value));
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
        AcademicYear = item.AcademicYear, Term = item.Term, MeasurementCycle = item.MeasurementCycle,
        Campus = item.Campus, EducationLevel = item.EducationLevel, GradeLevel = item.GradeLevel,
        Homeroom = item.Homeroom, StudentProfileId = item.StudentProfileId,
        Program = item.Program, Subject = item.Subject,
        ChartOrders = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(item.ChartOrdersJson) ?? []
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
