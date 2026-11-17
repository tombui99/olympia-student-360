using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using OlympiaStudent360.Api.Core.DTOs;

namespace OlympiaStudent360.Api.SisSync;

internal static partial class SisGradebookCell
{
    internal static string? PrimaryGrade(JsonElement record)
    {
        var grade = SisJson.Child(record, "grade");
        var code = ParseGrade(SisJson.Text(grade, "code", 50));
        var name = ParseGrade(SisJson.Text(grade, "name", 100));
        if (code.HasValue && name.HasValue && code != name) return null;
        var number = code ?? name;
        return number is >= 1 and <= 4 ? number.Value.ToString(CultureInfo.InvariantCulture) : null;
    }

    private static int? ParseGrade(string? value)
    {
        var match = GradeLabel().Match(value ?? "");
        return match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : null;
    }

    [GeneratedRegex(@"^(?:(?:khối|lớp|grade|g)\s*)?0?(1[0-2]|[1-9])$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GradeLabel();

    internal static SisTranscriptMarkDto Read(JsonElement record, string grade)
    {
        var year = SisJson.Child(record, "school_year");
        var subject = SisJson.Child(record, "subject");
        var item = SisJson.Child(record, "grade_item");
        var score = Number(record, "score");
        var letter = SisJson.Text(record, "letter");
        if (score.HasValue && letter is not null)
            throw new SisApiException("SIS gradebook cell returned both a number and a letter.");

        return new(
            SisJson.RequiredId(year), SchoolYearName(record),
            grade,
            SisJson.RequiredId(subject), SisJson.Text(subject, "name", 255) ?? SisJson.Text(subject, "code") ?? SisJson.RequiredId(subject),
            SisJson.RequiredId(item), SisJson.Text(item, "name", 255) ?? SisJson.Text(item, "code") ?? SisJson.RequiredId(item),
            SisJson.RequiredId(record, "semester"), SisJson.RequiredId(record, "stage"), SisJson.Text(record, "period_name", 255),
            SisJson.Text(SisJson.Child(record, "homeroom_class"), "name", 255),
            score, letter, Number(item, "max_score"), SisJson.Text(record, "comment", int.MaxValue));
    }

    internal static string SchoolYearName(JsonElement record)
    {
        var year = SisJson.Child(record, "school_year");
        return SisJson.Text(year, "name", 255) ?? SisJson.Text(year, "code") ?? SisJson.RequiredId(year);
    }

    private static decimal? Number(JsonElement value, string field)
    {
        var child = SisJson.Child(value, field);
        if (child.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null) return null;
        return child.ValueKind == JsonValueKind.Number && child.TryGetDecimal(out var number) ? number
            : throw new SisApiException($"SIS gradebook field {field} must be a number.");
    }
}
