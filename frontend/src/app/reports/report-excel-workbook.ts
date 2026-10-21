import { Workbook, CellValue } from 'exceljs';
import type { ReportExportCell, ReportExportColumn, ReportExportDefinition } from './report-excel-export.service';

const purple = 'FF32124F';
const muted = 'FF706578';

/** Build a real Office Open XML workbook, preserving typed values and readable layout. */
export function createReportWorkbook<T>(
  definition: ReportExportDefinition<T>, exportedAtLabel: string, exportedAt = new Date(),
): Workbook {
  if (!definition.columns.length) throw new Error('A report export must have at least one column.');
  const workbook = new Workbook();
  workbook.creator = 'Olympia Student 360';
  workbook.created = exportedAt;
  const name = cleanText(definition.title).replace(/[\\/*?:\[\]]/g, ' ').replace(/^'+|'+$/g, '').trim().slice(0, 31);
  const sheet = workbook.addWorksheet(name || 'Report');
  const lastColumn = Math.max(2, definition.columns.length);
  const values = definition.rows.map(row => definition.columns.map(column => cellValue(column.value(row), column)));
  const widths = definition.columns.map((column, index) => column.width ?? Math.min(60, Math.max(12,
    textLength(column.header) + 2, ...values.map(row => textLength(display(row[index], column)) + 2))));
  // Filter labels live in A, independently of the data column's type or length.
  widths[0] = Math.max(widths[0], Math.min(42, Math.max(22,
    ...(definition.filters ?? []).map(([label]) => textLength(label) + 2), textLength(exportedAtLabel) + 2)));
  for (let column = 1; column <= lastColumn; column++) sheet.getColumn(column).width = widths[column - 1] ?? 24;
  const spanWidth = Array.from({ length: lastColumn }, (_, index) => widths[index] ?? 24)
    .reduce((sum, width) => sum + width, 0);

  const title = sheet.addRow([cleanText(definition.title)]);
  sheet.mergeCells(title.number, 1, title.number, lastColumn);
  for (let column = 1; column <= lastColumn; column++) {
    const cell = title.getCell(column);
    cell.font = { name: 'Arial', size: 14, bold: true, color: { argb: 'FFFFFFFF' } };
    cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: purple } };
    cell.alignment = { vertical: 'middle', wrapText: true };
  }
  title.height = Math.max(34, wrappedLines(definition.title, spanWidth) * 19 + 12);
  if (definition.period) {
    const period = sheet.addRow([cleanText(definition.period)]);
    sheet.mergeCells(period.number, 1, period.number, lastColumn);
    period.font = { name: 'Arial', size: 10, italic: true, color: { argb: muted } };
    period.alignment = { vertical: 'middle', wrapText: true };
    period.height = Math.max(24, wrappedLines(definition.period, spanWidth) * 15 + 8);
  }
  const metadata: Array<[string, ReportExportCell]> = [...(definition.filters ?? []), [exportedAtLabel, exportedAt]];
  for (const [label, value] of metadata) {
    const row = sheet.addRow([cleanText(label), cellValue(value)]);
    if (lastColumn > 2) sheet.mergeCells(row.number, 2, row.number, lastColumn);
    row.font = { name: 'Arial', size: 10, color: { argb: purple } };
    row.alignment = { vertical: 'middle', wrapText: true };
    row.getCell(1).font = { name: 'Arial', size: 10, bold: true, color: { argb: purple } };
    row.getCell(1).fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FFF2ECF5' } };
    row.getCell(2).alignment = { horizontal: 'left', vertical: 'middle', wrapText: true };
    if (value instanceof Date) row.getCell(2).numFmt = 'yyyy-mm-dd hh:mm:ss" UTC"';
    const valueWidth = lastColumn === 2 ? (widths[1] ?? 24) : spanWidth - widths[0];
    row.height = Math.min(409, Math.max(24, wrappedLines(label, widths[0]) * 15 + 8,
      wrappedLines(String(value ?? ''), valueWidth) * 15 + 8));
  }
  sheet.addRow([]).height = 10;
  const header = sheet.addRow(definition.columns.map(column => cleanText(column.header)));
  header.height = Math.max(32, ...definition.columns.map((column, index) => wrappedLines(column.header, widths[index]) * 15 + 10));
  header.eachCell(cell => {
    cell.font = { name: 'Arial', size: 10, bold: true, color: { argb: 'FFFFFFFF' } };
    cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: purple } };
    cell.alignment = { horizontal: 'center', vertical: 'middle', wrapText: true };
    cell.border = { right: { style: 'thin', color: { argb: 'FFFFFFFF' } } };
  });
  values.forEach((items, index) => {
    const row = sheet.addRow(items);
    row.height = Math.min(409, Math.max(24, ...items.map((item, col) =>
      wrappedLines(display(item, definition.columns[col]), widths[col]) * 15 + 8)));
    row.eachCell({ includeEmpty: true }, (cell, column) => {
      cell.font = { name: 'Arial', size: 10, color: { argb: purple } };
      cell.alignment = { vertical: 'middle', horizontal: typeof cell.value === 'number' ? 'right' : 'left', wrapText: true };
      if (index % 2 === 1) cell.fill = { type: 'pattern', pattern: 'solid', fgColor: { argb: 'FFF7F3FA' } };
      const format = definition.columns[column - 1].format;
      cell.numFmt = format === 'percentage' ? '0.0%' : format === 'integer' ? '#,##0'
        : format === 'decimal' ? '0.0#' : format === 'text' ? '@'
        : cell.value instanceof Date ? 'yyyy-mm-dd' : typeof cell.value === 'number'
          ? (Number.isInteger(cell.value) ? '#,##0' : '0.0#') : 'General';
    });
  });
  sheet.autoFilter = { from: { row: header.number, column: 1 },
    to: { row: sheet.rowCount, column: definition.columns.length } };
  sheet.views = [{ state: 'frozen', ySplit: header.number, xSplit: definition.columns.length > 1 ? 1 : 0,
    showGridLines: false, topLeftCell: `${definition.columns.length > 1 ? 'B' : 'A'}${header.number + 1}` }];
  sheet.pageSetup = { orientation: 'landscape', fitToPage: true, fitToWidth: 1, fitToHeight: 0,
    printTitlesRow: `${header.number}:${header.number}` };
  return workbook;
}

function cellValue(value: ReportExportCell, column?: Pick<ReportExportColumn<unknown>, 'format'>): CellValue {
  if (value == null) return null;
  if (value instanceof Date) return Number.isFinite(value.getTime()) ? value : null;
  if (typeof value === 'number') return Number.isFinite(value)
    ? (column?.format === 'text' ? String(value) : column?.format === 'percentage' ? value / 100 : value) : null;
  // Strings stay literal strings in XLSX, including leading zeros and formula-like text.
  return typeof value === 'string' ? cleanText(value) : value;
}

function cleanText(value: string): string { return value.replace(/[\u0000-\u0008\u000b\u000c\u000e-\u001f]/g, ''); }
function textLength(value: string): number { return Math.max(0, ...value.normalize('NFC').split('\n').map(line => line.length)); }
function display(value: CellValue, column?: Pick<ReportExportColumn<unknown>, 'format'>): string {
  if (value == null) return '';
  if (value instanceof Date) return '0000-00-00 00:00:00 UTC';
  if (typeof value === 'number' && column?.format === 'percentage') return `${(value * 100).toFixed(1)}%`;
  return String(value);
}
function wrappedLines(value: string, width: number): number {
  return value.normalize('NFC').split('\n').reduce((count, line) => count + Math.max(1, Math.ceil(line.length / Math.max(1, width - 2))), 0);
}
