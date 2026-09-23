using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/module-records")]
[ApiController]
[Authorize]
public sealed class ModuleRecordsController(AppDbContext context) : ControllerBase
{
    [HttpGet("{moduleId}")]
    public async Task<ActionResult<ModuleRecordsPageDto>> List(
        string moduleId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? academicYear = null,
        [FromQuery(Name = "term")] string? reportingTerm = null)
    {
        if (!ImportCustomFieldService.IsKnownModule(moduleId)) return NotFound();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var searchTerm = Clean(search);
        if (searchTerm?.Length > 100) searchTerm = searchTerm[..100];
        (DateTime Start, DateTime End)? period = string.IsNullOrWhiteSpace(academicYear)
            ? null
            : DashboardPeriodNormalizer.PeriodRange(academicYear, reportingTerm);

        return moduleId switch
        {
            "student-behaviours" => Ok(await ListBehaviours(page, pageSize, searchTerm, period)),
            "student-projects" => Ok(await ListProjects(page, pageSize, searchTerm, period)),
            "student-mental-health" => Ok(await ListMentalHealth(page, pageSize, searchTerm, period)),
            "student-bus-attendance" => Ok(await ListBusAttendance(page, pageSize, searchTerm, period)),
            "student-health-visits" => Ok(await ListHealthVisits(page, pageSize, searchTerm, period)),
            _ => NotFound()
        };
    }

    [HttpPut("{moduleId}/{id:int}")]
    public async Task<IActionResult> Update(string moduleId, int id, UpdateModuleRecordDto model)
    {
        if (!ImportCustomFieldService.IsKnownModule(moduleId)) return NotFound();
        var error = moduleId switch
        {
            "student-behaviours" => await UpdateBehaviour(id, model.Values),
            "student-projects" => await UpdateProject(id, model.Values),
            "student-mental-health" => await UpdateMentalHealth(id, model.Values),
            "student-bus-attendance" => await UpdateBusAttendance(id, model.Values),
            "student-health-visits" => await UpdateHealthVisit(id, model.Values),
            _ => "This module is not supported."
        };
        if (error == "not-found") return NotFound();
        if (error != null) return BadRequest(new { Message = error });
        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpDelete("{moduleId}/{id:int}")]
    public async Task<IActionResult> Delete(string moduleId, int id)
    {
        if (!ImportCustomFieldService.IsKnownModule(moduleId)) return NotFound();
        int? historyId = null;
        var deleted = false;
        switch (moduleId)
        {
            case "student-behaviours":
            {
                var item = await context.StudentBehaviours.FindAsync(id);
                if (item != null) { historyId = item.ImportHistoryId; context.StudentBehaviours.Remove(item); deleted = true; }
                break;
            }
            case "student-projects":
            {
                var item = await context.StudentProjects.FindAsync(id);
                if (item != null) { historyId = item.ImportHistoryId; context.StudentProjects.Remove(item); deleted = true; }
                break;
            }
            case "student-mental-health":
            {
                var item = await context.StudentMentalHealthRecords.FindAsync(id);
                if (item != null) { historyId = item.ImportHistoryId; context.StudentMentalHealthRecords.Remove(item); deleted = true; }
                break;
            }
            case "student-bus-attendance":
            {
                var item = await context.StudentBusAttendanceRecords.FindAsync(id);
                if (item != null) { historyId = item.ImportHistoryId; context.StudentBusAttendanceRecords.Remove(item); deleted = true; }
                break;
            }
            case "student-health-visits":
            {
                var item = await context.StudentHealthVisits.FindAsync(id);
                if (item != null) { historyId = item.ImportHistoryId; context.StudentHealthVisits.Remove(item); deleted = true; }
                break;
            }
        }
        if (!deleted) return NotFound();
        if (historyId.HasValue)
        {
            var history = await context.ImportHistories.FindAsync(historyId.Value);
            if (history != null)
            {
                history.ValidRows = Math.Max(0, history.ValidRows - 1);
                history.UpdatedAt = DateTime.UtcNow;
            }
        }
        await context.SaveChangesAsync();
        return NoContent();
    }

