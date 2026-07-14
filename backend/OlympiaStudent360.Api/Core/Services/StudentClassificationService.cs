using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;

namespace OlympiaStudent360.Api.Core.Services;

/// <summary>
/// Temporary, structured behaviour-based classifier used until Canvas scores are available.
/// It deliberately evaluates only the BehaviourType field and never free-text notes.
/// </summary>
public static class StudentClassificationService
{
    public static readonly IReadOnlyList<string> SupportedBehaviourTypes =
    [
        "Commendation",
        "Excellent participation",
        "Helped a classmate",
        "Late to class",
        "Homework not submitted",
        "Disruptive in class"
    ];

    private static readonly HashSet<string> ConcerningBehaviourTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Late to class",
        "Homework not submitted",
        "Disruptive in class"
    };

    private static readonly HashSet<string> CommendationBehaviourTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Commendation",
        "Excellent participation",
        "Helped a classmate"
    };

    public static RiskClassification ClassifyRisk(StudentProfile student)
    {
        var concernCount = student.Behaviours.Count(IsConcerningBehaviour);
        var highRiskThreshold = IsGradeNine(student.GradeLevel) ? 2 : 3;

        var level = concernCount >= highRiskThreshold
            ? "High"
            : concernCount > 0
                ? "Medium"
                : "None";

        var flags = concernCount == 0
            ? []
            : new List<RiskFlagDto>
            {
                new()
                {
                    Code = "ConcerningBehaviours",
                    Severity = level,
                    Value = concernCount
                },
                new()
                {
                    Code = "GradeThreshold",
                    Severity = level,
                    Value = highRiskThreshold
                }
            };

        return new RiskClassification(level, flags);
    }

    public static bool IsConcerningBehaviour(StudentBehaviour behaviour) =>
        IsConcerningBehaviourType(behaviour.BehaviourType);

    public static bool IsCommendationBehaviour(StudentBehaviour behaviour) =>
        IsCommendationBehaviourType(behaviour.BehaviourType);

    public static bool IsConcerningBehaviourType(string behaviourType) =>
        ConcerningBehaviourTypes.Contains(behaviourType);

    public static bool IsCommendationBehaviourType(string behaviourType) =>
        CommendationBehaviourTypes.Contains(behaviourType);

    public static bool IsSupportedBehaviourType(string behaviourType) =>
        SupportedBehaviourTypes.Contains(behaviourType, StringComparer.OrdinalIgnoreCase);

    private static bool IsGradeNine(string? gradeLevel) =>
        int.TryParse(gradeLevel, out var grade) && grade >= 9;
}

public sealed record RiskClassification(string Level, List<RiskFlagDto> Flags);
