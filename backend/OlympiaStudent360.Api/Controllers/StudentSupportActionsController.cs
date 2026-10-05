using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/support-actions")]
[ApiController]
[Authorize]
public class StudentSupportActionsController(AppDbContext context, UserAccessService userAccess) : ControllerBase
{
    private static readonly string[] Categories = ["Learning", "Behaviour", "MentalHealth"];
    private static readonly string[] Statuses = ["Planned", "InProgress", "FollowUpRequired", "Completed"];
    private static readonly string[] ProgressValues =
        ["NotYetAssessed", "NoChange", "SomeProgress", "SignificantProgress", "GoalAchieved", "NeedsMoreSupport"];

    [HttpGet]
    public async Task<ActionResult<StudentSupportActionsPageDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        [FromQuery] string? status = null,
        [FromQuery] string? academicYear = null,
        [FromQuery] int? studentId = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = VisibleActions();

        var term = search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
        {
            term = term[..Math.Min(term.Length, 100)];
            query = query.Where(item =>
                item.Student.FullName.Contains(term) ||
                item.Student.SISUserId.Contains(term) ||
                item.Title.Contains(term) ||
                item.Goal.Contains(term) ||
                item.ActionTaken.Contains(term) ||
                item.CreatedByName.Contains(term));
        }

        var canonicalCategory = Canonical(Categories, category);
        if (canonicalCategory != null) query = query.Where(item => item.Category == canonicalCategory);
        var canonicalStatus = Canonical(Statuses, status);
        if (canonicalStatus != null) query = query.Where(item => item.Status == canonicalStatus);
        if (!string.IsNullOrWhiteSpace(academicYear) && academicYear != "all")
        {
            var year = DashboardPeriodNormalizer.AcademicYear(academicYear);
            query = query.Where(item => item.AcademicYear == year);
        }
        if (studentId.HasValue) query = query.Where(item => item.StudentProfileId == studentId.Value);

