namespace OlympiaStudent360.Api.Core.Services;

public static class StudentDataEntryOptions
{
    public static readonly IReadOnlyList<string> ProjectSubjects =
    [
        "Art", "Biology", "English", "History", "Mathematics", "Science", "Other"
    ];

    public static readonly IReadOnlyList<string> ProjectStatuses =
    [
        "Planned", "In Progress", "Completed", "On Hold"
    ];

    public static bool IsCompletedProjectStatus(string? status) =>
        string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, "Hoàn thành", StringComparison.OrdinalIgnoreCase);

    public static readonly IReadOnlyList<string> MentalHealthConcernTypes =
    [
        "Anxiety", "Stress", "Mood", "Behavioural", "Social/Family", "Academic pressure", "Other"
    ];

    public static readonly IReadOnlyList<string> MentalHealthRiskLevels =
    [
        "Low", "Medium", "High"
    ];

    public static readonly IReadOnlyList<string> BusTripDirections =
    [
        "To school", "From school"
    ];

    public static readonly IReadOnlyList<string> BusViolationTypes =
    [
        "Late", "No seatbelt"
    ];
}
