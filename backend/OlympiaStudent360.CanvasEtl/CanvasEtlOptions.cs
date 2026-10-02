using Microsoft.Extensions.Configuration;

namespace OlympiaStudent360.CanvasEtl;

public sealed class CanvasEtlOptions
{
    public required Uri BaseUri { get; init; }
    public required string AccessToken { get; init; }
    public required string AccountId { get; init; }
    public int PerPage { get; init; } = 100;
    public bool AllowEmptyCourses { get; init; }
    public bool AllowEmptyStudents { get; init; }
    public bool IncludeAllCourses { get; init; }
    public bool IncludeCompletedCourses { get; init; }
    public IReadOnlyList<long> EnrollmentTermIds { get; init; } = [];
    public IReadOnlyList<long> SyncAllEnrollmentTermIds { get; init; } = [];
    public DateTimeOffset? SinceUtc { get; init; }
    public int WatermarkOverlapMinutes { get; init; } = 120;

    public static CanvasEtlOptions FromConfiguration(IConfiguration configuration)
    {
        var baseUrl = Required(configuration, "Canvas:BaseUrl").TrimEnd('/') + "/";
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException("Canvas:BaseUrl must be an absolute URL.");
        }

        if (baseUri.Scheme != Uri.UriSchemeHttps && !baseUri.IsLoopback)
        {
            throw new InvalidOperationException("Canvas:BaseUrl must use HTTPS (except for local testing).");
        }

        var perPage = 100;
        var configuredPerPage = configuration["Canvas:PerPage"];
        if (!string.IsNullOrWhiteSpace(configuredPerPage) &&
            (!int.TryParse(configuredPerPage, out perPage) || perPage is < 1 or > 100))
        {
            throw new InvalidOperationException("Canvas:PerPage must be an integer from 1 through 100.");
        }

        var enrollmentTermIds = ParseIds(configuration["Canvas:EnrollmentTermIds"]);
        var syncAllEnrollmentTermIds = ParseIds(configuration["Canvas:SyncAllEnrollmentTermIds"]);
        var overlapMinutes = ParseBoundedInt(
            configuration["Canvas:WatermarkOverlapMinutes"], 120, 0, 1440,
            "Canvas:WatermarkOverlapMinutes");

        return new CanvasEtlOptions
        {
            BaseUri = baseUri,
            AccessToken = Required(configuration, "Canvas:AccessToken"),
            AccountId = Required(configuration, "Canvas:AccountId"),
            PerPage = perPage,
            AllowEmptyCourses = bool.TryParse(
                configuration["Canvas:AllowEmptyCourses"], out var allowEmptyCourses) && allowEmptyCourses,
            AllowEmptyStudents = bool.TryParse(
                configuration["Canvas:AllowEmptyStudents"], out var allowEmptyStudents) && allowEmptyStudents,
            IncludeAllCourses = bool.TryParse(
                configuration["Canvas:IncludeAllCourses"], out var includeAllCourses) && includeAllCourses,
            IncludeCompletedCourses = bool.TryParse(
                configuration["Canvas:IncludeCompletedCourses"], out var includeCompletedCourses) && includeCompletedCourses,
            EnrollmentTermIds = enrollmentTermIds,
            SyncAllEnrollmentTermIds = syncAllEnrollmentTermIds,
            WatermarkOverlapMinutes = overlapMinutes
        };
    }

    private static string Required(IConfiguration configuration, string name)
    {
        var value = configuration[name];
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException($"Required configuration value {name} is not configured.");
    }

    private static IReadOnlyList<long> ParseIds(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var ids = new List<long>();
        foreach (var item in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!long.TryParse(item, out var id) || id <= 0)
            {
                throw new InvalidOperationException(
                    "Canvas:EnrollmentTermIds must be a comma-separated list of positive integers.");
            }
            ids.Add(id);
        }

        return ids.Distinct().ToArray();
    }

    private static int ParseBoundedInt(string? value, int defaultValue, int minimum, int maximum, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (!int.TryParse(value, out var result) || result < minimum || result > maximum)
        {
            throw new InvalidOperationException($"{name} must be from {minimum} through {maximum}.");
        }

        return result;
    }
}
