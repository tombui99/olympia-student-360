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
