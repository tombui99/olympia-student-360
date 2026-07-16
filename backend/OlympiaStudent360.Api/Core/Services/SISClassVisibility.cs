using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Core.Services;

public static class SISClassVisibility
{
    public static IQueryable<int> StudentIds(AppDbContext context, string? teacherUserId) =>
        context.SISClassStudents.AsNoTracking()
            .Where(membership => membership.SISClass.IsActive &&
                membership.SISClass.Teachers.Any(assignment =>
                    assignment.TeacherUserId == teacherUserId))
            .Select(membership => membership.StudentProfileId)
            .Distinct();

    public static IQueryable<StudentProfile> Students(AppDbContext context, string? teacherUserId) =>
        context.StudentProfiles.AsNoTracking()
            .Where(student => student.SISClassMemberships.Any(membership =>
                membership.SISClass.IsActive && membership.SISClass.Teachers.Any(assignment =>
                    assignment.TeacherUserId == teacherUserId)));

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
