using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Core.Services;

/// <summary>Only persisted inputs consumed by report builders invalidate their snapshots.</summary>
internal static class ReportChangeTracking
{
    private static readonly string[] ImportedStudentFields =
        ["FullName", "SISUserId", "GradeLevel", "CanvasGradeLevel", "Homeroom", "Campus", "EducationLevel", "Program"];
    private static readonly string[] MealStudentFields = ["FullName", "GradeLevel", "CanvasGradeLevel", "Homeroom"];
    private static readonly string[] HealthFields =
        ["StudentProfileId", "SISUserId", "FullName", "ClassName", "ExamTime", "Diagnosis", "Symptoms",
            "Classification", "ExaminedBy", "SisVisitId", "SisSourceKey", "SisIsDeleted", "ImportHistoryId"];

    public static void Queue(AppDbContext db, bool deferNewStudents = false)
    {
        foreach (var entry in db.ChangeTracker.Entries().ToArray())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;
            if (entry.State == EntityState.Modified && entry.Properties.Where(x => x.IsModified &&
                    !Equals(x.OriginalValue, x.CurrentValue)).All(x => x.Metadata.Name is "CreatedAt" or "UpdatedAt"))
                continue;
            if (entry.Entity is StudentProfile student)
            {
                if (deferNewStudents && entry.State == EntityState.Added) continue;
                var areas = ReportArea.None;
                if (Changed(entry, ImportedStudentFields)) areas |= ReportArea.Imported;
                if (Changed(entry, MealStudentFields)) areas |= ReportArea.Meals;
                if (entry.State is EntityState.Added or EntityState.Deleted) areas |= ReportArea.Risk;
                Add(db, areas, student.Id > 0 ? student.Id : null);
                continue;
            }
            var scope = entry.Entity switch
            {
                StudentHealthVisit when Changed(entry, HealthFields) => ReportArea.Health,
                StudentBehaviour => ReportArea.Behaviours | ReportArea.Risk,
                StudentProject => ReportArea.Projects | ReportArea.Risk,
                StudentMentalHealthRecord => ReportArea.MentalHealth | ReportArea.Risk,
                StudentBusAttendanceRecord => ReportArea.Bus | ReportArea.Risk,
                StudentClassificationSetting or AcademicPeriod or CanvasDashboardAggregateBuild => ReportArea.Risk,
                StudentRecordFieldOption => ReportArea.Imported | ReportArea.Risk,
                _ => ReportArea.None
            };
            Add(db, scope);
        }
    }

    public static void Add(AppDbContext db, ReportArea areas, int? studentId = null)
    {
        if (areas == ReportArea.None) return;
        if (!db.ChangeTracker.Entries<ImportedReportChange>().Any(x => x.State == EntityState.Added &&
            x.Entity.Areas == areas && x.Entity.StudentProfileId == studentId))
            db.ImportedReportChanges.Add(new ImportedReportChange { Areas = areas, StudentProfileId = studentId });
    }

    private static bool Changed(EntityEntry entry, IEnumerable<string> names) =>
        entry.State is EntityState.Added or EntityState.Deleted || names.Any(name =>
            entry.Property(name).IsModified && !Equals(entry.Property(name).OriginalValue, entry.Property(name).CurrentValue));

    internal static object HealthValues(StudentHealthVisit visit) => new
    {
        visit.StudentProfileId, visit.SISUserId, visit.FullName, visit.ClassName, visit.ExamTime,
        visit.Diagnosis, visit.Symptoms, visit.Classification, visit.ExaminedBy, visit.SisVisitId,
        visit.SisSourceKey, visit.SisIsDeleted, visit.ImportHistoryId
    };
}