        var total = await query.CountAsync();
        var now = DateTime.UtcNow;
        var activeCount = await query.CountAsync(item => item.Status != "Completed");
        var followUpDueCount = await query.CountAsync(item =>
            item.Status != "Completed" && item.FollowUpDate.HasValue && item.FollowUpDate.Value < now);
        var goalAchievedCount = await query.CountAsync(item => item.Progress == "GoalAchieved");
        var years = await context.AcademicPeriods.AsNoTracking()
            .Select(period => period.AcademicYear)
            .Concat(context.StudentSupportActions.AsNoTracking().Select(item => item.AcademicYear))
            .Distinct()
            .OrderByDescending(value => value)
            .ToListAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Min(page, totalPages);
        var rows = await query.OrderByDescending(item => item.ActionDate)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new StudentSupportActionsPageDto
        {
            Items = rows.Select(ToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            TotalPages = totalPages,
            ActiveCount = activeCount,
            FollowUpDueCount = followUpDueCount,
            GoalAchievedCount = goalAchievedCount,
            AvailableAcademicYears = years
        });
    }

    [HttpGet("student/{studentId:int}")]
    public async Task<ActionResult<StudentSupportJourneyPageDto>> ForStudent(
        int studentId,
        [FromQuery] string? academicYear = null,
        [FromQuery] string? term = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 5)
    {
        if (!await VisibleStudents().AnyAsync(student => student.Id == studentId)) return NotFound();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = VisibleActions().Where(item => item.StudentProfileId == studentId);
        if (!string.IsNullOrWhiteSpace(academicYear) && academicYear != "all")
        {
            var year = DashboardPeriodNormalizer.AcademicYear(academicYear);
            query = query.Where(item => item.AcademicYear == year);
        }
        if (!string.IsNullOrWhiteSpace(term) && term != "all")
        {
            var normalizedTerm = DashboardPeriodNormalizer.Term(term);
            if (normalizedTerm is "hk1" or "hk2")
            {
                query = query.Where(item => item.Term == normalizedTerm);
            }
        }

        var total = await query.CountAsync();
        var now = DateTime.UtcNow;
        var activeCount = await query.CountAsync(item => item.Status != "Completed");
        var followUpDueCount = await query.CountAsync(item =>
            item.Status != "Completed" && item.FollowUpDate.HasValue && item.FollowUpDate.Value < now);
        var goalAchievedCount = await query.CountAsync(item => item.Progress == "GoalAchieved");
        var learningCount = await query.CountAsync(item => item.Category == "Learning");
        var behaviourCount = await query.CountAsync(item => item.Category == "Behaviour");
        var mentalHealthCount = await query.CountAsync(item => item.Category == "MentalHealth");
        var years = await VisibleActions()
            .Where(item => item.StudentProfileId == studentId)
            .Select(item => item.AcademicYear)
            .Distinct()
            .OrderByDescending(value => value)
            .ToListAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        page = Math.Min(page, totalPages);
        var rows = await query.OrderByDescending(item => item.ActionDate)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new StudentSupportJourneyPageDto
        {
            Items = rows.Select(ToDto).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            TotalPages = totalPages,
            ActiveCount = activeCount,
            FollowUpDueCount = followUpDueCount,
            GoalAchievedCount = goalAchievedCount,
            LearningCount = learningCount,
            BehaviourCount = behaviourCount,
            MentalHealthCount = mentalHealthCount,
            AvailableAcademicYears = years
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin,SchoolAdministration,Principal,Teacher")]
    public async Task<ActionResult<StudentSupportActionDto>> Create(SaveStudentSupportActionDto model)
    {
        var student = await VisibleStudents().FirstOrDefaultAsync(item => item.Id == model.StudentId);
        if (student == null) return BadRequest(new { Message = "The selected student is invalid or not visible to your account." });
        var normalized = ValidateAndNormalize(model);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var (userId, userName) = await CurrentUser();
        var action = new StudentSupportAction
        {
            StudentProfileId = student.Id,
            CreatedByUserId = userId,
            CreatedByName = userName
        };
        Apply(action, model, normalized.Category!, normalized.Status!, normalized.Progress!);
        context.StudentSupportActions.Add(action);
        await context.SaveChangesAsync();
        action.Student = student;
        return Created($"/api/support-actions/{action.Id}", ToDto(action));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin,SchoolAdministration,Principal,Teacher")]
    public async Task<ActionResult<StudentSupportActionDto>> Update(int id, SaveStudentSupportActionDto model)
    {
        var action = await EditableActions().Include(item => item.Student).FirstOrDefaultAsync(item => item.Id == id);
        if (action == null) return NotFound();
        if (action.StudentProfileId != model.StudentId &&
            !await VisibleStudents().AnyAsync(student => student.Id == model.StudentId))
            return BadRequest(new { Message = "The selected student is invalid or not visible to your account." });
        var normalized = ValidateAndNormalize(model);
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var (userId, userName) = await CurrentUser();
        action.StudentProfileId = model.StudentId;
        action.UpdatedByUserId = userId;
        action.UpdatedByName = userName;
        action.UpdatedAt = DateTime.UtcNow;
        Apply(action, model, normalized.Category!, normalized.Status!, normalized.Progress!);
        await context.SaveChangesAsync();
        action.Student = await VisibleStudents().FirstAsync(student => student.Id == model.StudentId);
        return Ok(ToDto(action));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin,SchoolAdministration,Principal,Teacher")]
    public async Task<IActionResult> Delete(int id)
    {
        var action = await EditableActions().FirstOrDefaultAsync(item => item.Id == id);
        if (action == null) return NotFound();
        context.StudentSupportActions.Remove(action);
        await context.SaveChangesAsync();
        return NoContent();
    }

    private (string? Category, string? Status, string? Progress) ValidateAndNormalize(SaveStudentSupportActionDto model)
    {
        var category = Canonical(Categories, model.Category);
        var status = Canonical(Statuses, model.Status);
        var progress = Canonical(ProgressValues, model.Progress);
        if (category == null) ModelState.AddModelError(nameof(model.Category), "Select a valid category.");
        if (category != null && !CanAccessCategory(category))
            ModelState.AddModelError(nameof(model.Category), "Your account cannot manage this support category.");
        if (status == null) ModelState.AddModelError(nameof(model.Status), "Select a valid status.");
        if (progress == null) ModelState.AddModelError(nameof(model.Progress), "Select a valid progress value.");
        if (model.FollowUpDate.HasValue && model.ActionDate.HasValue && model.FollowUpDate < model.ActionDate)
            ModelState.AddModelError(nameof(model.FollowUpDate), "Follow-up date cannot be before the action date.");
        return (category, status, progress);
    }

    private static void Apply(StudentSupportAction action, SaveStudentSupportActionDto model,
        string category, string status, string progress)
    {
        var actionDate = DateTime.SpecifyKind(model.ActionDate!.Value.Date, DateTimeKind.Utc);
        action.ActionDate = actionDate;
        action.FollowUpDate = model.FollowUpDate.HasValue
            ? DateTime.SpecifyKind(model.FollowUpDate.Value.Date, DateTimeKind.Utc) : null;
        action.AcademicYear = string.IsNullOrWhiteSpace(model.AcademicYear)
            ? DashboardPeriodNormalizer.AcademicYear(actionDate)
            : DashboardPeriodNormalizer.AcademicYear(model.AcademicYear);
        action.Term = Clean(model.Term);
        action.Category = category;
        action.Title = model.Title.Trim();
        action.Goal = model.Goal.Trim();
        action.ActionTaken = model.ActionTaken.Trim();
        action.Result = Clean(model.Result);
        action.NextStep = Clean(model.NextStep);
        action.Status = status;
        action.Progress = progress;
        action.Subject = category == "Learning" ? Clean(model.Subject) : null;
        action.ShareWithFamily = model.ShareWithFamily;
    }

    private IQueryable<StudentProfile> VisibleStudents() =>
        userAccess.ApplyStudentScope(context.StudentProfiles.AsNoTracking());

    private IQueryable<StudentSupportAction> VisibleActions()
    {
        var query = userAccess.ApplyStudentScope(
            context.StudentSupportActions.AsNoTracking().Include(item => item.Student), item => item.Student);
        if (!userAccess.Current.CanAccessHealth) query = query.Where(item => item.Category != "MentalHealth");
        if (!userAccess.Current.CanAccessAcademic) query = query.Where(item => item.Category == "MentalHealth");
        return query;
    }

    private IQueryable<StudentSupportAction> EditableActions()
    {
        var query = userAccess.ApplyStudentScope(context.StudentSupportActions, item => item.Student);
        if (!userAccess.Current.CanAccessHealth) query = query.Where(item => item.Category != "MentalHealth");
        if (!userAccess.Current.CanAccessAcademic) query = query.Where(item => item.Category == "MentalHealth");
        return query;
    }

    private bool CanAccessCategory(string category) => category == "MentalHealth"
        ? userAccess.Current.CanAccessHealth
        : userAccess.Current.CanAccessAcademic;

    private async Task<(string UserId, string UserName)> CurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var name = await context.Users.Where(user => user.Id == userId).Select(user => user.FullName).FirstOrDefaultAsync();
        return (userId, string.IsNullOrWhiteSpace(name) ? User.Identity?.Name ?? "Unknown" : name);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string? Canonical(IEnumerable<string> values, string? value) => values.FirstOrDefault(item =>
        string.Equals(item, value?.Trim(), StringComparison.OrdinalIgnoreCase));

    private static StudentSupportActionDto ToDto(StudentSupportAction item) => new()
    {
        Id = item.Id,
        StudentId = item.StudentProfileId,
        StudentName = item.Student.FullName,
        SISUserId = item.Student.SISUserId,
        GradeLevel = item.Student.GradeLevel ?? item.Student.CanvasGradeLevel,
        Homeroom = item.Student.Homeroom,
        ActionDate = item.ActionDate,
        FollowUpDate = item.FollowUpDate,
        AcademicYear = item.AcademicYear,
        Term = item.Term,
        Category = item.Category,
        Title = item.Title,
        Goal = item.Goal,
        ActionTaken = item.ActionTaken,
        Result = item.Result,
        NextStep = item.NextStep,
        Status = item.Status,
        Progress = item.Progress,
        Subject = item.Subject,
        ShareWithFamily = item.ShareWithFamily,
        CreatedByName = item.CreatedByName,
        UpdatedByName = item.UpdatedByName,
        CreatedAt = item.CreatedAt,
        UpdatedAt = item.UpdatedAt
    };
}
