using System.ComponentModel.DataAnnotations;

namespace OlympiaStudent360.Api.Core.DTOs;

public class ImportHistoryDto
{
    public int Id { get; set; }
    public string ModuleId { get; set; } = default!;
    public string Source { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public DateTime ImportedAt { get; set; }
    public string ImportedBy { get; set; } = default!;
    public string ImportedByName { get; set; } = default!;
    public string Status { get; set; } = default!;
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
}
