using Microsoft.AspNetCore.Identity;

namespace OlympiaStudent360.Api.Core.Entities;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = default!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public ICollection<StudentProfile> AssignedStudents { get; set; } = [];
}

public abstract class BaseEntity
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}

public class StudentProfile : BaseEntity
{
    public string StudentCode { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
    public string? Campus { get; set; }
    public string? EducationLevel { get; set; }
    public string? Program { get; set; }
    public string? TeacherUserId { get; set; }
    public ApplicationUser? Teacher { get; set; }
    public ICollection<StudentBehaviour> Behaviours { get; set; } = [];
    public ICollection<StudentProject> Projects { get; set; } = [];
    public ICollection<StudentParent> Parents { get; set; } = [];
}

public class StudentParent : BaseEntity
{
    public int StudentProfileId { get; set; }
    public StudentProfile Student { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? Relationship { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Email { get; set; }
    public string? AgeGroup { get; set; }
    public string? Occupation { get; set; }
    public string? EmploymentSector { get; set; }
}

public class StudentBehaviour : BaseEntity
{
    public int StudentProfileId { get; set; }
    public StudentProfile Student { get; set; } = default!;
    public DateTime IncidentDate { get; set; }
    public string BehaviourType { get; set; } = default!;
    public string? Notes { get; set; }
    public string? RecordedByName { get; set; }
    public string? AcademicYear { get; set; }
    public string? Term { get; set; }
    public string? MeasurementCycle { get; set; }
    public string? Program { get; set; }
    public string? Subject { get; set; }
}

public class StudentProject : BaseEntity
{
    public int StudentProfileId { get; set; }
    public StudentProfile Student { get; set; } = default!;
    public string ProjectName { get; set; } = default!;
    public string? Subject { get; set; }
    public string Status { get; set; } = default!;
    public string? TeacherNotes { get; set; }
    public string? AcademicYear { get; set; }
    public string? Term { get; set; }
    public string? MeasurementCycle { get; set; }
    public string? Program { get; set; }
    public DateTime? ActivityDate { get; set; }
}

public class AcademicPeriod : BaseEntity
{
    public string AcademicYear { get; set; } = default!;
    public string Term { get; set; } = default!;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsCurrent { get; set; }
}

public class DashboardMetric : BaseEntity
{
    public string Key { get; set; } = default!;
    public string Label { get; set; } = default!;
    public string Domain { get; set; } = default!;
    public string Source { get; set; } = default!;
    public string Unit { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public ICollection<DashboardMetricObservation> Observations { get; set; } = [];
}

public class DashboardMetricObservation : BaseEntity
{
    public int DashboardMetricId { get; set; }
    public DashboardMetric Metric { get; set; } = default!;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal Value { get; set; }
    public decimal? BaselineValue { get; set; }
    public decimal? KpiTarget { get; set; }
    public string? AcademicYear { get; set; }
    public string? Term { get; set; }
    public string? MeasurementCycle { get; set; }
    public string? Campus { get; set; }
    public string? EducationLevel { get; set; }
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
    public int? StudentProfileId { get; set; }
    public StudentProfile? Student { get; set; }
    public string? Program { get; set; }
    public string? Subject { get; set; }
}

public class DashboardPreference : BaseEntity
{
    public string UserId { get; set; } = default!;
    public string? AcademicYear { get; set; }
    public string? Term { get; set; }
    public string? MeasurementCycle { get; set; }
    public string? Campus { get; set; }
    public string? EducationLevel { get; set; }
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
    public int? StudentProfileId { get; set; }
    public string? Program { get; set; }
    public string? Subject { get; set; }
    public string ChartOrdersJson { get; set; } = "{}";
}

public class ImportHistory : BaseEntity
{
    public string ModuleId { get; set; } = default!;
    public string Source { get; set; } = default!;
    public string FileName { get; set; } = default!;
    public string Status { get; set; } = default!;
    public int ValidRows { get; set; }
    public int InvalidRows { get; set; }
    public string ImportedByUserId { get; set; } = default!;
    public string ImportedByUsername { get; set; } = default!;
    public string ImportedByName { get; set; } = default!;
}

public class CustomReport : BaseEntity
{
    public string OwnerUserId { get; set; } = default!;
    public string OwnerUsername { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string Description { get; set; } = default!;
    public string DefinitionJson { get; set; } = default!;

    public string SharedWithJson { get; set; } = "[]";
}
