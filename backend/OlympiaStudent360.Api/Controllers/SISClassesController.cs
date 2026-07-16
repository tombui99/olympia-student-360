using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/classes")]
[ApiController]
[Authorize]
public class SISClassesController : ControllerBase
{
    private readonly AppDbContext _context;

    public SISClassesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<SISClassPageDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? academicYear = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var visible = VisibleClasses().Where(item => item.IsActive);
        var academicYears = await visible.Select(item => item.AcademicYear)
            .Distinct().OrderByDescending(value => value).ToListAsync();

        var query = visible;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim();
            query = query.Where(item => item.ClassName.Contains(value) ||
                item.SISClassCode.Contains(value) ||
                item.Homeroom != null && item.Homeroom.Contains(value));
        }
        if (!string.IsNullOrWhiteSpace(academicYear) && academicYear != "all")
        {
            query = query.Where(item => item.AcademicYear == academicYear);
        }

        var totalCount = await query.CountAsync();
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);
        page = Math.Min(page, Math.Max(1, totalPages));
        var items = await query.OrderBy(item => item.GradeLevel)
            .ThenBy(item => item.ClassName)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new SISClassListItemDto
            {
                Id = item.Id,
                SISClassCode = item.SISClassCode,
                ClassName = item.ClassName,
                AcademicYear = item.AcademicYear,
                Term = item.Term,
                GradeLevel = item.GradeLevel,
                Homeroom = item.Homeroom,
                Campus = item.Campus,
                TeacherCount = item.Teachers.Count,
                StudentCount = item.Students.Count
            }).ToListAsync();

        return Ok(new SISClassPageDto
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages,
            AcademicYears = academicYears
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SISClassDetailDto>> Detail(int id)
    {
        var item = await VisibleClasses().AsNoTracking()
            .AsSplitQuery()
            .Include(sisClass => sisClass.Teachers).ThenInclude(assignment => assignment.Teacher)
            .Include(sisClass => sisClass.Students).ThenInclude(membership => membership.Student)
            .FirstOrDefaultAsync(sisClass => sisClass.Id == id && sisClass.IsActive);
        if (item == null) return NotFound();

        return Ok(new SISClassDetailDto
        {
            Id = item.Id,
            SISClassCode = item.SISClassCode,
            ClassName = item.ClassName,
            AcademicYear = item.AcademicYear,
            Term = item.Term,
            GradeLevel = item.GradeLevel,
            Homeroom = item.Homeroom,
            Campus = item.Campus,
            TeacherCount = item.Teachers.Count,
            StudentCount = item.Students.Count,
            Teachers = item.Teachers.OrderBy(assignment => assignment.Teacher.FullName)
                .Select(assignment => new SISClassTeacherDto
                {
                    Id = assignment.TeacherUserId,
                    Username = assignment.Teacher.UserName!,
                    FullName = assignment.Teacher.FullName,
                    Email = assignment.Teacher.Email,
                    AssignmentRole = assignment.AssignmentRole
                }).ToList(),
            Students = item.Students.OrderBy(membership => membership.Student.FullName)
                .Select(membership => new SISClassStudentDto
                {
                    Id = membership.StudentProfileId,
                    StudentCode = membership.Student.StudentCode,
                    FullName = membership.Student.FullName,
                    GradeLevel = membership.Student.GradeLevel,
                    Homeroom = membership.Student.Homeroom
                }).ToList()
        });
    }

    private IQueryable<SISClass> VisibleClasses()
    {
        var query = _context.SISClasses.AsNoTracking();
        if (User.IsInRole("Admin") || User.IsInRole("Principal")) return query;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return query.Where(item => item.Teachers.Any(assignment => assignment.TeacherUserId == userId));
    }
}
