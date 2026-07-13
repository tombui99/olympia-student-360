using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/import-history")]
[ApiController]
[Authorize]
public class ImportHistoryController : ControllerBase
{
    private readonly AppDbContext _context;

    public ImportHistoryController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<List<ImportHistoryDto>>> List()
    {
        var query = _context.ImportHistories
            .AsNoTracking()
            .Where(item => item.Status == "Valid");
        if (!User.IsInRole("Admin") && !User.IsInRole("Principal"))
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            query = query.Where(item => item.ImportedByUserId == userId);
        }

        var history = await query
            .OrderByDescending(item => item.CreatedAt)
            .Take(200)
            .Select(item => ToDto(item))
            .ToListAsync();

        return Ok(history);
    }

    private static ImportHistoryDto ToDto(ImportHistory history) => new()
    {
        Id = history.Id,
        ModuleId = history.ModuleId,
        Source = history.Source,
        FileName = history.FileName,
        ImportedAt = history.CreatedAt,
        ImportedBy = history.ImportedByUsername,
        ImportedByName = history.ImportedByName,
        Status = history.Status,
        ValidRows = history.ValidRows,
        InvalidRows = history.InvalidRows
    };
}
