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
    }
}
