using System.ComponentModel.DataAnnotations;

namespace OlympiaStudent360.Api.Core.DTOs;

public class ImportModuleFieldDto
{
    public int Id { get; set; }
    public string ModuleId { get; set; } = default!;
    public string FieldKey { get; set; } = default!;
    public string Label { get; set; } = default!;
    public string FieldType { get; set; } = "text";
    public bool Required { get; set; }
    public List<string> Options { get; set; } = [];
    public int SortOrder { get; set; }
}

public class SaveImportModuleFieldDto
{
    [Required, MaxLength(150)]
    public string Label { get; set; } = default!;

    [Required, MaxLength(20)]
    public string FieldType { get; set; } = "text";

    public bool Required { get; set; }
    public List<string> Options { get; set; } = [];
    public int SortOrder { get; set; }

    /// <summary>Optional explicit key; derived from the label when omitted.</summary>
    [MaxLength(100)]
    public string? FieldKey { get; set; }
}
