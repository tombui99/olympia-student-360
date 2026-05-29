namespace OlympiaStudent360.Api.Core.DTOs;

public class LoginDto
{
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
}

public class RegisterDto
{
    public string Username { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string? JobTitle { get; set; }
    public string Role { get; set; } = "Teacher";
}

public class AuthResponseDto
{
    public string Token { get; set; } = default!;
    public string Role { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string UserId { get; set; } = default!;
    public string? JobTitle { get; set; }
}

public class CurrentUserDto
{
    public string Role { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string FullName { get; set; } = default!;
    public string UserId { get; set; } = default!;
    public string? JobTitle { get; set; }
}
