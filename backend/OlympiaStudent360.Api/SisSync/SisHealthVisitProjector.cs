using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.SisSync;

/// <summary>Projects source visits into the existing health page/report table, with stable source keys.</summary>
public sealed class SisHealthVisitProjector(AppDbContext db)
{
    public async Task ProjectAsync(string sourceKey, JsonElement source, CancellationToken ct)
    {
        var visitId = SisJson.RequiredId(source);
        var visit = await db.StudentHealthVisits.IgnoreQueryFilters()
            .SingleOrDefaultAsync(x => x.SisSourceKey == sourceKey && x.SisVisitId == visitId, ct);
        var deleted = SisJson.RequiredBool(source, "is_deleted");
        if (deleted)
        {
            if (visit is not null) { visit.SisIsDeleted = true; visit.UpdatedAt = DateTime.UtcNow; }
            return;
        }
        if (!SisApiClient.TryTimestamp(SisJson.Text(source, "visited_at", 50), out var visitedAt))
            throw new SisApiException("SIS health visit is missing a valid visited_at timestamp.");
        var reference = SisJson.Child(source, "student");
        var userId = SisJson.RequiredId(reference, "user_id");
        var student = await db.StudentProfiles.SingleOrDefaultAsync(x => x.SchoolOnlineUserId == userId, ct);
        var code = SisJson.Text(reference, "code", 50);
        if (student is null && code is not null)
            student = await db.StudentProfiles.SingleOrDefaultAsync(x => x.SISStudentCode == code &&
                (x.SchoolOnlineUserId == null || x.SchoolOnlineUserId == userId), ct);
        if (visit is null)
        {
            visit = new StudentHealthVisit { SisSourceKey = sourceKey, SisVisitId = visitId };
            db.StudentHealthVisits.Add(visit);
        }
        visit.SisIsDeleted = false;
        visit.StudentProfileId = student?.Id;
        visit.SISUserId = student?.SISUserId;
        visit.FullName = SisJson.Text(reference, "full_name", 255) ?? student?.FullName ?? "";
        visit.Gender = SisJson.Text(reference, "gender", 20) ?? student?.Gender;
        visit.ClassName = SisJson.Text(SisJson.Child(source, "homeroom_class"), "name", 50) ?? student?.Homeroom;
        var severity = SisJson.Text(source, "severity", 100);
        var next = new SourceValues(
            // Existing manual health visits use Vietnam wall time for page/calendar filters.
            visitedAt.ToOffset(TimeSpan.FromHours(7)).DateTime,
            SisJson.Text(source, "symptoms", 1000),
            SisJson.Text(SisJson.Child(source, "diagnosis"), "name", 1000) ?? SisJson.Text(SisJson.Child(source, "diagnosis"), "code", 1000),
            severity switch
            {
                "NORMAL" => "Bình thường", "SERIOUS" => "Nặng", "INFECTIOUS" => "Truyền nhiễm",
                "VERY_SERIOUS" => "Rất nặng", _ => severity
            },
            SisJson.Text(source, "treatment", 1000), SisJson.Text(source, "medicine", 500),
            SisJson.Text(source, "notes", 1000), SisJson.Bool(source, "parent_notified") ?? false);
        visit.ExamTime = next.ExamTime;
        visit.Symptoms = next.Symptoms;
        visit.Diagnosis = next.Diagnosis;
        visit.Classification = next.Classification;
        visit.Treatment = next.Treatment;
        visit.Medication = next.Medication;
        visit.Notes = next.Notes;
        visit.NotificationSent = next.NotificationSent;
        visit.ExaminedBy = null; // Not supplied by the SIS visit contract.
        visit.UpdatedAt = DateTime.UtcNow;
    }

    private sealed record SourceValues(DateTime ExamTime, string? Symptoms, string? Diagnosis,
        string? Classification, string? Treatment, string? Medication, string? Notes, bool NotificationSent);
}
