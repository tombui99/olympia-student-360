using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Core.Services;

public sealed record CanvasReportSubaccountOption(
    long Id, string Name, string Path, List<long> AccountIds, List<string> GradeLevels);
public sealed record CanvasReportCohortOption(string Grade, string Homeroom);

/// <summary>
/// Shared Canvas report filters. Coefficient categories match only literal Canvas group labels;
/// no assignment group weight or coefficient is inferred from a course or assignment name.
/// </summary>
public static class CanvasReportFilters
{
    public static readonly string[] AssessmentCategories = ["all", "HS1", "HS2", "HS3", "GPA"];
    private static readonly Regex CoefficientGroupPattern = new(
        @"(?<![a-z0-9])(?:hs|he[\s._-]*so)[\s._-]*([123])(?=$|[.\s/_-])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex GpaGroupPattern = new(
        @"(?<![a-z0-9])gpa(?=$|[^a-z0-9])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SubjectGradePrefixPattern = new(
        @"^(?:g(?:rade)?[\s._-]*0*(?:1[0-2]|[1-9])(?:ib)?|(?:khoi|khối)[\s._-]*0*(?:1[0-2]|[1-9]))[\s._-]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static string? CanonicalCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "all";
        return value.Trim().ToUpperInvariant() switch
        {
            "ALL" => "all",
            "HS1" => "HS1",
            "HS2" => "HS2",
            "HS3" => "HS3",
            "GPA" => "GPA",
            _ => null
        };
    }

