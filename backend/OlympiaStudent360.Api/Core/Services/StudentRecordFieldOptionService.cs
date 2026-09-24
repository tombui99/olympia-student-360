using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Core.Services;

public sealed class StudentRecordFieldOptionService(AppDbContext context)
{
    public const string BehaviourModule = "student-behaviours";
    public const string ProjectModule = "student-projects";
    public const string BehaviourTypeField = "behaviour_type";
    public const string ProjectSubjectField = "subject";
    public const string ProjectStatusField = "status";

    public static bool IsConfigurable(string moduleId, string fieldKey) =>
        moduleId == BehaviourModule && fieldKey == BehaviourTypeField ||
        moduleId == ProjectModule && fieldKey is ProjectSubjectField or ProjectStatusField;

    public static bool IsValidGroup(string moduleId, string fieldKey, string? group) =>
        moduleId == BehaviourModule && fieldKey == BehaviourTypeField
            ? group is "positive" or "concerning"
            : string.IsNullOrWhiteSpace(group);

    public static string? CategoryName(string? optionGroup) => optionGroup switch
    {
        "positive" => "Positive",
        "concerning" => "Concerning",
        _ => null
    };

    public async Task<List<StudentRecordFieldOption>> ListAsync(string moduleId, string? fieldKey = null) =>
        await context.StudentRecordFieldOptions.AsNoTracking()
            .Where(item => item.ModuleId == moduleId && (fieldKey == null || item.FieldKey == fieldKey))
            .OrderBy(item => item.FieldKey)
            .ThenBy(item => item.SortOrder)
            .ThenBy(item => item.Id)
            .ToListAsync();

    public async Task<string?> CanonicalValueAsync(string moduleId, string fieldKey, string? value)
    {
        return (await FindAsync(moduleId, fieldKey, value))?.Value;
    }

    public async Task<StudentRecordFieldOption?> FindAsync(string moduleId, string fieldKey, string? value)
    {
        var cleaned = value?.Trim();
        if (string.IsNullOrWhiteSpace(cleaned)) return null;
        return await context.StudentRecordFieldOptions.AsNoTracking()
            .Where(item => item.ModuleId == moduleId && item.FieldKey == fieldKey && item.Value == cleaned)
            .FirstOrDefaultAsync();
    }

    public static string? CanonicalValue(IEnumerable<StudentRecordFieldOption> options, string? value)
    {
        var cleaned = value?.Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? null : options
            .FirstOrDefault(item => string.Equals(item.Value, cleaned, StringComparison.OrdinalIgnoreCase))?.Value;
    }
}
