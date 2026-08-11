using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/import-history")]
[ApiController]
[Authorize]
public class ImportHistoryController : ControllerBase
{
    private readonly AppDbContext _context;

    public ImportHistoryController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<ImportHistoryPageDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _context.ImportHistories
            .AsNoTracking()
            .Where(item => item.Status == "Valid");
        if (!User.IsInRole("Admin") &&
            !User.IsInRole("SchoolAdministration") &&
            !User.IsInRole("Principal"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            query = query.Where(item => item.ImportedByUserId == userId);
        }

        var totalCount = await query.CountAsync();
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);
        page = Math.Min(page, Math.Max(1, totalPages));
        var history = await query
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => ToDto(item))
            .ToListAsync();

        return Ok(new ImportHistoryPageDto
        {
            Items = history,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            TotalPages = totalPages
        });
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ImportHistoryDetailDto>> Detail(int id)
    {
        var history = await ManageableHistories().AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        if (history == null) return NotFound();

        var detail = new ImportHistoryDetailDto
        {
            Id = history.Id,
            ModuleId = history.ModuleId,
            FileName = history.FileName,
            ImportedAt = history.CreatedAt,
            ImportedBy = history.ImportedByUsername,
            ImportedByName = history.ImportedByName,
            Status = history.Status,
            ValidRows = history.ValidRows,
            InvalidRows = history.InvalidRows,
            Records = await LoadRecords(history)
        };
        return Ok(detail);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateHistory(int id, [FromBody] UpdateImportHistoryDto model)
    {
        var history = await ManageableHistories().FirstOrDefaultAsync(item => item.Id == id);
        if (history == null) return NotFound();
        history.FileName = model.FileName.Trim();
        history.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPut("{id:int}/records/{recordId:int}")]
    public async Task<IActionResult> UpdateRecord(
        int id, int recordId, [FromBody] UpdateImportHistoryRecordDto model)
    {
        var history = await ManageableHistories().FirstOrDefaultAsync(item => item.Id == id);
        if (history == null) return NotFound();

        var error = history.ModuleId switch
        {
            "student-behaviours" => await UpdateBehaviour(id, recordId, model.Values),
            "student-projects" => await UpdateProject(id, recordId, model.Values),
            "student-mental-health" => await UpdateMentalHealth(id, recordId, model.Values),
            "student-bus-attendance" => await UpdateBusAttendance(id, recordId, model.Values),
            _ => "This import module cannot be edited."
        };
        if (error != null) return BadRequest(new { Message = error });
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}/records/{recordId:int}")]
    public async Task<IActionResult> DeleteRecord(int id, int recordId)
    {
        var history = await ManageableHistories().FirstOrDefaultAsync(item => item.Id == id);
        if (history == null) return NotFound();

        var deleted = history.ModuleId switch
        {
            "student-behaviours" => await Remove(_context.StudentBehaviours, id, recordId),
            "student-projects" => await Remove(_context.StudentProjects, id, recordId),
            "student-mental-health" => await Remove(_context.StudentMentalHealthRecords, id, recordId),
            "student-bus-attendance" => await Remove(_context.StudentBusAttendanceRecords, id, recordId),
            _ => false
        };
        if (!deleted) return NotFound();
        history.ValidRows = Math.Max(0, history.ValidRows - 1);
        history.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteHistory(int id)
    {
        var history = await ManageableHistories().FirstOrDefaultAsync(item => item.Id == id);
        if (history == null) return NotFound();
        _context.ImportHistories.Remove(history);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    private IQueryable<ImportHistory> ManageableHistories()
    {
        var query = _context.ImportHistories.Where(item => item.Status == "Valid");
        if (User.IsInRole("Admin") ||
            User.IsInRole("SchoolAdministration") ||
            User.IsInRole("Principal")) return query;
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return query.Where(item => item.ImportedByUserId == userId);
    }

    private async Task<List<ImportHistoryRecordDto>> LoadRecords(ImportHistory history)
    {
        return history.ModuleId switch
        {
            "student-behaviours" => await LoadBehaviours(history.Id),
            "student-projects" => await LoadProjects(history.Id),
            "student-mental-health" => await LoadMentalHealth(history.Id),
            "student-bus-attendance" => await LoadBusAttendance(history.Id),
            _ => []
        };
    }

    private async Task<List<ImportHistoryRecordDto>> LoadBehaviours(int historyId)
    {
        var items = await _context.StudentBehaviours.AsNoTracking().Include(item => item.Student)
            .Where(item => item.ImportHistoryId == historyId).OrderByDescending(item => item.IncidentDate).ToListAsync();
        return items.Select(item => Record(item.Id, item.Student, item.CustomFieldsJson,
            ("incident_date", item.IncidentDate.ToString("yyyy-MM-dd")),
            ("behaviour_type", StudentClassificationService.CanonicalBehaviourTypeOrOriginal(item.BehaviourType)), ("notes", item.Notes))).ToList();
    }

    private async Task<List<ImportHistoryRecordDto>> LoadProjects(int historyId)
    {
        var items = await _context.StudentProjects.AsNoTracking().Include(item => item.Student)
            .Where(item => item.ImportHistoryId == historyId).OrderByDescending(item => item.CreatedAt).ToListAsync();
        return items.Select(item => Record(item.Id, item.Student, item.CustomFieldsJson,
            ("project_name", item.ProjectName), ("subject", item.Subject),
            ("status", item.Status), ("teacher_notes", item.TeacherNotes))).ToList();
    }

    private async Task<List<ImportHistoryRecordDto>> LoadMentalHealth(int historyId)
    {
        var items = await _context.StudentMentalHealthRecords.AsNoTracking().Include(item => item.Student)
            .Where(item => item.ImportHistoryId == historyId).OrderByDescending(item => item.RecordDate).ToListAsync();
        return items.Select(item => Record(item.Id, item.Student, item.CustomFieldsJson,
            ("record_date", item.RecordDate.ToString("yyyy-MM-dd")), ("academic_year", item.AcademicYear),
            ("concern_type", item.ConcernType), ("risk_level", item.RiskLevel), ("notes", item.Notes))).ToList();
    }

    private async Task<List<ImportHistoryRecordDto>> LoadBusAttendance(int historyId)
    {
        var items = await _context.StudentBusAttendanceRecords.AsNoTracking().Include(item => item.Student)
            .Where(item => item.ImportHistoryId == historyId).OrderByDescending(item => item.AttendanceDate).ToListAsync();
        return items.Select(item => Record(item.Id, item.Student, item.CustomFieldsJson,
            ("attendance_date", item.AttendanceDate.ToString("yyyy-MM-dd")), ("trip_direction", item.TripDirection),
            ("violation_type", item.ViolationType), ("bus_code", item.BusCode),
            ("route_name", item.RouteName), ("notes", item.Notes))).ToList();
    }

    private static ImportHistoryRecordDto Record(
        int id, StudentProfile student, string? customFieldsJson,
        params (string Key, string? Value)[] values)
    {
        var result = values.ToDictionary(item => item.Key, item => item.Value);
        foreach (var field in ImportCustomFieldService.Parse(customFieldsJson)) result[field.Key] = field.Value;
        return new ImportHistoryRecordDto
        {
            Id = id,
            SISUserId = student.SISUserId,
            StudentName = student.FullName,
            Values = result
        };
    }

    private async Task<string?> UpdateBehaviour(int historyId, int id, IDictionary<string, string?> values)
    {
        var item = await _context.StudentBehaviours.FirstOrDefaultAsync(x => x.Id == id && x.ImportHistoryId == historyId);
        if (item == null) return "The imported record was not found.";
        if (!TryDate(values, "incident_date", out var date)) return "incident_date is invalid.";
        var type = Required(values, "behaviour_type");
        if (type == null) return "behaviour_type is required.";
        if (!StudentClassificationService.TryCanonicalBehaviourType(type, out var canonicalType))
            return "behaviour_type is not supported.";
        var custom = await BuildCustomFields("student-behaviours", values);
        if (custom.Error != null) return custom.Error;
        item.IncidentDate = date; item.BehaviourType = canonicalType; item.Notes = Clean(Value(values, "notes"));
        item.CustomFieldsJson = custom.Json;
        item.UpdatedAt = DateTime.UtcNow;
        return null;
    }

    private async Task<string?> UpdateProject(int historyId, int id, IDictionary<string, string?> values)
    {
        var item = await _context.StudentProjects.FirstOrDefaultAsync(x => x.Id == id && x.ImportHistoryId == historyId);
        if (item == null) return "The imported record was not found.";
        var name = Required(values, "project_name"); var subject = Required(values, "subject");
        var status = Required(values, "status");
        if (name == null || subject == null || status == null) return "project_name, subject, and status are required.";
        var custom = await BuildCustomFields("student-projects", values);
        if (custom.Error != null) return custom.Error;
        item.ProjectName = name; item.Subject = subject; item.Status = status;
        item.TeacherNotes = Clean(Value(values, "teacher_notes")); item.CustomFieldsJson = custom.Json;
        item.UpdatedAt = DateTime.UtcNow;
        return null;
    }

    private async Task<string?> UpdateMentalHealth(int historyId, int id, IDictionary<string, string?> values)
    {
        var item = await _context.StudentMentalHealthRecords.FirstOrDefaultAsync(x => x.Id == id && x.ImportHistoryId == historyId);
        if (item == null) return "The imported record was not found.";
        if (!TryDate(values, "record_date", out var date)) return "record_date is invalid.";
        var year = Required(values, "academic_year"); var concern = Required(values, "concern_type");
        var risk = Required(values, "risk_level");
        if (year == null || concern == null || risk == null) return "academic_year, concern_type, and risk_level are required.";
        var custom = await BuildCustomFields("student-mental-health", values);
        if (custom.Error != null) return custom.Error;
        item.RecordDate = date; item.AcademicYear = year; item.ConcernType = concern; item.RiskLevel = risk;
        item.Notes = Clean(Value(values, "notes")); item.CustomFieldsJson = custom.Json;
        item.UpdatedAt = DateTime.UtcNow;
        return null;
    }

    private async Task<string?> UpdateBusAttendance(int historyId, int id, IDictionary<string, string?> values)
    {
        var item = await _context.StudentBusAttendanceRecords.FirstOrDefaultAsync(x => x.Id == id && x.ImportHistoryId == historyId);
        if (item == null) return "The imported record was not found.";
        if (!TryDate(values, "attendance_date", out var date)) return "attendance_date is invalid.";
        var direction = Required(values, "trip_direction"); var violation = Required(values, "violation_type");
        if (direction == null || violation == null) return "trip_direction and violation_type are required.";
        var custom = await BuildCustomFields("student-bus-attendance", values);
        if (custom.Error != null) return custom.Error;
        item.AttendanceDate = date; item.TripDirection = direction; item.ViolationType = violation;
        item.BusCode = Clean(Value(values, "bus_code")); item.RouteName = Clean(Value(values, "route_name"));
        item.Notes = Clean(Value(values, "notes")); item.CustomFieldsJson = custom.Json;
        item.UpdatedAt = DateTime.UtcNow;
        return null;
    }

    private async Task<(string? Error, string? Json)> BuildCustomFields(
        string moduleId, IDictionary<string, string?> values)
    {
        var fields = await _context.ImportModuleFields.AsNoTracking()
            .Where(field => field.ModuleId == moduleId && field.IsActive)
            .OrderBy(field => field.SortOrder)
            .ToListAsync();
        var result = ImportCustomFieldService.Build(fields, values);
        return result.Errors.Count == 0
            ? (null, result.Json)
            : (string.Join(" ", result.Errors), null);
    }

    private static async Task<bool> Remove<T>(DbSet<T> set, int historyId, int recordId) where T : BaseEntity
    {
        var item = await set.FirstOrDefaultAsync(entity => entity.Id == recordId &&
            EF.Property<int?>(entity, "ImportHistoryId") == historyId);
        if (item == null) return false;
        set.Remove(item);
        return true;
    }

    private static string? Value(IDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;
    private static string? Required(IDictionary<string, string?> values, string key) => Clean(Value(values, key));
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static bool TryDate(IDictionary<string, string?> values, string key, out DateTime date)
    {
        if (DateTime.TryParse(Value(values, key), out date))
        {
            date = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            return true;
        }
        return false;
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
