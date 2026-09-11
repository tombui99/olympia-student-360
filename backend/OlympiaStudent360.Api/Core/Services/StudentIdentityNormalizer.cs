namespace OlympiaStudent360.Api.Core.Services;

public static class StudentIdentityNormalizer
{
    public static string? SchoolUsername(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        var at = normalized.IndexOf('@');
        if (at >= 0)
        {
            normalized = normalized[..at];
        }

        normalized = normalized.Trim().ToLowerInvariant();
        return normalized.Length == 0 ? null : normalized;
    }
}
