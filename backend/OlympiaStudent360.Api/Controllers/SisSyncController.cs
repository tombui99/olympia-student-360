using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Controllers;

[ApiController]
[Route("api/sis-sync")]
[Authorize(Roles = "Admin")]
public sealed class SisSyncController(AppDbContext db) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken ct) => Ok(new
    {
        Runs = await db.SisSyncRuns.AsNoTracking().OrderByDescending(x => x.StartedAtUtc).Take(50).ToListAsync(ct),
        Watermarks = await db.SisSyncStates.AsNoTracking().OrderBy(x => x.Endpoint).ThenBy(x => x.Id).ToListAsync(ct)
    });
}
