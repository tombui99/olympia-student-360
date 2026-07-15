using System.Text;
using System.Text.Json;
using OlympiaStudent360.Api.Core.Entities;

namespace OlympiaStudent360.Api.Core.Services;

/// <summary>
/// Validates and serialises admin-defined custom field values against a module's field
/// definitions. Values live in a JSON object (fieldKey -> value) rather than columns.
/// </summary>
public static class ImportCustomFieldService
{
    public static readonly IReadOnlyList<string> Modules =
        ["student-behaviours", "student-projects", "student-mental-health", "student-bus-attendance"];

    public static readonly IReadOnlyList<string> FieldTypes =
        ["text", "textarea", "number", "date", "select", "checkbox"];

    public static bool IsKnownModule(string? moduleId) =>
        moduleId != null && Modules.Contains(moduleId);

    /// <summary>Turn a label into a stable snake_case key.</summary>
    public static string Slugify(string label)
    {
        var builder = new StringBuilder();
        var lastUnderscore = false;
        foreach (var ch in label.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                lastUnderscore = false;
            }
            else if (!lastUnderscore && builder.Length > 0)
            {
                builder.Append('_');
                lastUnderscore = true;
            }
        }
        var result = builder.ToString().Trim('_');
        return string.IsNullOrEmpty(result) ? "field" : result;
    }

    public static List<string> ParseOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson)) return [];
        try { return JsonSerializer.Deserialize<List<string>>(optionsJson) ?? []; }
        catch { return []; }
    }

    /// <summary>
    /// Validates the submitted values against the field definitions and produces the JSON to
    /// persist. <paramref name="rowPrefix"/> is prepended to errors (e.g. "Row 3: ").
    /// </summary>
    public static (List<string> Errors, string? Json) Build(
        IReadOnlyList<ImportModuleField> fields,
        IDictionary<string, string?>? values,
        string rowPrefix = "")
    {
        var errors = new List<string>();
        var result = new Dictionary<string, string>();

        foreach (var field in fields)
        {
            var raw = FindValue(values, field.FieldKey);
            var value = raw?.Trim() ?? string.Empty;

            if (string.IsNullOrEmpty(value))
            {
                if (field.Required)
                {
                    errors.Add($"{rowPrefix}{field.Label} is required.");
                }
                continue;
            }

            switch (field.FieldType)
            {
                case "number":
                    if (!double.TryParse(value, out _))
                        errors.Add($"{rowPrefix}{field.Label} must be a number.");
                    else
                        result[field.FieldKey] = value;
                    break;
                case "date":
                    if (!DateTime.TryParse(value, out _))
                        errors.Add($"{rowPrefix}{field.Label} must be a valid date.");
                    else
                        result[field.FieldKey] = value;
                    break;
                case "select":
                    var options = ParseOptions(field.OptionsJson);
                    var matched = options.FirstOrDefault(o => string.Equals(o, value, StringComparison.OrdinalIgnoreCase));
                    if (matched == null)
                        errors.Add($"{rowPrefix}{field.Label} must be one of: {string.Join(", ", options)}.");
                    else
                        result[field.FieldKey] = matched;
                    break;
                case "checkbox":
                    var truthy = value is "true" or "1" or "yes" or "y" or "on";
                    result[field.FieldKey] = truthy ? "true" : "false";
                    break;
                default:
                    result[field.FieldKey] = value;
                    break;
            }
        }

        var json = result.Count == 0 ? null : JsonSerializer.Serialize(result);
        return (errors, json);
    }

    private static string? FindValue(IDictionary<string, string?>? values, string fieldKey)
    {
        if (values == null) return null;
        if (values.TryGetValue(fieldKey, out var exact)) return exact;
        foreach (var pair in values)
        {
            if (string.Equals(pair.Key, fieldKey, StringComparison.OrdinalIgnoreCase))
                return pair.Value;
        }
        return null;
    }

    /// <summary>Deserialise a stored CustomFieldsJson back into a dictionary for display.</summary>
    public static Dictionary<string, string> Parse(string? customFieldsJson)
    {
        if (string.IsNullOrWhiteSpace(customFieldsJson)) return [];
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(customFieldsJson) ?? []; }
        catch { return []; }
    }
}
