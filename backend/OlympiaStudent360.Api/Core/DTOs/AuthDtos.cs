namespace OlympiaStudent360.Api.Core.DTOs;

using System.ComponentModel.DataAnnotations;

public class LoginDto
{
    [Required]
    public string Username { get; set; } = default!;
    [Required]
    public string Password { get; set; } = default!;
}

public class RegisterDto
{
    [Required]
    public string Username { get; set; } = default!;
    [Required]
    public string Password { get; set; } = default!;
    [Required]
    public string FullName { get; set; } = default!;
    [Required]
    public string Role { get; set; } = "Teacher";
}

public class AuthResponseDto
{
    public string Token { get; set; } = default!;
    public string Role { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string UserId { get; set; } = default!;
}

public class CurrentUserDto
{
    public string Role { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string UserId { get; set; } = default!;
}