    private async Task<ModuleRecordsPageDto> ListBehaviours(
        int page, int pageSize, string? term, (DateTime Start, DateTime End)? period)
    {
        var query = context.StudentBehaviours.AsNoTracking()
            .Include(item => item.Student).Include(item => item.ImportHistory).AsQueryable();
        if (period.HasValue)
            query = query.Where(item => item.IncidentDate >= period.Value.Start && item.IncidentDate < period.Value.End);
        if (term != null)
        {
            var pattern = $"%{EscapeLikePattern(term)}%";
            query = query.Where(item => EF.Functions.Like(item.Student.FullName, pattern, "!") ||
                EF.Functions.Like(item.Student.SISUserId, pattern, "!") ||
                EF.Functions.Like(item.BehaviourType, pattern, "!") ||
                item.Notes != null && EF.Functions.Like(item.Notes, pattern, "!"));
        }
        var total = await query.CountAsync();
        page = NormalizedPage(page, pageSize, total);
        var items = await query.OrderByDescending(item => item.IncidentDate).ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Page(items.Select(item => Record("student-behaviours", item, item.Student,
            item.ImportHistory?.ImportedByName ?? item.RecordedByName,
            ("incident_date", Date(item.IncidentDate)),
            ("behaviour_type", StudentClassificationService.CanonicalBehaviourTypeOrOriginal(item.BehaviourType)),
            ("notes", item.Notes))).ToList(), page, pageSize, total);
    }

    private async Task<ModuleRecordsPageDto> ListProjects(
        int page, int pageSize, string? term, (DateTime Start, DateTime End)? period)
    {
        var query = context.StudentProjects.AsNoTracking()
            .Include(item => item.Student).Include(item => item.ImportHistory).AsQueryable();
        if (period.HasValue)
            query = query.Where(item => (item.ActivityDate ?? item.CreatedAt) >= period.Value.Start &&
                (item.ActivityDate ?? item.CreatedAt) < period.Value.End);
        if (term != null)
        {
            var pattern = $"%{EscapeLikePattern(term)}%";
            query = query.Where(item => EF.Functions.Like(item.Student.FullName, pattern, "!") ||
                EF.Functions.Like(item.Student.SISUserId, pattern, "!") ||
                EF.Functions.Like(item.ProjectName, pattern, "!") ||
                item.Subject != null && EF.Functions.Like(item.Subject, pattern, "!") ||
                EF.Functions.Like(item.Status, pattern, "!"));
        }
        var total = await query.CountAsync();
        page = NormalizedPage(page, pageSize, total);
        var items = await query.OrderByDescending(item => item.ActivityDate ?? item.CreatedAt).ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Page(items.Select(item => Record("student-projects", item, item.Student,
            item.ImportHistory?.ImportedByName,
            ("activity_date", Date(item.ActivityDate ?? item.CreatedAt)),
            ("project_name", item.ProjectName), ("subject", item.Subject),
            ("status", item.Status), ("teacher_notes", item.TeacherNotes))).ToList(), page, pageSize, total);
    }

    private async Task<ModuleRecordsPageDto> ListMentalHealth(
        int page, int pageSize, string? term, (DateTime Start, DateTime End)? period)
    {
        var query = context.StudentMentalHealthRecords.AsNoTracking()
            .Include(item => item.Student).Include(item => item.ImportHistory).AsQueryable();
        if (period.HasValue)
            query = query.Where(item => item.RecordDate >= period.Value.Start && item.RecordDate < period.Value.End);
        if (term != null)
        {
            var pattern = $"%{EscapeLikePattern(term)}%";
            query = query.Where(item => EF.Functions.Like(item.Student.FullName, pattern, "!") ||
                EF.Functions.Like(item.Student.SISUserId, pattern, "!") ||
                EF.Functions.Like(item.ConcernType, pattern, "!") ||
                EF.Functions.Like(item.RiskLevel, pattern, "!") ||
                item.Notes != null && EF.Functions.Like(item.Notes, pattern, "!"));
        }
        var total = await query.CountAsync();
        page = NormalizedPage(page, pageSize, total);
        var items = await query.OrderByDescending(item => item.RecordDate).ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Page(items.Select(item => Record("student-mental-health", item, item.Student,
            item.ImportHistory?.ImportedByName ?? item.RecordedByName,
            ("record_date", Date(item.RecordDate)), ("academic_year", item.AcademicYear),
            ("concern_type", item.ConcernType), ("risk_level", item.RiskLevel),
            ("notes", item.Notes))).ToList(), page, pageSize, total);
    }

