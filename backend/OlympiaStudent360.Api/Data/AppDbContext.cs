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
    public DbSet<SISClass> SISClasses { get; set; }
    public DbSet<SISClassTeacher> SISClassTeachers { get; set; }
    public DbSet<SISClassStudent> SISClassStudents { get; set; }
    public DbSet<StudentBehaviour> StudentBehaviours { get; set; }
    public DbSet<StudentProject> StudentProjects { get; set; }
    public DbSet<SISStudentParent> SISStudentParents { get; set; }
    public DbSet<StudentMentalHealthRecord> StudentMentalHealthRecords { get; set; }
    public DbSet<SISHealthVisit> SISHealthVisits { get; set; }
    public DbSet<StudentBusAttendanceRecord> StudentBusAttendanceRecords { get; set; }
    public DbSet<ImportHistory> ImportHistories { get; set; }
    public DbSet<ImportModuleField> ImportModuleFields { get; set; }
    public DbSet<CustomReport> CustomReports { get; set; }
    public DbSet<WorkspacePreference> WorkspacePreferences { get; set; }
    public DbSet<AcademicPeriod> AcademicPeriods { get; set; }
    public DbSet<DashboardMetric> DashboardMetrics { get; set; }
    public DbSet<DashboardMetricObservation> DashboardMetricObservations { get; set; }
    public DbSet<DashboardPreference> DashboardPreferences { get; set; }
    public DbSet<CanvasCourse> CanvasCourses { get; set; }
    public DbSet<CanvasAssignment> CanvasAssignments { get; set; }
    public DbSet<CanvasEnrollment> CanvasEnrollments { get; set; }
    public DbSet<CanvasCourseScore> CanvasCourseScores { get; set; }
    public DbSet<CanvasSubmission> CanvasSubmissions { get; set; }
    public DbSet<StudentClassificationSetting> StudentClassificationSettings { get; set; }

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
            .Property(s => s.Gender)
            .HasMaxLength(50);

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

        modelBuilder.Entity<StudentClassificationSetting>(setting =>
        {
            setting.Property(item => item.HighRiskCanvasAverageBelow).HasPrecision(5, 2);
            setting.Property(item => item.CommendationCanvasAverageAtLeast).HasPrecision(5, 2);
            setting.Property(item => item.UpdatedByUserId).HasMaxLength(450);
        });

        modelBuilder.Entity<SISClass>(sisClass =>
        {
            sisClass.ToTable("SISClasses");
            sisClass.Property(item => item.SISClassCode).HasMaxLength(100);
            sisClass.Property(item => item.ClassName).HasMaxLength(255);
            sisClass.Property(item => item.AcademicYear).HasMaxLength(20);
            sisClass.Property(item => item.Term).HasMaxLength(50);
            sisClass.Property(item => item.GradeLevel).HasMaxLength(50);
            sisClass.Property(item => item.Homeroom).HasMaxLength(100);
            sisClass.Property(item => item.Campus).HasMaxLength(100);
            sisClass.HasIndex(item => item.SISClassCode).IsUnique();
            sisClass.HasIndex(item => new { item.AcademicYear, item.IsActive });
        });

        modelBuilder.Entity<SISClassTeacher>(assignment =>
        {
            assignment.ToTable("SISClassTeachers");
            assignment.Property(item => item.TeacherUserId).HasMaxLength(255);
            assignment.Property(item => item.AssignmentRole).HasMaxLength(100);
            assignment.HasIndex(item => new { item.SISClassId, item.TeacherUserId }).IsUnique();
            assignment.HasIndex(item => item.TeacherUserId);
            assignment.HasOne(item => item.SISClass).WithMany(item => item.Teachers)
                .HasForeignKey(item => item.SISClassId).OnDelete(DeleteBehavior.Cascade);
            assignment.HasOne(item => item.Teacher).WithMany(item => item.SISClassAssignments)
                .HasForeignKey(item => item.TeacherUserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SISClassStudent>(membership =>
        {
            membership.ToTable("SISClassStudents");
            membership.HasIndex(item => new { item.SISClassId, item.StudentProfileId }).IsUnique();
            membership.HasIndex(item => item.StudentProfileId);
            membership.HasOne(item => item.SISClass).WithMany(item => item.Students)
                .HasForeignKey(item => item.SISClassId).OnDelete(DeleteBehavior.Cascade);
            membership.HasOne(item => item.Student).WithMany(item => item.SISClassMemberships)
                .HasForeignKey(item => item.StudentProfileId).OnDelete(DeleteBehavior.Cascade);
        });

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
            behaviour.HasIndex(b => b.ImportHistoryId);
            behaviour.HasOne(b => b.Student)
                .WithMany(s => s.Behaviours)
                .HasForeignKey(b => b.StudentProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            behaviour.HasOne(b => b.ImportHistory)
                .WithMany(h => h.Behaviours)
                .HasForeignKey(b => b.ImportHistoryId)
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
            project.HasIndex(p => p.ImportHistoryId);
            project.HasOne(p => p.Student)
                .WithMany(s => s.Projects)
                .HasForeignKey(p => p.StudentProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            project.HasOne(p => p.ImportHistory)
                .WithMany(h => h.Projects)
                .HasForeignKey(p => p.ImportHistoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SISStudentParent>(parent =>
        {
            parent.ToTable("SISStudentParents");
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

        modelBuilder.Entity<StudentMentalHealthRecord>(record =>
        {
            record.Property(r => r.AcademicYear).HasMaxLength(20);
            record.Property(r => r.ConcernType).HasMaxLength(100);
            record.Property(r => r.RiskLevel).HasMaxLength(20);
            record.Property(r => r.Notes).HasMaxLength(1000);
            record.Property(r => r.RecordedByUserId).HasMaxLength(450);
            record.Property(r => r.RecordedByName).HasMaxLength(255);
            record.HasIndex(r => r.StudentProfileId);
            record.HasIndex(r => r.ImportHistoryId);
            record.HasIndex(r => r.RecordDate);
            record.HasIndex(r => new { r.AcademicYear, r.RiskLevel });
            record.HasOne(r => r.Student)
                .WithMany(s => s.MentalHealthRecords)
                .HasForeignKey(r => r.StudentProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            record.HasOne(r => r.ImportHistory)
                .WithMany(h => h.MentalHealthRecords)
                .HasForeignKey(r => r.ImportHistoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SISHealthVisit>(visit =>
        {
            visit.ToTable("SISHealthVisits");
            visit.Property(v => v.FullName).HasMaxLength(255);
            visit.Property(v => v.Gender).HasMaxLength(20);
            visit.Property(v => v.ClassName).HasMaxLength(50);
            visit.Property(v => v.Symptoms).HasMaxLength(1000);
            visit.Property(v => v.Diagnosis).HasMaxLength(1000);
            visit.Property(v => v.Classification).HasMaxLength(100);
            visit.Property(v => v.Treatment).HasMaxLength(1000);
            visit.Property(v => v.Medication).HasMaxLength(500);
            visit.Property(v => v.ExaminedBy).HasMaxLength(255);
            visit.HasIndex(v => v.StudentProfileId);
            visit.HasIndex(v => v.ExamTime);
            visit.HasOne(v => v.Student)
                .WithMany(s => s.SISHealthVisits)
                .HasForeignKey(v => v.StudentProfileId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<StudentBusAttendanceRecord>(record =>
        {
            record.Property(r => r.TripDirection).HasMaxLength(30);
            record.Property(r => r.ViolationType).HasMaxLength(30);
            record.Property(r => r.BusCode).HasMaxLength(100);
            record.Property(r => r.RouteName).HasMaxLength(255);
            record.Property(r => r.Notes).HasMaxLength(1000);
            record.Property(r => r.RecordedByName).HasMaxLength(255);
            record.Property(r => r.AcademicYear).HasMaxLength(20);
            record.Property(r => r.Term).HasMaxLength(50);
            record.HasIndex(r => r.StudentProfileId);
            record.HasIndex(r => r.ImportHistoryId);
            record.HasIndex(r => r.AttendanceDate);
            record.HasIndex(r => new { r.ViolationType, r.TripDirection, r.AttendanceDate });
            record.HasOne(r => r.Student)
                .WithMany(s => s.BusAttendanceRecords)
                .HasForeignKey(r => r.StudentProfileId)
                .OnDelete(DeleteBehavior.Cascade);
            record.HasOne(r => r.ImportHistory)
                .WithMany(h => h.BusAttendanceRecords)
                .HasForeignKey(r => r.ImportHistoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ImportHistory>(history =>
        {
            history.Property(item => item.ModuleId).HasMaxLength(100);
            history.Property(item => item.FileName).HasMaxLength(255);
            history.Property(item => item.Status).HasMaxLength(20);
            history.Property(item => item.ImportedByUserId).HasMaxLength(450);
            history.Property(item => item.ImportedByUsername).HasMaxLength(256);
            history.Property(item => item.ImportedByName).HasMaxLength(255);
            history.HasIndex(item => item.ImportedByUserId);
            history.HasIndex(item => item.CreatedAt);
            history.HasIndex(item => new { item.ModuleId, item.CreatedAt });
        });

        modelBuilder.Entity<ImportModuleField>(field =>
        {
            field.Property(f => f.ModuleId).HasMaxLength(100);
            field.Property(f => f.FieldKey).HasMaxLength(100);
            field.Property(f => f.Label).HasMaxLength(150);
            field.Property(f => f.FieldType).HasMaxLength(20);
            field.HasIndex(f => new { f.ModuleId, f.FieldKey }).IsUnique();
            field.HasIndex(f => f.ModuleId);
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

        modelBuilder.Entity<WorkspacePreference>(preference =>
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
            preference.HasIndex(p => new { p.CustomReportId, p.UserId }).IsUnique();
            preference.HasOne(p => p.CustomReport).WithMany(r => r.Preferences)
                .HasForeignKey(p => p.CustomReportId).OnDelete(DeleteBehavior.Cascade);
        });

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

        modelBuilder.Entity<CanvasCourse>(course =>
        {
            course.Property(c => c.SisSourceId).HasMaxLength(255);
            course.Property(c => c.Name).HasMaxLength(255);
            course.Property(c => c.CourseCode).HasMaxLength(100);
            course.Property(c => c.WorkflowState).HasMaxLength(50);
            course.Property(c => c.TermName).HasMaxLength(100);
            course.Property(c => c.Subject).HasMaxLength(100);
            course.Property(c => c.AcademicYear).HasMaxLength(20);
            course.Property(c => c.Term).HasMaxLength(50);
            course.HasIndex(c => c.CanvasCourseId).IsUnique();
            course.HasIndex(c => c.EnrollmentTermId);
        });

        modelBuilder.Entity<CanvasAssignment>(assignment =>
        {
            assignment.Property(a => a.Title).HasMaxLength(500);
            assignment.Property(a => a.WorkflowState).HasMaxLength(50);
            assignment.Property(a => a.AssignmentGroupName).HasMaxLength(255);
            assignment.Property(a => a.GradingType).HasMaxLength(50);
            assignment.HasIndex(a => a.CanvasAssignmentId).IsUnique();
            assignment.HasIndex(a => a.CanvasCourseId);
            assignment.HasOne(a => a.Course).WithMany(c => c.Assignments)
                .HasForeignKey(a => a.CanvasCourseId)
                .HasPrincipalKey(c => c.CanvasCourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CanvasEnrollment>(enrollment =>
        {
            enrollment.Property(e => e.Type).HasMaxLength(50);
            enrollment.Property(e => e.WorkflowState).HasMaxLength(50);
            enrollment.Property(e => e.SisUserId).HasMaxLength(100);
            enrollment.HasIndex(e => e.CanvasEnrollmentId).IsUnique();
            enrollment.HasIndex(e => e.CanvasUserId);
            enrollment.HasIndex(e => e.StudentProfileId);
            enrollment.HasOne(e => e.Course).WithMany(c => c.Enrollments)
                .HasForeignKey(e => e.CanvasCourseId)
                .HasPrincipalKey(c => c.CanvasCourseId)
                .OnDelete(DeleteBehavior.Cascade);
            enrollment.HasOne(e => e.Student).WithMany()
                .HasForeignKey(e => e.StudentProfileId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CanvasCourseScore>(score =>
        {
            score.Property(s => s.GradingPeriodTitle).HasMaxLength(100);
            score.Property(s => s.CourseName).HasMaxLength(255);
            score.Property(s => s.Subject).HasMaxLength(100);
            score.Property(s => s.AcademicYear).HasMaxLength(20);
            score.Property(s => s.Term).HasMaxLength(50);
            score.HasIndex(s => s.CanvasScoreId).IsUnique();
            score.HasIndex(s => s.StudentProfileId);
            score.HasIndex(s => new { s.AcademicYear, s.Term });
            score.HasOne(s => s.Enrollment).WithMany(e => e.Scores)
                .HasForeignKey(s => s.CanvasEnrollmentId)
                .HasPrincipalKey(e => e.CanvasEnrollmentId)
                .OnDelete(DeleteBehavior.Cascade);
            score.HasOne(s => s.Student).WithMany()
                .HasForeignKey(s => s.StudentProfileId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CanvasSubmission>(submission =>
        {
            submission.Property(s => s.Grade).HasMaxLength(50);
            submission.Property(s => s.WorkflowState).HasMaxLength(50);
            submission.Property(s => s.SubmissionType).HasMaxLength(50);
            submission.Property(s => s.SisUserId).HasMaxLength(100);
            submission.Property(s => s.Subject).HasMaxLength(100);
            submission.Property(s => s.AcademicYear).HasMaxLength(20);
            submission.Property(s => s.Term).HasMaxLength(50);
            submission.HasIndex(s => s.CanvasSubmissionId).IsUnique();
            submission.HasIndex(s => s.CanvasAssignmentId);
            submission.HasIndex(s => s.StudentProfileId);
            submission.HasIndex(s => new { s.AcademicYear, s.Term });
            submission.HasOne(s => s.Assignment).WithMany(a => a.Submissions)
                .HasForeignKey(s => s.CanvasAssignmentId)
                .HasPrincipalKey(a => a.CanvasAssignmentId)
                .OnDelete(DeleteBehavior.Cascade);
            submission.HasOne(s => s.Student).WithMany()
                .HasForeignKey(s => s.StudentProfileId)
                .OnDelete(DeleteBehavior.SetNull);
        });

    }
}
