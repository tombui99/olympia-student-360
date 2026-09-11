using System.Globalization;
using System.Text;

namespace OlympiaStudent360.Api.Core.Services;

public sealed class SisRosterSqlScriptGenerator
{
    public string Generate(SisRosterWorkbook workbook, string sourceFileName)
    {
        var replacesCompleteSchool = SisRosterWorkbooks.ReplacesCompleteSchool(workbook);
        var obsoleteStudentCondition = replacesCompleteSchool
            ? "roster.`StudentCode` IS NULL"
            : "roster.`StudentCode` IS NULL AND profile.`Id` IS NOT NULL AND EXISTS (SELECT 1 FROM `_sis_scope_grades` scope WHERE scope.`GradeLevel` = COALESCE(profile.`GradeLevel`, profile.`CanvasGradeLevel`))";
        var sql = new StringBuilder();
        sql.AppendLine("-- Olympia Student 360 authoritative SIS roster import");
        sql.AppendLine("-- Deploy the AddAuthoritativeSisStudentRoster migration before running this script.");
        sql.AppendLine(replacesCompleteSchool
            ? "-- The combined Grades 5-12 roster is authoritative for the complete school."
            : "-- This roster replaces only the grade levels present in the workbook; other grades are preserved.");
        sql.AppendLine("SET NAMES utf8mb4;");
        sql.AppendLine("SET @OLD_SQL_SAFE_UPDATES := @@SQL_SAFE_UPDATES;");
        sql.AppendLine("SET SQL_SAFE_UPDATES = 0;");
        sql.AppendLine("DROP TEMPORARY TABLE IF EXISTS `_sis_roster_stage`;");
        sql.AppendLine("CREATE TEMPORARY TABLE `_sis_roster_stage` (");
        sql.AppendLine("  `StudentCode` varchar(50) NOT NULL PRIMARY KEY,");
        sql.AppendLine("  `Username` varchar(100) NOT NULL UNIQUE,");
        sql.AppendLine("  `FullName` varchar(255) NOT NULL,");
        sql.AppendLine("  `ForeignName` varchar(255) NULL,");
        sql.AppendLine("  `Gender` varchar(50) NULL,");
        sql.AppendLine("  `DateOfBirth` date NOT NULL,");
        sql.AppendLine("  `Homeroom` varchar(100) NOT NULL,");
        sql.AppendLine("  `HomeroomCode` varchar(100) NOT NULL,");
        sql.AppendLine("  `GradeLevel` varchar(50) NOT NULL,");
        sql.AppendLine("  `Email` varchar(255) NOT NULL,");
        sql.AppendLine("  `PhoneNumber` varchar(50) NULL,");
        sql.AppendLine("  `SISStatus` varchar(50) NOT NULL,");
        sql.AppendLine("  `FatherName` varchar(255) NULL, `FatherUsername` varchar(255) NULL,");
        sql.AppendLine("  `FatherPhone` varchar(50) NULL, `FatherEmail` varchar(255) NULL,");
        sql.AppendLine("  `FatherOccupation` varchar(255) NULL, `FatherBirthYear` int NULL,");
        sql.AppendLine("  `MotherName` varchar(255) NULL, `MotherUsername` varchar(255) NULL,");
        sql.AppendLine("  `MotherPhone` varchar(50) NULL, `MotherEmail` varchar(255) NULL,");
        sql.AppendLine("  `MotherOccupation` varchar(255) NULL, `MotherBirthYear` int NULL,");
        sql.AppendLine("  `GuardianName` varchar(255) NULL, `GuardianPhone` varchar(50) NULL,");
        sql.AppendLine("  `GuardianOccupation` varchar(255) NULL, `GuardianBirthYear` int NULL");
        sql.AppendLine(") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;");

        foreach (var batch in workbook.Students.Chunk(100))
        {
            sql.AppendLine("INSERT INTO `_sis_roster_stage` VALUES");
            sql.AppendLine(string.Join(",\n", batch.Select(StudentValues)) + ";");
        }

        sql.AppendLine("DROP TEMPORARY TABLE IF EXISTS `_sis_scope_grades`;");
        sql.AppendLine("CREATE TEMPORARY TABLE `_sis_scope_grades` (`GradeLevel` varchar(50) NOT NULL PRIMARY KEY) ENGINE=InnoDB;");
        sql.AppendLine("INSERT INTO `_sis_scope_grades` (`GradeLevel`) SELECT DISTINCT `GradeLevel` FROM `_sis_roster_stage`;");

        sql.AppendLine("START TRANSACTION;");
        sql.AppendLine("SET @sis_imported_at := UTC_TIMESTAMP(6);");
        sql.AppendLine("DROP TEMPORARY TABLE IF EXISTS `_sis_canvas_identity`;");
        sql.AppendLine("CREATE TEMPORARY TABLE `_sis_canvas_identity` (");
        sql.AppendLine("  `Username` varchar(255) NOT NULL PRIMARY KEY,");
        sql.AppendLine("  `CanvasUserId` bigint NOT NULL,");
        sql.AppendLine("  KEY `IX_sis_canvas_identity_CanvasUserId` (`CanvasUserId`)");
        sql.AppendLine(") ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;");
        sql.AppendLine("INSERT INTO `_sis_canvas_identity` (`Username`, `CanvasUserId`)");
        sql.AppendLine("SELECT `Username`, MIN(`CanvasUserId`) AS `CanvasUserId`");
        sql.AppendLine("FROM (");
        sql.AppendLine("  SELECT `CanvasUserId`, LOWER(SUBSTRING_INDEX(TRIM(COALESCE(NULLIF(`SisUserId`, ''), NULLIF(`LoginId`, ''), `Email`)), '@', 1)) AS `Username`");
        sql.AppendLine("  FROM `CanvasUsers`");
        sql.AppendLine(") identity_rows WHERE `Username` IS NOT NULL AND `Username` <> ''");
        sql.AppendLine("GROUP BY `Username` HAVING COUNT(DISTINCT `CanvasUserId`) = 1;");

        sql.AppendLine("UPDATE `StudentProfiles` profile");
        sql.AppendLine("INNER JOIN `_sis_roster_stage` roster ON");
        sql.AppendLine("  profile.`SISStudentCode` = roster.`StudentCode` OR");
        sql.AppendLine("  (profile.`SISStudentCode` IS NULL AND LOWER(SUBSTRING_INDEX(TRIM(profile.`SISUserId`), '@', 1)) = roster.`Username`)");
        sql.AppendLine("LEFT JOIN `_sis_canvas_identity` canvas ON canvas.`Username` = roster.`Username`");
        sql.AppendLine("LEFT JOIN `StudentProfiles` canvas_owner ON canvas_owner.`CanvasUserId` = canvas.`CanvasUserId` AND canvas_owner.`Id` <> profile.`Id`");
        sql.AppendLine("SET profile.`CanvasUserId` = COALESCE(profile.`CanvasUserId`, IF(canvas_owner.`Id` IS NULL, canvas.`CanvasUserId`, NULL)),");
        sql.AppendLine("    profile.`SISStudentCode` = roster.`StudentCode`, profile.`SISUserId` = roster.`Username`,");
        sql.AppendLine("    profile.`FullName` = roster.`FullName`, profile.`ForeignName` = roster.`ForeignName`,");
        sql.AppendLine("    profile.`Email` = roster.`Email`, profile.`PhoneNumber` = roster.`PhoneNumber`,");
        sql.AppendLine("    profile.`DateOfBirth` = roster.`DateOfBirth`, profile.`Gender` = roster.`Gender`,");
        sql.AppendLine("    profile.`GradeLevel` = roster.`GradeLevel`, profile.`Homeroom` = roster.`Homeroom`,");
        sql.AppendLine("    profile.`HomeroomCode` = roster.`HomeroomCode`, profile.`SISStatus` = roster.`SISStatus`,");
        sql.AppendLine("    profile.`SISLastImportedAtUtc` = @sis_imported_at, profile.`UpdatedAt` = @sis_imported_at;");

        sql.AppendLine("INSERT INTO `StudentProfiles` (`CanvasUserId`, `SISStudentCode`, `SISUserId`, `FullName`, `ForeignName`, `Email`, `PhoneNumber`, `DateOfBirth`, `Gender`, `GradeLevel`, `Homeroom`, `HomeroomCode`, `SISStatus`, `SISLastImportedAtUtc`, `CreatedAt`)");
        sql.AppendLine("SELECT IF(canvas_owner.`Id` IS NULL, canvas.`CanvasUserId`, NULL), roster.`StudentCode`, roster.`Username`, roster.`FullName`, roster.`ForeignName`, roster.`Email`, roster.`PhoneNumber`, roster.`DateOfBirth`, roster.`Gender`, roster.`GradeLevel`, roster.`Homeroom`, roster.`HomeroomCode`, roster.`SISStatus`, @sis_imported_at, @sis_imported_at");
        sql.AppendLine("FROM `_sis_roster_stage` roster");
        sql.AppendLine("LEFT JOIN `StudentProfiles` existing ON existing.`SISStudentCode` = roster.`StudentCode` OR LOWER(SUBSTRING_INDEX(TRIM(existing.`SISUserId`), '@', 1)) = roster.`Username`");
        sql.AppendLine("LEFT JOIN `_sis_canvas_identity` canvas ON canvas.`Username` = roster.`Username`");
        sql.AppendLine("LEFT JOIN `StudentProfiles` canvas_owner ON canvas_owner.`CanvasUserId` = canvas.`CanvasUserId`");
        sql.AppendLine("WHERE existing.`Id` IS NULL;");

        sql.AppendLine("UPDATE `CanvasEnrollments` enrollment INNER JOIN `StudentProfiles` profile ON profile.`CanvasUserId` = enrollment.`CanvasUserId` SET enrollment.`StudentProfileId` = profile.`Id`, enrollment.`WarehouseUpdatedAt` = @sis_imported_at WHERE profile.`SISStudentCode` IS NOT NULL;");
        sql.AppendLine("UPDATE `CanvasSubmissions` submission INNER JOIN `StudentProfiles` profile ON profile.`CanvasUserId` = submission.`CanvasUserId` SET submission.`StudentProfileId` = profile.`Id`, submission.`WarehouseUpdatedAt` = @sis_imported_at WHERE profile.`SISStudentCode` IS NOT NULL;");
        sql.AppendLine("UPDATE `CanvasCourseScores` score INNER JOIN `CanvasEnrollments` enrollment ON enrollment.`CanvasEnrollmentId` = score.`CanvasEnrollmentId` SET score.`StudentProfileId` = enrollment.`StudentProfileId`, score.`WarehouseUpdatedAt` = @sis_imported_at WHERE enrollment.`StudentProfileId` IS NOT NULL;");
        sql.AppendLine("UPDATE `SISHealthVisits` visit INNER JOIN `StudentProfiles` profile ON LOWER(SUBSTRING_INDEX(TRIM(visit.`SISUserId`), '@', 1)) = profile.`SISUserId` SET visit.`StudentProfileId` = profile.`Id` WHERE visit.`SISUserId` IS NOT NULL;");

        sql.AppendLine($"DELETE score FROM `CanvasCourseScores` score LEFT JOIN `StudentProfiles` profile ON profile.`Id` = score.`StudentProfileId` LEFT JOIN `_sis_roster_stage` roster ON roster.`StudentCode` = profile.`SISStudentCode` WHERE {obsoleteStudentCondition};");
        sql.AppendLine($"DELETE submission FROM `CanvasSubmissions` submission LEFT JOIN `StudentProfiles` profile ON profile.`Id` = submission.`StudentProfileId` LEFT JOIN `_sis_roster_stage` roster ON roster.`StudentCode` = profile.`SISStudentCode` WHERE {obsoleteStudentCondition};");
        sql.AppendLine($"DELETE enrollment FROM `CanvasEnrollments` enrollment LEFT JOIN `StudentProfiles` profile ON profile.`Id` = enrollment.`StudentProfileId` LEFT JOIN `_sis_roster_stage` roster ON roster.`StudentCode` = profile.`SISStudentCode` WHERE {obsoleteStudentCondition};");
        sql.AppendLine($"DELETE response FROM `CustomFormResponses` response INNER JOIN `StudentProfiles` profile ON profile.`Id` = response.`StudentProfileId` LEFT JOIN `_sis_roster_stage` roster ON roster.`StudentCode` = profile.`SISStudentCode` WHERE {obsoleteStudentCondition};");
        sql.AppendLine($"DELETE observation FROM `DashboardMetricObservations` observation INNER JOIN `StudentProfiles` profile ON profile.`Id` = observation.`StudentProfileId` LEFT JOIN `_sis_roster_stage` roster ON roster.`StudentCode` = profile.`SISStudentCode` WHERE {obsoleteStudentCondition};");
        sql.AppendLine($"DELETE visit FROM `SISHealthVisits` visit INNER JOIN `StudentProfiles` profile ON profile.`Id` = visit.`StudentProfileId` LEFT JOIN `_sis_roster_stage` roster ON roster.`StudentCode` = profile.`SISStudentCode` WHERE {obsoleteStudentCondition};");
        sql.AppendLine($"UPDATE `WorkspacePreferences` preference INNER JOIN `StudentProfiles` profile ON profile.`Id` = preference.`StudentProfileId` LEFT JOIN `_sis_roster_stage` roster ON roster.`StudentCode` = profile.`SISStudentCode` SET preference.`StudentProfileId` = NULL WHERE {obsoleteStudentCondition};");
        sql.AppendLine($"DELETE profile FROM `StudentProfiles` profile LEFT JOIN `_sis_roster_stage` roster ON roster.`StudentCode` = profile.`SISStudentCode` WHERE {obsoleteStudentCondition};");

        sql.AppendLine("DELETE parent FROM `SISStudentParents` parent INNER JOIN `StudentProfiles` profile ON profile.`Id` = parent.`StudentProfileId` INNER JOIN `_sis_roster_stage` roster ON roster.`StudentCode` = profile.`SISStudentCode`;");
        AppendParentInsert(sql, "Father", "FatherName", "FatherUsername", "FatherPhone", "FatherEmail", "FatherOccupation", "FatherBirthYear");
        AppendParentInsert(sql, "Mother", "MotherName", "MotherUsername", "MotherPhone", "MotherEmail", "MotherOccupation", "MotherBirthYear");
        AppendParentInsert(sql, "Guardian", "GuardianName", null, "GuardianPhone", null, "GuardianOccupation", "GuardianBirthYear");

        sql.AppendLine("INSERT INTO `ImportHistories` (`ModuleId`, `FileName`, `Status`, `ValidRows`, `InvalidRows`, `ImportedByUserId`, `ImportedByUsername`, `ImportedByName`, `CreatedAt`)");
        sql.AppendLine($"VALUES ('sis-students', {Sql(sourceFileName)}, 'Valid', {workbook.Students.Count}, 0, 'rds-workbench', 'rds-workbench', 'RDS Workbench import', @sis_imported_at);");
        sql.AppendLine("COMMIT;");
        sql.AppendLine("DROP TEMPORARY TABLE IF EXISTS `_sis_canvas_identity`;");
        sql.AppendLine("DROP TEMPORARY TABLE IF EXISTS `_sis_scope_grades`;");
        sql.AppendLine("DROP TEMPORARY TABLE IF EXISTS `_sis_roster_stage`;");
        sql.AppendLine("SET SQL_SAFE_UPDATES = @OLD_SQL_SAFE_UPDATES;");
        sql.AppendLine();
        sql.AppendLine("SELECT COUNT(*) AS active_students, COUNT(`SISStudentCode`) AS sis_coded_students, SUM(`CanvasUserId` IS NOT NULL) AS canvas_linked_students FROM `StudentProfiles`;");
        sql.AppendLine("SELECT `GradeLevel`, COUNT(*) AS student_count FROM `StudentProfiles` GROUP BY `GradeLevel` ORDER BY CAST(`GradeLevel` AS UNSIGNED);");
        sql.AppendLine("SELECT COUNT(*) AS parents FROM `SISStudentParents`;");
        sql.AppendLine("SELECT (SELECT COUNT(*) FROM `CanvasEnrollments`) AS canvas_enrollments, (SELECT COUNT(*) FROM `CanvasCourseScores`) AS canvas_scores, (SELECT COUNT(*) FROM `CanvasSubmissions`) AS canvas_submissions;");
        sql.AppendLine("SELECT 'Canvas enrollments without an active SIS student' AS issue, COUNT(*) AS issue_count FROM `CanvasEnrollments` WHERE `StudentProfileId` IS NULL UNION ALL SELECT 'Canvas scores without an active SIS student', COUNT(*) FROM `CanvasCourseScores` WHERE `StudentProfileId` IS NULL UNION ALL SELECT 'Canvas submissions without an active SIS student', COUNT(*) FROM `CanvasSubmissions` WHERE `StudentProfileId` IS NULL;");
        return sql.ToString();
    }

