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

    public static readonly IReadOnlyList<string> HealthIncidentTypes =
    [
        "Injury", "Illness", "First aid", "Other health concern"
    ];

    public static readonly IReadOnlyList<int> HealthSeverityLevels = [1, 2, 3];

    public static readonly IReadOnlyList<string> HealthBodyAreas =
    [
        "Head", "Face", "Eye", "Arm", "Hand", "Finger", "Torso", "Hip",
        "Leg", "Knee", "Ankle", "Foot", "Other"
    ];

    public static readonly IReadOnlyList<string> HealthLocations =
    [
        "Classroom", "Hallway", "Playground", "Football field", "Basketball court",
        "Gymnasium", "Staircase", "Dining hall", "Other"
    ];
}
