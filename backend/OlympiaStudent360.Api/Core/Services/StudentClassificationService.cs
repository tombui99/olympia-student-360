using OlympiaStudent360.Api.Core.DTOs;
using OlympiaStudent360.Api.Core.Entities;

namespace OlympiaStudent360.Api.Core.Services;

/// <summary>School-wide, configurable classification rules shared by every student risk surface.</summary>
public static class StudentClassificationService
{
    public static readonly IReadOnlyList<string> PositiveBehaviourTypes =
    [
        "Khen thưởng",
        "Tham gia tích cực",
        "Hỗ trợ bạn học",
        "Nỗ lực nổi bật"
    ];

    public static readonly IReadOnlyList<string> ConcerningBehaviourTypes =
    [
        "Đi học muộn",
        "Chưa nộp bài tập",
        "Gây mất trật tự trong lớp"
    ];

    public static readonly IReadOnlyList<string> SupportedBehaviourTypes =
        [.. PositiveBehaviourTypes, .. ConcerningBehaviourTypes];

    private static readonly Dictionary<string, string> BehaviourAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Commendation"] = "Khen thưởng",
        ["Excellent participation"] = "Tham gia tích cực",
        ["Helped a classmate"] = "Hỗ trợ bạn học",
        ["Outstanding effort"] = "Nỗ lực nổi bật",
        ["Late to class"] = "Đi học muộn",
        ["Homework not submitted"] = "Chưa nộp bài tập",
        ["Missing homework"] = "Chưa nộp bài tập",
        ["Disruptive in class"] = "Gây mất trật tự trong lớp",
        ["Thiếu bài tập về nhà"] = "Chưa nộp bài tập"
    };

    static StudentClassificationService()
    {
        foreach (var type in SupportedBehaviourTypes) BehaviourAliases[type] = type;
    }

    public static RiskClassification ClassifyRisk(
        StudentSignalSummary signals,
        StudentClassificationSettingsDto settings)
    {
        var lowCanvasAverage = signals.AverageCanvasGrade.HasValue
            && signals.AverageCanvasGrade.Value < settings.HighRiskCanvasAverageBelow;
        var highBehaviourConcern = signals.ConcerningBehaviourCount > settings.HighRiskBehaviourCount;
        var highBusConcern = signals.RiskBusViolationCount > settings.HighRiskBusViolationCount;
        var hasConcern = signals.ConcerningBehaviourCount > 0 || signals.RiskBusViolationCount > 0;
        var level = lowCanvasAverage || highBehaviourConcern || highBusConcern
            ? "High"
            : hasConcern ? "Medium" : "None";
        if (level == "None") return new RiskClassification(level, []);

        var flags = new List<RiskFlagDto>();
        if (signals.ConcerningBehaviourCount > 0)
            flags.Add(new RiskFlagDto { Code = "ConcerningBehaviours", Severity = highBehaviourConcern ? "High" : "Medium", Value = signals.ConcerningBehaviourCount });
        if (lowCanvasAverage)
            flags.Add(new RiskFlagDto { Code = "LowCanvasGrade", Severity = "High", Value = (int)Math.Round(signals.AverageCanvasGrade!.Value) });
        if (signals.RiskBusViolationCount > 0)
            flags.Add(new RiskFlagDto { Code = "BusViolations", Severity = highBusConcern ? "High" : "Medium", Value = signals.RiskBusViolationCount });
        return new RiskClassification(level, flags);
    }

    public static RecognitionClassification ClassifyRecognition(
        StudentSignalSummary signals,
        StudentClassificationSettingsDto settings)
    {
        var positiveBehaviourQualified = signals.PositiveBehaviourCount >= settings.CommendationPositiveBehaviourCount;
        var highGrade = signals.AverageCanvasGrade.HasValue
            && signals.AverageCanvasGrade.Value >= settings.CommendationCanvasAverageAtLeast;
        var busRequirementMet = signals.CommendationBusViolationCount <= settings.CommendationMaxBusViolations;
        return new RecognitionClassification(
            busRequirementMet && (positiveBehaviourQualified || highGrade),
            positiveBehaviourQualified,
            highGrade,
            busRequirementMet);
    }

    public static bool IsConcerningBehaviour(StudentBehaviour behaviour) =>
        IsConcerningBehaviourType(behaviour.BehaviourType);

    public static bool IsCommendationBehaviour(StudentBehaviour behaviour) =>
        IsCommendationBehaviourType(behaviour.BehaviourType);

    public static bool IsConcerningBehaviourType(string behaviourType) =>
        TryCanonicalBehaviourType(behaviourType, out var canonical)
        && ConcerningBehaviourTypes.Contains(canonical, StringComparer.OrdinalIgnoreCase);

    public static bool IsCommendationBehaviourType(string behaviourType) =>
        TryCanonicalBehaviourType(behaviourType, out var canonical)
        && PositiveBehaviourTypes.Contains(canonical, StringComparer.OrdinalIgnoreCase);

    public static bool IsSupportedBehaviourType(string behaviourType) =>
        TryCanonicalBehaviourType(behaviourType, out _);

    public static bool TryCanonicalBehaviourType(string? behaviourType, out string canonical) =>
        BehaviourAliases.TryGetValue(behaviourType?.Trim() ?? string.Empty, out canonical!);

    public static string CanonicalBehaviourTypeOrOriginal(string behaviourType) =>
        TryCanonicalBehaviourType(behaviourType, out var canonical) ? canonical : behaviourType;

    public static string BehaviourCategory(string behaviourType) =>
        IsCommendationBehaviourType(behaviourType) ? "Positive"
        : IsConcerningBehaviourType(behaviourType) ? "Concerning"
        : "Other";

}

public sealed record RiskClassification(string Level, List<RiskFlagDto> Flags);
public sealed record RecognitionClassification(
    bool Qualifies,
    bool PositiveBehaviourQualified,
    bool HighCanvasGrade,
    bool BusRequirementMet);
public sealed record StudentSignalSummary(
    int ConcerningBehaviourCount,
    int PositiveBehaviourCount,
    decimal? AverageCanvasGrade,
    int RiskBusViolationCount,
    int CommendationBusViolationCount);