    private async Task<ModuleRecordsPageDto> ListBusAttendance(
        int page, int pageSize, string? term, (DateTime Start, DateTime End)? period)
    {
        var query = context.StudentBusAttendanceRecords.AsNoTracking()
            .Include(item => item.Student).Include(item => item.ImportHistory).AsQueryable();
        if (period.HasValue)
            query = query.Where(item => item.AttendanceDate >= period.Value.Start && item.AttendanceDate < period.Value.End);
        if (term != null)
        {
            var pattern = $"%{EscapeLikePattern(term)}%";
            query = query.Where(item => EF.Functions.Like(item.Student.FullName, pattern, "!") ||
                EF.Functions.Like(item.Student.SISUserId, pattern, "!") ||
                EF.Functions.Like(item.TripDirection, pattern, "!") ||
                EF.Functions.Like(item.ViolationType, pattern, "!") ||
                item.RouteName != null && EF.Functions.Like(item.RouteName, pattern, "!"));
        }
        var total = await query.CountAsync();
        page = NormalizedPage(page, pageSize, total);
        var items = await query.OrderByDescending(item => item.AttendanceDate).ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Page(items.Select(item => Record("student-bus-attendance", item, item.Student,
            item.ImportHistory?.ImportedByName ?? item.RecordedByName,
            ("attendance_date", Date(item.AttendanceDate)), ("trip_direction", item.TripDirection),
            ("violation_type", item.ViolationType), ("bus_code", item.BusCode),
            ("route_name", item.RouteName), ("notes", item.Notes))).ToList(), page, pageSize, total);
    }

    private async Task<ModuleRecordsPageDto> ListHealthVisits(
        int page, int pageSize, string? term, (DateTime Start, DateTime End)? period)
    {
        var query = context.StudentHealthVisits.AsNoTracking()
            .Include(item => item.Student).Include(item => item.ImportHistory).AsQueryable();
        if (period.HasValue)
            query = query.Where(item => item.ExamTime >= period.Value.Start && item.ExamTime < period.Value.End);
        if (term != null)
        {
            var pattern = $"%{EscapeLikePattern(term)}%";
            query = query.Where(item => EF.Functions.Like(item.FullName, pattern, "!") ||
                item.SISUserId != null && EF.Functions.Like(item.SISUserId, pattern, "!") ||
                item.ClassName != null && EF.Functions.Like(item.ClassName, pattern, "!") ||
                item.Symptoms != null && EF.Functions.Like(item.Symptoms, pattern, "!") ||
                item.Diagnosis != null && EF.Functions.Like(item.Diagnosis, pattern, "!") ||
                item.Classification != null && EF.Functions.Like(item.Classification, pattern, "!") ||
                item.Notes != null && EF.Functions.Like(item.Notes, pattern, "!") ||
                item.ExaminedBy != null && EF.Functions.Like(item.ExaminedBy, pattern, "!"));
        }
        var total = await query.CountAsync();
        page = NormalizedPage(page, pageSize, total);
        var items = await query.OrderByDescending(item => item.ExamTime).ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var records = items.Select(item =>
        {
            var record = Record("student-health-visits", item, item.Student, item.ImportHistory?.ImportedByName,
                ("visit_date", item.ExamTime.ToString("yyyy-MM-ddTHH:mm")), ("symptoms", item.Symptoms),
                ("diagnosis", item.Diagnosis), ("classification", item.Classification),
                ("treatment", item.Treatment), ("medication", item.Medication),
                ("notes", item.Notes), ("notification_sent", item.NotificationSent ? "true" : "false"),
                ("examined_by", item.ExaminedBy));
            record.Homeroom = item.ClassName ?? record.Homeroom;
            return record;
        }).ToList();
        return Page(records, page, pageSize, total);
    }

    private async Task<string?> UpdateBehaviour(int id, IDictionary<string, string?> values)
    {
        var item = await context.StudentBehaviours.FindAsync(id);
        if (item == null) return "not-found";
        if (!TryDate(values, "incident_date", out var date)) return "incident_date is invalid.";
        var type = Required(values, "behaviour_type");
        if (type == null || !StudentClassificationService.TryCanonicalBehaviourType(type, out var canonical))
            return "behaviour_type is invalid.";
        var custom = await CustomFields("student-behaviours", values);
        if (custom.Error != null) return custom.Error;
        item.IncidentDate = date; item.BehaviourType = canonical; item.Notes = Clean(Value(values, "notes"));
        item.CustomFieldsJson = custom.Json; item.UpdatedAt = DateTime.UtcNow;
        return null;
    }