    public static string? CategoryForGroup(string? groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName)) return null;
        var decomposed = groupName.Trim().Normalize(NormalizationForm.FormD);
        var normalized = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark) continue;
            normalized.Append(character == 'đ' || character == 'Đ'
                ? 'd' : char.ToLowerInvariant(character));
        }

        // Canvas often names these groups by term and assessment number, e.g. HK1-HS1.1.
        // Match coefficient tokens in the group label, but not unrelated numbers such as HS10.
        var label = normalized.ToString();
        // Both midterm assignment groups use coefficient 2. The trailing digit is
        // the semester, so GHK1 must not be interpreted as coefficient 1.
        if (label is "ghk1" or "ghk2") return "HS2";
        var coefficients = CoefficientGroupPattern.Matches(label)
            .Select(match => match.Groups[1].Value).Distinct().ToList();
        var hasGpa = GpaGroupPattern.IsMatch(label);
        if ((hasGpa && coefficients.Count > 0) || coefficients.Count > 1) return null;
        if (hasGpa) return "GPA";
        return coefficients.Count == 1 ? $"HS{coefficients[0]}" : null;
    }

    public static List<string> AvailableCategories(IEnumerable<string?> categories)
    {
        var available = categories.OfType<string>().ToHashSet(StringComparer.Ordinal);
        return AssessmentCategories
            .Where(category => category == "all" || available.Contains(category))
            .ToList();
    }

    public static async Task<List<CanvasReportSubaccountOption>> VisibleSubaccounts(
        AppDbContext db, IReadOnlyCollection<long> reportableCourseIds, CancellationToken cancellationToken)
    {
        if (reportableCourseIds.Count == 0) return [];
        var courses = await db.CanvasCourses.AsNoTracking()
            .Where(course => course.CanvasAccountId != null)
            .WhereMatchesAnyValue(course => course.CanvasCourseId, reportableCourseIds)
            .Select(course => new
            {
                course.CanvasCourseId,
                AccountId = course.CanvasAccountId!.Value,
                course.CanvasGradeLevel
            })
            .ToListAsync(cancellationToken);
        if (courses.Count == 0) return [];

        var accounts = await db.CanvasAccounts.AsNoTracking()
            .Select(account => new { account.CanvasAccountId, account.ParentAccountId, account.Name })
            .ToListAsync(cancellationToken);
        var byId = accounts.ToDictionary(account => account.CanvasAccountId);
        // Canvas stores the same subject in separate grade-specific accounts such as G5_English
        // and G11_English. Collect the direct accounts first, then expose one subject option whose
        // account ids still preserve the exact Canvas filtering scope.
        var grades = new Dictionary<long, HashSet<string>>();
        foreach (var course in courses)
        {
            if (!byId.ContainsKey(course.AccountId)) continue;
            if (!grades.TryGetValue(course.AccountId, out var values))
                grades[course.AccountId] = values = [];
            if (!string.IsNullOrWhiteSpace(course.CanvasGradeLevel))
                values.Add(course.CanvasGradeLevel);
        }

        string PathFor(long id)
        {
            var names = new List<string>();
            var visited = new HashSet<long>();
            long? cursor = id;
            while (cursor is { } accountId && visited.Add(accountId) && byId.TryGetValue(accountId, out var account))
            {
                names.Add(string.IsNullOrWhiteSpace(account.Name) ? accountId.ToString() : account.Name!);
                cursor = account.ParentAccountId;
            }
            names.Reverse();
            return string.Join(" / ", names);
        }

        // Every underlying account is directly used by at least one reportable Canvas course. A
        // restricted user's options therefore reveal no unrelated branches or structural parents.
        var directOptions = grades
            // A report subject must participate in the structured Canvas grade hierarchy.
            // Broad program/cohort containers can also own courses (for example, one account
            // containing several unrelated subjects), but they have no derived grade and must
            // not be presented as subjects.
            .Where(item => item.Value.Count > 0)
            .Select(item => new
        {
            Id = item.Key,
            Name = SubjectDisplayName(byId[item.Key].Name, item.Key),
            Path = PathFor(item.Key),
            GradeLevels = item.Value
        }).ToList();
        return directOptions
            .GroupBy(option => option.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var ordered = group.OrderBy(option => option.Id).ToList();
                var representative = ordered[0];
                return new CanvasReportSubaccountOption(
                    representative.Id,
                    representative.Name,
                    representative.Path,
                    ordered.Select(option => option.Id).Distinct().OrderBy(id => id).ToList(),
                    ordered.SelectMany(option => option.GradeLevels)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToList());
            })
            .OrderBy(option => option.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public static string SubjectDisplayName(string? name, long id)
    {
        var source = string.IsNullOrWhiteSpace(name) ? id.ToString() : name.Trim();
        var display = SubjectGradePrefixPattern.Replace(source, "", 1).Trim();
        return display.Length == 0 ? source : display;
    }

    public static async Task<List<long>> AccountAndDescendantIds(
        AppDbContext db, IReadOnlyCollection<long> selectedIds, CancellationToken cancellationToken)
    {
        if (selectedIds.Count == 0) return [];
        var accounts = await db.CanvasAccounts.AsNoTracking()
            .Select(account => new { account.CanvasAccountId, account.ParentAccountId, account.Name })
            .ToListAsync(cancellationToken);
        var children = accounts.Where(account => account.ParentAccountId != null)
            .GroupBy(account => account.ParentAccountId!.Value)
            .ToDictionary(group => group.Key, group => group.Select(account => account.CanvasAccountId).ToList());
        var knownIds = accounts.Select(account => account.CanvasAccountId).ToHashSet();
        var eligibleSubjectIds = (await db.CanvasCourses.AsNoTracking()
                .Where(course => course.CanvasAccountId != null && course.CanvasGradeLevel != null)
                .Select(course => course.CanvasAccountId!.Value).Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();
        var selectedSubjectNames = accounts
            .Where(account => selectedIds.Contains(account.CanvasAccountId) &&
                eligibleSubjectIds.Contains(account.CanvasAccountId))
            .Select(account => SubjectDisplayName(account.Name, account.CanvasAccountId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matchingSubjectIds = accounts
            .Where(account => eligibleSubjectIds.Contains(account.CanvasAccountId) &&
                selectedSubjectNames.Contains(
                SubjectDisplayName(account.Name, account.CanvasAccountId)))
            .Select(account => account.CanvasAccountId);
        var result = new HashSet<long>();
        var queue = new Queue<long>(selectedIds.Where(id =>
                knownIds.Contains(id) && eligibleSubjectIds.Contains(id))
            .Concat(matchingSubjectIds).Distinct());
        while (queue.TryDequeue(out var id))
        {
            if (!result.Add(id)) continue;
            if (children.TryGetValue(id, out var descendants))
                foreach (var childId in descendants) queue.Enqueue(childId);
        }
        return result.ToList();
    }
}
