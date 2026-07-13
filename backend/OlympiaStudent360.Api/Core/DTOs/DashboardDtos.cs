namespace OlympiaStudent360.Api.Core.DTOs;

public class MetricCardDto
{
    public string Label { get; set; } = default!;
    public string Value { get; set; } = default!;
    public string Delta { get; set; } = default!;
}

public class DashboardSummaryDto
{
    public List<MetricCardDto> Metrics { get; set; } = new();
}

public class DashboardOverviewDto
{
    public string PeriodYear { get; set; } = default!;
    public string PeriodTerm { get; set; } = default!;
    public int TotalStudents { get; set; }
    public int AtRiskTotal { get; set; }
    public int AtRiskHigh { get; set; }
    public int AtRiskMedium { get; set; }
    public int OnTrack { get; set; }
    public int TotalBehaviours { get; set; }
    public int BehavioursLast30Days { get; set; }
    public int TotalProjects { get; set; }
    public int ActiveProjects { get; set; }
    public List<DashboardTrendPointDto> BehaviourTrend { get; set; } = [];
    public List<DashboardGradeDto> GradeDistribution { get; set; } = [];
}

public class DashboardTrendPointDto
{
    public int Year { get; set; }
    public int Month { get; set; }
    public int Total { get; set; }
}

public class DashboardGradeDto
{
    public string Grade { get; set; } = default!;
    public int Students { get; set; }
    public int AtRisk { get; set; }
}

public class DashboardFilterOptionsDto
{
    public List<string> AcademicYears { get; set; } = [];
    public List<string> Terms { get; set; } = [];
    public List<string> MeasurementCycles { get; set; } = [];
    public List<string> Campuses { get; set; } = [];
    public List<string> EducationLevels { get; set; } = [];
    public List<string> GradeLevels { get; set; } = [];
    public List<string> Homerooms { get; set; } = [];
    public List<DashboardStudentOptionDto> Students { get; set; } = [];
    public List<string> Programs { get; set; } = [];
    public List<string> Subjects { get; set; } = [];
}

public class DashboardStudentOptionDto
{
    public int Id { get; set; }
    public string StudentCode { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
}

public class DashboardPreferenceDto
{
    public string? AcademicYear { get; set; }
    public string? Term { get; set; }
    public string? MeasurementCycle { get; set; }
    public string? Campus { get; set; }
    public string? EducationLevel { get; set; }
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
    public int? StudentProfileId { get; set; }
    public string? Program { get; set; }
    public string? Subject { get; set; }
    public Dictionary<string, List<string>> ChartOrders { get; set; } = [];
}

public class DashboardMetricDto
{
    public string Id { get; set; } = default!;
    public string Label { get; set; } = default!;
    public string Category { get; set; } = default!;
    public string Source { get; set; } = default!;
    public string Unit { get; set; } = "";
    public decimal? Value { get; set; }
    public decimal? Trend { get; set; }
    public decimal? BaselineValue { get; set; }
    public decimal? KpiTarget { get; set; }
    public List<DashboardMetricPointDto> Series { get; set; } = [];
}

public class DashboardMetricPointDto
{
    public DateTime PeriodStart { get; set; }
    public decimal Value { get; set; }
}
