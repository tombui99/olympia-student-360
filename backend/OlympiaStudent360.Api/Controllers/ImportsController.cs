using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/imports")]
[ApiController]
[Authorize]
public class ImportsController : ControllerBase
{
    private readonly AppDbContext _context;

    public ImportsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpPost("student-behaviours")]
    public async Task<ActionResult<ImportHistoryDto>> ImportBehaviours([FromBody] ExcelBehaviourImportDto model)
    {
        var students = await VisibleStudents().ToDictionaryAsync(student => student.StudentCode, StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var behaviours = new List<StudentBehaviour>();
        var recordedByName = await CurrentUserDisplayName();

        for (var index = 0; index < model.Rows.Count; index++)
        {
            var row = model.Rows[index];
            var rowNumber = index + 2;
            if (!TryGetStudent(students, row.StudentId, row.StudentName, rowNumber, errors, out var student))
            {
                continue;
            }

            if (!StudentClassificationService.IsSupportedBehaviourType(row.BehaviourType))
            {
                errors.Add($"Row {rowNumber}: behaviour_type is not supported.");
                continue;
            }

            var behaviourType = StudentClassificationService.SupportedBehaviourTypes.First(type =>
                string.Equals(type, row.BehaviourType, StringComparison.OrdinalIgnoreCase));
            behaviours.Add(new StudentBehaviour
            {
                StudentProfileId = student!.Id,
                IncidentDate = DateTime.SpecifyKind(row.IncidentDate!.Value, DateTimeKind.Utc),
                BehaviourType = behaviourType,
                Notes = string.IsNullOrWhiteSpace(row.Notes) ? null : row.Notes.Trim(),
                RecordedByName = recordedByName,
                AcademicYear = Clean(row.AcademicYear), Term = Clean(row.Term),
                MeasurementCycle = Clean(row.MeasurementCycle), Program = Clean(row.Program), Subject = Clean(row.Subject)
            });
        }

        if (errors.Count > 0)
        {
            return BadRequest(new { Message = "No rows were imported. Correct every error and try again.", Errors = errors });
        }

        var history = await CreateExcelHistory("student-behaviours", model.FileName, behaviours.Count);
        _context.StudentBehaviours.AddRange(behaviours);
        _context.ImportHistories.Add(history);
        await _context.SaveChangesAsync();
        return Created($"/api/import-history/{history.Id}", ToDto(history));
    }

    [HttpPost("student-projects")]
    public async Task<ActionResult<ImportHistoryDto>> ImportProjects([FromBody] ExcelProjectImportDto model)
    {
        var students = await VisibleStudents().ToDictionaryAsync(student => student.StudentCode, StringComparer.OrdinalIgnoreCase);
        var errors = new List<string>();
        var projects = new List<StudentProject>();

        for (var index = 0; index < model.Rows.Count; index++)
        {
            var row = model.Rows[index];
            var rowNumber = index + 2;
            if (!TryGetStudent(students, row.StudentId, row.StudentName, rowNumber, errors, out var student))
            {
                continue;
            }

            var subject = StudentDataEntryOptions.ProjectSubjects.FirstOrDefault(value =>
                string.Equals(value, row.Subject, StringComparison.OrdinalIgnoreCase));
            var status = StudentDataEntryOptions.ProjectStatuses.FirstOrDefault(value =>
                string.Equals(value, row.Status, StringComparison.OrdinalIgnoreCase));
            if (subject == null || status == null)
            {
                errors.Add($"Row {rowNumber}: subject or status is not supported.");
                continue;
            }

            projects.Add(new StudentProject
            {
                StudentProfileId = student!.Id,
                ProjectName = row.ProjectName.Trim(),
                Subject = subject,
                Status = status,
                TeacherNotes = string.IsNullOrWhiteSpace(row.TeacherNotes) ? null : row.TeacherNotes.Trim(),
                AcademicYear = Clean(row.AcademicYear), Term = Clean(row.Term),
                MeasurementCycle = Clean(row.MeasurementCycle), Program = Clean(row.Program),
                ActivityDate = row.ActivityDate.HasValue ? DateTime.SpecifyKind(row.ActivityDate.Value, DateTimeKind.Utc) : null
            });
        }

        if (errors.Count > 0)
        {
            return BadRequest(new { Message = "No rows were imported. Correct every error and try again.", Errors = errors });
        }

        var history = await CreateExcelHistory("student-projects", model.FileName, projects.Count);
        _context.StudentProjects.AddRange(projects);
        _context.ImportHistories.Add(history);
        await _context.SaveChangesAsync();
        return Created($"/api/import-history/{history.Id}", ToDto(history));
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool TryGetStudent(
        IReadOnlyDictionary<string, StudentProfile> students,
        string studentCode,
        string studentName,
        int rowNumber,
        List<string> errors,
        out StudentProfile? student)
    {
        if (!students.TryGetValue(studentCode.Trim(), out student))
        {
            errors.Add($"Row {rowNumber}: student_id '{studentCode}' is invalid or is not assigned to you.");
            return false;
        }

        if (!string.Equals(student.FullName.Trim(), studentName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Row {rowNumber}: student_name does not match student_id '{studentCode}'.");
            return false;
        }

        return true;
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

    private async Task<string> CurrentUserDisplayName()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var fullName = await _context.Users
            .Where(user => user.Id == userId)
            .Select(user => user.FullName)
            .FirstOrDefaultAsync();
        return string.IsNullOrWhiteSpace(fullName) ? User.Identity?.Name ?? "Unknown" : fullName;
    }

    private async Task<ImportHistory> CreateExcelHistory(string moduleId, string fileName, int rowCount)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var username = User.Identity?.Name ?? "unknown";
        return new ImportHistory
        {
            ModuleId = moduleId,
            Source = "Excel",
            FileName = fileName.Trim(),
            Status = "Valid",
            ValidRows = rowCount,
            InvalidRows = 0,
            ImportedByUserId = userId,
            ImportedByUsername = username,
            ImportedByName = await CurrentUserDisplayName()
        };
    }

    private static ImportHistoryDto ToDto(ImportHistory history) => new()
    {
        Id = history.Id,
        ModuleId = history.ModuleId,
        Source = history.Source,
        FileName = history.FileName,
        ImportedAt = history.CreatedAt,
        ImportedBy = history.ImportedByUsername,
        ImportedByName = history.ImportedByName,
        Status = history.Status,
        ValidRows = history.ValidRows,
        InvalidRows = history.InvalidRows
    };
}
