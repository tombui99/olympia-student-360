using System.Globalization;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;

namespace OlympiaStudent360.Api.Core.Services;

public sealed record SisRosterParentRow(
    string Relationship,
    string FullName,
    string? Username,
    string? PhoneNumber,
    string? Email,
    string? Occupation,
    int? BirthYear);

public sealed record SisRosterStudentRow(
    string StudentCode,
    string Username,
    string FullName,
    string? ForeignName,
    string? Gender,
    DateTime DateOfBirth,
    string Homeroom,
    string HomeroomCode,
    string GradeLevel,
    string Email,
    string? PhoneNumber,
    string Status,
    IReadOnlyList<SisRosterParentRow> Parents);

public sealed record SisRosterWorkbook(IReadOnlyList<SisRosterStudentRow> Students);

public static class SisRosterWorkbooks
{
    private static readonly HashSet<string> CompleteSchoolGrades =
        Enumerable.Range(5, 8).Select(grade => grade.ToString(CultureInfo.InvariantCulture)).ToHashSet();

    public static SisRosterWorkbook Combine(IEnumerable<SisRosterWorkbook> workbooks)
    {
        var students = workbooks.SelectMany(workbook => workbook.Students).ToList();
        var errors = students.GroupBy(student => student.StudentCode, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => $"Student code '{group.Key}' appears in more than one workbook.")
            .Concat(students.GroupBy(student => student.Username, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => $"Student email username '{group.Key}' appears in more than one workbook."))
            .ToList();
        if (errors.Count > 0)
            throw new SisRosterValidationException(errors);
        return new SisRosterWorkbook(students);
    }

    public static HashSet<string> GradeScope(SisRosterWorkbook workbook) =>
        workbook.Students.Select(student => student.GradeLevel)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static bool ReplacesCompleteSchool(SisRosterWorkbook workbook) =>
        GradeScope(workbook).SetEquals(CompleteSchoolGrades);
}

public sealed class SisRosterValidationException : Exception
{
    public SisRosterValidationException(IReadOnlyList<string> errors)
        : base("The SIS workbook failed validation.")
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}

public sealed partial class SisRosterWorkbookReader
{
    private const int MaxRows = 5000;

    private static readonly string[] RequiredHeaders =
    [
        "code", "fullname", "foreign_name", "gender", "date_of_birth",
        "homeroom_class", "code_homeroom_class", "email",
        "fa_fullname", "fa_username", "fa_phone", "fa_email", "father_job", "NAM_SINH_CHA",
        "mo_fullname", "mo_username", "mo_phone", "mo_email", "mother_job", "NAM_SINH_ME",
        "tutor_fullname", "tutor_job", "NAM_SINH_NGUOI_DO_DAU", "tutor_phone", "phone", "status"
    ];

