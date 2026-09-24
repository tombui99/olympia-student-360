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
        "Gây mất trật tự trong lớp",
        "An toàn_Bài xích, cô lập, bè phái, gây tổn thương danh dự và tâm lý người khác / Safety_Bullying and isolating others, causing psychological harm",
        "An toàn_Sử dụng ma túy, chất gây nghiện / Safety_Using drugs and addictive substances",
        "An toàn_Tham gia, tổ chức cá cược, đánh bạc dưới mọi hình thức / Safety_Participating in gambling in any form",
        "An toàn_Đánh nhau / Safety_Fighting",
        "Chuyên cần_Nghỉ tiết không phép / Attendance_Unauthorized absence from class",
        "Chuyên cần_Trốn học / Attendance_Skipping school",
        "Chuyên cần_Vào lớp muộn / Attendance_Late arrival to class",
        "Chuyên cần_Vào trường muộn / Attendance_Late arrival at school",
        "Khác / Other",
        "Trang phục_Phụ kiện / Costume_Accessories",
        "Trang phục_Đồng phục / Costume_Uniforms",
        "Trung thực_Mạo danh người khác, sử dụng tài khoản của người khác (CĐ2) / Faithful_Impersonating others and using their accounts (Level 2)",
        "Trung thực_Nói dối, không hợp tác khi làm tường trình, khai báo / Faithful_Lying and not cooperating during reports",
        "Trung thực_Sử dụng đồ đạc của người khác khi không được phép / Faithful_Using others' belongings without permission",
        "Trung thực_Đạo văn, gian dối trong làm bài dự án, kiểm tra cuối kì / Faithful_Plagiarism in final project examinations",
        "Trung thực_Đạo văn, gian dối trong làm bài tập / Faithful_Plagiarism in assignments",
        "Trách nhiệm_Không giữ vệ sinh / Responsibility_Not maintaining cleanliness",
        "Trách nhiệm_Làm hư hại tài sản (CĐ1) / Responsibility_Damaging property (Level 1)",
        "Trách nhiệm_Làm việc riêng / Responsibility_Doing personal work",
        "Trách nhiệm_Ra khỏi chỗ không xin phép / Responsibility_Leaving the place without permission",
        "Trách nhiệm_Sử dụng thiết bị điện tử sai mục đích / Responsibility_Misuse of electronic devices",
        "Tôn trọng_Chống đối lại quy ước lớp học/nội quy chung của nhà trường (CĐ2) / Respect_Defying class/school rules (Level 2)",
        "Tôn trọng_Chống đối lại quy ước lớp học/nội quy chung của nhà trường (CĐ3) / Respect_Defying class/school rules (Level 3)",
        "Tôn trọng_Mất lịch sự (CĐ2) / Respect_Rudeness (Level 2)",
        "Tôn trọng_Mất lịch sự (CĐ3) / Respect_Rudeness (Level 3)",
        "Tôn trọng_Mất trật tự trong lớp / Respect_Disruptive behavior in class",
        "Tôn trọng_Thể hiện tình cảm quá mức / Respect_Excessive public displays of affection",
        "Tôn trọng_Xúc phạm, chế giễu người khác / Respect_Insulting and mocking others"
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
        IsConcerningBehaviourType(behaviour.BehaviourType, behaviour.ClassificationCategory);

    public static bool IsCommendationBehaviour(StudentBehaviour behaviour) =>
        IsCommendationBehaviourType(behaviour.BehaviourType, behaviour.ClassificationCategory);

    public static bool IsConcerningBehaviourType(string behaviourType, string? category = null) =>
        string.Equals(category, "Concerning", StringComparison.OrdinalIgnoreCase) ||
        string.IsNullOrWhiteSpace(category) &&
        TryCanonicalBehaviourType(behaviourType, out var canonical)
        && ConcerningBehaviourTypes.Contains(canonical, StringComparer.OrdinalIgnoreCase);

    public static bool IsCommendationBehaviourType(string behaviourType, string? category = null) =>
        string.Equals(category, "Positive", StringComparison.OrdinalIgnoreCase) ||
        string.IsNullOrWhiteSpace(category) &&
        TryCanonicalBehaviourType(behaviourType, out var canonical)
        && PositiveBehaviourTypes.Contains(canonical, StringComparer.OrdinalIgnoreCase);

    public static bool IsSupportedBehaviourType(string behaviourType) =>
        TryCanonicalBehaviourType(behaviourType, out _);

    public static bool TryCanonicalBehaviourType(string? behaviourType, out string canonical) =>
        BehaviourAliases.TryGetValue(behaviourType?.Trim() ?? string.Empty, out canonical!);

    public static string CanonicalBehaviourTypeOrOriginal(string behaviourType) =>
        TryCanonicalBehaviourType(behaviourType, out var canonical) ? canonical : behaviourType;

    public static string BehaviourCategory(string behaviourType, string? category = null) =>
        IsCommendationBehaviourType(behaviourType, category) ? "Positive"
        : IsConcerningBehaviourType(behaviourType, category) ? "Concerning"
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
