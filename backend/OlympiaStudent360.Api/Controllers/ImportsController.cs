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
        var customFields = await ModuleFields("student-behaviours");
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

            if (!StudentClassificationService.TryCanonicalBehaviourType(row.BehaviourType, out var behaviourType))
            {
                errors.Add($"Row {rowNumber}: behaviour_type is not supported.");
                continue;
            }

            var (customErrors, customJson) = ImportCustomFieldService.Build(customFields, row.CustomFields, $"Row {rowNumber}: ");
            errors.AddRange(customErrors);

            behaviours.Add(new StudentBehaviour
            {
                StudentProfileId = student!.Id,
                IncidentDate = DateTime.SpecifyKind(row.IncidentDate!.Value, DateTimeKind.Utc),
                BehaviourType = behaviourType,
                Notes = string.IsNullOrWhiteSpace(row.Notes) ? null : row.Notes.Trim(),
                RecordedByName = recordedByName,
                AcademicYear = Clean(row.AcademicYear), Term = Clean(row.Term),
                MeasurementCycle = Clean(row.MeasurementCycle), Program = Clean(row.Program), Subject = Clean(row.Subject),
                CustomFieldsJson = customJson
            });
        }

        if (errors.Count > 0)
        {
            return BadRequest(new { Message = "No rows were imported. Correct every error and try again.", Errors = errors });
        }

        var history = await CreateExcelHistory("student-behaviours", model.FileName, behaviours.Count);
        foreach (var behaviour in behaviours) behaviour.ImportHistory = history;
        _context.StudentBehaviours.AddRange(behaviours);
        _context.ImportHistories.Add(history);
        await _context.SaveChangesAsync();
        return Created($"/api/import-history/{history.Id}", ToDto(history));
    }

    [HttpPost("student-projects")]
    public async Task<ActionResult<ImportHistoryDto>> ImportProjects([FromBody] ExcelProjectImportDto model)
    {
        var students = await VisibleStudents().ToDictionaryAsync(student => student.StudentCode, StringComparer.OrdinalIgnoreCase);
        var customFields = await ModuleFields("student-projects");
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

            var (customErrors, customJson) = ImportCustomFieldService.Build(customFields, row.CustomFields, $"Row {rowNumber}: ");
            errors.AddRange(customErrors);

            projects.Add(new StudentProject
            {
                StudentProfileId = student!.Id,
                ProjectName = row.ProjectName.Trim(),
                Subject = subject,
                Status = status,
                TeacherNotes = string.IsNullOrWhiteSpace(row.TeacherNotes) ? null : row.TeacherNotes.Trim(),
                AcademicYear = Clean(row.AcademicYear), Term = Clean(row.Term),
                MeasurementCycle = Clean(row.MeasurementCycle), Program = Clean(row.Program),
                ActivityDate = row.ActivityDate.HasValue ? DateTime.SpecifyKind(row.ActivityDate.Value, DateTimeKind.Utc) : null,
                CustomFieldsJson = customJson
            });
        }

        if (errors.Count > 0)
        {
            return BadRequest(new { Message = "No rows were imported. Correct every error and try again.", Errors = errors });
        }

        var history = await CreateExcelHistory("student-projects", model.FileName, projects.Count);
        foreach (var project in projects) project.ImportHistory = history;
        _context.StudentProjects.AddRange(projects);
        _context.ImportHistories.Add(history);
        await _context.SaveChangesAsync();
        return Created($"/api/import-history/{history.Id}", ToDto(history));
    }

    [HttpPost("student-mental-health")]
    [Authorize(Roles = "Admin,SchoolAdministration,Principal,Teacher")]
    public async Task<ActionResult<ImportHistoryDto>> ImportMentalHealth(
        [FromBody] ExcelMentalHealthImportDto model)
    {
        var students = await VisibleStudents().ToDictionaryAsync(student => student.StudentCode, StringComparer.OrdinalIgnoreCase);
        var academicYears = await _context.AcademicPeriods.AsNoTracking()
            .Select(period => period.AcademicYear)
            .Distinct()
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase);
        var customFields = await ModuleFields("student-mental-health");
        var errors = new List<string>();
        var records = new List<StudentMentalHealthRecord>();
        var recordedByName = await CurrentUserDisplayName();
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

        for (var index = 0; index < model.Rows.Count; index++)
        {
            var row = model.Rows[index];
            var rowNumber = index + 2;
            if (!TryGetStudent(students, row.StudentId, row.StudentName, rowNumber, errors, out var student))
            {
                continue;
            }

            var concernType = Canonical(StudentDataEntryOptions.MentalHealthConcernTypes, row.ConcernType);
            var riskLevel = Canonical(StudentDataEntryOptions.MentalHealthRiskLevels, row.RiskLevel);
            if (!academicYears.Contains(row.AcademicYear.Trim()))
                errors.Add($"Row {rowNumber}: academic_year is not configured.");
            if (concernType == null)
                errors.Add($"Row {rowNumber}: concern_type is not supported.");
            if (riskLevel == null)
                errors.Add($"Row {rowNumber}: risk_level must be Low, Medium, or High.");
            if (concernType == null || riskLevel == null || !academicYears.Contains(row.AcademicYear.Trim()))
            {
                continue;
            }

            var (customErrors, customJson) = ImportCustomFieldService.Build(customFields, row.CustomFields, $"Row {rowNumber}: ");
            errors.AddRange(customErrors);

            records.Add(new StudentMentalHealthRecord
            {
                StudentProfileId = student!.Id,
                RecordDate = DateTime.SpecifyKind(row.RecordDate!.Value, DateTimeKind.Utc),
                AcademicYear = row.AcademicYear.Trim(),
                ConcernType = concernType,
                RiskLevel = riskLevel,
                Notes = Clean(row.Notes),
                RecordedByUserId = userId,
                RecordedByName = recordedByName,
                CustomFieldsJson = customJson
            });
        }

        if (errors.Count > 0)
        {
            return BadRequest(new { Message = "No rows were imported. Correct every error and try again.", Errors = errors });
        }

        var history = await CreateExcelHistory("student-mental-health", model.FileName, records.Count);
        foreach (var record in records) record.ImportHistory = history;
        _context.StudentMentalHealthRecords.AddRange(records);
        _context.ImportHistories.Add(history);
        await _context.SaveChangesAsync();
        return Created($"/api/import-history/{history.Id}", ToDto(history));
    }

    [HttpPost("student-bus-attendance")]
    public async Task<ActionResult<ImportHistoryDto>> ImportBusAttendance(
        [FromBody] ExcelBusAttendanceImportDto model)
    {
        var students = await VisibleStudents().ToDictionaryAsync(
            student => student.StudentCode, StringComparer.OrdinalIgnoreCase);
        var customFields = await ModuleFields("student-bus-attendance");
        var errors = new List<string>();
        var records = new List<StudentBusAttendanceRecord>();
        var recordedByName = await CurrentUserDisplayName();

        for (var index = 0; index < model.Rows.Count; index++)
        {
            var row = model.Rows[index];
            var rowNumber = index + 2;
            if (!TryGetStudent(students, row.StudentId, row.StudentName, rowNumber, errors, out var student))
            {
                continue;
            }

            var direction = Canonical(StudentDataEntryOptions.BusTripDirections, row.TripDirection);
            var violation = Canonical(StudentDataEntryOptions.BusViolationTypes, row.ViolationType);
            if (direction == null) errors.Add($"Row {rowNumber}: trip_direction must be To school or From school.");
            if (violation == null) errors.Add($"Row {rowNumber}: violation_type must be Late or No seatbelt.");
            if (direction == null || violation == null) continue;

            var (customErrors, customJson) = ImportCustomFieldService.Build(
                customFields, row.CustomFields, $"Row {rowNumber}: ");
            errors.AddRange(customErrors);

            records.Add(new StudentBusAttendanceRecord
            {
                StudentProfileId = student!.Id,
                AttendanceDate = DateTime.SpecifyKind(row.AttendanceDate!.Value, DateTimeKind.Utc),
                TripDirection = direction,
                ViolationType = violation,
                BusCode = Clean(row.BusCode),
                RouteName = Clean(row.RouteName),
                Notes = Clean(row.Notes),
                RecordedByName = recordedByName,
                AcademicYear = Clean(row.AcademicYear),
                Term = Clean(row.Term),
                CustomFieldsJson = customJson
            });
        }

        if (errors.Count > 0)
        {
            return BadRequest(new { Message = "No rows were imported. Correct every error and try again.", Errors = errors });
        }

        var history = await CreateExcelHistory("student-bus-attendance", model.FileName, records.Count);
        foreach (var record in records) record.ImportHistory = history;
        _context.StudentBusAttendanceRecords.AddRange(records);
        _context.ImportHistories.Add(history);
        await _context.SaveChangesAsync();
        return Created($"/api/import-history/{history.Id}", ToDto(history));
    }

    private async Task<List<ImportModuleField>> ModuleFields(string moduleId) =>
        await _context.ImportModuleFields.AsNoTracking()
            .Where(f => f.ModuleId == moduleId)
            .OrderBy(f => f.SortOrder).ToListAsync();

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Canonical(IReadOnlyList<string> options, string value) =>
        options.FirstOrDefault(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase));

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
            errors.Add($"Row {rowNumber}: student_id '{studentCode}' is invalid.");
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
        => _context.StudentProfiles.AsNoTracking();

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
        FileName = history.FileName,
        ImportedAt = history.CreatedAt,
        ImportedBy = history.ImportedByUsername,
        ImportedByName = history.ImportedByName,
        Status = history.Status,
        ValidRows = history.ValidRows,
        InvalidRows = history.InvalidRows
    };
}
