namespace OlympiaStudent360.Api.Core.DTOs;

using System.ComponentModel.DataAnnotations;

public class TeacherStudentAssignmentsDto
{
    public List<AssignmentTeacherDto> Teachers { get; set; } = [];
    public List<AssignmentStudentDto> Students { get; set; } = [];
}

public class AssignmentTeacherDto
{
    public string Id { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string? Email { get; set; }
    public string FullName { get; set; } = default!;
    public int AssignedStudentCount { get; set; }
}

public class AssignmentStudentDto
{
    public int Id { get; set; }
    public string StudentCode { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? GradeLevel { get; set; }
    public string? Homeroom { get; set; }
    public string? TeacherId { get; set; }
}

public class SaveTeacherStudentAssignmentsDto
{
    [Required]
    public List<int> StudentIds { get; set; } = [];
}