    public SisRosterWorkbook Read(Stream stream)
    {
        using var document = SpreadsheetDocument.Open(stream, false);
        var workbookPart = document.WorkbookPart
            ?? throw new SisRosterValidationException(["The Excel workbook is missing its workbook definition."]);
        var workbookDefinition = workbookPart.Workbook
            ?? throw new SisRosterValidationException(["The Excel workbook definition is empty."]);
        var firstSheet = workbookDefinition.Sheets?.Elements<Sheet>().FirstOrDefault()
            ?? throw new SisRosterValidationException(["The Excel workbook does not contain a worksheet."]);
        var worksheetPart = (WorksheetPart)workbookPart.GetPartById(firstSheet.Id!);
        var sharedStrings = workbookPart.SharedStringTablePart?.SharedStringTable;
        var worksheet = worksheetPart.Worksheet
            ?? throw new SisRosterValidationException(["The first Excel worksheet is empty."]);
        var rows = worksheet.GetFirstChild<SheetData>()?.Elements<Row>().ToList() ?? [];

        var headerIndex = rows.FindIndex(row =>
        {
            var values = ReadRow(row, sharedStrings);
            return values.Values.Any(value => string.Equals(value, "code", StringComparison.OrdinalIgnoreCase)) &&
                values.Values.Any(value => string.Equals(value, "email", StringComparison.OrdinalIgnoreCase));
        });
        if (headerIndex < 0)
        {
            throw new SisRosterValidationException(
                ["The technical header row containing 'code' and 'email' was not found."]);
        }

        var headerValues = ReadRow(rows[headerIndex], sharedStrings);
        var headers = headerValues
            .Where(item => !string.IsNullOrWhiteSpace(item.Value))
            .GroupBy(item => item.Value!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Key, StringComparer.OrdinalIgnoreCase);
        var missingHeaders = RequiredHeaders.Where(header => !headers.ContainsKey(header)).ToList();
        if (missingHeaders.Count > 0)
        {
            throw new SisRosterValidationException(
                [$"The workbook is missing required columns: {string.Join(", ", missingHeaders)}."]);
        }

        var errors = new List<string>();
        var students = new List<SisRosterStudentRow>();
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var usernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows.Skip(headerIndex + 1))
        {
            var values = ReadRow(row, sharedStrings);
            string? Value(string key) => Clean(values.GetValueOrDefault(headers[key]));
            var studentCode = Value("code");
            var fullName = Value("fullname");
            var email = Value("email");

            // Olympia SIS exports a Vietnamese display-label row immediately below the
            // machine-readable headers. It is documentation, not a student record.
            if (string.Equals(studentCode, "Mã học sinh", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            if (studentCode is null && fullName is null && email is null)
            {
                continue;
            }
            if (students.Count >= MaxRows)
            {
                errors.Add($"The workbook contains more than the supported maximum of {MaxRows:N0} student rows.");
                break;
            }

            var rowNumber = row.RowIndex?.Value ?? (uint)(headerIndex + students.Count + 2);
            if (studentCode is null) errors.Add($"Row {rowNumber}: code is required.");
            if (fullName is null) errors.Add($"Row {rowNumber}: fullname is required.");
            if (email is null) errors.Add($"Row {rowNumber}: email is required.");
            var username = StudentIdentityNormalizer.SchoolUsername(email);
            if (email is not null && (!email.Contains('@') || username is null))
                errors.Add($"Row {rowNumber}: email is invalid.");

            var homeroom = Value("homeroom_class");
            var homeroomCode = Value("code_homeroom_class");
            if (homeroom is null) errors.Add($"Row {rowNumber}: homeroom_class is required.");
            if (homeroomCode is null) errors.Add($"Row {rowNumber}: code_homeroom_class is required.");
            var gradeMatch = GradePattern().Match(homeroomCode ?? homeroom ?? string.Empty);
            if (!gradeMatch.Success) errors.Add($"Row {rowNumber}: the grade could not be derived from the homeroom code.");

            var birthText = Value("date_of_birth");
            if (!TryDate(birthText, out var birthDate))
                errors.Add($"Row {rowNumber}: date_of_birth must be a valid date.");

            if (studentCode is not null && !codes.Add(studentCode))
                errors.Add($"Row {rowNumber}: duplicate student code '{studentCode}'.");
            if (username is not null && !usernames.Add(username))
                errors.Add($"Row {rowNumber}: duplicate student email username '{username}'.");

            ValidateLength(errors, rowNumber, "code", studentCode, 50);
            ValidateLength(errors, rowNumber, "fullname", fullName, 255);
            ValidateLength(errors, rowNumber, "foreign_name", Value("foreign_name"), 255);
            ValidateLength(errors, rowNumber, "email", email, 255);
            ValidateLength(errors, rowNumber, "email username", username, 100);
            ValidateLength(errors, rowNumber, "gender", Value("gender"), 50);
            ValidateLength(errors, rowNumber, "homeroom_class", homeroom, 100);
            ValidateLength(errors, rowNumber, "code_homeroom_class", homeroomCode, 100);
            ValidateLength(errors, rowNumber, "phone", Value("phone"), 50);
            ValidateLength(errors, rowNumber, "status", Value("status"), 50);

            var parents = new List<SisRosterParentRow>();
            AddParent(parents, errors, rowNumber, "Father", Value("fa_fullname"),
                Value("fa_username"), Value("fa_phone"), Value("fa_email"),
                Value("father_job"), Value("NAM_SINH_CHA"));
            AddParent(parents, errors, rowNumber, "Mother", Value("mo_fullname"),
                Value("mo_username"), Value("mo_phone"), Value("mo_email"),
                Value("mother_job"), Value("NAM_SINH_ME"));
            AddParent(parents, errors, rowNumber, "Guardian", Value("tutor_fullname"),
                null, Value("tutor_phone"), null,
                Value("tutor_job"), Value("NAM_SINH_NGUOI_DO_DAU"));

            if (studentCode is null || fullName is null || email is null || username is null ||
                homeroom is null || homeroomCode is null || !gradeMatch.Success || birthDate is null)
            {
                continue;
            }

            students.Add(new SisRosterStudentRow(
                studentCode,
                username,
                fullName,
                Value("foreign_name"),
                Value("gender"),
                birthDate.Value,
                homeroom,
                homeroomCode,
                gradeMatch.Groups["grade"].Value,
                email,
                Value("phone"),
                Value("status") ?? "Đang học",
                parents));
        }

        if (students.Count == 0 && errors.Count == 0)
            errors.Add("The workbook does not contain any student rows.");
        if (errors.Count > 0)
            throw new SisRosterValidationException(errors.Take(200).ToList());

        return new SisRosterWorkbook(students);
    }

    private static Dictionary<int, string?> ReadRow(Row row, SharedStringTable? sharedStrings)
    {
        var values = new Dictionary<int, string?>();
        foreach (var cell in row.Elements<Cell>())
        {
            var columnIndex = ColumnIndex(cell.CellReference?.Value);
            if (columnIndex >= 0)
                values[columnIndex] = CellText(cell, sharedStrings);
        }
        return values;
    }

    private static string? CellText(Cell cell, SharedStringTable? sharedStrings)
    {
        if (cell.DataType?.Value == CellValues.SharedString &&
            int.TryParse(cell.CellValue?.InnerText, out var sharedIndex))
        {
            return sharedStrings?.Elements<SharedStringItem>().ElementAtOrDefault(sharedIndex)?.InnerText;
        }
        if (cell.DataType?.Value == CellValues.InlineString)
            return cell.InlineString?.InnerText;
        return cell.CellValue?.InnerText ?? cell.InnerText;
    }

    private static int ColumnIndex(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) return -1;
        var result = 0;
        foreach (var character in reference.TakeWhile(char.IsLetter))
            result = result * 26 + char.ToUpperInvariant(character) - 'A' + 1;
        return result - 1;
    }

