namespace OlympiaStudent360.Api.Core.DTOs;

using System.ComponentModel.DataAnnotations;

public class AdminUserDto
{
    public string Id { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string? Email { get; set; }
    public string FullName { get; set; } = default!;
    public string Role { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
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
}

public class UpdateAdminUserDto
{
    [Required]
    public string FullName { get; set; } = default!;
    public string? Email { get; set; }
    [Required]
    public string Role { get; set; } = "Teacher";
    public string? NewPassword { get; set; }
}
