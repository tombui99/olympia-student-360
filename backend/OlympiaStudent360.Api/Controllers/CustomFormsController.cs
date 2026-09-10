using System.Globalization;
using System.Data;
using System.Net.Mail;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/custom-forms")]
[ApiController]
[Authorize]
public class CustomFormsController : ControllerBase
{
    private const string Managers = "Admin,SchoolAdministration,Principal";
    private readonly AppDbContext _context;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public CustomFormsController(AppDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CustomFormSummaryDto>>> List()
    {
        var canManage = User.IsInRole("Admin") || User.IsInRole("SchoolAdministration") || User.IsInRole("Principal");
        return Ok(await FormSummaries(false, canManage));
    }

    [HttpGet("archived")]
    [Authorize(Roles = Managers)]
    public async Task<ActionResult<IEnumerable<CustomFormSummaryDto>>> Archived() =>
        Ok(await FormSummaries(true, true));

    private async Task<List<CustomFormSummaryDto>> FormSummaries(bool archived, bool includeResponseCount) =>
        await _context.CustomForms.AsNoTracking()
            .Where(item => item.IsArchived == archived)
            .OrderByDescending(item => item.UpdatedAt ?? item.CreatedAt)
            .Select(item => new CustomFormSummaryDto
            {
                Id = item.Id, PublicId = item.PublicId, Title = item.Title, Description = item.Description,
                AllowMultipleSubmissions = item.AllowMultipleSubmissions,
                OpensAt = item.OpensAt, ClosesAt = item.ClosesAt, ResponseLimit = item.ResponseLimit,
                ResponseCount = includeResponseCount ? item.Responses.Count(response => !response.IsDeleted) : 0,
                CreatedByName = item.CreatedByName, CreatedAt = item.CreatedAt, UpdatedAt = item.UpdatedAt
            }).ToListAsync();

    [HttpGet("{id:int}")]
    public async Task<ActionResult<CustomFormDetailDto>> Detail(int id)
    {
        var form = await _context.CustomForms.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id && !item.IsArchived);
        return form == null ? NotFound() : Ok(ToDetail(form));
    }

