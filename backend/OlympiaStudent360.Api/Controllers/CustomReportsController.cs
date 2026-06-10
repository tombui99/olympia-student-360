using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/reports/custom")]
[ApiController]
[Authorize]
public class CustomReportsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public CustomReportsController(
        AppDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
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
