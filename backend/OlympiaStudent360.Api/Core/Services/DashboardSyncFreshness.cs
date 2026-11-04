using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Core.Services;

internal static class DashboardSyncFreshness
{
    public static async Task<DateTime?> LatestSuccessfulSisAtUtcAsync(AppDbContext db, CancellationToken ct = default)
    {
        var completed = await db.SisSyncRuns.AsNoTracking().Where(x => x.Status == "completed")
            .MaxAsync(x => x.CompletedAtUtc, ct);
        return completed.HasValue ? DateTime.SpecifyKind(completed.Value, DateTimeKind.Utc) : null;
    }

    public static async Task<DateTime?> LatestSuccessfulCanvasAtUtcAsync(AppDbContext db, CancellationToken ct = default)
    {
        var completed = await db.CanvasSyncRuns.AsNoTracking().Where(x => x.Status == "completed")
            .MaxAsync(x => x.CompletedAtUtc, ct);
        if (completed.HasValue) return DateTime.SpecifyKind(completed.Value, DateTimeKind.Utc);
        // Compatibility for a warehouse predating durable run history. A source watermark is
        // not a newer completed run and must never override actual completion history.
        var legacy = await db.CanvasSyncStates.AsNoTracking().Where(x => x.LastSuccessfulWatermarkUtc != null)
            .Select(x => x.LastStatus == "completed" ? x.LastCompletedAtUtc ?? x.LastSuccessfulWatermarkUtc : x.LastSuccessfulWatermarkUtc)
            .MaxAsync(ct);
        return legacy.HasValue ? DateTime.SpecifyKind(legacy.Value, DateTimeKind.Utc) : null;
    }
}
