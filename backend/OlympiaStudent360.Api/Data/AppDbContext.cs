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
    public DbSet<ImportHistory> ImportHistories { get; set; }
    public DbSet<CustomReport> CustomReports { get; set; }

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
            project.HasIndex(p => p.StudentProfileId);
            project.HasOne(p => p.Student)
                .WithMany(s => s.Projects)
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

    }
}
