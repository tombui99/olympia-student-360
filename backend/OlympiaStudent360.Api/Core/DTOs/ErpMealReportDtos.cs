namespace OlympiaStudent360.Api.Core.DTOs;

public sealed class ErpMealReportRowDto
{
    public string Key { get; set; } = default!;
    public string Label { get; set; } = default!;
    public int? StudentProfileId { get; set; }
    public string? ErpStudentId { get; set; }
    public string? Grade { get; set; }
    public string? ClassName { get; set; }
    public DateTime? PeriodStart { get; set; }
    public DateTime? LatestDay { get; set; }
    public int Count { get; set; }
    public int Units { get; set; }
    public int StudentCount { get; set; }
}

public sealed class ErpMealReportPageDto
{
    public List<ErpMealReportRowDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
    public int TotalEvents { get; set; }
    public int TotalUnits { get; set; }
    public int TotalStudents { get; set; }
    public DateTime BuiltAtUtc { get; set; }
}

public sealed class ErpMealReportOptionsDto
{
    public List<string> Grades { get; set; } = [];
    public List<string> Classes { get; set; } = [];
}

public sealed class ErpMealReportDetailDto
{
    public int Id { get; set; }
    public int? StudentProfileId { get; set; }
    public string ErpStudentId { get; set; } = default!;
    public string StudentName { get; set; } = default!;
    public string? Grade { get; set; }
    public string? ClassName { get; set; }
    public DateTime EventDay { get; set; }
    public string? Meal { get; set; }
    public string? OrderStatus { get; set; }
    public string? TicketStatus { get; set; }
    public string? ProductCode { get; set; }
    public string? ProductName { get; set; }
    public int Quantity { get; set; }
}

public sealed class ErpMealReportDetailsPageDto
{
    public List<ErpMealReportDetailDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}
