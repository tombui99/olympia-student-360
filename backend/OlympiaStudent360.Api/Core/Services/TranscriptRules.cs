using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using OlympiaStudent360.Api.Core.DTOs;

namespace OlympiaStudent360.Api.Core.Services;

public static partial class TranscriptRules
{
    private static readonly HashSet<string> PassFailCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "music", "physical-education", "fine-arts", "service-learning", "cas", "basketball", "soccer",
        "volleyball", "gym", "piano", "guitar", "drums", "traditional-musical-instruments", "vocal",
        "choir", "graphic-design"
    };

    public static bool IsPassFail(string code) => PassFailCodes.Contains(code);

    public static double? AnnualAverage(double? semester1, double? semester2, double? explicitYear)
    {
        return semester1.HasValue && semester2.HasValue
            ? Math.Round((semester1.Value + 2 * semester2.Value) / 3, 1, MidpointRounding.AwayFromZero)
            : explicitYear;
    }

    public static double? AnnualGpa(IEnumerable<TranscriptSubjectDto> subjects)
    {
        var eligible = subjects.Where(subject => subject.Credit > 0 && !IsPassFail(subject.SubjectCode)).ToList();
        if (eligible.Count == 0 || eligible.Any(subject => subject.YearAverage == null)) return null;
        return Math.Round(eligible.Sum(subject => ToGpa(subject.YearAverage!.Value) * subject.Credit) /
            eligible.Sum(subject => subject.Credit), 2, MidpointRounding.AwayFromZero);
    }

    public static double ToGpa(double score) => score >= 95 ? 4.2 : score >= 90 ? 4 : score >= 80 ? 3.7 :
        score >= 75 ? 3.3 : score >= 70 ? 3 : score >= 65 ? 2.7 : score >= 60 ? 2.3 : score >= 55 ? 2 :
        score >= 50 ? 1.7 : score >= 45 ? 1.3 : score >= 40 ? 1 : .7;

    public static string? HistoricalGrade(string? courseName, string? courseCode, string? courseGrade)
    {
        foreach (var value in new[] { courseName, courseCode })
        {
            var match = ClassPattern().Match(value ?? "");
            if (match.Success) return int.Parse(match.Groups["grade"].Value).ToString();
        }
        return int.TryParse(courseGrade, out var grade) && grade is >= 1 and <= 12 ? grade.ToString() : null;
    }

    // Course totals are live Canvas totals until the course (or school year) has ended.
    public static bool AnnualTotalAvailable(string academicYear, DateTime? courseEnd, DateTime now)
    {
        if (courseEnd.HasValue) return courseEnd.Value <= now;
        var start = DashboardPeriodNormalizer.AcademicYearStart(academicYear);
        return start != int.MinValue && new DateTime(start + 1, 6, 1) <= now;
    }

    public static string InternationalName(string? name)
    {
        var parts = (name ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var ordered = parts.Length < 2 ? parts : new[] { parts[^1] }.Concat(parts.Skip(1).SkipLast(1)).Append(parts[0]);
        var normalized = string.Join(' ', ordered).Normalize(NormalizationForm.FormD);
        return string.Concat(normalized.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
            .Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormC);
    }

    public static string? Validate(TranscriptRecordDto record)
    {
        if (record.Revision < 0 || record.TranscriptName?.Length > 255 || record.Notes?.Length > 2000 ||
            record.IbCorePoints is < 0 or > 3 || record.Years == null || record.Years.Count > 20)
            return "Invalid transcript information or field length.";
        if (new[] { record.EnrollmentDate, record.GraduationDate, record.GraduationExamDate, record.IbReleaseDate }
            .Any(date => date.HasValue && (date.Value.Year < 1900 || date.Value.Year > 2200)))
            return "Invalid transcript date.";
        if (record.EnrollmentDate > record.GraduationDate) return "Enrollment must precede graduation.";
        if (record.Years.Any(year => year == null)) return "Invalid academic year entry.";
        if (record.Years.Select(year => year.AcademicYear).Distinct().Count() != record.Years.Count)
            return "Each academic year may occur once.";
        foreach (var year in record.Years)
        {
            if (year == null) return "Invalid academic year entry.";
            if (!DashboardPeriodNormalizer.IsAcademicYear(year.AcademicYear) ||
                DashboardPeriodNormalizer.AcademicYear(year.AcademicYear) != year.AcademicYear ||
                !string.IsNullOrWhiteSpace(year.GradeLevel) &&
                (!int.TryParse(year.GradeLevel, out var grade) || grade is < 1 or > 12) ||
                year.Credits == null || year.Credits.Count > 100 || year.IbSubjects == null || year.IbSubjects.Count > 30)
                return "Invalid academic year, grade or subject list.";
            if (year.IbSubjects.Count > 0 && (!int.TryParse(year.GradeLevel, out var ibGrade) || ibGrade is < 11 or > 12))
                return "IB entries require a recorded grade of 11 or 12 for that academic year.";
            if (year.Credits.Any(row => row == null || !ValidCode(row.SubjectCode) || !double.IsFinite(row.Credit) || row.Credit is < 0 or > 2 ||
                row.EnglishName?.Length > 255 || row.VietnameseName?.Length > 255) ||
                year.Credits.Select(row => row.SubjectCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != year.Credits.Count)
                return "Credits must be between 0 and 2, with one entry per subject.";
            if (year.IbSubjects.Any(row => row == null)) return "Invalid IB subject entry.";
            if (year.IbSubjects.Select(row => row.SubjectCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != year.IbSubjects.Count)
                return "IB subjects must have unique subject codes within each year.";
            foreach (var row in year.IbSubjects)
                if (row == null || !ValidCode(row.SubjectCode) || string.IsNullOrWhiteSpace(row.SubjectName) || row.SubjectName.Length > 255 ||
                    row.Level is not (null or "" or "HL" or "SL") ||
                    new[] { row.PredictedGrade, row.Term1, row.Term2, row.Term3, row.Term4 }
                        .Any(value => !string.IsNullOrWhiteSpace(value) && !IbMarkPattern().IsMatch(value)))
                    return "IB marks must be 1–7, A–E, Pass or Fail; levels must be HL or SL.";
        }
        return null;
    }

    private static bool ValidCode(string? code) => code != null && SubjectCodePattern().IsMatch(code);
    [GeneratedRegex(@"(?<![a-z0-9])(?<grade>1[0-2]|[1-9])[a-z]{1,3}\d{1,2}(?:\.\d+)?(?![a-z0-9])", RegexOptions.IgnoreCase)]
    private static partial Regex ClassPattern();
    [GeneratedRegex(@"^[a-zA-Z0-9][a-zA-Z0-9_-]{0,99}$")]
    private static partial Regex SubjectCodePattern();
    [GeneratedRegex(@"^(?:[1-7]|[A-E]|Pass|Fail)$")]
    private static partial Regex IbMarkPattern();
}
