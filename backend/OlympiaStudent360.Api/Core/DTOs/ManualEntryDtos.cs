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
}

public class CreateStudentBehaviourDto
{
    [Required]
    public DateTime? IncidentDate { get; set; }

    [Required, MaxLength(100)]
    public string BehaviourType { get; set; } = default!;

    [MaxLength(1000)]
    public string? Notes { get; set; }
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
}
