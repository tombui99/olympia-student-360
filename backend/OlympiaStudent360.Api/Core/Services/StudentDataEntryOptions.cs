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
}
