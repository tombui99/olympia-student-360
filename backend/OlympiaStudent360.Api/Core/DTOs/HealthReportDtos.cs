namespace OlympiaStudent360.Api.Core.DTOs;

public sealed class HealthReportRowDto
{
    public string Key { get; set; } = default!;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int? VisitId { get; set; }
    public int? StudentProfileId { get; set; }
    public string? SISUserId { get; set; }
    public string StudentName { get; set; } = default!;
    public string? ClassName { get; set; }
    public string? Diagnosis { get; set; }
    public string? Classification { get; set; }
    public int VisitCount { get; set; }
    public int StudentCount { get; set; }
    public DateTime LatestVisit { get; set; }
}

public sealed class HealthReportPageDto
{
    public List<HealthReportRowDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int TotalVisits { get; set; }
}

public sealed class HealthVisitReportDetailDto
{
    public int Id { get; set; }
    public int? StudentProfileId { get; set; }
    public string? SISUserId { get; set; }
    public string StudentName { get; set; } = default!;
    public string? ClassName { get; set; }
    public DateTime ExamTime { get; set; }
    public string? Classification { get; set; }
    public string? Diagnosis { get; set; }
    public string? Symptoms { get; set; }
    public string? Treatment { get; set; }
    public string? Medication { get; set; }
    public string? Notes { get; set; }
    public bool NotificationSent { get; set; }
    public string? ExaminedBy { get; set; }
    public Dictionary<string, string> CustomFields { get; set; } = [];
}

public sealed class HealthVisitReportDetailsPageDto
{
    public List<HealthVisitReportDetailDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
