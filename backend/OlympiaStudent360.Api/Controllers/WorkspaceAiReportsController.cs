using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/reports/custom/{reportId:int}/ai-report")]
[ApiController]
[Authorize]
public class WorkspaceAiReportsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly GeminiService _gemini;
    private readonly WorkspaceAiReportDocumentService _documents;
    private readonly ILogger<WorkspaceAiReportsController> _logger;

    public WorkspaceAiReportsController(
        AppDbContext context,
        GeminiService gemini,
        WorkspaceAiReportDocumentService documents,
        ILogger<WorkspaceAiReportsController> logger)
    {
        _context = context;
        _gemini = gemini;
        _documents = documents;
        _logger = logger;
    }

    [HttpPost]
    [Produces("application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    public async Task<IActionResult> Generate(
        int reportId,
        [FromBody] GenerateWorkspaceAiReportRequestDto request,
        CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = User.FindFirstValue(ClaimTypes.Name);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(username)) return Unauthorized();

        var report = await _context.CustomReports.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == reportId, cancellationToken);
        if (report == null) return NotFound();
        if (report.OwnerUserId != userId && !SharedWith(report).Contains(username, StringComparer.OrdinalIgnoreCase))
        {
            return Forbid();
        }

        var allowedChartIds = ChartIds(report.DefinitionJson);
        request = Sanitize(request, allowedChartIds);
        if (request.Charts.Count == 0)
        {
            return BadRequest(new { Message = "At least one valid workspace chart with data is required." });
        }

        try
        {
            var narrative = await _gemini.GenerateWorkspaceReportAsync(
                report.Title, report.Description, request, cancellationToken);
            var bytes = _documents.Build(report.Title, report.Description, request, narrative, username);
            var fileName = $"{SafeFileName(report.Title)}-AI-report-{DateTime.Now:yyyyMMdd}.docx";
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                fileName);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("API key", StringComparison.OrdinalIgnoreCase))
        {
            return Problem("Gemini API is not configured for this environment.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (GeminiProviderException ex) when (ex.IsCredentialFailure)
        {
            _logger.LogError(
                "Gemini credentials were rejected while generating report {ReportId}; provider status {Status}.",
                reportId,
                (int)ex.ProviderStatusCode);
            return Problem(
                "Gemini credentials were rejected. Update the server API key and try again.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (GeminiProviderException ex)
        {
            _logger.LogWarning(
                ex,
                "Gemini failed while generating report {ReportId}; provider status {Status}.",
                reportId,
                (int)ex.ProviderStatusCode);
            return Problem(
                "Gemini could not generate the report. Please try again.",
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Gemini failed while generating report {ReportId}.", reportId);
            return Problem(
                "Gemini could not generate the report. Please try again.",
                statusCode: StatusCodes.Status502BadGateway);
        }
        catch (InvalidDataException ex)
        {
            _logger.LogWarning(ex, "Gemini returned an invalid report for workspace {ReportId}.", reportId);
            return Problem("Gemini returned an invalid report. Please try again.", statusCode: StatusCodes.Status502BadGateway);
        }
    }

    private static GenerateWorkspaceAiReportRequestDto Sanitize(
        GenerateWorkspaceAiReportRequestDto request,
        HashSet<string> allowedChartIds)
    {
        request.Language = request.Language == "en" ? "en" : "vi";
        request.Audience = new[] { "leadership", "teachers", "parents" }.Contains(request.Audience) ? request.Audience : "leadership";
        request.Tone = new[] { "constructive", "formal", "concise" }.Contains(request.Tone) ? request.Tone : "constructive";
        request.DetailLevel = new[] { "brief", "standard", "detailed" }.Contains(request.DetailLevel) ? request.DetailLevel : "standard";
        request.AdditionalInstructions = Cut(request.AdditionalInstructions, 1200);
        request.Filters = request.Filters.Take(20)
            .Select(item => new WorkspaceAiFilterDto { Label = Cut(item.Label, 100)!, Value = Cut(item.Value, 250)! })
            .Where(item => item.Label.Length > 0 && item.Value.Length > 0)
            .ToList();
        request.Charts = request.Charts
            .Where(chart => allowedChartIds.Contains(chart.ChartId))
            .GroupBy(chart => chart.ChartId, StringComparer.Ordinal)
            .Select(group => group.First())
            .Take(20)
            .Select(chart => new WorkspaceAiChartDto
            {
                ChartId = chart.ChartId,
                Title = Cut(chart.Title, 200)!,
                ChartType = Cut(chart.ChartType, 40)!,
                Source = Cut(chart.Source, 100)!,
                ImageDataUrl = chart.ImageDataUrl,
                Series = chart.Series.Take(12).Select(series => new WorkspaceAiSeriesDto
                {
                    Name = Cut(series.Name, 160)!,
                    Unit = Cut(series.Unit, 30),
                    Points = series.Points.Take(200).Select(point => new WorkspaceAiPointDto
                    {
                        Label = Cut(point.Label, 120)!,
                        Value = point.Value
                    }).Where(point => point.Label.Length > 0).ToList()
                }).Where(series => series.Name.Length > 0 && series.Points.Count > 0).ToList()
            })
            .Where(chart => chart.Title.Length > 0 && chart.Series.Count > 0)
            .ToList();

        var totalImageBytes = 0;
        foreach (var chart in request.Charts)
        {
            var (dataUrl, byteCount) = SanitizePngDataUrl(chart.ImageDataUrl);
            if (dataUrl != null && totalImageBytes + byteCount <= 10_000_000)
            {
                chart.ImageDataUrl = dataUrl;
                totalImageBytes += byteCount;
            }
            else
            {
                chart.ImageDataUrl = null;
            }
        }
        return request;
    }

    private static (string? DataUrl, int ByteCount) SanitizePngDataUrl(string? value)
    {
        const string prefix = "data:image/png;base64,";
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return (null, 0);
        }

        try
        {
            var bytes = Convert.FromBase64String(value[prefix.Length..]);
            if (bytes.Length is < 64 or > 1_500_000 || !IsPng(bytes)) return (null, 0);
            return ($"{prefix}{Convert.ToBase64String(bytes)}", bytes.Length);
        }
        catch (FormatException)
        {
            return (null, 0);
        }
    }

    private static bool IsPng(byte[] bytes) =>
        bytes.Length >= 24 &&
        bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47 &&
        bytes[4] == 0x0D && bytes[5] == 0x0A && bytes[6] == 0x1A && bytes[7] == 0x0A;

    private static HashSet<string> ChartIds(string definitionJson)
    {
        try
        {
            using var json = JsonDocument.Parse(definitionJson);
            if (!json.RootElement.TryGetProperty("charts", out var charts) || charts.ValueKind != JsonValueKind.Array)
            {
                return [];
            }
            return charts.EnumerateArray()
                .Where(chart => chart.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.String)
                .Select(chart => chart.GetProperty("id").GetString())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id!)
                .ToHashSet(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static List<string> SharedWith(CustomReport report)
    {
        try { return JsonSerializer.Deserialize<List<string>>(report.SharedWithJson) ?? []; }
        catch (JsonException) { return []; }
    }

    private static string? Cut(string? value, int length)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var clean = value.Trim();
        return clean[..Math.Min(clean.Length, length)];
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var clean = new string(value.Where(character => !invalid.Contains(character)).ToArray()).Trim();
        if (string.IsNullOrWhiteSpace(clean)) clean = "workspace";
        return clean[..Math.Min(clean.Length, 80)];
    }
}
