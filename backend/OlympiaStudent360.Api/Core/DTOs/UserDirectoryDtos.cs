namespace OlympiaStudent360.Api.Core.DTOs;

public class UserDirectoryDto
{
    public string Id { get; set; } = default!;
    public string Username { get; set; } = default!;
    public string? Email { get; set; }
    public string FullName { get; set; } = default!;
}
