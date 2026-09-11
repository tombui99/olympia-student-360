using Microsoft.EntityFrameworkCore;
using OlympiaStudent360.Api.Core.Entities;
using OlympiaStudent360.Api.Data;

namespace OlympiaStudent360.Api.Core.Services;

public sealed record SisRosterImportActor(string UserId, string Username, string FullName);

public sealed record SisRosterImportResult(
    int ImportHistoryId,
    int StudentsImported,
    int StudentsAdded,
    int StudentsUpdated,
    int StudentsRemoved,
    int ParentsImported,
    int CanvasLinkedStudents,
    int CanvasFactsRemoved);

public sealed class SisRosterImportService(AppDbContext db)
{
    public async Task<SisRosterImportResult> Import(
        SisRosterWorkbook workbook,
        string fileName,
        SisRosterImportActor actor,
        CancellationToken cancellationToken = default)
    {
        if (workbook.Students.Count == 0)
            throw new SisRosterValidationException(["The SIS workbook does not contain any students."]);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var existingProfiles = await db.StudentProfiles.ToListAsync(cancellationToken);
        var profilesByCode = existingProfiles
            .Where(profile => !string.IsNullOrWhiteSpace(profile.SISStudentCode))
            .ToDictionary(profile => profile.SISStudentCode!, StringComparer.OrdinalIgnoreCase);
        var profilesByUsername = existingProfiles
            .Select(profile => new { Profile = profile, Username = StudentIdentityNormalizer.SchoolUsername(profile.SISUserId) })
            .Where(item => item.Username is not null)
            .ToDictionary(item => item.Username!, item => item.Profile, StringComparer.OrdinalIgnoreCase);
        var profilesByCanvasId = existingProfiles
            .Where(profile => profile.CanvasUserId is not null)
            .ToDictionary(profile => profile.CanvasUserId!.Value);

        var canvasUsers = await db.CanvasUsers.AsNoTracking().ToListAsync(cancellationToken);
        var canvasByUsername = BuildCanvasUserMap(canvasUsers, workbook.Students);
        var activeProfiles = new List<StudentProfile>(workbook.Students.Count);
        var studentRowsByProfile = new Dictionary<StudentProfile, SisRosterStudentRow>();
        var errors = new List<string>();
        var added = 0;
        var updated = 0;

        foreach (var row in workbook.Students)
        {
            profilesByCode.TryGetValue(row.StudentCode, out var codeMatch);
            profilesByUsername.TryGetValue(row.Username, out var usernameMatch);
            canvasByUsername.TryGetValue(row.Username, out var canvasMatch);
            StudentProfile? canvasProfile = null;
            if (canvasMatch is not null)
                profilesByCanvasId.TryGetValue(canvasMatch.CanvasUserId, out canvasProfile);

            var profile = codeMatch ?? usernameMatch ?? canvasProfile;
            var matches = new[] { codeMatch, usernameMatch, canvasProfile }
                .Where(match => match is not null)
                .Select(match => match!.Id)
                .Distinct()
                .ToList();
            if (matches.Count > 1)
            {
                errors.Add(
                    $"Student '{row.StudentCode}' ({row.Username}) matches conflicting existing profiles: " +
                    string.Join(", ", matches));
                continue;
            }

            if (profile is null)
            {
                profile = new StudentProfile { CreatedAt = now };
                db.StudentProfiles.Add(profile);
                added++;
            }
            else
            {
                updated++;
            }

            if (canvasMatch is not null && profile.CanvasUserId is { } assignedCanvasId &&
                assignedCanvasId != canvasMatch.CanvasUserId)
            {
                errors.Add(
                    $"Student '{row.StudentCode}' is already linked to Canvas user {assignedCanvasId}, " +
                    $"but the SIS email matches Canvas user {canvasMatch.CanvasUserId}.");
                continue;
            }
            if (canvasMatch is not null && profilesByCanvasId.TryGetValue(canvasMatch.CanvasUserId, out var owner) &&
                owner != profile)
            {
                errors.Add(
                    $"Canvas user {canvasMatch.CanvasUserId} for '{row.Username}' is already linked to another profile.");
                continue;
            }

            profile.CanvasUserId ??= canvasMatch?.CanvasUserId;
            profile.SISStudentCode = row.StudentCode;
            profile.SISUserId = row.Username;
            profile.FullName = row.FullName;
            profile.ForeignName = row.ForeignName;
            profile.Email = row.Email;
            profile.PhoneNumber = row.PhoneNumber;
            profile.DateOfBirth = DateTime.SpecifyKind(row.DateOfBirth.Date, DateTimeKind.Utc);
            profile.Gender = row.Gender;
            profile.GradeLevel = row.GradeLevel;
            profile.Homeroom = row.Homeroom;
            profile.HomeroomCode = row.HomeroomCode;
            profile.SISStatus = row.Status;
            profile.SISLastImportedAtUtc = now;
            profile.UpdatedAt = profile.Id == 0 ? null : now;

            activeProfiles.Add(profile);
            studentRowsByProfile[profile] = row;
        }

        if (errors.Count > 0)
            throw new SisRosterValidationException(errors);

        await db.SaveChangesAsync(cancellationToken);
        var activeProfileIds = activeProfiles.Select(profile => profile.Id).ToArray();
        var importedGrades = SisRosterWorkbooks.GradeScope(workbook);
        var replacesCompleteSchool = SisRosterWorkbooks.ReplacesCompleteSchool(workbook);
        var obsoleteProfileIds = existingProfiles
            .Where(profile => !activeProfileIds.Contains(profile.Id) &&
                (replacesCompleteSchool || importedGrades.Contains(profile.GradeLevel ?? profile.CanvasGradeLevel ?? string.Empty)))
            .Select(profile => profile.Id)
            .ToArray();

        // Link any historical Canvas facts that arrived before this SIS roster, including Canvas
        // users whose sis_user_id contains the full school email rather than only its username.
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE `CanvasEnrollments` e
            INNER JOIN `StudentProfiles` p ON p.`CanvasUserId` = e.`CanvasUserId`
            SET e.`StudentProfileId` = p.`Id`, e.`WarehouseUpdatedAt` = UTC_TIMESTAMP(6)
            WHERE p.`SISStudentCode` IS NOT NULL
            """, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE `CanvasSubmissions` s
            INNER JOIN `StudentProfiles` p ON p.`CanvasUserId` = s.`CanvasUserId`
            SET s.`StudentProfileId` = p.`Id`, s.`WarehouseUpdatedAt` = UTC_TIMESTAMP(6)
            WHERE p.`SISStudentCode` IS NOT NULL
            """, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE `CanvasCourseScores` score
            INNER JOIN `CanvasEnrollments` e ON e.`CanvasEnrollmentId` = score.`CanvasEnrollmentId`
            SET score.`StudentProfileId` = e.`StudentProfileId`, score.`WarehouseUpdatedAt` = UTC_TIMESTAMP(6)
            WHERE e.`StudentProfileId` IS NOT NULL
            """, cancellationToken);
        await db.Database.ExecuteSqlRawAsync("""
            UPDATE `SISHealthVisits` visit
            INNER JOIN `StudentProfiles` p
              ON LOWER(SUBSTRING_INDEX(TRIM(visit.`SISUserId`), '@', 1)) = p.`SISUserId`
            SET visit.`StudentProfileId` = p.`Id`
            WHERE visit.`SISUserId` IS NOT NULL
            """, cancellationToken);

        db.ChangeTracker.Clear();
        var canvasFactsRemoved = 0;
        if (replacesCompleteSchool)
        {
            canvasFactsRemoved += await db.CanvasCourseScores
                .Where(score => score.StudentProfileId == null ||
                    obsoleteProfileIds.Contains(score.StudentProfileId.Value))
                .ExecuteDeleteAsync(cancellationToken);
            canvasFactsRemoved += await db.CanvasSubmissions
                .Where(submission => submission.StudentProfileId == null ||
                    obsoleteProfileIds.Contains(submission.StudentProfileId.Value))
                .ExecuteDeleteAsync(cancellationToken);
            canvasFactsRemoved += await db.CanvasEnrollments
                .Where(enrollment => enrollment.StudentProfileId == null ||
                    obsoleteProfileIds.Contains(enrollment.StudentProfileId.Value))
                .ExecuteDeleteAsync(cancellationToken);
        }
        else
        {
            canvasFactsRemoved += await db.CanvasCourseScores
                .Where(score => score.StudentProfileId != null &&
                    obsoleteProfileIds.Contains(score.StudentProfileId.Value))
                .ExecuteDeleteAsync(cancellationToken);
            canvasFactsRemoved += await db.CanvasSubmissions
                .Where(submission => submission.StudentProfileId != null &&
                    obsoleteProfileIds.Contains(submission.StudentProfileId.Value))
                .ExecuteDeleteAsync(cancellationToken);
            canvasFactsRemoved += await db.CanvasEnrollments
                .Where(enrollment => enrollment.StudentProfileId != null &&
                    obsoleteProfileIds.Contains(enrollment.StudentProfileId.Value))
                .ExecuteDeleteAsync(cancellationToken);
        }

        await db.CustomFormResponses
            .Where(response => obsoleteProfileIds.Contains(response.StudentProfileId))
            .ExecuteDeleteAsync(cancellationToken);
        await db.DashboardMetricObservations
            .Where(observation => observation.StudentProfileId != null &&
                obsoleteProfileIds.Contains(observation.StudentProfileId.Value))
            .ExecuteDeleteAsync(cancellationToken);
        await db.SISHealthVisits
            .Where(visit => visit.StudentProfileId != null &&
                obsoleteProfileIds.Contains(visit.StudentProfileId.Value))
            .ExecuteDeleteAsync(cancellationToken);
        await db.WorkspacePreferences
            .Where(preference => preference.StudentProfileId != null &&
                obsoleteProfileIds.Contains(preference.StudentProfileId.Value))
            .ExecuteUpdateAsync(setters => setters.SetProperty(preference => preference.StudentProfileId, (int?)null),
                cancellationToken);

        var removed = await db.StudentProfiles
            .Where(profile => obsoleteProfileIds.Contains(profile.Id))
            .ExecuteDeleteAsync(cancellationToken);

        await db.SISStudentParents
            .Where(parent => activeProfileIds.Contains(parent.StudentProfileId))
            .ExecuteDeleteAsync(cancellationToken);
        var parentRows = activeProfiles.SelectMany(profile =>
            studentRowsByProfile[profile].Parents.Select(parent => new SISStudentParent
            {
                StudentProfileId = profile.Id,
                SISUserId = studentRowsByProfile[profile].Username,
                FullName = parent.FullName,
                Relationship = parent.Relationship,
                Username = parent.Username,
                PhoneNumber = parent.PhoneNumber,
                Email = parent.Email,
                BirthYear = parent.BirthYear,
                Occupation = parent.Occupation,
                CreatedAt = now
            })).ToList();
        db.SISStudentParents.AddRange(parentRows);

        var history = new ImportHistory
        {
            ModuleId = "sis-students",
            FileName = fileName.Trim()[..Math.Min(fileName.Trim().Length, 255)],
            Status = "Valid",
            ValidRows = workbook.Students.Count,
            InvalidRows = 0,
            ImportedByUserId = actor.UserId,
            ImportedByUsername = actor.Username,
            ImportedByName = actor.FullName,
            CreatedAt = now
        };
        db.ImportHistories.Add(history);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new SisRosterImportResult(
            history.Id,
            workbook.Students.Count,
            added,
            updated,
            removed,
            parentRows.Count,
            activeProfiles.Count(profile => profile.CanvasUserId is not null),
            canvasFactsRemoved);
    }

    private static Dictionary<string, CanvasUser> BuildCanvasUserMap(
        IEnumerable<CanvasUser> canvasUsers,
        IReadOnlyCollection<SisRosterStudentRow> students)
    {
        var required = students.Select(student => student.Username).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var matches = new Dictionary<string, CanvasUser>(StringComparer.OrdinalIgnoreCase);
        foreach (var canvasUser in canvasUsers)
        {
            var candidates = new[] { canvasUser.SisUserId, canvasUser.LoginId, canvasUser.Email }
                .Select(StudentIdentityNormalizer.SchoolUsername)
                .Where(username => username is not null && required.Contains(username))
                .Distinct(StringComparer.OrdinalIgnoreCase);
            foreach (var username in candidates)
            {
                if (matches.TryGetValue(username!, out var existing) &&
                    existing.CanvasUserId != canvasUser.CanvasUserId)
                {
                    throw new SisRosterValidationException(
                        [$"SIS username '{username}' matches multiple Canvas users ({existing.CanvasUserId} and {canvasUser.CanvasUserId})."]);
                }
                matches[username!] = canvasUser;
            }
        }
        return matches;
    }
}
