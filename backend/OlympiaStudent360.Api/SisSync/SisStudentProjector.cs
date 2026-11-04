using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Core.Services;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.SisSync;

public sealed class SisStudentProjector(AppDbContext db)
{
    public async Task ProjectAsync(JsonElement source, CancellationToken ct)
    {
        var userId = SisJson.RequiredId(source, "student_user_id");
        var schoolId = SisJson.RequiredId(SisJson.Child(source, "school"));
        var yearId = SisJson.RequiredId(SisJson.Child(source, "school_year"));
        var active = SisJson.RequiredBool(source, "is_active");
        var code = SisJson.Text(source, "student_code", 50);
        var email = SisJson.Text(source, "email", 255);
        var username = StudentIdentityNormalizer.SchoolUsername(email) ??
            StudentIdentityNormalizer.SchoolUsername(SisJson.Text(source, "username", 255));
        if (username?.Length > 100) throw new SisApiException("SIS school username exceeds the warehouse limit.");
        var deleted = SisJson.RequiredBool(source, "is_deleted");
        var candidates = await db.StudentProfiles.Where(x => x.SchoolOnlineUserId == userId ||
            code != null && x.SISStudentCode == code || !deleted && username != null && x.SISUserId == username).ToListAsync(ct);
        if (candidates.Count > 1 || candidates.Any(x => x.SchoolOnlineUserId is not null && x.SchoolOnlineUserId != userId))
            throw new SisApiException("SIS identity conflict: stable ID, student code and school username must identify one profile. No name-based matching is allowed.");
        var student = candidates.SingleOrDefault();
        // Deleted accounts can reuse an active student's email prefix. Their source document
        // is retained by the caller, but a tombstone must only touch an established stable ID
        // or permanent code. Do not create a new application profile for an unlinked tombstone.
        if (deleted && student is null) return;
        if (student is null)
        {
            student = new StudentProfile { SISUserId = username ?? "schoolonline:" + userId };
            if (student.SISUserId.Length > 100) throw new SisApiException("SIS fallback username exceeds the warehouse limit.");
            db.StudentProfiles.Add(student);
        }
        student.SchoolOnlineUserId = userId;
        student.SchoolOnlineSchoolId = schoolId;
        student.SchoolOnlineSchoolYearId = yearId;
        student.SISStudentCode = code;
        if (!deleted && username is not null) student.SISUserId = username;
        student.FullName = SisJson.Text(source, "full_name", 255) ?? "";
        student.ForeignName = SisJson.Text(source, "foreign_name", 255);
        student.Email = email;
        student.PhoneNumber = SisJson.Text(source, "contact_phone", 50);
        student.DateOfBirth = SisJson.Date(source, "date_of_birth");
        student.Gender = SisJson.Text(source, "gender", 50);
        student.GradeLevel = Grade(SisJson.Child(source, "grade"));
        student.EducationLevel = EducationLevel(student.GradeLevel);
        student.Homeroom = SisJson.Text(SisJson.Child(source, "homeroom_class"), "name", 100);
        student.HomeroomCode = SisJson.Text(SisJson.Child(source, "homeroom_class"), "code", 100);
        student.SISIsDeleted = deleted;
        student.SISIsActive = active;
        student.SISStatus = student.SISIsDeleted ? "DELETED" : SisJson.Text(source, "status", 50);
        student.SISLastImportedAtUtc = DateTime.UtcNow;
        student.UpdatedAt = DateTime.UtcNow;
        // Save inside the caller's transaction to allocate a new profile ID before parents.
        await db.SaveChangesAsync(ct);
        var parents = await db.SISStudentParents.IgnoreQueryFilters().Where(x => x.StudentProfileId == student.Id).ToListAsync(ct);
        foreach (var role in new[] { "father", "mother", "guardian" })
        {
            if (!source.TryGetProperty(role, out var value)) throw new SisApiException("SIS student response is missing a parent field.");
            var matches = parents.Where(x => ParentRole(x.Relationship) == role).ToArray();
            if (matches.Length > 1) throw new SisApiException("SIS parent relationship is ambiguous; review existing contacts before syncing.");
            var parent = matches.SingleOrDefault();
            if (value.ValueKind == JsonValueKind.Null)
            {
                // Keep the row and staff-authored note, but hide a withdrawn source contact.
                if (parent is not null) parent.SISIsDeleted = true;
                continue;
            }
            if (value.ValueKind != JsonValueKind.Object) throw new SisApiException("Invalid SIS parent object.");
            if (parent is null)
            {
                parent = new SISStudentParent { StudentProfileId = student.Id, Relationship = role };
                db.SISStudentParents.Add(parent);
            }
            parent.SISIsDeleted = student.SISIsDeleted;
            parent.SISUserId = student.SISUserId;
            parent.FullName = SisJson.Text(value, "full_name", 255) ?? "";
            parent.Username = SisJson.Text(value, "username", 255);
            parent.PhoneNumber = SisJson.Text(value, "phone", 50);
            parent.Email = SisJson.Text(value, "email", 255);
            parent.Occupation = SisJson.Text(value, "job", 255);
            parent.BirthYear = value.TryGetProperty("birth_year", out var birth) && birth.ValueKind != JsonValueKind.Null
                ? birth.GetInt32() : null;
            // These former export attributes are not supplied by the API.
            parent.AgeGroup = null;
            parent.EmploymentSector = null;
            parent.UpdatedAt = DateTime.UtcNow;
        }
        // Only the three contacts in the API contract remain live; keep other
        // exported contact rows and their notes as archived context.
        foreach (var parent in parents.Where(x => ParentRole(x.Relationship) is null))
            parent.SISIsDeleted = true;
    }

