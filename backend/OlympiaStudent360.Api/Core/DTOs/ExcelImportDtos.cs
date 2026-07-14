using System.ComponentModel.DataAnnotations;

namespace OlympiaStudent360.Api.Core.DTOs;

public class ExcelBehaviourImportDto
{
    [Required, MaxLength(255)]
    public string FileName { get; set; } = default!;

    [Required, MinLength(1)]
    public List<ExcelBehaviourRowDto> Rows { get; set; } = [];
}

public class ExcelBehaviourRowDto
{
    [Required, MaxLength(100)]
    public string StudentId { get; set; } = default!;

    [Required, MaxLength(255)]
    public string StudentName { get; set; } = default!;

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

public class ExcelProjectImportDto
{
    [Required, MaxLength(255)]
    public string FileName { get; set; } = default!;

    [Required, MinLength(1)]
    public List<ExcelProjectRowDto> Rows { get; set; } = [];
}

public class ExcelProjectRowDto
{
    [Required, MaxLength(100)]
    public string StudentId { get; set; } = default!;

    [Required, MaxLength(255)]
    public string StudentName { get; set; } = default!;

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

public class ExcelHealthIncidentImportDto
{
    [Required, MaxLength(255)]
    public string FileName { get; set; } = default!;

    [Required, MinLength(1)]
    public List<ExcelHealthIncidentRowDto> Rows { get; set; } = [];
}

public class ExcelHealthIncidentRowDto
{
    [Required, MaxLength(100)] public string StudentId { get; set; } = default!;
    [Required, MaxLength(255)] public string StudentName { get; set; } = default!;
    [Required] public DateTime? IncidentDate { get; set; }
    [Required, MaxLength(20)] public string AcademicYear { get; set; } = default!;
    [Required, MaxLength(100)] public string IncidentType { get; set; } = default!;
    [Range(1, 3)] public int SeverityLevel { get; set; }
    [MaxLength(100)] public string? BodyArea { get; set; }
    [MaxLength(150)] public string? Location { get; set; }
    [Required, MaxLength(1000)] public string Cause { get; set; } = default!;
    [MaxLength(1000)] public string? ActionTaken { get; set; }
}
