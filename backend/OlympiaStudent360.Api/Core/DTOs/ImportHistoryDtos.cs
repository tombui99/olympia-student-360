using System.ComponentModel.DataAnnotations;

namespace OlympiaStudent360.Api.Core.DTOs;

public class ImportHistoryDto
{
    public int Id { get; set; }
    public string ModuleId { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public DateTime ImportedAt { get; set; }
    public string ImportedBy { get; set; } = default!;
    public string ImportedByName { get; set; } = default!;
    public string Status { get; set; } = default!;
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
}

public class ImportHistoryPageDto
{
    public List<ImportHistoryDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

public class ImportHistoryDetailDto : ImportHistoryDto
{
    public List<ImportHistoryRecordDto> Records { get; set; } = [];
}

public class ImportHistoryRecordDto
{
    public int Id { get; set; }
    public string SISUserId { get; set; } = default!;
    public string StudentName { get; set; } = default!;
    public Dictionary<string, string?> Values { get; set; } = [];
}

public class UpdateImportHistoryDto
{
    [Required, MaxLength(255)]
    public string FileName { get; set; } = default!;
}

public class UpdateImportHistoryRecordDto
{
    [Required]
    public Dictionary<string, string?> Values { get; set; } = [];
}
