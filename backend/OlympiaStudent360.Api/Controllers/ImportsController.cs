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
    private readonly UserAccessService _userAccess;
    private readonly StudentRecordFieldOptionService _recordFieldOptions;

    public ImportsController(
        AppDbContext context,
        UserAccessService userAccess,
        StudentRecordFieldOptionService recordFieldOptions)
    {
        _context = context;
        _userAccess = userAccess;
        _recordFieldOptions = recordFieldOptions;
    }

    [HttpPost("student-behaviours")]
    public async Task<ActionResult<ExcelImportResultDto>> ImportBehaviours([FromBody] ExcelBehaviourImportDto model)
    {
        var students = await VisibleStudents().ToDictionaryAsync(student => student.SISUserId, StringComparer.OrdinalIgnoreCase);
        var behaviourOptions = await _recordFieldOptions.ListAsync(
            StudentRecordFieldOptionService.BehaviourModule,
            StudentRecordFieldOptionService.BehaviourTypeField);
        var customFields = await ModuleFields("student-behaviours");
        var errors = new List<string>();
        var behaviours = new List<StudentBehaviour>();
        var recordedByName = await CurrentUserDisplayName();

        for (var index = 0; index < model.Rows.Count; index++)
        {
            var row = model.Rows[index];
            var rowNumber = index + model.DataStartRow;
            if (!TryGetStudent(students, row.StudentId, row.StudentName, rowNumber, errors, out var student))
            {
                continue;
            }

            var behaviourOption = behaviourOptions.FirstOrDefault(option =>
                string.Equals(option.Value, row.BehaviourType?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (behaviourOption == null)
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
                BehaviourType = behaviourOption.Value,
                ClassificationCategory = StudentRecordFieldOptionService.CategoryName(behaviourOption.OptionGroup),
                Notes = string.IsNullOrWhiteSpace(row.Notes) ? null : row.Notes.Trim(),
                RecordedByName = recordedByName,
                RecordedByEmail = Clean(row.RecordedByEmail), RecordedByTitle = Clean(row.RecordedByTitle),
                AcademicYear = Clean(row.AcademicYear), Term = Clean(row.Term),
                MeasurementCycle = Clean(row.MeasurementCycle), Program = Clean(row.Program), Subject = Clean(row.Subject),
                IncidentTimeText = Clean(row.IncidentTimeText), Decision = Clean(row.Decision),
                ConductDeductionLevel = Clean(row.ConductDeductionLevel), OccurrenceCount = row.OccurrenceCount,
                BehaviourPointsDeducted = Clean(row.BehaviourPointsDeducted),
                SourceRecordId = row.SourceRecordId, SourceRowNumber = row.SourceRowNumber ?? rowNumber,
                SourceFileName = Clean(row.SourceFileName) ?? model.FileName.Trim(),
                SourceStudentName = Clean(row.SourceStudentName) ?? row.StudentName.Trim(),
                SourceGradeLevel = Clean(row.SourceGradeLevel), StudentMatchStatus = Clean(row.StudentMatchStatus),
                StudentMatchNotes = Clean(row.StudentMatchNotes), SourceDataIssues = Clean(row.SourceDataIssues),
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
        return Ok(ToResult(history));
    }

    [HttpPost("student-projects")]
    public async Task<ActionResult<ExcelImportResultDto>> ImportProjects([FromBody] ExcelProjectImportDto model)
    {
        var students = await VisibleStudents().ToDictionaryAsync(student => student.SISUserId, StringComparer.OrdinalIgnoreCase);
        var projectOptions = await _recordFieldOptions.ListAsync(StudentRecordFieldOptionService.ProjectModule);
        var subjectOptions = projectOptions.Where(item => item.FieldKey == StudentRecordFieldOptionService.ProjectSubjectField).ToList();
        var statusOptions = projectOptions.Where(item => item.FieldKey == StudentRecordFieldOptionService.ProjectStatusField).ToList();
        var customFields = await ModuleFields("student-projects");
        var errors = new List<string>();
        var projects = new List<StudentProject>();

        for (var index = 0; index < model.Rows.Count; index++)
        {
            var row = model.Rows[index];
            var rowNumber = index + model.DataStartRow;
            if (!TryGetStudent(students, row.StudentId, row.StudentName, rowNumber, errors, out var student))
            {
                continue;
            }

            var subject = StudentRecordFieldOptionService.CanonicalValue(subjectOptions, row.Subject);
            var status = StudentRecordFieldOptionService.CanonicalValue(statusOptions, row.Status);
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
        return Ok(ToResult(history));
    }

    [HttpPost("student-mental-health")]
    [Authorize(Roles = "Admin,SchoolAdministration,Principal,Teacher")]
    public async Task<ActionResult<ExcelImportResultDto>> ImportMentalHealth(
        [FromBody] ExcelMentalHealthImportDto model)
    {
        var students = await VisibleStudents().ToDictionaryAsync(student => student.SISUserId, StringComparer.OrdinalIgnoreCase);
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
            var rowNumber = index + model.DataStartRow;
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
        return Ok(ToResult(history));
    }

    [HttpPost("student-bus-attendance")]
    public async Task<ActionResult<ExcelImportResultDto>> ImportBusAttendance(
        [FromBody] ExcelBusAttendanceImportDto model)
    {
        var students = await VisibleStudents().ToDictionaryAsync(
            student => student.SISUserId, StringComparer.OrdinalIgnoreCase);
        var customFields = await ModuleFields("student-bus-attendance");
        var errors = new List<string>();
        var records = new List<StudentBusAttendanceRecord>();
        var recordedByName = await CurrentUserDisplayName();

        for (var index = 0; index < model.Rows.Count; index++)
        {
            var row = model.Rows[index];
            var rowNumber = index + model.DataStartRow;
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
        return Ok(ToResult(history));
    }

    [HttpPost("student-health-visits")]
    public async Task<ActionResult<ExcelImportResultDto>> ImportHealthVisits(
        [FromBody] ExcelHealthVisitImportDto model)
    {
        var students = await VisibleStudents().ToDictionaryAsync(
            student => student.SISUserId, StringComparer.OrdinalIgnoreCase);
        var customFields = await ModuleFields("student-health-visits");
        var errors = new List<string>();
        var records = new List<StudentHealthVisit>();

        for (var index = 0; index < model.Rows.Count; index++)
        {
            var row = model.Rows[index];
            var rowNumber = index + model.DataStartRow;
            if (!TryGetStudent(students, row.StudentId, row.StudentName, rowNumber, errors, out var student))
            {
                continue;
            }

            var (customErrors, customJson) = ImportCustomFieldService.Build(
                customFields, row.CustomFields, $"Row {rowNumber}: ");
            errors.AddRange(customErrors);

            records.Add(new StudentHealthVisit
            {
                StudentProfileId = student!.Id,
                SISUserId = student.SISUserId,
                ExamTime = DateTime.SpecifyKind(row.VisitDate!.Value, DateTimeKind.Utc),
                FullName = student.FullName,
                Gender = student.Gender,
                ClassName = student.Homeroom,
                Symptoms = row.Symptoms.Trim(),
                Diagnosis = Clean(row.Diagnosis),
                Classification = Clean(row.Classification),
                Treatment = Clean(row.Treatment),
                Medication = Clean(row.Medication),
                Notes = Clean(row.Notes),
                NotificationSent = row.NotificationSent,
                ExaminedBy = Clean(row.ExaminedBy),
                CustomFieldsJson = customJson
            });
        }

        if (errors.Count > 0)
        {
            return BadRequest(new { Message = "No rows were imported. Correct every error and try again.", Errors = errors });
        }

        var history = await CreateExcelHistory("student-health-visits", model.FileName, records.Count);
        foreach (var record in records) record.ImportHistory = history;
        _context.StudentHealthVisits.AddRange(records);
        _context.ImportHistories.Add(history);
        await _context.SaveChangesAsync();
        return Ok(ToResult(history));
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
        string sisUserId,
        string studentName,
        int rowNumber,
        List<string> errors,
        out StudentProfile? student)
    {
        if (!students.TryGetValue(sisUserId.Trim(), out student))
        {
            errors.Add($"Row {rowNumber}: student_id '{sisUserId}' is invalid.");
            return false;
        }

        if (!string.Equals(student.FullName.Trim(), studentName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Row {rowNumber}: student_name does not match student_id '{sisUserId}'.");
            return false;
        }

        return true;
    }

    private IQueryable<StudentProfile> VisibleStudents()
        => _userAccess.ApplyStudentScope(_context.StudentProfiles.AsNoTracking());

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

    private static ExcelImportResultDto ToResult(ImportHistory history) => new()
    {
        ModuleId = history.ModuleId,
        ImportedRows = history.ValidRows
    };
}
