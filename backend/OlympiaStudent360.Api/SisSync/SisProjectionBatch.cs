using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.SisSync;

/// <summary>Preloads a page's lookups and writes rows in batches within the caller's transaction.</summary>
internal sealed class SisProjectionBatch(AppDbContext db)
{
    public Dictionary<string, SisWarehouseRecord> Warehouse { get; } = new(StringComparer.Ordinal);
    public List<StudentProfile> Students { get; private set; } = [];
    public List<SISStudentParent> Parents { get; private set; } = [];
    public List<StudentHealthVisit> Visits { get; private set; } = [];
    public HashSet<StudentHealthVisit> ChangedVisits { get; } = [];
    public bool HealthReportsChanged { get; set; }

    public static async Task<SisProjectionBatch> LoadAsync(AppDbContext db, string source, string endpoint,
        IReadOnlyList<JsonElement> records, bool projectStudents, CancellationToken ct)
    {
        var batch = new SisProjectionBatch(db);
        var keys = records.Select(row => SisSyncRunner.RecordKey(endpoint, row)).ToArray();
        foreach (var chunk in keys.Chunk(500))
        {
            var existing = await db.SisWarehouseRecords.AsNoTracking()
                .Where(x => x.SourceKey == source && x.Endpoint == endpoint)
                .WhereMatchesAny(x => x.RecordKey, chunk).ToListAsync(ct);
            foreach (var row in existing) batch.Warehouse.Add(row.RecordKey, row);
        }
        if (projectStudents && endpoint == "students")
        {
            batch.Students = await db.StudentProfiles.ToListAsync(ct);
            batch.Parents = await db.SISStudentParents.IgnoreQueryFilters().ToListAsync(ct);
        }
        else if (endpoint == "health/visits")
        {
            batch.Students = await db.StudentProfiles.AsNoTracking().ToListAsync(ct);
            foreach (var chunk in keys.Chunk(500))
                batch.Visits.AddRange(await db.StudentHealthVisits.IgnoreQueryFilters().AsNoTracking()
                    .Where(x => x.SisSourceKey == source).WhereMatchesAny(x => x.SisVisitId, chunk).ToListAsync(ct));
        }
        return batch;
    }

    public async Task FlushAsync(CancellationToken ct)
    {
        // Existing source-owned fields can be upserted without one network request per row.
        // Only EF's modified columns are updated, preserving manual campus/programme and Canvas fields.
        db.ChangeTracker.DetectChanges();
        var newStudents = db.ChangeTracker.Entries<StudentProfile>().Where(x => x.State == EntityState.Added)
            .Select(x => x.Entity).ToArray();
        ReportChangeTracking.Queue(db, deferNewStudents: true);
        await FlushModifiedAsync<StudentProfile>(ct);
        await FlushModifiedAsync<SISStudentParent>(ct);
        if (HealthReportsChanged) ReportChangeTracking.Add(db, ReportArea.Health);
        var changes = db.ChangeTracker.Entries<ImportedReportChange>().Where(x => x.State == EntityState.Added).ToArray();
        foreach (var entry in changes) entry.State = EntityState.Detached;
        // EF allocates IDs and fixes up new student/parent relationships. Existing rows were accepted above.
        await db.SaveQueuedSourceChangesAsync(ct);
        await MySqlRowBatchWriter.InsertAsync(db, changes.Select(x => x.Entity).Concat(newStudents.Select(student =>
            new ImportedReportChange { Areas = ReportArea.All, StudentProfileId = student.Id })), ct);
        await MySqlRowBatchWriter.UpsertAsync(db, Warehouse.Values, null, ct);
        await MySqlRowBatchWriter.UpsertAsync(db, ChangedVisits, null, ct);
    }

    private async Task<int> FlushModifiedAsync<T>(CancellationToken ct) where T : BaseEntity
    {
        var entries = db.ChangeTracker.Entries<T>().Where(x => x.State == EntityState.Modified).ToArray();
        if (entries.Length == 0) return 0;
        var properties = entries.SelectMany(x => x.Properties.Where(p => p.IsModified).Select(p => p.Metadata.Name))
            .Where(name => name is not (nameof(BaseEntity.Id) or nameof(BaseEntity.CreatedAt)))
            .ToHashSet(StringComparer.Ordinal);
        await MySqlRowBatchWriter.UpsertAsync(db, entries.Select(x => x.Entity), properties, ct);
        foreach (var entry in entries) entry.State = EntityState.Unchanged;
        return entries.Length;
    }
}
