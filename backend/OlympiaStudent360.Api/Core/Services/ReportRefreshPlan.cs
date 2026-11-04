using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Core.Services;

internal sealed class ReportRefreshPlan
{
    public const int SchemaVersion = 1;
    public static readonly (ReportArea Area, string Source)[] Sources =
    [
        (ReportArea.Behaviours, "student-behaviours"), (ReportArea.Projects, "student-projects"),
        (ReportArea.MentalHealth, "student-mental-health"), (ReportArea.Bus, "student-bus-attendance"),
        (ReportArea.Health, "student-health-visits")
    ];
    private ReportArea all;
    private readonly Dictionary<int, ReportArea> students = [];
    public int LastChangeId { get; private set; }
    public bool GloballyIncludes(ReportArea area) => (all & area) == area;
    public bool Has(ReportArea area) => (all & area) != 0 || students.Values.Any(x => (x & area) != 0);
    public bool Includes(ReportArea area, int? studentId) => (all & area) != 0 ||
        studentId.HasValue && students.TryGetValue(studentId.Value, out var areas) && (areas & area) != 0;

    public static async Task<ReportRefreshPlan> LoadAsync(AppDbContext db, int after, bool full,
        bool dayChanged, CancellationToken ct)
    {
        var changes = await db.ImportedReportChanges.AsNoTracking().Where(x => x.Id > after).ToListAsync(ct);
        var plan = new ReportRefreshPlan { LastChangeId = changes.Count == 0 ? after : changes.Max(x => x.Id) };
        foreach (var change in changes)
        {
            if (change.StudentProfileId is int id)
                plan.students[id] = plan.students.GetValueOrDefault(id) | change.Areas;
            else plan.all |= change.Areas;
        }
        if (full) plan.all = ReportArea.All;
        // Only these calculations depend on today's date. Health and meal facts do not.
        else if (dayChanged) plan.all |= ReportArea.Behaviours | ReportArea.Risk;
        return plan;
    }

    public IQueryable<T> Filter<T>(IQueryable<T> query, ReportArea area, Expression<Func<T, int?>> student)
    {
        if ((all & area) != 0) return query;
        var ids = students.Where(x => (x.Value & area) != 0).Select(x => x.Key).ToArray();
        var body = Expression.Coalesce(student.Body, Expression.Constant(-1));
        return query.WhereMatchesAnyValue(Expression.Lambda<Func<T, int>>(body, student.Parameters), ids);
    }

    public static async Task PruneAsync(AppDbContext db, CancellationToken ct)
    {
        var imported = await db.ImportedReportBuilds.AsNoTracking().MaxAsync(x => (int?)x.LastChangeId, ct) ?? 0;
        var meals = await db.ErpMealReportBuilds.AsNoTracking().MaxAsync(x => (int?)x.LastStudentChangeId, ct) ?? 0;
        var through = Math.Min(imported, meals);
        // Both independent consumers must acknowledge a change before it can be removed.
        await db.ImportedReportChanges.Where(x => x.Id < through).ExecuteDeleteAsync(ct);
    }
}