    private static string StudentValues(SisRosterStudentRow student)
    {
        var parents = student.Parents.ToDictionary(parent => parent.Relationship, StringComparer.OrdinalIgnoreCase);
        parents.TryGetValue("Father", out var father);
        parents.TryGetValue("Mother", out var mother);
        parents.TryGetValue("Guardian", out var guardian);
        return $"({Sql(student.StudentCode)}, {Sql(student.Username)}, {Sql(student.FullName)}, {Sql(student.ForeignName)}, " +
            $"{Sql(student.Gender)}, {Sql(student.DateOfBirth.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))}, " +
            $"{Sql(student.Homeroom)}, {Sql(student.HomeroomCode)}, {Sql(student.GradeLevel)}, {Sql(student.Email)}, " +
            $"{Sql(student.PhoneNumber)}, {Sql(student.Status)}, " +
            $"{ParentValues(father)}, {ParentValues(mother)}, " +
            $"{Sql(guardian?.FullName)}, {Sql(guardian?.PhoneNumber)}, {Sql(guardian?.Occupation)}, {Sql(guardian?.BirthYear)})";
    }

    private static string ParentValues(SisRosterParentRow? parent) =>
        $"{Sql(parent?.FullName)}, {Sql(parent?.Username)}, {Sql(parent?.PhoneNumber)}, " +
        $"{Sql(parent?.Email)}, {Sql(parent?.Occupation)}, {Sql(parent?.BirthYear)}";

