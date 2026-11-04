using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;

namespace OlympiaStudent360.Api.SisSync;

public sealed class SisEtlOptions
{
    public Uri BaseUri { get; init; } = new("https://data-sync-api.schoolonline.edu.vn/");
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public int PageSize { get; init; } = 500;
    public int LookbackDays { get; init; } = 31;
    public int TimetableFutureDays { get; init; } = 14;
    public int RequestIntervalMilliseconds { get; init; } = 1100;
    public IReadOnlyList<string> SchoolIds { get; init; } = [];
    public IReadOnlyList<string> SchoolYearIds { get; init; } = [];
    // Core roster by default. Other documented feeds can be enabled explicitly.
    public IReadOnlyList<string> Resources { get; init; } =
        ["schools", "students", "health/visits"];
    public DateOnly? FromDate { get; init; }
    public DateOnly? ToDate { get; init; }
    public bool AllowEmptyStudents { get; init; }
    public string SourceKey => Hash(BaseUri.AbsoluteUri.TrimEnd('/') + "|" + ClientId);
    internal static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static SisEtlOptions FromConfiguration(IConfiguration config)
    {
        string Required(string name) => !string.IsNullOrWhiteSpace(config[name]) ? config[name]!
            : throw new SisApiException($"Required configuration {name} is missing.");
        int Number(string name, int fallback, int min, int max) => config[name] is not { Length: > 0 } value ? fallback
            : int.TryParse(value, out var n) && n >= min && n <= max ? n
            : throw new SisApiException($"{name} must be between {min} and {max}.");
        string[] List(string name) => (config[name] ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct().ToArray();
        DateOnly? Date(string name) => config[name] is not { Length: > 0 } text ? null
            : DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date) ? date
            : throw new SisApiException($"{name} must use YYYY-MM-DD.");
        var url = config["Sis:BaseUrl"] ?? "https://data-sync-api.schoolonline.edu.vn/";
        if (!Uri.TryCreate(url.TrimEnd('/') + "/", UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0 || uri.AbsolutePath != "/")
            throw new SisApiException("Sis:BaseUrl must be an HTTPS origin without credentials, path, query or fragment.");
        var resources = List("Sis:Resources");
        if (resources.Length == 0) resources = ["schools", "students", "health/visits"];
        foreach (var resource in resources)
            if (!SisEndpoints.All.Any(x => x.Name == resource)) throw new SisApiException("Unknown Sis:Resources endpoint.");
        if (!resources.Contains("schools") || !resources.Contains("students"))
            throw new SisApiException("Sis:Resources must include schools and students.");
        var schools = List("Sis:SchoolIds");
        var years = List("Sis:SchoolYearIds");
        foreach (var id in schools.Concat(years))
            if (id.Length > 100 || id[0] == '0' || id.Any(c => c < '0' || c > '9'))
                throw new SisApiException("SIS school/year IDs must be digit strings without leading zeros.");
        var from = Date("Sis:FromDate");
        var to = Date("Sis:ToDate");
        if (from.HasValue != to.HasValue || from > to)
            throw new SisApiException("Configure both Sis:FromDate and Sis:ToDate, with FromDate <= ToDate.");
        var allowEmpty = config["Sis:AllowEmptyStudents"];
        if (!string.IsNullOrEmpty(allowEmpty) && !bool.TryParse(allowEmpty, out _))
            throw new SisApiException("Sis:AllowEmptyStudents must be true or false.");
        return new SisEtlOptions
        {
            BaseUri = uri, ClientId = Required("Sis:ClientId"), ClientSecret = Required("Sis:ClientSecret"),
            PageSize = Number("Sis:PageSize", 500, 1, 500),
            LookbackDays = Number("Sis:LookbackDays", 31, 1, 366),
            TimetableFutureDays = Number("Sis:TimetableFutureDays", 14, 0, 90),
            RequestIntervalMilliseconds = Number("Sis:RequestIntervalMilliseconds", 1100, 0, 60000),
            SchoolIds = schools, SchoolYearIds = years, Resources = resources, FromDate = from, ToDate = to,
            AllowEmptyStudents = bool.TryParse(allowEmpty, out var allowed) && allowed
        };
    }
}

public sealed record SisEndpoint(string Name, string Scope, bool Year = false, bool School = false,
    bool Daily = false, bool Typed = false, bool Incremental = true);

public static class SisEndpoints
{
    public static readonly IReadOnlyList<SisEndpoint> All =
    [
        new("schools", "catalog.read"),
        new("students", "students.read", Year: true, School: true),
        new("employees", "employees.read", School: true),
        new("homeroom-classes", "classes.read", Year: true, School: true),
        new("courses", "courses.read", Year: true, School: true),
        new("timetable", "timetable.read", Year: true, School: true, Daily: true, Incremental: false),
        new("substitutions", "substitutions.read", Year: true, School: true),
        new("attendances", "attendance.read", Year: true, School: true, Daily: true, Typed: true),
        new("comments", "comments.read", Year: true, School: true, Daily: true, Typed: true),
        new("bus/attendances", "bus.read", Year: true, School: true, Daily: true),
        new("health/profiles", "health.read", Year: true, School: true),
        new("health/physical-checks", "health.read", Year: true, School: true),
        new("health/diseases", "health.read"),
        new("health/visits", "health-visits.read", Year: true, School: true)
    ];
}
