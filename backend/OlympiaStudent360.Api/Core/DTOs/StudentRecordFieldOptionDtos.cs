using System.ComponentModel.DataAnnotations;

namespace OlympiaStudent360.Api.Core.DTOs;

public sealed class StudentRecordFieldOptionDto
{
    public int Id { get; set; }
    public string ModuleId { get; set; } = default!;
    public string FieldKey { get; set; } = default!;
    public string Value { get; set; } = default!;
    public string? OptionGroup { get; set; }
    public int SortOrder { get; set; }
}

public sealed class SaveStudentRecordFieldOptionDto
{
    [Required, MaxLength(100)] public string FieldKey { get; set; } = default!;
    [Required, MaxLength(255)] public string Value { get; set; } = default!;
    [MaxLength(50)] public string? OptionGroup { get; set; }
    public int SortOrder { get; set; }
}
