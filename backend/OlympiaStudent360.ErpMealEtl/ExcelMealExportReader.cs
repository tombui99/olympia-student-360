using System.Globalization;
using System.Security.Cryptography;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using OlympiaStudent360.Api.Core.Services;

namespace OlympiaStudent360.ErpMealEtl;

/// <summary>Adapter for the three current ERP exports; future API adapters produce the same inputs.</summary>
public static class ExcelMealExportReader
{
    public static IReadOnlyList<ErpMealStatusInput> ReadStatuses(string path)
    {
        var rows = ReadRows(path, 9);
        RequireHeaders(rows, "TT", "Ngày", "ID học sinh", "Họ và tên", "EMAIL", "Mã lớp", "Bữa ăn", "Tình trạng đặt ăn", "Tình trạng đi ăn");
        var prefix = Fingerprint(path);
        return rows.Skip(3).Where(IsDataRow).Select(row => new ErpMealStatusInput(
            $"xlsx:{prefix}:{row.RowNumber}", Required(row, 2), Value(row, 3), Value(row, 4),
            Value(row, 5), ParseDate(row, 1), Required(row, 6), Value(row, 7), Value(row, 8)))
            .ToArray();
    }

    public static IReadOnlyList<ErpCanteenPurchaseInput> ReadPurchases(string path)
    {
        var rows = ReadRows(path, 8);
        RequireHeaders(rows, "TT", "Ngày", "Họ và tên", "ID học sinh", "Lớp học", "Mã set ăn", "Tên set ăn", "Số lượng");
        var prefix = Fingerprint(path);
        return rows.Skip(3).Where(IsDataRow).Select(row => new ErpCanteenPurchaseInput(
            $"xlsx:{prefix}:{row.RowNumber}", Required(row, 3), Value(row, 2), Value(row, 4),
            ParseDate(row, 1), Value(row, 5), Value(row, 6), ParseInteger(row, 7)))
            .ToArray();
    }

    private static IReadOnlyList<ExportRow> ReadRows(string path, int width)
    {
        using var document = SpreadsheetDocument.Open(path, false);
        var workbook = document.WorkbookPart ?? throw new InvalidDataException("Workbook has no workbook part.");
        var sheet = workbook.Workbook?.Sheets?.Elements<Sheet>().FirstOrDefault()
            ?? throw new InvalidDataException("Workbook has no worksheet.");
        var worksheet = (WorksheetPart)workbook.GetPartById(sheet.Id!);
        var shared = workbook.SharedStringTablePart?.SharedStringTable?.Elements<SharedStringItem>()
            .Select(item => item.InnerText).ToArray() ?? [];
        var sheetData = worksheet.Worksheet ?? throw new InvalidDataException("Worksheet is empty.");
        return sheetData.Descendants<Row>().Select(row =>
        {
            var cells = new string?[width];
            foreach (var cell in row.Elements<Cell>())
            {
                var column = ColumnIndex(cell.CellReference?.Value);
                if (column >= width) continue;
                var raw = cell.CellValue?.Text ?? cell.InlineString?.InnerText;
                if (cell.DataType?.Value == CellValues.SharedString &&
                    int.TryParse(raw, out var sharedIndex) && sharedIndex >= 0 && sharedIndex < shared.Length)
                    raw = shared[sharedIndex];
                cells[column] = raw;
            }
            return new ExportRow(row.RowIndex?.Value ?? 0, cells);
        }).ToArray();
    }

    private static int ColumnIndex(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference)) throw new InvalidDataException("Cell reference is missing.");
        var column = 0;
        foreach (var character in reference)
        {
            if (character is < 'A' or > 'Z') break;
            column = column * 26 + character - 'A' + 1;
        }
        return column - 1;
    }

    private static void RequireHeaders(IReadOnlyList<ExportRow> rows, params string[] expected)
    {
        if (rows.Count < 3 || !expected.SequenceEqual(rows[2].Cells.Select(x => x?.Trim() ?? "")))
            throw new InvalidDataException("ERP worksheet headers differ from the supported export format.");
    }

    private static bool IsDataRow(ExportRow row) => int.TryParse(Value(row, 0), out _);

    private static string? Value(ExportRow row, int column) => row.Cells[column]?.Trim();

    private static string Required(ExportRow row, int column) =>
        !string.IsNullOrWhiteSpace(Value(row, column)) ? Value(row, column)!
            : throw new InvalidDataException($"Required value missing on Excel row {row.RowNumber}.");

    private static DateTime ParseDate(ExportRow row, int column)
    {
        var value = Required(row, column);
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial))
            return DateTime.FromOADate(serial).Date;
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return date.Date;
        throw new InvalidDataException($"Invalid date on Excel row {row.RowNumber}.");
    }

    private static int ParseInteger(ExportRow row, int column) =>
        int.TryParse(Required(row, column), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result : throw new InvalidDataException($"Invalid quantity on Excel row {row.RowNumber}.");

    private static string Fingerprint(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream))[..32].ToLowerInvariant();
    }

    private sealed record ExportRow(uint RowNumber, string?[] Cells);
}
