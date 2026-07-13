using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/students")]
[ApiController]
[Authorize]
public class StudentsController : ControllerBase
{
    private readonly AppDbContext _context;

    public StudentsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<StudentListItemDto>>> GetStudents()
    {
        var students = await LoadVisibleWithActivity();

        var result = students
            .OrderBy(student => student.FullName)
            .ThenBy(student => student.StudentCode)
            .Select(ToListItem)
            .ToList();

        return Ok(result);
    }

    [HttpGet("at-risk")]
    public async Task<ActionResult<AtRiskStudentsPageDto>> GetAtRiskStudents(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);
        var atRiskStudents = (await LoadVisibleWithActivity())
            .Select(student => new { Student = student, Risk = StudentClassificationService.ClassifyRisk(student) })
            .Where(entry => entry.Risk.Level != "None")
            .OrderByDescending(entry => entry.Risk.Level == "High")
            .ThenByDescending(entry => entry.Risk.Flags.First().Value)
            .ThenBy(entry => entry.Student.FullName)
            .ToList();

        var totalCount = atRiskStudents.Count;
        return Ok(new AtRiskStudentsPageDto
        {
            Items = atRiskStudents
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(entry => new AtRiskStudentDto
                {
                    Id = entry.Student.Id,
                    StudentCode = entry.Student.StudentCode,
                    FullName = entry.Student.FullName,
                    GradeLevel = entry.Student.GradeLevel,
                    Homeroom = entry.Student.Homeroom,
                    TeacherName = entry.Student.Teacher?.FullName,
                    BehaviourCount = entry.Student.Behaviours.Count,
                    ProjectCount = entry.Student.Projects.Count,
                    RiskLevel = entry.Risk.Level,
                    RiskFlagCount = entry.Risk.Flags.Count,
                    Flags = entry.Risk.Flags
                })
                .ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = TotalPages(totalCount, pageSize),
            HighRiskCount = atRiskStudents.Count(entry => entry.Risk.Level == "High"),
            MediumRiskCount = atRiskStudents.Count(entry => entry.Risk.Level == "Medium")
        });
    }

    [HttpGet("commendations")]
    public async Task<ActionResult<StudentCommendationsPageDto>> GetCommendations(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] string? search = null,
        [FromQuery] string? grade = null)
    {
        (page, pageSize) = NormalizePaging(page, pageSize);
        var commendations = (await LoadVisibleWithActivity())
            .SelectMany(student => student.Behaviours
                .Where(StudentClassificationService.IsCommendationBehaviour)
                .Select(behaviour => new StudentCommendationDto
                {
                    Id = behaviour.Id,
                    StudentId = student.Id,
                    StudentCode = student.StudentCode,
                    StudentName = student.FullName,
                    GradeLevel = student.GradeLevel,
                    Homeroom = student.Homeroom,
                    TeacherName = student.Teacher?.FullName,
                    IncidentDate = behaviour.IncidentDate,
                    CommendationType = behaviour.BehaviourType,
                    Notes = behaviour.Notes,
                    RecordedByName = behaviour.RecordedByName
                }))
            .ToList();

        var availableGrades = commendations
            .Select(commendation => commendation.GradeLevel)
            .Where(gradeLevel => !string.IsNullOrWhiteSpace(gradeLevel))
            .Select(gradeLevel => gradeLevel!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(gradeLevel => gradeLevel, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var filtered = commendations.Where(commendation =>
            (string.IsNullOrWhiteSpace(search) ||
             commendation.StudentName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             commendation.StudentCode.Contains(search, StringComparison.OrdinalIgnoreCase) ||
             commendation.CommendationType.Contains(search, StringComparison.OrdinalIgnoreCase)) &&
            (string.IsNullOrWhiteSpace(grade) || string.Equals(commendation.GradeLevel, grade, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(commendation => commendation.IncidentDate)
            .ThenBy(commendation => commendation.StudentName)
            .ToList();

        var totalCount = filtered.Count;
        return Ok(new StudentCommendationsPageDto
        {
            Items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = TotalPages(totalCount, pageSize),
            TotalCommendationCount = commendations.Count,
            StudentsRecognisedCount = commendations.Select(commendation => commendation.StudentId).Distinct().Count(),
            RecentCount = commendations.Count(commendation => commendation.IncidentDate >= DateTime.UtcNow.AddDays(-30)),
            AvailableGrades = availableGrades
        });
    }

    [HttpGet("cohorts")]
    public async Task<ActionResult<CohortsResponseDto>> GetCohorts()
    {
        var students = await LoadVisibleWithActivity();

        var evaluated = students
            .Select(student =>
            {
                var risk = StudentClassificationService.ClassifyRisk(student);
                return new CohortEntry(
                    string.IsNullOrWhiteSpace(student.GradeLevel) ? "—" : student.GradeLevel!,
                    string.IsNullOrWhiteSpace(student.Homeroom) ? "—" : student.Homeroom!,
                    risk.Level,
                    student.Behaviours.Count,
                    student.Projects.Count);
            })
            .ToList();

        return Ok(new CohortsResponseDto
        {
            Grades = BuildCohorts(evaluated.GroupBy(entry => entry.Grade), key => $"Grade {key}"),
            Homerooms = BuildCohorts(evaluated.GroupBy(entry => entry.Homeroom), key => key)
        });
    }

    private record CohortEntry(
        string Grade,
        string Homeroom,
        string Level,
        int BehaviourCount,
        int ProjectCount);

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StudentProfileDetailDto>> GetStudent(int id)
    {
        var student = await VisibleStudents()
            .Include(candidate => candidate.Teacher)
            .Include(candidate => candidate.Behaviours)
            .Include(candidate => candidate.Projects)
            .FirstOrDefaultAsync(candidate => candidate.Id == id);

        if (student == null)
        {
            return NotFound(new { Message = "Student was not found." });
        }

        var now = DateTime.UtcNow;
        var last30Days = now.AddDays(-30);
        var risk = StudentClassificationService.ClassifyRisk(student);

        var detail = new StudentProfileDetailDto
        {
            Id = student.Id,
            StudentCode = student.StudentCode,
            FullName = student.FullName,
            GradeLevel = student.GradeLevel,
            Homeroom = student.Homeroom,
            TeacherName = student.Teacher?.FullName,
            RiskLevel = risk.Level,
            Flags = risk.Flags,
            Summary = new StudentSummaryDto
            {
                TotalBehaviours = student.Behaviours.Count,
                BehavioursLast30Days = student.Behaviours.Count(behaviour => behaviour.IncidentDate >= last30Days),
                TotalProjects = student.Projects.Count,
                ActiveProjects = student.Projects.Count(project =>
                    !string.Equals(project.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            },
            Behaviours = student.Behaviours
                .OrderByDescending(behaviour => behaviour.IncidentDate)
                .Select(behaviour => new StudentBehaviourDto
                {
                    Id = behaviour.Id,
                    IncidentDate = behaviour.IncidentDate,
                    BehaviourType = behaviour.BehaviourType,
                    Notes = behaviour.Notes,
                    RecordedByName = behaviour.RecordedByName
                })
                .ToList(),
            Projects = student.Projects
                .OrderByDescending(project => project.CreatedAt)
                .Select(project => new StudentProjectDto
                {
                    Id = project.Id,
                    ProjectName = project.ProjectName,
                    Subject = project.Subject,
                    Status = project.Status,
                    TeacherNotes = project.TeacherNotes,
                    CreatedAt = project.CreatedAt
                })
                .ToList()
        };

        return Ok(detail);
    }

    private static StudentListItemDto ToListItem(StudentProfile student)
    {
        var risk = StudentClassificationService.ClassifyRisk(student);
        return new StudentListItemDto
        {
            Id = student.Id,
            StudentCode = student.StudentCode,
            FullName = student.FullName,
            GradeLevel = student.GradeLevel,
            Homeroom = student.Homeroom,
            TeacherName = student.Teacher?.FullName,
            BehaviourCount = student.Behaviours.Count,
            ProjectCount = student.Projects.Count,
            RiskLevel = risk.Level,
            RiskFlagCount = risk.Flags.Count
        };
    }

    private static List<CohortStatsDto> BuildCohorts(
        IEnumerable<IGrouping<string, CohortEntry>> groups,
        Func<string, string> labelFactory)
    {
        return groups
            .Select(group =>
            {
                var entries = group.ToList();
                var studentCount = entries.Count;
                var totalBehaviours = entries.Sum(entry => entry.BehaviourCount);
                return new CohortStatsDto
                {
                    Key = group.Key,
                    Label = labelFactory(group.Key),
                    StudentCount = studentCount,
                    AtRiskCount = entries.Count(entry => entry.Level != "None"),
                    HighRiskCount = entries.Count(entry => entry.Level == "High"),
                    TotalBehaviours = totalBehaviours,
                    TotalProjects = entries.Sum(entry => entry.ProjectCount),
                    AvgBehaviours = studentCount == 0 ? 0 : Math.Round((double)totalBehaviours / studentCount, 1)
                };
            })
            .OrderBy(cohort => cohort.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<List<StudentProfile>> LoadVisibleWithActivity()
    {
        return await VisibleStudents()
            .Include(student => student.Teacher)
            .Include(student => student.Behaviours)
            .Include(student => student.Projects)
            .ToListAsync();
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

    private static (int Page, int PageSize) NormalizePaging(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, 100));

    private static int TotalPages(int totalCount, int pageSize) =>
        totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);

}
