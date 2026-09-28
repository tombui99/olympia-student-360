using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/career-guidance")]
[ApiController]
[Authorize]
public sealed class StudentCareerGuidanceController(AppDbContext context, UserAccessService userAccess) : ControllerBase
{
    public static readonly string[] RecordTypes =
        ["GeneralPlan", "UniversityApplication", "Preparation", "Internship", "Counseling"];
    public static readonly string[] Statuses = ["Planned", "InProgress", "NeedsAttention", "Completed"];

    [HttpGet]
    public async Task<ActionResult<StudentCareerGuidancePageDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? recordType = null,
        [FromQuery] string? status = null,
        [FromQuery] string? academicYear = null,
        [FromQuery] string? term = null,
        [FromQuery] int? studentId = null)
    {
        if (!userAccess.Current.CanAccessAcademic) return Forbid();

        var query = ApplyFilters(VisibleRecords(), search, recordType, status, academicYear, term, studentId);
        return Ok(await ToPage(query, page, pageSize));
    }

    [HttpGet("student/{studentId:int}")]
    public async Task<ActionResult<StudentCareerGuidancePageDto>> ForStudent(
        int studentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 5,
        [FromQuery] string? search = null,
        [FromQuery] string? recordType = null,
        [FromQuery] string? status = null,
        [FromQuery] string? academicYear = null,
        [FromQuery] string? term = null)
    {
        if (!userAccess.Current.CanAccessAcademic) return Forbid();
        if (!await VisibleStudents().AnyAsync(student => student.Id == studentId)) return NotFound();

        var query = ApplyFilters(VisibleRecords(), search, recordType, status, academicYear, term, studentId);
        return Ok(await ToPage(query, page, pageSize));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<StudentCareerGuidanceRecordDto>> Get(int id)
    {
        if (!userAccess.Current.CanAccessAcademic) return Forbid();
        var record = await VisibleRecords().FirstOrDefaultAsync(item => item.Id == id);
        return record == null ? NotFound() : Ok(ToDto(record));
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SchoolAdministration,Principal,Teacher")]
    public async Task<ActionResult<StudentCareerGuidanceRecordDto>> Create(SaveStudentCareerGuidanceRecordDto model)
    {
        if (!userAccess.Current.CanAccessAcademic) return Forbid();
        var student = await VisibleStudents().FirstOrDefaultAsync(item => item.Id == model.StudentId);
        if (student == null)
            return BadRequest(new { Message = "The selected student is invalid or not visible to your account." });

        var normalized = ValidateAndNormalize(model);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var (userId, userName) = await CurrentUser();
        var record = new StudentCareerGuidanceRecord
        {
            StudentProfileId = student.Id,
            RecordedByUserId = userId,
            RecordedByName = userName
        };
        Apply(record, model, normalized.RecordType!, normalized.Status!);
        context.StudentCareerGuidanceRecords.Add(record);
        await context.SaveChangesAsync();
        record.Student = student;
        return Created($"/api/career-guidance/{record.Id}", ToDto(record));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,SchoolAdministration,Principal,Teacher")]
    public async Task<ActionResult<StudentCareerGuidanceRecordDto>> Update(
        int id, SaveStudentCareerGuidanceRecordDto model)
    {
        if (!userAccess.Current.CanAccessAcademic) return Forbid();
        var record = await EditableRecords().Include(item => item.Student)
            .FirstOrDefaultAsync(item => item.Id == id);
        if (record == null) return NotFound();

        if (record.StudentProfileId != model.StudentId &&
            !await VisibleStudents().AnyAsync(student => student.Id == model.StudentId))
            return BadRequest(new { Message = "The selected student is invalid or not visible to your account." });

        var normalized = ValidateAndNormalize(model);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var (userId, userName) = await CurrentUser();
        record.StudentProfileId = model.StudentId;
        record.UpdatedByUserId = userId;
        record.UpdatedByName = userName;
        record.UpdatedAt = DateTime.UtcNow;
        Apply(record, model, normalized.RecordType!, normalized.Status!);
        await context.SaveChangesAsync();
        record.Student = await VisibleStudents().FirstAsync(student => student.Id == model.StudentId);
        return Ok(ToDto(record));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,SchoolAdministration,Principal,Teacher")]
    public async Task<IActionResult> Delete(int id)
    {
        if (!userAccess.Current.CanAccessAcademic) return Forbid();
        var record = await EditableRecords().FirstOrDefaultAsync(item => item.Id == id);
        if (record == null) return NotFound();
        context.StudentCareerGuidanceRecords.Remove(record);
        await context.SaveChangesAsync();
        return NoContent();
    }

    private (string? RecordType, string? Status) ValidateAndNormalize(SaveStudentCareerGuidanceRecordDto model)
    {
        var recordType = Canonical(RecordTypes, model.RecordType);
        var status = Canonical(Statuses, model.Status);
        if (recordType == null)
            ModelState.AddModelError(nameof(model.RecordType), "Select a valid career guidance record type.");
        if (status == null)
            ModelState.AddModelError(nameof(model.Status), "Select a valid status.");
        if (model.InternshipStartDate.HasValue && model.InternshipEndDate.HasValue &&
            model.InternshipEndDate.Value.Date < model.InternshipStartDate.Value.Date)
            ModelState.AddModelError(nameof(model.InternshipEndDate),
                "The internship end date cannot be before its start date.");
        return (recordType, status);
    }

    private static void Apply(StudentCareerGuidanceRecord record, SaveStudentCareerGuidanceRecordDto model,
        string recordType, string status)
    {
        var recordDate = UtcDate(model.RecordDate!.Value);
        record.RecordType = recordType;
        record.RecordDate = recordDate;
        record.AcademicYear = string.IsNullOrWhiteSpace(model.AcademicYear)
            ? DashboardPeriodNormalizer.AcademicYear(recordDate)
            : DashboardPeriodNormalizer.AcademicYear(model.AcademicYear);
        record.Term = Clean(model.Term);
        record.Title = model.Title.Trim();
        record.Status = status;
        record.TargetCountries = Clean(model.TargetCountries);
        record.TargetUniversities = Clean(model.TargetUniversities);
        record.TargetMajors = Clean(model.TargetMajors);
        record.CareerInterests = Clean(model.CareerInterests);
        record.ApplicationProgressPercent = model.ApplicationProgressPercent;
        record.PreparationProgressPercent = model.PreparationProgressPercent;
        record.QualityRating = model.QualityRating;
        record.ActivitiesSummary = Clean(model.ActivitiesSummary);
        record.Issues = Clean(model.Issues);
        record.Solutions = Clean(model.Solutions);
        record.StudentSupport = Clean(model.StudentSupport);
        record.StudentFeedback = Clean(model.StudentFeedback);
        record.NextSteps = Clean(model.NextSteps);
        record.Recommendations = Clean(model.Recommendations);
        record.Proposals = Clean(model.Proposals);
        record.InternshipCompany = Clean(model.InternshipCompany);
        record.InternshipRole = Clean(model.InternshipRole);
        record.InternshipStartDate = UtcDate(model.InternshipStartDate);
        record.InternshipEndDate = UtcDate(model.InternshipEndDate);
        record.InternshipProgressPercent = model.InternshipProgressPercent;
        record.InternshipQualityRating = model.InternshipQualityRating;
        record.StudentSelfAssessment = Clean(model.StudentSelfAssessment);
        record.HostAssessment = Clean(model.HostAssessment);
        record.FinalProduct = Clean(model.FinalProduct);
    }

    private IQueryable<StudentProfile> VisibleStudents() =>
        userAccess.ApplyStudentScope(context.StudentProfiles.AsNoTracking());

    private IQueryable<StudentCareerGuidanceRecord> VisibleRecords() =>
        userAccess.ApplyStudentScope(
            context.StudentCareerGuidanceRecords.AsNoTracking().Include(item => item.Student),
            item => item.Student);

    private IQueryable<StudentCareerGuidanceRecord> EditableRecords() =>
        userAccess.ApplyStudentScope(context.StudentCareerGuidanceRecords, item => item.Student);

    private static IQueryable<StudentCareerGuidanceRecord> ApplyFilters(
        IQueryable<StudentCareerGuidanceRecord> query,
        string? search,
        string? recordType,
        string? status,
        string? academicYear,
        string? term,
        int? studentId)
    {
        var searchTerm = Clean(search);
        if (searchTerm != null)
        {
            searchTerm = searchTerm[..Math.Min(searchTerm.Length, 100)];
            query = query.Where(item =>
                item.Student.FullName.Contains(searchTerm) ||
                item.Student.SISUserId.Contains(searchTerm) ||
                item.Title.Contains(searchTerm) ||
                (item.TargetCountries != null && item.TargetCountries.Contains(searchTerm)) ||
                (item.TargetUniversities != null && item.TargetUniversities.Contains(searchTerm)) ||
                (item.TargetMajors != null && item.TargetMajors.Contains(searchTerm)) ||
                (item.CareerInterests != null && item.CareerInterests.Contains(searchTerm)) ||
                item.RecordedByName.Contains(searchTerm));
        }

        var canonicalRecordType = Canonical(RecordTypes, recordType);
        if (canonicalRecordType != null) query = query.Where(item => item.RecordType == canonicalRecordType);
        var canonicalStatus = Canonical(Statuses, status);
        if (canonicalStatus != null) query = query.Where(item => item.Status == canonicalStatus);
        if (!string.IsNullOrWhiteSpace(academicYear) && academicYear != "all")
        {
            var year = DashboardPeriodNormalizer.AcademicYear(academicYear);
            query = query.Where(item => item.AcademicYear == year);
        }
        if (!string.IsNullOrWhiteSpace(term) && term != "all")
        {
            var normalizedTerm = DashboardPeriodNormalizer.Term(term);
            query = query.Where(item => item.Term == normalizedTerm);
        }
        if (studentId.HasValue) query = query.Where(item => item.StudentProfileId == studentId.Value);
        return query;
    }

    private async Task<StudentCareerGuidancePageDto> ToPage(
        IQueryable<StudentCareerGuidanceRecord> query, int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await query.CountAsync();
        var activeCount = await query.CountAsync(item => item.Status != "Completed");
        var inProgressCount = await query.CountAsync(item => item.Status == "InProgress");
        var needsAttentionCount = await query.CountAsync(item => item.Status == "NeedsAttention");
        var completedCount = await query.CountAsync(item => item.Status == "Completed");
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Min(page, totalPages);
        var rows = await query.OrderByDescending(item => item.RecordDate)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        var years = await context.AcademicPeriods.AsNoTracking()
            .Select(period => period.AcademicYear)
            .Concat(VisibleRecords().Select(item => item.AcademicYear))
            .Distinct()
            .OrderByDescending(value => value)
            .ToListAsync();

        return new StudentCareerGuidancePageDto
        {
            Items = rows.Select(ToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            TotalPages = totalPages,
            ActiveCount = activeCount,
            InProgressCount = inProgressCount,
            NeedsAttentionCount = needsAttentionCount,
            CompletedCount = completedCount,
            AvailableAcademicYears = years
        };
    }

    private async Task<(string UserId, string UserName)> CurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var name = await context.Users.Where(user => user.Id == userId)
            .Select(user => user.FullName).FirstOrDefaultAsync();
        return (userId, string.IsNullOrWhiteSpace(name) ? User.Identity?.Name ?? "Unknown" : name);
    }

    private static StudentCareerGuidanceRecordDto ToDto(StudentCareerGuidanceRecord item) => new()
    {
        Id = item.Id,
        StudentId = item.StudentProfileId,
        StudentName = item.Student.FullName,
        SISUserId = item.Student.SISUserId,
        GradeLevel = item.Student.GradeLevel ?? item.Student.CanvasGradeLevel,
        Homeroom = item.Student.Homeroom,
        RecordType = item.RecordType,
        RecordDate = item.RecordDate,
        AcademicYear = item.AcademicYear,
        Term = item.Term,
        Title = item.Title,
        Status = item.Status,
        TargetCountries = item.TargetCountries,
        TargetUniversities = item.TargetUniversities,
        TargetMajors = item.TargetMajors,
        CareerInterests = item.CareerInterests,
        ApplicationProgressPercent = item.ApplicationProgressPercent,
        PreparationProgressPercent = item.PreparationProgressPercent,
        QualityRating = item.QualityRating,
        ActivitiesSummary = item.ActivitiesSummary,
        Issues = item.Issues,
        Solutions = item.Solutions,
        StudentSupport = item.StudentSupport,
        StudentFeedback = item.StudentFeedback,
        NextSteps = item.NextSteps,
        Recommendations = item.Recommendations,
        Proposals = item.Proposals,
        InternshipCompany = item.InternshipCompany,
        InternshipRole = item.InternshipRole,
        InternshipStartDate = item.InternshipStartDate,
        InternshipEndDate = item.InternshipEndDate,
        InternshipProgressPercent = item.InternshipProgressPercent,
        InternshipQualityRating = item.InternshipQualityRating,
        StudentSelfAssessment = item.StudentSelfAssessment,
        HostAssessment = item.HostAssessment,
        FinalProduct = item.FinalProduct,
        RecordedByName = item.RecordedByName,
        UpdatedByName = item.UpdatedByName,
        CreatedAt = item.CreatedAt,
        UpdatedAt = item.UpdatedAt
    };

    private static DateTime UtcDate(DateTime value) =>
        DateTime.SpecifyKind(value.Date, DateTimeKind.Utc);

    private static DateTime? UtcDate(DateTime? value) => value.HasValue ? UtcDate(value.Value) : null;

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Canonical(IEnumerable<string> values, string? value) => values.FirstOrDefault(item =>
        string.Equals(item, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}
