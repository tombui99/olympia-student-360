using System.ComponentModel.DataAnnotations;

namespace OlympiaStudent360.Api.Core.DTOs;

public class ManualEntryStudentDto
{
    public int Id { get; set; }
    public string SISUserId { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
}

public class ManualEntryOptionsDto
{
    public IReadOnlyList<string> PositiveBehaviourTypes { get; set; } = [];
    public IReadOnlyList<string> ConcerningBehaviourTypes { get; set; } = [];
    public IReadOnlyList<string> ProjectSubjects { get; set; } = [];
    public IReadOnlyList<string> ProjectStatuses { get; set; } = [];
    public IReadOnlyList<string> AcademicYears { get; set; } = [];
    public IReadOnlyList<string> MentalHealthConcernTypes { get; set; } = [];
    public IReadOnlyList<string> MentalHealthRiskLevels { get; set; } = [];
    public IReadOnlyList<string> BusTripDirections { get; set; } = [];
    public IReadOnlyList<string> BusViolationTypes { get; set; } = [];
}

public class CreateStudentBehaviourDto
{
    [Required]
    public DateTime? IncidentDate { get; set; }

    [Required, MaxLength(255)]
    public string BehaviourType { get; set; } = default!;

    [MaxLength(4000)]
    public string? Notes { get; set; }
    [MaxLength(100)] public string? IncidentTime { get; set; }
    [MaxLength(20)] public string? AcademicYear { get; set; }
    [MaxLength(50)] public string? Term { get; set; }
    [MaxLength(50)] public string? MeasurementCycle { get; set; }
    [MaxLength(100)] public string? Program { get; set; }
    [MaxLength(100)] public string? Subject { get; set; }
    [MaxLength(255)] public string? RecordedByEmail { get; set; }
    [MaxLength(100)] public string? RecordedByTitle { get; set; }
    [MaxLength(255)] public string? Decision { get; set; }
    [MaxLength(50)] public string? ConductDeductionLevel { get; set; }
    [Range(1, int.MaxValue)] public int? OccurrenceCount { get; set; }
    [MaxLength(50)] public string? BehaviourPointsDeducted { get; set; }
    public Dictionary<string, string?>? CustomFields { get; set; }
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
    public Dictionary<string, string?>? CustomFields { get; set; }
}

public class CreateStudentMentalHealthRecordDto
{
    [Required] public DateTime? RecordDate { get; set; }
    [Required, MaxLength(20)] public string AcademicYear { get; set; } = default!;
    [Required, MaxLength(100)] public string ConcernType { get; set; } = default!;
    [Required, MaxLength(20)] public string RiskLevel { get; set; } = default!;
    [MaxLength(1000)] public string? Notes { get; set; }
    public Dictionary<string, string?>? CustomFields { get; set; }
}

public class CreateStudentBusAttendanceRecordDto
{
    [Required] public DateTime? AttendanceDate { get; set; }
    [Required, MaxLength(30)] public string TripDirection { get; set; } = default!;
    [Required, MaxLength(30)] public string ViolationType { get; set; } = default!;
    [MaxLength(100)] public string? BusCode { get; set; }
    [MaxLength(255)] public string? RouteName { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    [MaxLength(20)] public string? AcademicYear { get; set; }
    [MaxLength(50)] public string? Term { get; set; }
    public Dictionary<string, string?>? CustomFields { get; set; }
}

public class CreateStudentHealthVisitDto
{
    [Required] public DateTime? VisitDate { get; set; }
    [Required, MaxLength(1000)] public string Symptoms { get; set; } = default!;
    [MaxLength(1000)] public string? Diagnosis { get; set; }
    [MaxLength(100)] public string? Classification { get; set; }
    [MaxLength(1000)] public string? Treatment { get; set; }
    [MaxLength(500)] public string? Medication { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
    public bool NotificationSent { get; set; }
    [MaxLength(255)] public string? ExaminedBy { get; set; }
    public Dictionary<string, string?>? CustomFields { get; set; }
}
