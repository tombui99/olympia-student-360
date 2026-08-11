using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[Route("api/sync-history")]
[ApiController]
[Authorize(Roles = "Admin")]
public sealed class SyncHistoryController : ControllerBase
{
    private readonly AppDbContext _db;

    public SyncHistoryController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<ActionResult<SyncHistoryPageDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _db.CanvasSyncRuns.AsNoTracking();
        var totalCount = await query.CountAsync(cancellationToken);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling((double)totalCount / pageSize);
        page = Math.Min(page, Math.Max(1, totalPages));
        var items = await query
            .OrderByDescending(item => item.StartedAtUtc)
            .ThenByDescending(item => item.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new SyncRunDto(
                item.Id,
                item.Source,
                item.Workflow,
                item.Status,
                item.StartedAtUtc,
                item.CompletedAtUtc,
                item.Error,
                item.Courses,
                item.Assignments,
                item.Enrollments,
                item.Scores,
                item.Submissions))
            .ToListAsync(cancellationToken);

        return Ok(new SyncHistoryPageDto(items, page, pageSize, totalCount, totalPages));
    }
}

public sealed record SyncHistoryPageDto(
    IReadOnlyList<SyncRunDto> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record SyncRunDto(
    int Id,
    string Source,
    string Workflow,
    string Status,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    string? Error,
    int? Courses,
    int? Assignments,
    int? Enrollments,
    int? Scores,
    int? Submissions);
