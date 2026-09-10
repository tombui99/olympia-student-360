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
    public DbSet<SISStudentParent> SISStudentParents { get; set; }
    public DbSet<StudentMentalHealthRecord> StudentMentalHealthRecords { get; set; }
    public DbSet<SISHealthVisit> SISHealthVisits { get; set; }
    public DbSet<StudentBusAttendanceRecord> StudentBusAttendanceRecords { get; set; }
    public DbSet<ImportHistory> ImportHistories { get; set; }
    public DbSet<ImportModuleField> ImportModuleFields { get; set; }
    public DbSet<CustomForm> CustomForms { get; set; }
    public DbSet<CustomFormResponse> CustomFormResponses { get; set; }
    public DbSet<CustomFormAccessSession> CustomFormAccessSessions { get; set; }
    public DbSet<CustomReport> CustomReports { get; set; }
    public DbSet<WorkspacePreference> WorkspacePreferences { get; set; }
    public DbSet<AcademicPeriod> AcademicPeriods { get; set; }
    public DbSet<DashboardMetric> DashboardMetrics { get; set; }
    public DbSet<DashboardMetricObservation> DashboardMetricObservations { get; set; }
    public DbSet<DashboardPreference> DashboardPreferences { get; set; }
    public DbSet<CanvasAccount> CanvasAccounts { get; set; }
    public DbSet<CanvasUser> CanvasUsers { get; set; }
    public DbSet<CanvasCourse> CanvasCourses { get; set; }
    public DbSet<CanvasCourseSection> CanvasCourseSections { get; set; }
    public DbSet<CanvasAssignment> CanvasAssignments { get; set; }
    public DbSet<CanvasEnrollment> CanvasEnrollments { get; set; }
    public DbSet<CanvasCourseScore> CanvasCourseScores { get; set; }
    public DbSet<CanvasSubmission> CanvasSubmissions { get; set; }
    public DbSet<CanvasSyncState> CanvasSyncStates { get; set; }
    public DbSet<CanvasSyncRun> CanvasSyncRuns { get; set; }
    public DbSet<StudentClassificationSetting> StudentClassificationSettings { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ApplicationUser>()
            .Property(u => u.FullName)
            .HasMaxLength(255);

        modelBuilder.Entity<CustomForm>(form =>
        {
            form.Property(item => item.PublicId).HasMaxLength(64);
            form.Property(item => item.Title).HasMaxLength(255);
            form.Property(item => item.Description).HasMaxLength(4000);
            form.Property(item => item.ConfirmationMessage).HasMaxLength(2000);
            form.Property(item => item.CreatedByUserId).HasMaxLength(450);
            form.Property(item => item.CreatedByName).HasMaxLength(255);
            form.HasIndex(item => item.PublicId).IsUnique();
            form.HasIndex(item => item.IsArchived);
        });

        modelBuilder.Entity<CustomFormResponse>(response =>
        {
            response.Property(item => item.RespondentName).HasMaxLength(255);
            response.Property(item => item.RespondentRelationship).HasMaxLength(100);
            response.Property(item => item.RespondentEmail).HasMaxLength(255);
            response.Property(item => item.RespondentPhone).HasMaxLength(50);
            response.Property(item => item.DeletedByUserId).HasMaxLength(450);
            response.HasIndex(item => new { item.CustomFormId, item.StudentProfileId });
            response.HasIndex(item => new { item.CustomFormId, item.IsDeleted, item.CreatedAt });
            response.HasOne(item => item.Form).WithMany(item => item.Responses)
                .HasForeignKey(item => item.CustomFormId).OnDelete(DeleteBehavior.Cascade);
            response.HasOne(item => item.Student).WithMany()
                .HasForeignKey(item => item.StudentProfileId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CustomFormAccessSession>(session =>
        {
            session.Property(item => item.TokenHash).HasMaxLength(64);
            session.HasIndex(item => item.TokenHash).IsUnique();
            session.HasIndex(item => item.ExpiresAt);
            session.HasOne(item => item.Form).WithMany(item => item.AccessSessions)
                .HasForeignKey(item => item.CustomFormId).OnDelete(DeleteBehavior.Cascade);
            session.HasOne(item => item.Student).WithMany()
                .HasForeignKey(item => item.StudentProfileId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<StudentProfile>()
            .Property(s => s.SISUserId)
            .HasMaxLength(100);

        modelBuilder.Entity<StudentProfile>()
            .Property(s => s.FullName)
            .HasMaxLength(255);

        modelBuilder.Entity<StudentProfile>()
            .Property(s => s.Email)
            .HasMaxLength(255);

        modelBuilder.Entity<StudentProfile>()
            .Property(s => s.PhoneNumber)
            .HasMaxLength(50);

        modelBuilder.Entity<StudentProfile>()
            .Property(s => s.Gender)
            .HasMaxLength(50);

        modelBuilder.Entity<StudentProfile>()
            .Property(s => s.CanvasGradeLevel)
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
            .HasAlternateKey(s => s.SISUserId);

        modelBuilder.Entity<StudentProfile>()
            .HasIndex(s => s.CanvasUserId)
            .IsUnique();

        modelBuilder.Entity<StudentClassificationSetting>(setting =>
        {
            setting.Property(item => item.HighRiskCanvasAverageBelow).HasPrecision(5, 2);
            setting.Property(item => item.CommendationCanvasAverageAtLeast).HasPrecision(5, 2);
            setting.Property(item => item.UpdatedByUserId).HasMaxLength(450);
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
            parent.Property(p => p.SISUserId).HasMaxLength(100);
            parent.HasIndex(p => p.SISUserId);
            parent.HasOne(p => p.Student)
                .WithMany(s => s.Parents)
                .HasForeignKey(p => p.SISUserId)
                .HasPrincipalKey(s => s.SISUserId)
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
            visit.Property(v => v.SISUserId).HasMaxLength(100);
            visit.HasIndex(v => v.SISUserId);
            visit.HasIndex(v => v.ExamTime);
            visit.HasOne(v => v.Student)
                .WithMany(s => s.SISHealthVisits)
                .HasForeignKey(v => v.SISUserId)
                .HasPrincipalKey(s => s.SISUserId)
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
            course.Property(c => c.CreatedAt).HasColumnName("WarehouseCreatedAt");
            course.Property(c => c.UpdatedAt).HasColumnName("WarehouseUpdatedAt");
            course.Property(c => c.SisCourseId).HasMaxLength(255);
            course.Property(c => c.Name).HasMaxLength(255);
            course.Property(c => c.CourseCode).HasMaxLength(100);
            course.Property(c => c.WorkflowState).HasMaxLength(50);
            course.Property(c => c.CanvasGradeLevel).HasMaxLength(50);
            course.Property(c => c.EnrollmentTermName).HasMaxLength(100);
            course.HasIndex(c => c.CanvasCourseId).IsUnique();
            course.HasIndex(c => c.CanvasAccountId);
            course.HasIndex(c => c.CanvasGradeLevel);
            course.HasIndex(c => c.EnrollmentTermId);
        });

        modelBuilder.Entity<CanvasAccount>(account =>
        {
            account.Property(item => item.CreatedAt).HasColumnName("WarehouseCreatedAt");
            account.Property(item => item.UpdatedAt).HasColumnName("WarehouseUpdatedAt");
            account.Property(item => item.SisAccountId).HasMaxLength(255);
            account.Property(item => item.Name).HasMaxLength(255);
            account.Property(item => item.WorkflowState).HasMaxLength(50);
            account.HasIndex(item => item.CanvasAccountId).IsUnique();
            account.HasIndex(item => item.ParentAccountId);
            account.HasIndex(item => item.SisAccountId);
        });

        modelBuilder.Entity<CanvasUser>(user =>
        {
            user.Property(item => item.CreatedAt).HasColumnName("WarehouseCreatedAt");
            user.Property(item => item.UpdatedAt).HasColumnName("WarehouseUpdatedAt");
            user.Property(item => item.SisUserId).HasMaxLength(100);
            user.Property(item => item.IntegrationId).HasMaxLength(255);
            user.Property(item => item.LoginId).HasMaxLength(255);
            user.Property(item => item.Name).HasMaxLength(255);
            user.Property(item => item.ShortName).HasMaxLength(255);
            user.Property(item => item.SortableName).HasMaxLength(255);
            user.Property(item => item.Email).HasMaxLength(255);
            user.HasIndex(item => item.CanvasUserId).IsUnique();
            user.HasIndex(item => item.SisUserId);
        });

        modelBuilder.Entity<CanvasCourseSection>(section =>
        {
            section.Property(item => item.CreatedAt).HasColumnName("WarehouseCreatedAt");
            section.Property(item => item.UpdatedAt).HasColumnName("WarehouseUpdatedAt");
            section.Property(item => item.SisSectionId).HasMaxLength(255);
            section.Property(item => item.SisCourseId).HasMaxLength(255);
            section.Property(item => item.IntegrationId).HasMaxLength(255);
            section.Property(item => item.Name).HasMaxLength(255);
            section.HasIndex(item => item.CanvasSectionId).IsUnique();
            section.HasIndex(item => item.CanvasCourseId);
            section.HasIndex(item => item.SisSectionId);
            section.HasOne(item => item.Course).WithMany(item => item.Sections)
                .HasForeignKey(item => item.CanvasCourseId)
                .HasPrincipalKey(item => item.CanvasCourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CanvasAssignment>(assignment =>
        {
            assignment.Property(a => a.CreatedAt).HasColumnName("WarehouseCreatedAt");
            assignment.Property(a => a.UpdatedAt).HasColumnName("WarehouseUpdatedAt");
            assignment.Property(a => a.Name).HasMaxLength(500);
            assignment.Property(a => a.WorkflowState).HasMaxLength(50);
            assignment.Property(a => a.AssignmentGroupName).HasMaxLength(255);
            assignment.Property(a => a.GradingType).HasMaxLength(50);
            assignment.HasIndex(a => a.CanvasAssignmentId).IsUnique();
            assignment.HasIndex(a => a.CanvasCourseId);
            assignment.HasIndex(a => a.AssignmentGroupId);
            assignment.HasOne(a => a.Course).WithMany(c => c.Assignments)
                .HasForeignKey(a => a.CanvasCourseId)
                .HasPrincipalKey(c => c.CanvasCourseId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CanvasEnrollment>(enrollment =>
        {
            enrollment.Property(e => e.CreatedAt).HasColumnName("WarehouseCreatedAt");
            enrollment.Property(e => e.UpdatedAt).HasColumnName("WarehouseUpdatedAt");
            enrollment.Property(e => e.Type).HasMaxLength(50);
            enrollment.Property(e => e.Role).HasMaxLength(100);
            enrollment.Property(e => e.EnrollmentState).HasMaxLength(50);
            enrollment.Property(e => e.SisUserId).HasMaxLength(100);
            enrollment.Property(e => e.SisSectionId).HasMaxLength(255);
            enrollment.Property(e => e.SisCourseId).HasMaxLength(255);
            enrollment.Property(e => e.SisAccountId).HasMaxLength(255);
            enrollment.HasIndex(e => e.CanvasEnrollmentId).IsUnique();
            enrollment.HasIndex(e => e.CanvasUserId);
            enrollment.HasIndex(e => e.CanvasCourseSectionId);
            enrollment.HasIndex(e => e.StudentProfileId);
            enrollment.HasOne(e => e.Course).WithMany(c => c.Enrollments)
                .HasForeignKey(e => e.CanvasCourseId)
                .HasPrincipalKey(c => c.CanvasCourseId)
                .OnDelete(DeleteBehavior.Cascade);
            enrollment.HasOne(e => e.Section).WithMany()
                .HasForeignKey(e => e.CanvasCourseSectionId)
                .HasPrincipalKey(e => e.CanvasSectionId)
                .OnDelete(DeleteBehavior.SetNull);
            enrollment.HasOne(e => e.Student).WithMany()
                .HasForeignKey(e => e.StudentProfileId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CanvasCourseScore>(score =>
        {
            score.Property(s => s.CreatedAt).HasColumnName("WarehouseCreatedAt");
            score.Property(s => s.UpdatedAt).HasColumnName("WarehouseUpdatedAt");
            score.Property(s => s.GradingPeriodTitle).HasMaxLength(100);
            score.Property(s => s.CurrentGrade).HasMaxLength(50);
            score.Property(s => s.FinalGrade).HasMaxLength(50);
            score.Property(s => s.UnpostedCurrentGrade).HasMaxLength(50);
            score.Property(s => s.UnpostedFinalGrade).HasMaxLength(50);
            score.HasIndex(s => s.WarehouseScoreFactId).IsUnique();
            score.HasIndex(s => s.StudentProfileId);
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
            submission.Property(s => s.CreatedAt).HasColumnName("WarehouseCreatedAt");
            submission.Property(s => s.UpdatedAt).HasColumnName("WarehouseUpdatedAt");
            submission.Property(s => s.Grade).HasMaxLength(50);
            submission.Property(s => s.WorkflowState).HasMaxLength(50);
            submission.Property(s => s.SubmissionType).HasMaxLength(50);
            submission.Property(s => s.LatePolicyStatus).HasMaxLength(50);
            submission.HasIndex(s => s.CanvasSubmissionId).IsUnique();
            submission.HasIndex(s => s.CanvasAssignmentId);
            submission.HasIndex(s => s.StudentProfileId);
            submission.HasOne(s => s.Assignment).WithMany(a => a.Submissions)
                .HasForeignKey(s => s.CanvasAssignmentId)
                .HasPrincipalKey(a => a.CanvasAssignmentId)
                .OnDelete(DeleteBehavior.Cascade);
            submission.HasOne(s => s.Student).WithMany()
                .HasForeignKey(s => s.StudentProfileId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<CanvasSyncState>(state =>
        {
            state.Property(item => item.CreatedAt).HasColumnName("WarehouseCreatedAt");
            state.Property(item => item.UpdatedAt).HasColumnName("WarehouseUpdatedAt");
            state.Property(item => item.SourceKey).HasMaxLength(255);
            state.Property(item => item.LastRunMode).HasMaxLength(50);
            state.Property(item => item.LastStatus).HasMaxLength(50);
            state.Property(item => item.LastError).HasMaxLength(2000);
            state.HasIndex(item => item.SourceKey).IsUnique();
        });

        modelBuilder.Entity<CanvasSyncRun>(run =>
        {
            run.Property(item => item.CreatedAt).HasColumnName("WarehouseCreatedAt");
            run.Property(item => item.UpdatedAt).HasColumnName("WarehouseUpdatedAt");
            run.Property(item => item.SourceKey).HasMaxLength(255);
            run.Property(item => item.Source).HasMaxLength(255);
            run.Property(item => item.Workflow).HasMaxLength(50);
            run.Property(item => item.Status).HasMaxLength(50);
            run.Property(item => item.Error).HasMaxLength(2000);
            run.HasIndex(item => item.StartedAtUtc);
            run.HasIndex(item => new { item.SourceKey, item.StartedAtUtc });
        });

    }
}
