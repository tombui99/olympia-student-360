using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;

namespace OlympiaStudent360.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<StudentProfile> StudentProfiles { get; set; }
    public DbSet<StudentBehaviour> StudentBehaviours { get; set; }
    public DbSet<StudentProject> StudentProjects { get; set; }
    public DbSet<StudentParent> StudentParents { get; set; }
    public DbSet<ImportHistory> ImportHistories { get; set; }
    public DbSet<CustomReport> CustomReports { get; set; }
    public DbSet<AcademicPeriod> AcademicPeriods { get; set; }
    public DbSet<DashboardMetric> DashboardMetrics { get; set; }
    public DbSet<DashboardMetricObservation> DashboardMetricObservations { get; set; }
    public DbSet<DashboardPreference> DashboardPreferences { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>()
            .Property(u => u.FullName)
            .HasMaxLength(255);

        modelBuilder.Entity<StudentProfile>()
            .Property(s => s.StudentCode)
            .HasMaxLength(100);

        modelBuilder.Entity<StudentProfile>()
            .Property(s => s.FullName)
            .HasMaxLength(255);

        modelBuilder.Entity<StudentProfile>()
            .Property(s => s.GradeLevel)
            .HasMaxLength(50);

        modelBuilder.Entity<StudentProfile>()
            .Property(s => s.Homeroom)
            .HasMaxLength(100);

        modelBuilder.Entity<StudentProfile>().Property(s => s.Campus).HasMaxLength(100);
        modelBuilder.Entity<StudentProfile>().Property(s => s.EducationLevel).HasMaxLength(100);
        modelBuilder.Entity<StudentProfile>().Property(s => s.Program).HasMaxLength(100);

        modelBuilder.Entity<StudentProfile>()
            .HasIndex(s => s.StudentCode)
            .IsUnique();

        modelBuilder.Entity<StudentProfile>()
            .HasOne(s => s.Teacher)
            .WithMany(u => u.AssignedStudents)
            .HasForeignKey(s => s.TeacherUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<StudentProfile>()
            .HasIndex(s => s.TeacherUserId);

        modelBuilder.Entity<StudentBehaviour>(behaviour =>
        {
            behaviour.Property(b => b.BehaviourType).HasMaxLength(100);
            behaviour.Property(b => b.Notes).HasMaxLength(1000);
            behaviour.Property(b => b.RecordedByName).HasMaxLength(255);
            behaviour.Property(b => b.AcademicYear).HasMaxLength(20);
            behaviour.Property(b => b.Term).HasMaxLength(50);
            behaviour.Property(b => b.MeasurementCycle).HasMaxLength(50);
            behaviour.Property(b => b.Program).HasMaxLength(100);
            behaviour.Property(b => b.Subject).HasMaxLength(100);
            behaviour.HasIndex(b => b.StudentProfileId);
            behaviour.HasIndex(b => b.IncidentDate);
            behaviour.HasOne(b => b.Student)
                .WithMany(s => s.Behaviours)
                .HasForeignKey(b => b.StudentProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StudentProject>(project =>
        {
            project.Property(p => p.ProjectName).HasMaxLength(255);
            project.Property(p => p.Subject).HasMaxLength(100);
            project.Property(p => p.Status).HasMaxLength(50);
            project.Property(p => p.TeacherNotes).HasMaxLength(1000);
            project.Property(p => p.AcademicYear).HasMaxLength(20);
            project.Property(p => p.Term).HasMaxLength(50);
            project.Property(p => p.MeasurementCycle).HasMaxLength(50);
            project.Property(p => p.Program).HasMaxLength(100);
            project.HasIndex(p => p.StudentProfileId);
            project.HasOne(p => p.Student)
                .WithMany(s => s.Projects)
                .HasForeignKey(p => p.StudentProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StudentParent>(parent =>
        {
            parent.Property(p => p.FullName).HasMaxLength(255);
            parent.Property(p => p.Relationship).HasMaxLength(50);
            parent.Property(p => p.PhoneNumber).HasMaxLength(50);
            parent.Property(p => p.Email).HasMaxLength(255);
            parent.Property(p => p.AgeGroup).HasMaxLength(50);
            parent.Property(p => p.Occupation).HasMaxLength(255);
            parent.Property(p => p.EmploymentSector).HasMaxLength(50);
            parent.HasIndex(p => p.StudentProfileId);
            parent.HasOne(p => p.Student)
                .WithMany(s => s.Parents)
                .HasForeignKey(p => p.StudentProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ImportHistory>(history =>
        {
            history.Property(item => item.ModuleId).HasMaxLength(100);
            history.Property(item => item.Source).HasMaxLength(20);
            history.Property(item => item.FileName).HasMaxLength(255);
            history.Property(item => item.Status).HasMaxLength(20);
            history.Property(item => item.ImportedByUserId).HasMaxLength(450);
            history.Property(item => item.ImportedByUsername).HasMaxLength(256);
            history.Property(item => item.ImportedByName).HasMaxLength(255);
            history.HasIndex(item => item.ImportedByUserId);
            history.HasIndex(item => item.CreatedAt);
            history.HasIndex(item => new { item.ModuleId, item.CreatedAt });
        });

        modelBuilder.Entity<CustomReport>()
            .Property(r => r.OwnerUserId)
            .HasMaxLength(450);

        modelBuilder.Entity<CustomReport>()
            .Property(r => r.OwnerUsername)
            .HasMaxLength(256);

        modelBuilder.Entity<CustomReport>()
            .Property(r => r.Title)
            .HasMaxLength(255);

        modelBuilder.Entity<CustomReport>()
            .Property(r => r.Description)
            .HasMaxLength(1000);

        modelBuilder.Entity<CustomReport>()
            .HasIndex(r => r.OwnerUserId);

        modelBuilder.Entity<AcademicPeriod>(period =>
        {
            period.Property(p => p.AcademicYear).HasMaxLength(20);
            period.Property(p => p.Term).HasMaxLength(50);
            period.HasIndex(p => new { p.AcademicYear, p.Term }).IsUnique();
            period.HasIndex(p => p.IsCurrent);
        });

        modelBuilder.Entity<DashboardMetric>(metric =>
        {
            metric.Property(m => m.Key).HasMaxLength(100);
            metric.Property(m => m.Label).HasMaxLength(255);
            metric.Property(m => m.Domain).HasMaxLength(100);
            metric.Property(m => m.Source).HasMaxLength(100);
            metric.Property(m => m.Unit).HasMaxLength(30);
            metric.HasIndex(m => m.Key).IsUnique();
        });

        modelBuilder.Entity<DashboardMetricObservation>(observation =>
        {
            observation.Property(o => o.Value).HasPrecision(18, 4);
            observation.Property(o => o.BaselineValue).HasPrecision(18, 4);
            observation.Property(o => o.KpiTarget).HasPrecision(18, 4);
            observation.Property(o => o.AcademicYear).HasMaxLength(20);
            observation.Property(o => o.Term).HasMaxLength(50);
            observation.Property(o => o.MeasurementCycle).HasMaxLength(50);
            observation.Property(o => o.Campus).HasMaxLength(100);
            observation.Property(o => o.EducationLevel).HasMaxLength(100);
            observation.Property(o => o.GradeLevel).HasMaxLength(50);
            observation.Property(o => o.Homeroom).HasMaxLength(100);
            observation.Property(o => o.Program).HasMaxLength(100);
            observation.Property(o => o.Subject).HasMaxLength(100);
            observation.HasIndex(o => new { o.DashboardMetricId, o.PeriodStart });
            observation.HasIndex(o => new { o.AcademicYear, o.Term, o.MeasurementCycle });
            observation.HasOne(o => o.Metric).WithMany(m => m.Observations)
                .HasForeignKey(o => o.DashboardMetricId).OnDelete(DeleteBehavior.Cascade);
            observation.HasOne(o => o.Student).WithMany()
                .HasForeignKey(o => o.StudentProfileId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<DashboardPreference>(preference =>
        {
            preference.Property(p => p.UserId).HasMaxLength(450);
            preference.Property(p => p.AcademicYear).HasMaxLength(20);
            preference.Property(p => p.Term).HasMaxLength(50);
            preference.Property(p => p.MeasurementCycle).HasMaxLength(50);
            preference.Property(p => p.Campus).HasMaxLength(100);
            preference.Property(p => p.EducationLevel).HasMaxLength(100);
            preference.Property(p => p.GradeLevel).HasMaxLength(50);
            preference.Property(p => p.Homeroom).HasMaxLength(100);
            preference.Property(p => p.Program).HasMaxLength(100);
            preference.Property(p => p.Subject).HasMaxLength(100);
            preference.HasIndex(p => p.UserId).IsUnique();
        });

    }
}