    [HttpPost]
    [Authorize(Roles = Managers)]
    public async Task<ActionResult<CustomFormDetailDto>> Create(SaveCustomFormDto model)
    {
        var error = ValidateForm(model);
        if (error != null) return BadRequest(new { Message = error });
        var form = new CustomForm
        {
            PublicId = NewPublicId(), Title = model.Title.Trim(), Description = Clean(model.Description),
            ConfirmationMessage = Clean(model.ConfirmationMessage),
            AllowMultipleSubmissions = model.AllowMultipleSubmissions,
            OpensAt = model.OpensAt, ClosesAt = model.ClosesAt, ResponseLimit = model.ResponseLimit,
            DefinitionJson = JsonSerializer.Serialize(model.Definition),
            CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)!,
            CreatedByName = User.FindFirstValue("fullName") ?? User.Identity?.Name ?? "Administrator"
        };
        _context.CustomForms.Add(form);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(Detail), new { id = form.Id }, ToDetail(form));
    }

    [HttpPut("{id:int}")]
    [Authorize(Roles = Managers)]
    public async Task<ActionResult<CustomFormDetailDto>> Update(int id, SaveCustomFormDto model)
    {
        var error = ValidateForm(model);
        if (error != null) return BadRequest(new { Message = error });
        var form = await _context.CustomForms.FirstOrDefaultAsync(item => item.Id == id && !item.IsArchived);
        if (form == null) return NotFound();
        form.Title = model.Title.Trim(); form.Description = Clean(model.Description);
        form.ConfirmationMessage = Clean(model.ConfirmationMessage);
        form.AllowMultipleSubmissions = model.AllowMultipleSubmissions;
        form.OpensAt = model.OpensAt; form.ClosesAt = model.ClosesAt; form.ResponseLimit = model.ResponseLimit;
        form.DefinitionJson = JsonSerializer.Serialize(model.Definition);
        form.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(ToDetail(form));
    }

    [HttpDelete("{id:int}")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> Archive(int id)
    {
        var form = await _context.CustomForms.FirstOrDefaultAsync(item => item.Id == id && !item.IsArchived);
        if (form == null) return NoContent();
        form.IsArchived = true; form.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPatch("{id:int}/restore")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> Restore(int id)
    {
        var form = await _context.CustomForms.FirstOrDefaultAsync(item => item.Id == id && item.IsArchived);
        if (form == null) return NotFound();
        form.IsArchived = false; form.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:int}/responses")]
    [Authorize(Roles = Managers)]
    public async Task<ActionResult<CustomFormResponsePageDto>> Responses(int id, int page = 1, int pageSize = 20)
    {
        if (!await _context.CustomForms.AnyAsync(item => item.Id == id && !item.IsArchived)) return NotFound();
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var query = _context.CustomFormResponses.AsNoTracking().Include(item => item.Student)
            .Where(item => item.CustomFormId == id && !item.IsDeleted);
        var total = await query.CountAsync();
        var pages = total == 0 ? 0 : (int)Math.Ceiling((double)total / pageSize);
        page = Math.Min(page, Math.Max(1, pages));
        var items = await query.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new CustomFormResponsePageDto
        {
            Items = items.Select(ToResponse).ToList(), Page = page, PageSize = pageSize,
            TotalCount = total, TotalPages = pages
        });
    }

    [HttpGet("{id:int}/responses/{responseId:int}")]
    [Authorize(Roles = Managers)]
    public async Task<ActionResult<CustomFormResponseDto>> ResponseDetail(int id, int responseId)
    {
        var response = await _context.CustomFormResponses.AsNoTracking().Include(item => item.Student)
            .FirstOrDefaultAsync(item => item.Id == responseId && item.CustomFormId == id && !item.IsDeleted);
        return response == null ? NotFound() : Ok(ToResponse(response));
    }

    [HttpDelete("{id:int}/responses/{responseId:int}")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> DeleteResponse(int id, int responseId)
    {
        var response = await _context.CustomFormResponses
            .FirstOrDefaultAsync(item => item.Id == responseId && item.CustomFormId == id && !item.IsDeleted);
        if (response == null) return NoContent();
        response.IsDeleted = true; response.DeletedAt = DateTime.UtcNow;
        response.DeletedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier); response.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("{id:int}/responses/export")]
    [Authorize(Roles = Managers)]
    public async Task<IActionResult> Export(int id)
    {
        var form = await _context.CustomForms.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id && !item.IsArchived);
        if (form == null) return NotFound();
        var responses = await _context.CustomFormResponses.AsNoTracking().Include(item => item.Student)
            .Where(item => item.CustomFormId == id && !item.IsDeleted).OrderBy(item => item.CreatedAt).ToListAsync();
        var definition = Definition(form.DefinitionJson);
        var questions = definition.Sections.SelectMany(section => section.Questions).ToList();
        using var stream = new MemoryStream();
        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
        {
            var workbookPart = document.AddWorkbookPart(); workbookPart.Workbook = new Workbook();
            var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
            var sheetData = new SheetData(); worksheetPart.Worksheet = new Worksheet(sheetData);
            var sheets = workbookPart.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet { Id = workbookPart.GetIdOfPart(worksheetPart), SheetId = 1, Name = "Responses" });
            AddRow(sheetData, ["Submitted at", "Student ID", "Student name", "Grade", "Homeroom", "Respondent", "Relationship", "Email", "Phone", .. questions.Select(q => q.Label)]);
            foreach (var response in responses)
            {
                var answers = Answers(response.AnswersJson);
                AddRow(sheetData, [response.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), response.Student.SISUserId,
                    response.Student.FullName, response.Student.GradeLevel ?? "", response.Student.Homeroom ?? "", response.RespondentName,
                    response.RespondentRelationship, response.RespondentEmail, response.RespondentPhone,
                    .. questions.Select(question => Display(answers.GetValueOrDefault(question.Id)))]);
            }
            workbookPart.Workbook.Save();
        }
        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"{SafeFileName(form.Title)}-responses.xlsx");
    }

    private static void AddRow(SheetData data, IEnumerable<string> values) => data.Append(new Row(values.Select(value =>
        new Cell { DataType = CellValues.InlineString, InlineString = new InlineString(new Text(value ?? "")) })));

    private static string SafeFileName(string value) => Regex.Replace(value, "[^a-zA-Z0-9_-]+", "-").Trim('-') is { Length: > 0 } name ? name : "form";

    private static string? ValidateForm(SaveCustomFormDto model)
    {
        if (string.IsNullOrWhiteSpace(model.Title)) return "A form title is required.";
        if (model.Title.Trim().Length > 255) return "The form title is too long.";
        if ((model.Description?.Length ?? 0) > 4000 || (model.ConfirmationMessage?.Length ?? 0) > 2000) return "Form text is too long.";
        if (model.OpensAt.HasValue && model.ClosesAt.HasValue && model.OpensAt >= model.ClosesAt) return "The closing date must be after the opening date.";
        if (model.ResponseLimit.HasValue && model.ResponseLimit < 1) return "The response limit must be at least 1.";
        if (model.Definition.Sections.Count == 0) return "At least one section is required.";
        if (model.Definition.Sections.Count > 50 || model.Definition.Sections.Sum(item => item.Questions.Count) > 300) return "The form has too many sections or questions.";
        var sectionIds = new HashSet<string>(); var questionIds = new HashSet<string>();
        var types = new HashSet<string>(["shortText", "paragraph", "multipleChoice", "checkboxes", "dropdown", "linearScale", "rating", "multipleChoiceGrid", "checkboxGrid", "date", "time"]);
        foreach (var section in model.Definition.Sections)
        {
            if (string.IsNullOrWhiteSpace(section.Id) || !sectionIds.Add(section.Id)) return "Section IDs must be present and unique.";
            foreach (var question in section.Questions)
            {
                if (string.IsNullOrWhiteSpace(question.Id) || !questionIds.Add(question.Id)) return "Question IDs must be present and unique.";
                if (!types.Contains(question.Type)) return $"Question type '{question.Type}' is not supported.";
                if (string.IsNullOrWhiteSpace(question.Label)) return "Every question needs a label.";
                if (question.Label.Length > 1000 || (question.Description?.Length ?? 0) > 4000) return "Question text is too long.";
                if (!ValidItems(question.Options, 200) || !ValidItems(question.Rows, 200) || !ValidItems(question.Columns, 200)) return $"'{question.Label}' has invalid or duplicate options.";
                if (question.Type is "multipleChoice" or "checkboxes" or "dropdown" && question.Options.Count == 0) return $"'{question.Label}' needs at least one option.";
                if (question.Type is "multipleChoiceGrid" or "checkboxGrid" && (question.Rows.Count == 0 || question.Columns.Count == 0)) return $"'{question.Label}' needs grid rows and columns.";
                if (question.Type is "linearScale" or "rating" && ((question.Min ?? 1) > (question.Max ?? 5) || (question.Max ?? 5) - (question.Min ?? 1) > 20)) return $"'{question.Label}' has an invalid scale.";
                if (!string.IsNullOrWhiteSpace(question.ValidationPattern))
                    try { _ = new Regex(question.ValidationPattern, RegexOptions.None, TimeSpan.FromMilliseconds(100)); }
                    catch { return $"'{question.Label}' has an invalid validation pattern."; }
            }
        }
        foreach (var branch in model.Definition.Sections.SelectMany(s => s.Questions).SelectMany(q => q.Branches))
            if (branch.TargetSectionId != "__submit" && !sectionIds.Contains(branch.TargetSectionId)) return "A branch points to a missing section.";
        return null;
    }

    private static bool ValidItems(List<string> values, int max) => values.Count <= max &&
        values.All(value => !string.IsNullOrWhiteSpace(value) && value.Length <= 500) &&
        values.Distinct(StringComparer.Ordinal).Count() == values.Count;

    private static CustomFormDetailDto ToDetail(CustomForm form) => new()
    {
        Id = form.Id, PublicId = form.PublicId, Title = form.Title, Description = form.Description,
        ConfirmationMessage = form.ConfirmationMessage,
        AllowMultipleSubmissions = form.AllowMultipleSubmissions, CreatedByName = form.CreatedByName,
        OpensAt = form.OpensAt, ClosesAt = form.ClosesAt, ResponseLimit = form.ResponseLimit,
        CreatedAt = form.CreatedAt, UpdatedAt = form.UpdatedAt, Definition = Definition(form.DefinitionJson)
    };

    private static CustomFormResponseDto ToResponse(CustomFormResponse item) => new()
    {
        Id = item.Id, StudentProfileId = item.StudentProfileId, StudentId = item.Student.SISUserId,
        StudentName = item.Student.FullName,
        GradeLevel = item.Student.GradeLevel ?? item.Student.CanvasGradeLevel,
        Homeroom = item.Student.Homeroom,
        RespondentName = item.RespondentName, RespondentRelationship = item.RespondentRelationship,
        RespondentEmail = item.RespondentEmail, RespondentPhone = item.RespondentPhone,
        Answers = Answers(item.AnswersJson), QuestionSnapshot = Definition(item.QuestionSnapshotJson), SubmittedAt = item.CreatedAt
    };

    internal static CustomFormDefinitionDto Definition(string json) => JsonSerializer.Deserialize<CustomFormDefinitionDto>(json, JsonOptions) ?? new();
    internal static Dictionary<string, JsonElement> Answers(string json) => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions) ?? [];
    internal static string Display(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Array => string.Join(", ", value.EnumerateArray().Select(Display)),
        JsonValueKind.Object => string.Join("; ", value.EnumerateObject().Select(item => $"{item.Name}: {Display(item.Value)}")),
        JsonValueKind.String => value.GetString() ?? "", JsonValueKind.Null or JsonValueKind.Undefined => "", _ => value.ToString()
    };
    internal static string HashToken(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string NewPublicId() => Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

[Route("api/public/forms")]
[ApiController]
[AllowAnonymous]
[EnableRateLimiting("public-forms")]
public class PublicCustomFormsController : ControllerBase
{
    private readonly AppDbContext _context;
    public PublicCustomFormsController(AppDbContext context) => _context = context;

    [HttpGet("{publicId}")]
    public async Task<IActionResult> Summary(string publicId)
    {
        var form = await AvailableForm(publicId, false);
        return form == null ? NotFound() : Ok(new { form.PublicId, form.Title, form.Description });
    }

    [HttpPost("{publicId}/verify")]
    public async Task<ActionResult<VerifiedFormStudentDto>> Verify(string publicId, VerifyFormStudentDto model)
    {
        var form = await AvailableForm(publicId, true);
        if (form == null) return NotFound();
        var studentId = model.StudentId?.Trim();
        if (string.IsNullOrWhiteSpace(studentId)) return InvalidVerification();
        var student = await _context.StudentProfiles
            .FirstOrDefaultAsync(item => item.SISUserId.ToLower() == studentId.ToLower());
        if (student == null) return InvalidVerification();
        if (!form.AllowMultipleSubmissions && await _context.CustomFormResponses.AnyAsync(item => item.CustomFormId == form.Id && item.StudentProfileId == student.Id && !item.IsDeleted))
            return Conflict(new { Message = "A response has already been submitted for this student." });
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var expires = DateTime.UtcNow.AddMinutes(30);
        _context.CustomFormAccessSessions.Add(new CustomFormAccessSession
        {
            CustomFormId = form.Id, StudentProfileId = student.Id,
            TokenHash = CustomFormsController.HashToken(token), ExpiresAt = expires
        });
        await _context.SaveChangesAsync();
        return Ok(new VerifiedFormStudentDto
        {
            AccessToken = token, ExpiresAt = expires, StudentProfileId = student.Id, StudentId = student.SISUserId,
            StudentName = student.FullName,
            GradeLevel = student.GradeLevel ?? student.CanvasGradeLevel,
            Homeroom = student.Homeroom,
            Form = new PublicFormDto
            {
                PublicId = form.PublicId, Title = form.Title, Description = form.Description,
                ConfirmationMessage = form.ConfirmationMessage, AllowMultipleSubmissions = form.AllowMultipleSubmissions,
                OpensAt = form.OpensAt, ClosesAt = form.ClosesAt, ResponseLimit = form.ResponseLimit,
                Definition = CustomFormsController.Definition(form.DefinitionJson)
            }
        });
    }

    [HttpPost("{publicId}/students/lookup")]
    public async Task<ActionResult<IEnumerable<FormStudentCandidateDto>>> LookupStudents(
        string publicId, LookupFormStudentDto model)
    {
        var form = await AvailableForm(publicId, false);
        if (form == null) return NotFound();

        var email = model.Email?.Trim().ToLowerInvariant();
        var phone = NormalizePhone(model.Phone);
        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(phone))
            return UnprocessableEntity(new { Message = "Enter a parent or guardian email or phone number." });

        var students = _context.StudentProfiles.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(email))
            students = students.Where(student =>
                student.Email != null && student.Email.Trim().ToLower() == email ||
                student.Parents.Any(parent => parent.Email != null && parent.Email.Trim().ToLower() == email));
        if (!string.IsNullOrWhiteSpace(phone))
            students = students.Where(student =>
                student.PhoneNumber != null &&
                student.PhoneNumber.Replace(" ", "").Replace("-", "").Replace(".", "")
                    .Replace("(", "").Replace(")", "").Replace("+", "") == phone ||
                student.Parents.Any(parent => parent.PhoneNumber != null &&
                    parent.PhoneNumber.Replace(" ", "").Replace("-", "").Replace(".", "")
                        .Replace("(", "").Replace(")", "").Replace("+", "") == phone));

        var matches = await students
            .OrderBy(student => student.FullName)
            .Take(20)
            .Select(student => new FormStudentCandidateDto
            {
                StudentId = student.SISUserId,
                StudentName = student.FullName,
                GradeLevel = student.GradeLevel ?? student.CanvasGradeLevel,
                Homeroom = student.Homeroom
            })
            .ToListAsync();

        return matches.Count == 0
            ? UnprocessableEntity(new { Message = "We could not find a student for those contact details." })
            : Ok(matches);
    }

    [HttpPost("{publicId}/responses")]
    public async Task<IActionResult> Submit(string publicId, SubmitCustomFormDto model)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var hash = CustomFormsController.HashToken(model.AccessToken ?? "");
        var session = await _context.CustomFormAccessSessions.Include(item => item.Form).Include(item => item.Student)
            .FirstOrDefaultAsync(item => item.TokenHash == hash && item.Form.PublicId == publicId);
        if (session == null || session.UsedAt != null || session.ExpiresAt <= DateTime.UtcNow || !IsWithinWindow(session.Form))
            return Unauthorized(new { Message = "Your verification session is invalid or has expired." });
        if (session.Form.ResponseLimit.HasValue && await _context.CustomFormResponses.CountAsync(item => item.CustomFormId == session.Form.Id && !item.IsDeleted) >= session.Form.ResponseLimit)
            return Conflict(new { Message = "This form is no longer accepting responses." });
        if (!session.Form.AllowMultipleSubmissions && await _context.CustomFormResponses.AnyAsync(item => item.CustomFormId == session.Form.Id && item.StudentProfileId == session.StudentProfileId && !item.IsDeleted))
            return Conflict(new { Message = "A response has already been submitted for this student." });
        if (string.IsNullOrWhiteSpace(model.RespondentName) || string.IsNullOrWhiteSpace(model.RespondentRelationship) ||
            string.IsNullOrWhiteSpace(model.RespondentEmail) || string.IsNullOrWhiteSpace(model.RespondentPhone))
            return BadRequest(new { Message = "Guardian name, relationship, email, and phone are required." });
        if (model.RespondentName.Trim().Length > 255 || model.RespondentRelationship.Trim().Length > 100 ||
            model.RespondentEmail.Trim().Length > 255 || model.RespondentPhone.Trim().Length > 50)
            return BadRequest(new { Message = "One or more guardian details are too long." });
        if (!MailAddress.TryCreate(model.RespondentEmail.Trim(), out _))
            return BadRequest(new { Message = "A valid guardian email is required." });
        var definition = CustomFormsController.Definition(session.Form.DefinitionJson);
        var validationError = ValidateAnswers(definition, model.Answers);
        if (validationError != null) return BadRequest(new { Message = validationError });
        _context.CustomFormResponses.Add(new CustomFormResponse
        {
            CustomFormId = session.CustomFormId, StudentProfileId = session.StudentProfileId,
            RespondentName = model.RespondentName.Trim(), RespondentRelationship = model.RespondentRelationship.Trim(),
            RespondentEmail = model.RespondentEmail.Trim(), RespondentPhone = model.RespondentPhone.Trim(),
            AnswersJson = JsonSerializer.Serialize(model.Answers), QuestionSnapshotJson = session.Form.DefinitionJson
        });
        session.UsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
        return Ok(new { Message = session.Form.ConfirmationMessage ?? "Thank you. Your response has been recorded." });
    }

    private ActionResult<VerifiedFormStudentDto> InvalidVerification() => StatusCode(StatusCodes.Status422UnprocessableEntity,
        new { Message = "We could not find a student with that SIS ID." });

    private static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        return string.IsNullOrWhiteSpace(digits) ? null : digits;
    }

    private async Task<CustomForm?> AvailableForm(string publicId, bool tracking)
    {
        var query = tracking ? _context.CustomForms.AsQueryable() : _context.CustomForms.AsNoTracking();
        var form = await query.FirstOrDefaultAsync(item => item.PublicId == publicId && !item.IsArchived);
        if (form == null || !IsWithinWindow(form)) return null;
        if (form.ResponseLimit.HasValue && await _context.CustomFormResponses.CountAsync(item => item.CustomFormId == form.Id && !item.IsDeleted) >= form.ResponseLimit) return null;
        return form;
    }

    private static bool IsWithinWindow(CustomForm form) => !form.IsArchived &&
        (!form.OpensAt.HasValue || form.OpensAt <= DateTime.UtcNow) && (!form.ClosesAt.HasValue || form.ClosesAt > DateTime.UtcNow);

    private static string? ValidateAnswers(CustomFormDefinitionDto definition, Dictionary<string, JsonElement> answers)
    {
        var visited = VisitedSections(definition, answers);
        foreach (var question in definition.Sections.Where(section => visited.Contains(section.Id)).SelectMany(section => section.Questions))
        {
            answers.TryGetValue(question.Id, out var value);
            if (question.Required && !HasValue(value, question)) return $"'{question.Label}' is required.";
            if (!HasValue(value, question)) continue;
            var typeError = ValidateAnswerType(question, value);
            if (typeError != null) return typeError;
            if (!string.IsNullOrWhiteSpace(question.ValidationPattern) && value.ValueKind == JsonValueKind.String)
            {
                try
                {
                    if (!Regex.IsMatch(value.GetString() ?? "", question.ValidationPattern, RegexOptions.None, TimeSpan.FromMilliseconds(100)))
                        return question.ValidationMessage ?? $"'{question.Label}' is invalid.";
                }
                catch { return $"'{question.Label}' has invalid validation settings."; }
            }
        }
        return null;
    }

    private static string? ValidateAnswerType(FormQuestionDto question, JsonElement value)
    {
        bool Allowed(string? option) => option != null && question.Options.Contains(option);
        switch (question.Type)
        {
            case "shortText": case "paragraph": case "date": case "time":
                if (value.ValueKind != JsonValueKind.String) return $"'{question.Label}' has an invalid answer.";
                break;
            case "multipleChoice": case "dropdown":
                if (value.ValueKind != JsonValueKind.String || !Allowed(value.GetString())) return $"'{question.Label}' has an invalid option.";
                break;
            case "checkboxes":
                if (value.ValueKind != JsonValueKind.Array || value.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String || !Allowed(item.GetString()))) return $"'{question.Label}' has invalid options.";
                break;
            case "linearScale": case "rating":
                if (!value.TryGetInt32(out var number) || number < (question.Min ?? 1) || number > (question.Max ?? 5)) return $"'{question.Label}' is outside the allowed scale.";
                break;
            case "multipleChoiceGrid": case "checkboxGrid":
                if (value.ValueKind != JsonValueKind.Object) return $"'{question.Label}' has an invalid grid response.";
                foreach (var row in value.EnumerateObject())
                {
                    if (!question.Rows.Contains(row.Name)) return $"'{question.Label}' has an invalid grid row.";
                    if (question.Type == "multipleChoiceGrid" && (row.Value.ValueKind != JsonValueKind.String || !question.Columns.Contains(row.Value.GetString() ?? ""))) return $"'{question.Label}' has an invalid grid option.";
                    if (question.Type == "checkboxGrid" && (row.Value.ValueKind != JsonValueKind.Array || row.Value.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String || !question.Columns.Contains(item.GetString() ?? "")))) return $"'{question.Label}' has invalid grid options.";
                }
                break;
        }
        return null;
    }

    private static HashSet<string> VisitedSections(CustomFormDefinitionDto definition, Dictionary<string, JsonElement> answers)
    {
        var visited = new HashSet<string>(); if (definition.Sections.Count == 0) return visited;
        var current = definition.Sections[0].Id;
        for (var guard = 0; guard < definition.Sections.Count; guard++)
        {
            if (!visited.Add(current)) break;
            var index = definition.Sections.FindIndex(section => section.Id == current); if (index < 0) break;
            var section = definition.Sections[index]; string? target = null;
            foreach (var question in section.Questions.Where(item => item.Type is "multipleChoice" or "dropdown"))
                if (answers.TryGetValue(question.Id, out var answer) && answer.ValueKind == JsonValueKind.String)
                    target = question.Branches.FirstOrDefault(branch => branch.Value == answer.GetString())?.TargetSectionId ?? target;
            if (target == "__submit") break;
            current = target ?? (index + 1 < definition.Sections.Count ? definition.Sections[index + 1].Id : "");
            if (current == "") break;
        }
        return visited;
    }

    private static bool HasValue(JsonElement value, FormQuestionDto question)
    {
        if (value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return false;
        if (value.ValueKind == JsonValueKind.String) return !string.IsNullOrWhiteSpace(value.GetString());
        if (value.ValueKind == JsonValueKind.Array) return value.GetArrayLength() > 0;
        if (value.ValueKind == JsonValueKind.Object)
        {
            var properties = value.EnumerateObject().ToDictionary(item => item.Name, item => item.Value);
            return question.Rows.Count == 0 ? properties.Count > 0 : question.Rows.All(row => properties.TryGetValue(row, out var rowValue) &&
                rowValue.ValueKind switch { JsonValueKind.String => !string.IsNullOrWhiteSpace(rowValue.GetString()), JsonValueKind.Array => rowValue.GetArrayLength() > 0, _ => false });
        }
        return true;
    }
}
