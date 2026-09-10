using System.Text.RegularExpressions;

namespace OlympiaStudent360.Api.Core.Services;

/// <summary>
/// Converts source-system period labels to the canonical values used by dashboard filters.
/// The source labels themselves remain unchanged in the warehouse; this is only a reporting
/// projection so equivalent Canvas labels do not appear as separate filter choices.
/// </summary>
public static partial class DashboardPeriodNormalizer
{
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

    public static string Term(string value)
    {
        var trimmed = value.Trim();
        var match = SemesterPattern().Match(trimmed);
        return match.Success ? $"hk{match.Groups["semester"].Value}" : trimmed;
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

    [GeneratedRegex(@"(?:semester|term|học\s*kỳ|hoc\s*ky|hk)\s*(?<semester>[12])(?!\d)|^s(?<semester>[12])f?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SemesterPattern();
}