    private static void AppendParentInsert(
        StringBuilder sql,
        string relationship,
        string nameColumn,
        string? usernameColumn,
        string phoneColumn,
        string? emailColumn,
        string occupationColumn,
        string birthYearColumn)
    {
        sql.AppendLine("INSERT INTO `SISStudentParents` (`StudentProfileId`, `SISUserId`, `FullName`, `Relationship`, `Username`, `PhoneNumber`, `Email`, `BirthYear`, `Occupation`, `CreatedAt`)");
        sql.AppendLine($"SELECT profile.`Id`, roster.`Username`, roster.`{nameColumn}`, '{relationship}', " +
            (usernameColumn is null ? "NULL" : $"roster.`{usernameColumn}`") + ", " +
            $"roster.`{phoneColumn}`, " + (emailColumn is null ? "NULL" : $"roster.`{emailColumn}`") +
            $", roster.`{birthYearColumn}`, roster.`{occupationColumn}`, @sis_imported_at ");
        sql.AppendLine($"FROM `_sis_roster_stage` roster INNER JOIN `StudentProfiles` profile ON profile.`SISStudentCode` = roster.`StudentCode` WHERE roster.`{nameColumn}` IS NOT NULL;");
    }

    private static string Sql(string? value) => value is null
        ? "NULL"
        : "'" + value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("'", "''", StringComparison.Ordinal)
            .Replace("\0", string.Empty, StringComparison.Ordinal) + "'";

    private static string Sql(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "NULL";
}
