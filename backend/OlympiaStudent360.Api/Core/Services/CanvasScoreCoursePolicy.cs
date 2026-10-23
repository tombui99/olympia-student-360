using System.Text.RegularExpressions;

namespace OlympiaStudent360.Api.Core.Services;

/// <summary>Identifies the English programmes selected for score reporting.</summary>
public static class CanvasScoreCoursePolicy
{
    public static readonly string[] Programmes = ["ESL", "ELA"];
    private static readonly Regex English = new(
        @"(?<![a-z])(?:english|tiếng[\s._/-]*anh|tieng[\s._/-]*anh)(?![a-z])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex Esl = Programme("esl");
    private static readonly Regex Ela = Programme("ela");

    private static Regex Programme(string label) => new(
        // Canvas uses both 'English ESL 9E1.1' and '2627-EnglishESL9E1.1'.
        $@"(?<![a-z0-9])(?:english[\s._/-]*)?{label}(?=$|[^a-z])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool Include(string? courseName, string? courseCode, bool inEnglishSubject,
        IReadOnlyCollection<string> programmes)
    {
        var name = courseName ?? "";
        var code = courseCode ?? "";
        var hasEsl = Esl.IsMatch(name) || Esl.IsMatch(code);
        var hasEla = Ela.IsMatch(name) || Ela.IsMatch(code);
        var isEnglish = inEnglishSubject || hasEsl || hasEla || English.IsMatch(name) || English.IsMatch(code);
        // An empty selection means both programmes, matching the multi-select UI.
        // Never guess the programme of an unlabelled or ambiguously labelled course.
        if (!isEnglish) return true;
        if (hasEsl == hasEla) return false;
        return programmes.Count == 0 || programmes.Contains(hasEsl ? "ESL" : "ELA");
    }

    public static HashSet<long> EnglishAccountIds(IEnumerable<(long Id, long? ParentId, string? Name)> accounts)
    {
        var rows = accounts.ToList();
        var children = rows.Where(row => row.ParentId is not null).GroupBy(row => row.ParentId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(row => row.Id).ToList());
        var result = new HashSet<long>();
        var queue = new Queue<long>(rows.Where(row => English.IsMatch(row.Name ?? "")).Select(row => row.Id));
        while (queue.TryDequeue(out var id))
        {
            if (!result.Add(id)) continue;
            if (children.TryGetValue(id, out var descendants))
                foreach (var child in descendants) queue.Enqueue(child);
        }
        return result;
    }
}
