using System.ComponentModel.DataAnnotations;

namespace OlympiaStudent360.Api.Core.DTOs;

public class ExcelBehaviourImportDto
{
    [Required, MaxLength(255)]
    public string FileName { get; set; } = default!;

    [Range(2, 3)]
    public int DataStartRow { get; set; } = 2;

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
    public Dictionary<string, string?>? CustomFields { get; set; }
}

public class ExcelProjectImportDto
{
    [Required, MaxLength(255)]
    public string FileName { get; set; } = default!;

    [Range(2, 3)]
    public int DataStartRow { get; set; } = 2;

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
    public Dictionary<string, string?>? CustomFields { get; set; }
}

public class ExcelMentalHealthImportDto
{
    [Required, MaxLength(255)]
    public string FileName { get; set; } = default!;

    [Range(2, 3)]
    public int DataStartRow { get; set; } = 2;

    [Required, MinLength(1)]
    public List<ExcelMentalHealthRowDto> Rows { get; set; } = [];
}

public class ExcelMentalHealthRowDto
{
    [Required, MaxLength(100)] public string StudentId { get; set; } = default!;
    [Required, MaxLength(255)] public string StudentName { get; set; } = default!;
    [Required] public DateTime? RecordDate { get; set; }
    [Required, MaxLength(20)] public string AcademicYear { get; set; } = default!;
    [Required, MaxLength(100)] public string ConcernType { get; set; } = default!;
    [Required, MaxLength(20)] public string RiskLevel { get; set; } = default!;
    [MaxLength(1000)] public string? Notes { get; set; }
    public Dictionary<string, string?>? CustomFields { get; set; }
}

public class ExcelBusAttendanceImportDto
{
    [Required, MaxLength(255)] public string FileName { get; set; } = default!;
    [Range(2, 3)] public int DataStartRow { get; set; } = 2;
    [Required, MinLength(1)] public List<ExcelBusAttendanceRowDto> Rows { get; set; } = [];
}

public class ExcelBusAttendanceRowDto
{
    [Required, MaxLength(100)] public string StudentId { get; set; } = default!;
    [Required, MaxLength(255)] public string StudentName { get; set; } = default!;
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

public class ExcelHealthVisitImportDto
{
    [Required, MaxLength(255)] public string FileName { get; set; } = default!;
    [Range(2, 3)] public int DataStartRow { get; set; } = 2;
    [Required, MinLength(1)] public List<ExcelHealthVisitRowDto> Rows { get; set; } = [];
}

public class ExcelHealthVisitRowDto
{
    [Required, MaxLength(100)] public string StudentId { get; set; } = default!;
    [Required, MaxLength(255)] public string StudentName { get; set; } = default!;
    [Required] public DateTime? VisitDate { get; set; }
    [Required, MaxLength(1000)] public string Symptoms { get; set; } = default!;
    [MaxLength(1000)] public string? Diagnosis { get; set; }
    [MaxLength(100)] public string? Classification { get; set; }
    [MaxLength(1000)] public string? Treatment { get; set; }
    [MaxLength(500)] public string? Medication { get; set; }
    [MaxLength(255)] public string? ExaminedBy { get; set; }
    public Dictionary<string, string?>? CustomFields { get; set; }
}