    private static string? ParentRole(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "father" or "cha" or "bố" or "ba" => "father",
        "mother" or "mẹ" or "má" => "mother",
        "guardian" or "người giám hộ" or "giám hộ" => "guardian",
        _ => null
    };

    internal static string? Grade(JsonElement value)
    {
        var code = SisJson.Text(value, "code", 50);
        if (int.TryParse(code, out var number) && number is >= 1 and <= 12) return number.ToString(CultureInfo.InvariantCulture);
        var name = SisJson.Text(value, "name", 50);
        var match = Regex.Match(name ?? code ?? "", @"(?<!\d)(1[0-2]|[1-9])(?!\d)");
        return match.Success ? match.Value : name ?? code;
    }

    internal static string? EducationLevel(string? grade) =>
        TeacherSchoolLevels.Values.FirstOrDefault(level => TeacherSchoolLevels.IncludesGrade(level, grade));
}

internal static class SisJson
{
    internal static JsonElement Child(JsonElement value, string key) =>
        value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out var child) ? child : default;
    internal static string? Text(JsonElement value, string key, int max = 100)
    {
        var child = Child(value, key);
        if (child.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (child.ValueKind != JsonValueKind.String) throw new SisApiException($"SIS field {key} must be a string.");
        var text = child.GetString();
        if (text?.Length > max) throw new SisApiException($"SIS field {key} exceeds the warehouse limit.");
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }
    internal static string RequiredId(JsonElement value, string key = "id") =>
        Text(value, key) ?? throw new SisApiException($"SIS record is missing {key}.");
    internal static string? RefId(JsonElement value, string key) => Text(Child(value, key), "id");
    internal static bool? Bool(JsonElement value, string key)
    {
        var child = Child(value, key);
        return child.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? null : child.GetBoolean();
    }
    internal static bool RequiredBool(JsonElement value, string key) => Bool(value, key) ?? throw new SisApiException($"SIS record is missing {key}.");
    internal static DateTime? Date(JsonElement value, string key)
    {
        var text = Text(value, key, 10);
        return text is null ? null : DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date : throw new SisApiException($"Invalid SIS date in {key}.");
    }
}
