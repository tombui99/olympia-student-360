using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

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

    [HttpGet("overview")]
    public async Task<ActionResult<DashboardOverviewDto>> Overview()
    {
        var now = DateTime.UtcNow;
        var students = await VisibleStudents()
            .Include(student => student.Behaviours)
            .Include(student => student.Projects)
            .ToListAsync();

        var overview = new DashboardOverviewDto
        {
            TotalStudents = students.Count,
            TotalProjects = students.Sum(student => student.Projects.Count),
            ActiveProjects = students.Sum(student => student.Projects
                .Count(project => !string.Equals(project.Status, "Completed", StringComparison.OrdinalIgnoreCase)))
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
                overview.TotalBehaviours++;
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
        }

        overview.AtRiskTotal = overview.AtRiskHigh + overview.AtRiskMedium;
        overview.BehaviourTrend = BuildTrend(students, now);
        overview.GradeDistribution = gradeBuckets.Values
            .OrderBy(grade => grade.Grade, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Ok(overview);
    }

    private static List<DashboardTrendPointDto> BuildTrend(List<StudentProfile> students, DateTime now)
    {
        var points = new List<DashboardTrendPointDto>();
        var index = new Dictionary<(int Year, int Month), DashboardTrendPointDto>();

        for (var offset = 5; offset >= 0; offset--)
        {
            var month = new DateTime(now.Year, now.Month, 1).AddMonths(-offset);
            var point = new DashboardTrendPointDto { Year = month.Year, Month = month.Month };
            points.Add(point);
            index[(month.Year, month.Month)] = point;
        }

        foreach (var behaviour in students.SelectMany(student => student.Behaviours))
        {
            if (!index.TryGetValue((behaviour.IncidentDate.Year, behaviour.IncidentDate.Month), out var point))
            {
                continue;
            }

            point.Total++;
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
}
