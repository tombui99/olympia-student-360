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
    public string? TeacherUserId { get; set; }
    public ApplicationUser? Teacher { get; set; }
    public ICollection<StudentBehaviour> Behaviours { get; set; } = [];
    public ICollection<StudentProject> Projects { get; set; } = [];
}

public class StudentBehaviour : BaseEntity
{
    public int StudentProfileId { get; set; }
    public StudentProfile Student { get; set; } = default!;
    public DateTime IncidentDate { get; set; }
    public string BehaviourType { get; set; } = default!;
    public string? Notes { get; set; }
    public string? RecordedByName { get; set; }
}

public class StudentProject : BaseEntity
{
    public int StudentProfileId { get; set; }
    public StudentProfile Student { get; set; } = default!;
    public string ProjectName { get; set; } = default!;
    public string? Subject { get; set; }
    public string Status { get; set; } = default!;
    public string? TeacherNotes { get; set; }
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
