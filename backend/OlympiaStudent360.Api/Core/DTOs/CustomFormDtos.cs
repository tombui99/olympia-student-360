using System.Text.Json;

namespace OlympiaStudent360.Api.Core.DTOs;

public class FormBranchDto
{
    public string Value { get; set; } = default!;
    public string TargetSectionId { get; set; } = default!;
}

public class FormQuestionDto
{
    public string Id { get; set; } = default!;
    public string Type { get; set; } = "shortText";
    public string Label { get; set; } = default!;
    public string? Description { get; set; }
    public bool Required { get; set; }
    public List<string> Options { get; set; } = [];
    public List<string> Rows { get; set; } = [];
    public List<string> Columns { get; set; } = [];
    public int? Min { get; set; }
    public int? Max { get; set; }
    public string? MinLabel { get; set; }
    public string? MaxLabel { get; set; }
    public string? ValidationPattern { get; set; }
    public string? ValidationMessage { get; set; }
    public List<FormBranchDto> Branches { get; set; } = [];
}

public class FormSectionDto
{
    public string Id { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public List<FormQuestionDto> Questions { get; set; } = [];
}

public class CustomFormDefinitionDto
{
    public List<FormSectionDto> Sections { get; set; } = [];
}

public class SaveCustomFormDto
{
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public string? ConfirmationMessage { get; set; }
    public bool AllowMultipleSubmissions { get; set; } = true;
    public DateTime? OpensAt { get; set; }
    public DateTime? ClosesAt { get; set; }
    public int? ResponseLimit { get; set; }
    public CustomFormDefinitionDto Definition { get; set; } = new();
}

public class CustomFormSummaryDto
{
    public int Id { get; set; }
    public string PublicId { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public bool AllowMultipleSubmissions { get; set; }
    public DateTime? OpensAt { get; set; }
    public DateTime? ClosesAt { get; set; }
    public int? ResponseLimit { get; set; }
    public int ResponseCount { get; set; }
    public string CreatedByName { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class CustomFormDetailDto : CustomFormSummaryDto
{
    public string? ConfirmationMessage { get; set; }
    public CustomFormDefinitionDto Definition { get; set; } = new();
}

public class VerifyFormStudentDto
{
    public string? StudentId { get; set; }
}

public class LookupFormStudentDto
{
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public class FormStudentCandidateDto
{
    public string StudentId { get; set; } = default!;
    public string StudentName { get; set; } = default!;
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
}

public class PublicFormDto
{
    public string PublicId { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public string? ConfirmationMessage { get; set; }
    public bool AllowMultipleSubmissions { get; set; }
    public DateTime? OpensAt { get; set; }
    public DateTime? ClosesAt { get; set; }
    public int? ResponseLimit { get; set; }
    public CustomFormDefinitionDto Definition { get; set; } = new();
}

public class VerifiedFormStudentDto
{
    public string AccessToken { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public int StudentProfileId { get; set; }
    public string StudentId { get; set; } = default!;
    public string StudentName { get; set; } = default!;
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
    public PublicFormDto Form { get; set; } = new();
}

public class SubmitCustomFormDto
{
    public string AccessToken { get; set; } = default!;
    public Dictionary<string, JsonElement> Answers { get; set; } = [];
}

public class CustomFormResponseDto
{
    public int Id { get; set; }
    public int StudentProfileId { get; set; }
    public string StudentId { get; set; } = default!;
    public string StudentName { get; set; } = default!;
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
    public Dictionary<string, JsonElement> Answers { get; set; } = [];
    public CustomFormDefinitionDto QuestionSnapshot { get; set; } = new();
    public DateTime SubmittedAt { get; set; }
}

public class CustomFormResponsePageDto
{
    public List<CustomFormResponseDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
