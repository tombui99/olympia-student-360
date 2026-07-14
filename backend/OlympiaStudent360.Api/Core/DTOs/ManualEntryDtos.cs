using System.ComponentModel.DataAnnotations;

namespace OlympiaStudent360.Api.Core.DTOs;

public class ManualEntryStudentDto
{
    public int Id { get; set; }
    public string StudentCode { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
}

public class ManualEntryOptionsDto
{
    public List<ManualEntryStudentDto> Students { get; set; } = [];
    public IReadOnlyList<string> BehaviourTypes { get; set; } = [];
    public IReadOnlyList<string> ProjectSubjects { get; set; } = [];
    public IReadOnlyList<string> ProjectStatuses { get; set; } = [];
    public IReadOnlyList<string> AcademicYears { get; set; } = [];
    public IReadOnlyList<string> HealthIncidentTypes { get; set; } = [];
    public IReadOnlyList<int> HealthSeverityLevels { get; set; } = [];
    public IReadOnlyList<string> HealthBodyAreas { get; set; } = [];
    public IReadOnlyList<string> HealthLocations { get; set; } = [];
}

public class CreateStudentBehaviourDto
{
    [Required]
    public DateTime? IncidentDate { get; set; }

    [Required, MaxLength(100)]
    public string BehaviourType { get; set; } = default!;

    [MaxLength(1000)]
    public string? Notes { get; set; }
    [MaxLength(20)] public string? AcademicYear { get; set; }
    [MaxLength(50)] public string? Term { get; set; }
    [MaxLength(50)] public string? MeasurementCycle { get; set; }
    [MaxLength(100)] public string? Program { get; set; }
    [MaxLength(100)] public string? Subject { get; set; }
}

public class CreateStudentProjectDto
{
    [Required, MaxLength(255)]
    public string ProjectName { get; set; } = default!;

    [Required, MaxLength(100)]
    public string Subject { get; set; } = default!;

    [Required, MaxLength(50)]
    public string Status { get; set; } = default!;

    [MaxLength(1000)]
    public string? TeacherNotes { get; set; }
    [MaxLength(20)] public string? AcademicYear { get; set; }
    [MaxLength(50)] public string? Term { get; set; }
    [MaxLength(50)] public string? MeasurementCycle { get; set; }
    [MaxLength(100)] public string? Program { get; set; }
    public DateTime? ActivityDate { get; set; }
}

public class CreateStudentHealthIncidentDto
{
    [Required] public DateTime? IncidentDate { get; set; }
    [Required, MaxLength(20)] public string AcademicYear { get; set; } = default!;
    [Required, MaxLength(100)] public string IncidentType { get; set; } = default!;
    [Range(1, 3)] public int SeverityLevel { get; set; }
    [MaxLength(100)] public string? BodyArea { get; set; }
    [MaxLength(150)] public string? Location { get; set; }
    [Required, MaxLength(1000)] public string Cause { get; set; } = default!;
    [MaxLength(1000)] public string? ActionTaken { get; set; }
}