    private static void AddParent(
        ICollection<SisRosterParentRow> target,
        ICollection<string> errors,
        uint rowNumber,
        string relationship,
        string? fullName,
        string? username,
        string? phone,
        string? email,
        string? occupation,
        string? birthYearText)
    {
        if (fullName is null && username is null && phone is null && email is null &&
            occupation is null && birthYearText is null)
        {
            return;
        }
        if (fullName is null)
        {
            errors.Add($"Row {rowNumber}: {relationship.ToLowerInvariant()} details exist but the full name is missing.");
            return;
        }

        int? birthYear = null;
        if (birthYearText is not null)
        {
            var normalized = birthYearText.EndsWith(".0", StringComparison.Ordinal)
                ? birthYearText[..^2]
                : birthYearText;
            if (!int.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedYear) ||
                parsedYear is < 1900 or > 2100)
            {
                errors.Add($"Row {rowNumber}: {relationship.ToLowerInvariant()} birth year is invalid.");
            }
            else
            {
                birthYear = parsedYear;
            }
        }

        ValidateLength(errors, rowNumber, $"{relationship.ToLowerInvariant()} fullname", fullName, 255);
        ValidateLength(errors, rowNumber, $"{relationship.ToLowerInvariant()} username", username, 255);
        ValidateLength(errors, rowNumber, $"{relationship.ToLowerInvariant()} phone", phone, 50);
        ValidateLength(errors, rowNumber, $"{relationship.ToLowerInvariant()} email", email, 255);
        ValidateLength(errors, rowNumber, $"{relationship.ToLowerInvariant()} occupation", occupation, 255);
        target.Add(new SisRosterParentRow(relationship, fullName, username, phone, email, occupation, birthYear));
    }

    private static void ValidateLength(
        ICollection<string> errors, uint rowNumber, string field, string? value, int maximum)
    {
        if (value?.Length > maximum)
            errors.Add($"Row {rowNumber}: {field} exceeds {maximum} characters.");
    }

    private static bool TryDate(string? value, out DateTime? date)
    {
        date = null;
        if (value is null) return false;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial > 0)
        {
            try
            {
                date = DateTime.FromOADate(serial).Date;
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        string[] formats = ["dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd"];
        if (!DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces, out var parsed))
            return false;
        date = parsed.Date;
        return true;
    }

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"^\s*0*(?<grade>1[0-2]|[1-9])", RegexOptions.CultureInvariant)]
    private static partial Regex GradePattern();
}
