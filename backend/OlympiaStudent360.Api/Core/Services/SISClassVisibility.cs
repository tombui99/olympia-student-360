using OlympiaStudent360.Api.Core.Entities;

namespace OlympiaStudent360.Api.Core.Services;

public static class SISClassVisibility
{
    public static string? TeacherNames(StudentProfile student)
    {
        var names = student.SISClassMemberships
            .SelectMany(membership => membership.SISClass.Teachers)
            .Select(assignment => assignment.Teacher.FullName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return names.Count == 0 ? null : string.Join(", ", names);
    }
}
