namespace OlympiaStudent360.Api.Core.DTOs;

using System.ComponentModel.DataAnnotations;

public class AdminUserDto
{
    public string Id { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string? Email { get; set; }
    public string FullName { get; set; } = default!;
    public string Role { get; set; } = default!;
    public List<string> AccessScopes { get; set; } = [];
    public List<string> Homerooms { get; set; } = [];
    public List<string> SchoolLevels { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class AdminUserPageDto
{
    public List<AdminUserDto> Items { get; set; } = [];
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

public class AdminUserOptionsDto
{
    public List<string> Roles { get; set; } = [];
    public List<string> AccessScopes { get; set; } = [];
    public List<string> Homerooms { get; set; } = [];
    public List<string> SchoolLevels { get; set; } = [];
}

public class CreateAdminUserDto
{
    [Required]
    public string Username { get; set; } = default!;
    [Required]
    public string Password { get; set; } = default!;
    [Required]
    public string FullName { get; set; } = default!;
    public string? Email { get; set; }
    [Required]
    public string Role { get; set; } = "Teacher";
    public List<string> AccessScopes { get; set; } = [];
    public List<string> Homerooms { get; set; } = [];
    public List<string> SchoolLevels { get; set; } = [];
}

public class UpdateAdminUserDto
{
    [Required]
    public string FullName { get; set; } = default!;
    public string? Email { get; set; }
    [Required]
    public string Role { get; set; } = "Teacher";
    public string? NewPassword { get; set; }
    public List<string> AccessScopes { get; set; } = [];
    public List<string> Homerooms { get; set; } = [];
    public List<string> SchoolLevels { get; set; } = [];
}
