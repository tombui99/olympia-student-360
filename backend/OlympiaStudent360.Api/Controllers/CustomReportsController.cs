using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/reports/custom")]
[ApiController]
[Authorize]
public class CustomReportsController : ControllerBase
{
    private const int MaxChartsPerWorkspace = 6;
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly UserAccessService _userAccess;

    public CustomReportsController(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        UserAccessService userAccess)
    {
        _context = context;
        _userManager = userManager;
        _userAccess = userAccess;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<CustomReportDto>>> List()
    {
        var userId = CurrentUserId();
        var username = CurrentUsername();
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(username))
        {
            return Unauthorized();
        }

        var reports = await _context.CustomReports
            .Where(report => report.OwnerUserId == userId || (report.SharedWithJson != null && report.SharedWithJson != "[]"))
            .OrderByDescending(report => report.UpdatedAt ?? report.CreatedAt)
            .ToListAsync();

        return Ok(reports
            .Where(report => report.OwnerUserId == userId || SharedWith(report).Contains(username, StringComparer.OrdinalIgnoreCase))
            .Select(ToDto));
    }

    [HttpPost]
    public async Task<ActionResult<CustomReportDto>> Create([FromBody] SaveCustomReportDto model)
    {
        var userId = CurrentUserId();
        var username = CurrentUsername();
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(username))
        {
            return Unauthorized();
        }

        if (ChartCount(model.Definition) > MaxChartsPerWorkspace)
        {
            return ChartLimitExceeded();
        }
        if (await ContainsDisallowedMetrics(model.Definition)) return AccessScopeViolation();

        var sharedWith = await ValidRecipients(model.SharedWith, userId);
        var report = new CustomReport
        {
            OwnerUserId = userId,
            OwnerUsername = username,
            Title = model.Title,
            Description = model.Description,
            DefinitionJson = model.Definition.GetRawText(),

            SharedWithJson = JsonSerializer.Serialize(sharedWith),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.CustomReports.Add(report);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(List), new { id = report.Id }, ToDto(report));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CustomReportDto>> Update(int id, [FromBody] SaveCustomReportDto model)
    {
        var userId = CurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var report = await _context.CustomReports.FindAsync(id);
        if (report == null)
        {
            return NotFound();
        }

        if (report.OwnerUserId != userId)
        {
            return Forbid();
        }

        var proposedChartCount = ChartCount(model.Definition);
        var existingChartCount = ChartCount(report.DefinitionJson);
        // Keep legacy workspaces editable, but never allow their chart count to increase.
        if (proposedChartCount > MaxChartsPerWorkspace && proposedChartCount > existingChartCount)
        {
            return ChartLimitExceeded();
        }
        if (await ContainsDisallowedMetrics(model.Definition)) return AccessScopeViolation();

        report.Title = model.Title;
        report.Description = model.Description;
        report.DefinitionJson = model.Definition.GetRawText();
        var sharedWith = await ValidRecipients(model.SharedWith, userId);

        report.SharedWithJson = JsonSerializer.Serialize(sharedWith);
        report.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return Ok(ToDto(report));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var userId = CurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var report = await _context.CustomReports.FindAsync(id);
        if (report == null)
        {
            return NoContent();
        }

        if (report.OwnerUserId != userId)
        {
            return Forbid();
        }

        _context.CustomReports.Remove(report);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{id:int}/preference")]
    public async Task<ActionResult<WorkspacePreferenceDto>> Preference(int id)
    {
        var userId = CurrentUserId();
        var username = CurrentUsername();
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(username))
        {
            return Unauthorized();
        }

        var report = await _context.CustomReports.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        if (report == null)
        {
            return NotFound();
        }
        if (!CanAccess(report, userId, username))
        {
            return Forbid();
        }

        var preference = await _context.WorkspacePreferences.AsNoTracking()
            .FirstOrDefaultAsync(item => item.CustomReportId == id && item.UserId == userId);
        return Ok(preference == null ? new WorkspacePreferenceDto() : ToPreferenceDto(preference));
    }

    [HttpPut("{id:int}/preference")]
    public async Task<ActionResult<WorkspacePreferenceDto>> SavePreference(
        int id, [FromBody] WorkspacePreferenceDto model)
    {
        var userId = CurrentUserId();
        var username = CurrentUsername();
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(username))
        {
            return Unauthorized();
        }

