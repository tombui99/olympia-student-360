namespace OlympiaStudent360.Api.Core.DTOs;

public sealed class BehaviourReportSummaryDto
{
    public int TotalRecords { get; set; }
    public int StudentCount { get; set; }
    public int BehaviourTypeCount { get; set; }
    public int PositiveCount { get; set; }
    public int ConcerningCount { get; set; }
    public int OtherCount { get; set; }
    public List<BehaviourTypeSummaryDto> TopTypes { get; set; } = [];
    public List<BehaviourGradeSummaryDto> ByGrade { get; set; } = [];
    public List<BehaviourStudentSummaryDto> ByStudent { get; set; } = [];
}

public sealed class BehaviourTypeSummaryDto
{
    public string BehaviourType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int RecordCount { get; set; }
    public int StudentCount { get; set; }
    public decimal SharePercentage { get; set; }
}

public sealed class BehaviourGradeSummaryDto
{
    public string Grade { get; set; } = string.Empty;
    public int RecordCount { get; set; }
    public int StudentCount { get; set; }
    public int PositiveCount { get; set; }
    public int ConcerningCount { get; set; }
    public int OtherCount { get; set; }
}

public sealed class BehaviourStudentSummaryDto
{
    public string Key { get; set; } = string.Empty;
    public int? StudentProfileId { get; set; }
    public string? SISUserId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? Grade { get; set; }
    public string? Homeroom { get; set; }
    public int RecordCount { get; set; }
    public int PositiveCount { get; set; }
    public int ConcerningCount { get; set; }
    public int OtherCount { get; set; }
}

public sealed class BehaviourReportDetailsPageDto
{
    public List<BehaviourReportDetailDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

public sealed class BehaviourReportDetailDto
{
    public int Id { get; set; }
    public int? StudentProfileId { get; set; }
    public string? SISUserId { get; set; }
    public string StudentName { get; set; } = string.Empty;
    public string? Grade { get; set; }
    public string? Homeroom { get; set; }
    public DateTime IncidentDate { get; set; }
    public string BehaviourType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string? Subject { get; set; }
    public string? Decision { get; set; }
    public string? RecordedBy { get; set; }
}
