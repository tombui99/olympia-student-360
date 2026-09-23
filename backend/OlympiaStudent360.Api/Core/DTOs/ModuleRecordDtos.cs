using System.ComponentModel.DataAnnotations;

namespace OlympiaStudent360.Api.Core.DTOs;

public sealed class ModuleRecordDto
{
    public int Id { get; set; }
    public string ModuleId { get; set; } = default!;
    public int StudentProfileId { get; set; }
    public string SISUserId { get; set; } = default!;
    public string StudentName { get; set; } = default!;
    public string? GradeLevel { get; set; }
    public string? CanvasGradeLevel { get; set; }
    public string? Homeroom { get; set; }
    public string? EnteredByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, string?> Values { get; set; } = [];
}

public sealed class ModuleRecordsPageDto
{
    public List<ModuleRecordDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

public sealed class UpdateModuleRecordDto
{
    [Required]
    public Dictionary<string, string?> Values { get; set; } = [];
}