    private async Task<string?> UpdateProject(int id, IDictionary<string, string?> values)
    {
        var item = await context.StudentProjects.FindAsync(id);
        if (item == null) return "not-found";
        var name = Required(values, "project_name");
        var subject = Canonical(StudentDataEntryOptions.ProjectSubjects, Required(values, "subject"));
        var status = Canonical(StudentDataEntryOptions.ProjectStatuses, Required(values, "status"));
        if (name == null || subject == null || status == null) return "project_name, subject, or status is invalid.";
        var custom = await CustomFields("student-projects", values);
        if (custom.Error != null) return custom.Error;
        item.ProjectName = name; item.Subject = subject; item.Status = status;
        item.TeacherNotes = Clean(Value(values, "teacher_notes")); item.CustomFieldsJson = custom.Json;
        item.UpdatedAt = DateTime.UtcNow;
        return null;
    }

    private async Task<string?> UpdateMentalHealth(int id, IDictionary<string, string?> values)
    {
        var item = await context.StudentMentalHealthRecords.FindAsync(id);
        if (item == null) return "not-found";
        if (!TryDate(values, "record_date", out var date)) return "record_date is invalid.";
        var year = Required(values, "academic_year");
        var concern = Canonical(StudentDataEntryOptions.MentalHealthConcernTypes, Required(values, "concern_type"));
        var risk = Canonical(StudentDataEntryOptions.MentalHealthRiskLevels, Required(values, "risk_level"));
        if (year == null || concern == null || risk == null) return "academic_year, concern_type, or risk_level is invalid.";
        if (!await context.AcademicPeriods.AsNoTracking().AnyAsync(period => period.AcademicYear == year))
            return "academic_year is not configured.";
        var custom = await CustomFields("student-mental-health", values);
        if (custom.Error != null) return custom.Error;
        item.RecordDate = date; item.AcademicYear = year; item.ConcernType = concern; item.RiskLevel = risk;
        item.Notes = Clean(Value(values, "notes")); item.CustomFieldsJson = custom.Json; item.UpdatedAt = DateTime.UtcNow;
        return null;
    }

    private async Task<string?> UpdateBusAttendance(int id, IDictionary<string, string?> values)
    {
        var item = await context.StudentBusAttendanceRecords.FindAsync(id);
        if (item == null) return "not-found";
        if (!TryDate(values, "attendance_date", out var date)) return "attendance_date is invalid.";
        var direction = Canonical(StudentDataEntryOptions.BusTripDirections, Required(values, "trip_direction"));
        var violation = Canonical(StudentDataEntryOptions.BusViolationTypes, Required(values, "violation_type"));
        if (direction == null || violation == null) return "trip_direction or violation_type is invalid.";
        var custom = await CustomFields("student-bus-attendance", values);
        if (custom.Error != null) return custom.Error;
        item.AttendanceDate = date; item.TripDirection = direction; item.ViolationType = violation;
        item.BusCode = Clean(Value(values, "bus_code")); item.RouteName = Clean(Value(values, "route_name"));
        item.Notes = Clean(Value(values, "notes")); item.CustomFieldsJson = custom.Json; item.UpdatedAt = DateTime.UtcNow;
        return null;
    }

    private async Task<string?> UpdateHealthVisit(int id, IDictionary<string, string?> values)
    {
        var item = await context.StudentHealthVisits.FindAsync(id);
        if (item == null) return "not-found";
        if (!TryDateTime(values, "visit_date", out var date)) return "visit_date is invalid.";
        var symptoms = Required(values, "symptoms");
        if (symptoms == null) return "symptoms is required.";
        var custom = await CustomFields("student-health-visits", values);
        if (custom.Error != null) return custom.Error;
        item.ExamTime = date; item.Symptoms = symptoms;
        item.Diagnosis = Clean(Value(values, "diagnosis"));
        item.Classification = Clean(Value(values, "classification"));
        item.Treatment = Clean(Value(values, "treatment"));
        item.Medication = Clean(Value(values, "medication"));
        item.Notes = Clean(Value(values, "notes"));
        item.NotificationSent = Checkbox(Value(values, "notification_sent"));
        if (values.ContainsKey("examined_by")) item.ExaminedBy = Clean(Value(values, "examined_by"));
        item.CustomFieldsJson = custom.Json; item.UpdatedAt = DateTime.UtcNow;
        return null;
    }

