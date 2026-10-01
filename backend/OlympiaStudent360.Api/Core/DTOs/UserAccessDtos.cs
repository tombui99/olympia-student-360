using System.ComponentModel.DataAnnotations;

namespace OlympiaStudent360.Api.Core.DTOs;

public class UserAccessCapabilitiesDto
{
    public bool IsTeacher { get; set; }
    public bool HasAll { get; set; }
    public bool RestrictsClasses { get; set; }
    public bool RestrictsStudents { get; set; }
    public List<string> Scopes { get; set; } = [];
    public List<string> Homerooms { get; set; } = [];
    public List<string> SchoolLevels { get; set; } = [];
    public bool CanAccessHealth { get; set; }
    public bool CanAccessBus { get; set; }
    public bool CanAccessAcademic { get; set; }
    public bool CanAccessWorkspace { get; set; }
    public bool CanAccessAtRisk { get; set; }
    public bool CanAccessCommendations { get; set; }
    public bool CanAccessSupport { get; set; }
}

public class TeacherAccessUserDto
{
    public string UserId { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string? Email { get; set; }
    public string FullName { get; set; } = default!;
    public List<string> Scopes { get; set; } = [];
    public List<string> Homerooms { get; set; } = [];
    public List<string> SchoolLevels { get; set; } = [];
}

public class UserAccessManagementDto
{
    public List<TeacherAccessUserDto> Users { get; set; } = [];
    public List<string> AvailableScopes { get; set; } = [];
    public List<string> AvailableHomerooms { get; set; } = [];
    public List<string> AvailableSchoolLevels { get; set; } = [];
}

public class UpdateTeacherAccessDto
{
    [Required]
    public List<string> Scopes { get; set; } = [];
    public List<string> Homerooms { get; set; } = [];
    public List<string> SchoolLevels { get; set; } = [];
}
