using System.Text.Json;

namespace OlympiaStudent360.Api.Core.DTOs;

public class CustomReportDto
{
    public int Id { get; set; }
    public string OwnerUsername { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public JsonElement Definition { get; set; }

    public List<string> SharedWith { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class SaveCustomReportDto
{
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public JsonElement Definition { get; set; }

    public List<string> SharedWith { get; set; } = [];
}