    private async Task<(string? Error, string? Json)> CustomFields(
        string moduleId, IDictionary<string, string?> values)
    {
        var fields = await context.ImportModuleFields.AsNoTracking()
            .Where(field => field.ModuleId == moduleId && field.IsActive)
            .OrderBy(field => field.SortOrder).ThenBy(field => field.Id).ToListAsync();
        var result = ImportCustomFieldService.Build(fields, values);
        return result.Errors.Count == 0 ? (null, result.Json) : (string.Join(" ", result.Errors), null);
    }

    private static ModuleRecordDto Record(
        string moduleId, BaseEntity entity, StudentProfile? student, string? enteredByName,
        params (string Key, string? Value)[] standardValues)
    {
        var values = standardValues.ToDictionary(item => item.Key, item => item.Value);
        var customJson = entity switch
        {
            StudentBehaviour item => item.CustomFieldsJson,
            StudentProject item => item.CustomFieldsJson,
            StudentMentalHealthRecord item => item.CustomFieldsJson,
            StudentBusAttendanceRecord item => item.CustomFieldsJson,
            StudentHealthVisit item => item.CustomFieldsJson,
            _ => null
        };
        foreach (var field in ImportCustomFieldService.Parse(customJson)) values[field.Key] = field.Value;
        var healthVisit = entity as StudentHealthVisit;
        return new ModuleRecordDto
        {
            Id = entity.Id, ModuleId = moduleId, StudentProfileId = student?.Id ?? healthVisit?.StudentProfileId,
            SISUserId = student?.SISUserId ?? healthVisit?.SISUserId ?? string.Empty,
            StudentName = student?.FullName ?? healthVisit?.FullName ?? string.Empty,
            GradeLevel = student?.GradeLevel ?? student?.CanvasGradeLevel,
            Homeroom = healthVisit?.ClassName ?? student?.Homeroom,
            CanvasGradeLevel = student?.CanvasGradeLevel,
            EnteredByName = enteredByName, CreatedAt = entity.CreatedAt, Values = values
        };
    }

    private static ModuleRecordsPageDto Page(
        List<ModuleRecordDto> items, int page, int pageSize, int total) => new()
    {
        Items = items, Page = page, PageSize = pageSize, TotalCount = total,
        TotalPages = total == 0 ? 0 : (int)Math.Ceiling((double)total / pageSize)
    };

    private static int NormalizedPage(int page, int pageSize, int total)
    {
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling((double)total / pageSize);
        return Math.Min(page, Math.Max(1, totalPages));
    }

    private static string Date(DateTime value) => value.ToString("yyyy-MM-dd");
    private static string? Value(IDictionary<string, string?> values, string key) =>
        values.TryGetValue(key, out var value) ? value : null;
    private static string? Required(IDictionary<string, string?> values, string key) => Clean(Value(values, key));
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Canonical(IReadOnlyList<string> options, string? value) => value == null
        ? null : options.FirstOrDefault(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase));
    private static bool TryDate(IDictionary<string, string?> values, string key, out DateTime date)
    {
        if (DateTime.TryParse(Value(values, key), out date))
        {
            date = DateTime.SpecifyKind(date.Date, DateTimeKind.Utc);
            return true;
        }
        return false;
    }
    private static bool TryDateTime(IDictionary<string, string?> values, string key, out DateTime date)
    {
        if (DateTime.TryParse(Value(values, key), out date))
        {
            date = DateTime.SpecifyKind(date, DateTimeKind.Utc);
            return true;
        }
        return false;
    }
    private static bool Checkbox(string? value) => value?.Trim().ToLowerInvariant() is "true" or "1" or "yes" or "y" or "on" or "có";
    private static string EscapeLikePattern(string value) =>
        value.Replace("!", "!!", StringComparison.Ordinal)
            .Replace("%", "!%", StringComparison.Ordinal)
            .Replace("_", "!_", StringComparison.Ordinal);
}
