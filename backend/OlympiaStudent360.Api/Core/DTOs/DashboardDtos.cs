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
