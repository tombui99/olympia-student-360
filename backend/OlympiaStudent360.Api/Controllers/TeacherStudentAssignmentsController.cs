using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/admin/teacher-student-assignments")]
[ApiController]
[Authorize(Roles = "Admin")]
public class TeacherStudentAssignmentsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public TeacherStudentAssignmentsController(
        AppDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<TeacherStudentAssignmentsDto>> GetAssignments()
    {
        var teachers = (await _userManager.GetUsersInRoleAsync("Teacher"))
            .OrderBy(teacher => teacher.FullName)
            .ThenBy(teacher => teacher.UserName)
            .ToList();

        var students = await _context.StudentProfiles
            .AsNoTracking()
            .OrderBy(student => student.FullName)
            .ThenBy(student => student.StudentCode)
            .ToListAsync();

        var counts = students
            .Where(student => student.TeacherUserId != null)
            .GroupBy(student => student.TeacherUserId!)
            .ToDictionary(group => group.Key, group => group.Count());

        return Ok(new TeacherStudentAssignmentsDto
        {
            Teachers = teachers.Select(teacher => new AssignmentTeacherDto
            {
                Id = teacher.Id,
                Username = teacher.UserName!,
                Email = teacher.Email,
                FullName = teacher.FullName,
                AssignedStudentCount = counts.GetValueOrDefault(teacher.Id)
            }).ToList(),
            Students = students.Select(student => new AssignmentStudentDto
            {
                Id = student.Id,
                StudentCode = student.StudentCode,
                FullName = student.FullName,
                GradeLevel = student.GradeLevel,
                Homeroom = student.Homeroom,
                TeacherId = student.TeacherUserId
            }).ToList()
        });
    }

    [HttpPut("{teacherId}")]
    public async Task<IActionResult> SaveAssignments(
        string teacherId,
        [FromBody] SaveTeacherStudentAssignmentsDto model)
    {
        var teacher = await _userManager.FindByIdAsync(teacherId);
        if (teacher == null || !await _userManager.IsInRoleAsync(teacher, "Teacher"))
        {
            return NotFound(new { Message = "Teacher account was not found." });
        }

        var requestedIds = model.StudentIds.Distinct().ToList();
        var requestedStudents = await _context.StudentProfiles
            .Where(student => requestedIds.Contains(student.Id))
            .ToListAsync();

        if (requestedStudents.Count != requestedIds.Count)
        {
            return BadRequest(new { Message = "One or more students were not found." });
        }

        var conflicts = requestedStudents
            .Where(student => student.TeacherUserId != null && student.TeacherUserId != teacherId)
            .Select(student => student.FullName)
            .ToList();

        if (conflicts.Count > 0)
        {
            return Conflict(new
            {
                Message = "One or more students are already assigned to another teacher.",
                Students = conflicts
            });
        }

        var currentStudents = await _context.StudentProfiles
            .Where(student => student.TeacherUserId == teacherId)
            .ToListAsync();

        foreach (var student in currentStudents)
        {
            student.TeacherUserId = null;
            student.UpdatedAt = DateTime.UtcNow;
        }

        foreach (var student in requestedStudents)
        {
            student.TeacherUserId = teacherId;
            student.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return NoContent();
    }
}
