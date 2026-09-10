using System.Text.RegularExpressions;

namespace OlympiaStudent360.Api.Core.Services;

/// <summary>
/// Converts source-system period labels to the canonical values used by dashboard filters.
/// The source labels themselves remain unchanged in the warehouse; this is only a reporting
/// projection so equivalent Canvas labels do not appear as separate filter choices.
/// </summary>
public static partial class DashboardPeriodNormalizer
{
    public const int SchoolYearStartMonth = 9;

    public static string AcademicYear(string value)
    {
        var trimmed = value.Trim();
        var match = AcademicYearPattern().Match(trimmed);
        if (!match.Success)
        {
            match = CompactAcademicYearPattern().Match(trimmed);
        }
        if (!match.Success ||
            !TryExpandYear(match.Groups["start"].Value, null, out var start) ||
            !TryExpandYear(match.Groups["end"].Value, start, out var end) ||
            end != start + 1)
        {
            return trimmed;
        }

        return $"{start:D4}-{end:D4}";
    }

    public static bool IsAcademicYear(string value) =>
        CanonicalAcademicYearPattern().IsMatch(AcademicYear(value));

    public static string AcademicYear(DateTime value)
    {
        var startYear = value.Month >= SchoolYearStartMonth ? value.Year : value.Year - 1;
        return $"{startYear:D4}-{startYear + 1:D4}";
    }

    public static string AcademicYear(string sourceLabel, DateTime? sourceDate)
    {
        var normalized = AcademicYear(sourceLabel);
        return IsAcademicYear(normalized) || sourceDate is null
            ? normalized
            : AcademicYear(sourceDate.Value);
    }

    public static string Term(string value)
    {
        var trimmed = value.Trim();
        if (SemesterOnePattern().IsMatch(trimmed)) return "hk1";
        if (SemesterTwoPattern().IsMatch(trimmed)) return "hk2";
        return trimmed;
    }

    public static (DateTime Start, DateTime End) AcademicYearRange(string academicYear)
    {
        var startYear = AcademicYearStart(academicYear);
        if (startYear == int.MinValue)
        {
            startYear = AcademicYearStart(AcademicYear(DateTime.UtcNow));
        }

        return (
            new DateTime(startYear, SchoolYearStartMonth, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(startYear + 1, SchoolYearStartMonth, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    public static (DateTime Start, DateTime End) PeriodRange(string academicYear, string? term)
    {
        var startYear = AcademicYearStart(academicYear);
        if (startYear == int.MinValue)
        {
            startYear = AcademicYearStart(AcademicYear(DateTime.UtcNow));
        }

        return Term(term ?? "") switch
        {
            // Olympia's reporting fallback follows the Vietnamese school calendar. Canvas
            // grading-period dates remain authoritative whenever Canvas supplied them.
            "hk1" => (
                new DateTime(startYear, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(startYear + 1, 1, 19, 0, 0, 0, DateTimeKind.Utc)),
            "hk2" => (
                new DateTime(startYear + 1, 1, 19, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(startYear + 1, 6, 1, 0, 0, 0, DateTimeKind.Utc)),
            _ => AcademicYearRange(academicYear)
        };
    }

    public static int AcademicYearStart(string value)
    {
        var normalized = AcademicYear(value);
        return int.TryParse(normalized.Split('-', '/', StringSplitOptions.TrimEntries)[0], out var year)
            ? year
            : int.MinValue;
    }

    private static bool TryExpandYear(string value, int? startYear, out int year)
    {
        if (!int.TryParse(value, out year)) return false;
        if (value.Length == 4) return year is >= 1900 and <= 2199;

        if (value.Length != 2) return false;
        year = startYear.HasValue
            ? startYear.Value / 100 * 100 + year
            : 2000 + year;
        if (startYear.HasValue && year < startYear.Value) year += 100;
        return true;
    }

    [GeneratedRegex(@"(?<!\d)(?<start>(?:19|20)?\d{2})\s*[-\u2013\u2014/]\s*(?<end>(?:19|20)?\d{2})(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex AcademicYearPattern();

    // Canvas also uses compact source labels such as 2627_TERM_CLUB. Treat only adjacent
    // two-digit years as an academic year; unrelated numeric course labels remain untouched.
    [GeneratedRegex(@"(?<!\d)(?<start>\d{2})(?<end>\d{2})(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex CompactAcademicYearPattern();

    [GeneratedRegex(@"^(?:19|20)\d{2}-(?:19|20)\d{2}$", RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalAcademicYearPattern();

    [GeneratedRegex(@"(?:semester|term|học\s*k[ỳì]|hoc\s*k[yi]|hk)\s*(?:1|i)(?![\div])|^s1f?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SemesterOnePattern();

    [GeneratedRegex(@"(?:semester|term|học\s*k[ỳì]|hoc\s*k[yi]|hk)\s*(?:2|ii)(?![\div])|^s2f?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SemesterTwoPattern();
}
