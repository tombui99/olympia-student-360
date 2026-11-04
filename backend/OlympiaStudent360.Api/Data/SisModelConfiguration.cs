using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;

namespace OlympiaStudent360.Api.Data;

internal static class SisModelConfiguration
{
    internal static void Configure(ModelBuilder model)
    {
        model.Entity<SISStudentParent>().HasQueryFilter(x => !x.SISIsDeleted);
        model.Entity<StudentHealthVisit>(row =>
        {
            row.Property(x => x.SisSourceKey).HasMaxLength(64).UseCollation("utf8mb4_bin");
            row.Property(x => x.SisVisitId).HasMaxLength(100).UseCollation("utf8mb4_bin");
            row.HasIndex(x => new { x.SisSourceKey, x.SisVisitId }).IsUnique();
            // Legacy manual/Excel records remain archived in this table, outside the live SIS feed.
            row.HasQueryFilter(x => x.SisVisitId != null && !x.SisIsDeleted);
        });
        model.Entity<DashboardMetric>().HasQueryFilter(x => x.Source.Trim().ToUpper() != "SIS");
        model.Entity<DashboardMetricObservation>().HasQueryFilter(x => x.Metric.Source.Trim().ToUpper() != "SIS");
        model.Entity<StudentProfile>(row =>
        {
            row.Property(x => x.SchoolOnlineUserId).HasMaxLength(100).UseCollation("utf8mb4_bin");
            row.Property(x => x.SchoolOnlineSchoolId).HasMaxLength(100).UseCollation("utf8mb4_bin");
            row.Property(x => x.SchoolOnlineSchoolYearId).HasMaxLength(100).UseCollation("utf8mb4_bin");
            row.HasIndex(x => x.SchoolOnlineUserId).IsUnique();
        });
        model.Entity<SisWarehouseRecord>(row =>
        {
            row.Property(x => x.SourceKey).HasMaxLength(64).UseCollation("utf8mb4_bin");
            row.Property(x => x.Endpoint).HasMaxLength(64).UseCollation("utf8mb4_bin");
            row.Property(x => x.RecordKey).HasMaxLength(191).UseCollation("utf8mb4_bin");
            row.Property(x => x.SourceId).HasMaxLength(100).UseCollation("utf8mb4_bin");
            row.Property(x => x.SchoolId).HasMaxLength(100).UseCollation("utf8mb4_bin");
            row.Property(x => x.SchoolYearId).HasMaxLength(100).UseCollation("utf8mb4_bin");
            row.Property(x => x.StudentUserId).HasMaxLength(100).UseCollation("utf8mb4_bin");
            row.Property(x => x.RecordType).HasMaxLength(50);
            row.Property(x => x.RecordDate).HasColumnType("date");
            row.Property(x => x.PayloadJson).HasColumnType("json");
            row.Property(x => x.LastSeenScanId).HasMaxLength(32);
            row.HasIndex(x => new { x.SourceKey, x.Endpoint, x.RecordKey }).IsUnique();
            row.HasIndex(x => new { x.SourceKey, x.Endpoint, x.SchoolYearId, x.RecordDate });
            row.HasIndex(x => new { x.SourceKey, x.StudentUserId });
        });
        model.Entity<SisSyncState>(row =>
        {
            row.Property(x => x.SourceKey).HasMaxLength(64).UseCollation("utf8mb4_bin");
            row.Property(x => x.Endpoint).HasMaxLength(64).UseCollation("utf8mb4_bin");
            row.Property(x => x.FilterKey).HasMaxLength(64).UseCollation("utf8mb4_bin");
            row.Property(x => x.FiltersJson).HasColumnType("json");
            row.Property(x => x.LastSuccessfulWatermark).HasMaxLength(50);
            row.HasIndex(x => new { x.SourceKey, x.Endpoint, x.FilterKey }).IsUnique();
        });
        model.Entity<SisSyncRun>(row =>
        {
            row.Property(x => x.SourceKey).HasMaxLength(64).UseCollation("utf8mb4_bin");
            row.Property(x => x.Mode).HasMaxLength(30);
            row.Property(x => x.Status).HasMaxLength(30);
            row.Property(x => x.Error).HasMaxLength(500);
            row.HasIndex(x => new { x.SourceKey, x.StartedAtUtc });
        });
    }
}
