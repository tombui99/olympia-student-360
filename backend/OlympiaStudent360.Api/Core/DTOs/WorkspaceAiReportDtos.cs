namespace OlympiaStudent360.Api.Core.DTOs;

public class GenerateWorkspaceAiReportRequestDto
{
    public string Language { get; set; } = "vi";
    public string Audience { get; set; } = "leadership";
    public string Tone { get; set; } = "constructive";
    public string DetailLevel { get; set; } = "standard";
    public string? AdditionalInstructions { get; set; }
    public bool IncludeRecommendations { get; set; } = true;
    public List<WorkspaceAiFilterDto> Filters { get; set; } = [];
    public List<WorkspaceAiChartDto> Charts { get; set; } = [];
}

public class WorkspaceAiFilterDto
{
    public string Label { get; set; } = default!;
    public string Value { get; set; } = default!;
}

public class WorkspaceAiChartDto
{
    public string ChartId { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string ChartType { get; set; } = default!;
    public string Source { get; set; } = default!;
    public string? ImageDataUrl { get; set; }
    public List<WorkspaceAiSeriesDto> Series { get; set; } = [];
}

public class WorkspaceAiSeriesDto
{
    public string Name { get; set; } = default!;
    public string? Unit { get; set; }
    public List<WorkspaceAiPointDto> Points { get; set; } = [];
}

public class WorkspaceAiPointDto
{
    public string Label { get; set; } = default!;
    public decimal? Value { get; set; }
}

public class WorkspaceAiNarrative
{
    public string Title { get; set; } = default!;
    public string ExecutiveSummary { get; set; } = default!;
    public List<WorkspaceAiNarrativeSection> Sections { get; set; } = [];
    public List<string> OverallRecommendations { get; set; } = [];
    public List<string> Caveats { get; set; } = [];
}

public class WorkspaceAiNarrativeSection
{
    public string ChartId { get; set; } = default!;
    public string Heading { get; set; } = default!;
    public string Analysis { get; set; } = default!;
    public List<string> KeyFindings { get; set; } = [];
    public List<string> Recommendations { get; set; } = [];
}