        var report = await _context.CustomReports.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id);
        if (report == null)
        {
            return NotFound();
        }
        if (!CanAccess(report, userId, username))
        {
            return Forbid();
        }

        var preference = await _context.WorkspacePreferences
            .FirstOrDefaultAsync(item => item.CustomReportId == id && item.UserId == userId);
        if (preference == null)
        {
            preference = new WorkspacePreference { CustomReportId = id, UserId = userId };
            _context.WorkspacePreferences.Add(preference);
        }

        preference.AcademicYear = NormalizeAcademicYear(model.AcademicYear);
        preference.Term = NormalizeTerm(model.Term);
        preference.MeasurementCycle = Clean(model.MeasurementCycle);
        preference.Campus = Clean(model.Campus);
        preference.EducationLevel = Clean(model.EducationLevel);
        preference.GradeLevel = Clean(model.GradeLevel);
        preference.Homeroom = Clean(model.Homeroom);
        preference.StudentProfileId = model.StudentProfileId;
        preference.Program = Clean(model.Program);
        preference.Subject = Clean(model.Subject);
        preference.ChartOrderJson = JsonSerializer.Serialize(model.ChartOrder ?? []);
        preference.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(ToPreferenceDto(preference));
    }

    private string? CurrentUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private string? CurrentUsername()
    {
        return User.FindFirstValue(ClaimTypes.Name);
    }

    private static CustomReportDto ToDto(CustomReport report)
    {
        return new CustomReportDto
        {
            Id = report.Id,
            OwnerUsername = report.OwnerUsername,
            Title = report.Title,
            Description = report.Description,
            Definition = JsonSerializer.Deserialize<JsonElement>(report.DefinitionJson),

            SharedWith = SharedWith(report),
            CreatedAt = report.CreatedAt,
            UpdatedAt = report.UpdatedAt
        };
    }

    private static List<string> SharedWith(CustomReport report)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(report.SharedWithJson) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static bool CanAccess(CustomReport report, string userId, string username) =>
        report.OwnerUserId == userId || SharedWith(report).Contains(username, StringComparer.OrdinalIgnoreCase);

    private static int ChartCount(JsonElement definition) =>
        definition.ValueKind == JsonValueKind.Object
        && definition.TryGetProperty("charts", out var charts)
        && charts.ValueKind == JsonValueKind.Array
            ? charts.GetArrayLength()
            : 0;

    private static int ChartCount(string definitionJson)
    {
        try
        {
            using var definition = JsonDocument.Parse(definitionJson);
            return ChartCount(definition.RootElement);
        }
        catch (JsonException)
        {
            return 0;
        }
    }

    private static BadRequestObjectResult ChartLimitExceeded() => new(new
    {
        code = "workspace_chart_limit_exceeded",
        message = $"A workspace can contain at most {MaxChartsPerWorkspace} charts."
    });

    private async Task<bool> ContainsDisallowedMetrics(JsonElement definition)
    {
        if (!_userAccess.Current.IsTeacher || definition.ValueKind != JsonValueKind.Object ||
            !definition.TryGetProperty("charts", out var charts) || charts.ValueKind != JsonValueKind.Array)
            return false;

        var metricIds = charts.EnumerateArray()
            .Where(chart => chart.ValueKind == JsonValueKind.Object &&
                chart.TryGetProperty("metrics", out var metrics) && metrics.ValueKind == JsonValueKind.Array)
            .SelectMany(chart => chart.GetProperty("metrics").EnumerateArray())
            .Where(metric => metric.ValueKind == JsonValueKind.String)
            .Select(metric => metric.GetString()!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var storedDefinitions = await _context.DashboardMetrics.AsNoTracking()
            .Select(metric => new { metric.Key, metric.Source, metric.Domain })
            .ToListAsync();
        var definitions = storedDefinitions
            .Where(metric => metricIds.Contains(metric.Key, StringComparer.Ordinal))
            .ToDictionary(metric => metric.Key, StringComparer.Ordinal);
        return metricIds.Any(metricId =>
        {
            definitions.TryGetValue(metricId, out var metricDefinition);
            return !_userAccess.Current.CanAccessMetric(
                metricId, metricDefinition?.Source, metricDefinition?.Domain);
        });
    }

    private static BadRequestObjectResult AccessScopeViolation() => new(new
    {
        code = "access_scope_updated",
        message = "One or more charts use data outside your current access scope."
    });

    private static WorkspacePreferenceDto ToPreferenceDto(WorkspacePreference preference) => new()
    {
        AcademicYear = NormalizeAcademicYear(preference.AcademicYear),
        Term = NormalizeTerm(preference.Term),
        MeasurementCycle = preference.MeasurementCycle,
        Campus = preference.Campus,
        EducationLevel = preference.EducationLevel,
        GradeLevel = preference.GradeLevel,
        Homeroom = preference.Homeroom,
        StudentProfileId = preference.StudentProfileId,
        Program = preference.Program,
        Subject = preference.Subject,
        ChartOrder = JsonSerializer.Deserialize<List<string>>(preference.ChartOrderJson) ?? []
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeAcademicYear(string? value) =>
        Clean(value) is { } cleaned ? DashboardPeriodNormalizer.AcademicYear(cleaned) : null;

    private static string? NormalizeTerm(string? value) =>
        Clean(value) is { } cleaned ? DashboardPeriodNormalizer.Term(cleaned) : null;

    private async Task<List<string>> ValidRecipients(IEnumerable<string> recipients, string ownerUserId)
    {
        var requestedRecipients = recipients
            .Select(recipient => recipient.Trim())
            .Where(recipient => !string.IsNullOrWhiteSpace(recipient))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (requestedRecipients.Count == 0)
        {
            return [];
        }

        var validUsernames = new List<string>();
        foreach (var username in requestedRecipients)
        {
            var user = await _userManager.FindByNameAsync(username);
            if (user != null && user.Id != ownerUserId)
            {
                validUsernames.Add(user.UserName!);
            }
        }

        return validUsernames;
    }
}
