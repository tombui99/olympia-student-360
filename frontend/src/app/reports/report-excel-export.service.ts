import { inject, Injectable } from '@angular/core';
import { LanguageService } from '../core/language/language.service';

export type ReportExportCell = string | number | boolean | Date | null | undefined;

export interface ReportExportColumn<T> {
  header: string;
  value: (row: T) => ReportExportCell;
}

export interface ReportExportDefinition<T> {
  fileName: string;
  title: string;
  period?: string;
  filters?: Array<[string, ReportExportCell]>;
  columns: ReportExportColumn<T>[];
  rows: T[];
}

export interface ReportExportPage<T> {
  items: T[];
  totalCount: number;
  totalPages: number;
}

export class ReportExportLimitError extends Error {
  constructor(readonly totalCount: number, readonly maximum: number) {
    super(`The filtered report contains ${totalCount} rows; the export limit is ${maximum}.`);
  }
}

@Injectable({ providedIn: 'root' })
export class ReportExcelExportService {
  private readonly language = inject(LanguageService);
  readonly maximumRows = 25_000;
  readonly batchSize = 1_000;

  async collectPages<T>(loader: (page: number, pageSize: number) => Promise<ReportExportPage<T>>): Promise<T[]> {
    const rows: T[] = [];
    let page = 1;
    while (true) {
      const result = await loader(page, this.batchSize);
      if (result.totalCount > this.maximumRows) {
        throw new ReportExportLimitError(result.totalCount, this.maximumRows);
      }
      if (rows.length + result.items.length > this.maximumRows) {
        throw new ReportExportLimitError(rows.length + result.items.length, this.maximumRows);
      }
      rows.push(...result.items);
      if (page >= result.totalPages || result.items.length === 0) return rows;
      page++;
    }
  }

  download<T>(definition: ReportExportDefinition<T>): void {
    const metadata = [
      [definition.title],
      ...(definition.period ? [[definition.period]] : []),
      ...(definition.filters ?? []).map(([label, value]) => [label, this.displayValue(value)]),
      [this.language.t('reports.exportedAt'), new Date().toISOString()],
      [],
    ];
    const table = [
      ...metadata,
      definition.columns.map(column => column.header),
      ...definition.rows.map(row => definition.columns.map(column => this.displayValue(column.value(row)))),
    ];
    const content = table.map(row => row.map(cell => this.safeCell(cell)).join('\t')).join('\r\n');
    const blob = new Blob([`\uFEFF${content}`], { type: 'application/vnd.ms-excel;charset=utf-8' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = `${this.safeFileName(definition.fileName)}.xls`;
    link.click();
    URL.revokeObjectURL(link.href);
  }

  private displayValue(value: ReportExportCell): string {
    if (value == null) return '';
    if (value instanceof Date) return value.toISOString();
    return String(value);
  }

  private safeCell(value: string): string {
    const singleLine = value.replace(/[\t\r\n]+/g, ' ').trim();
    return /^[=+\-@]/.test(singleLine) ? `'${singleLine}` : singleLine;
  }

  private safeFileName(value: string): string {
    return value.normalize('NFKD').replace(/[\\/:*?"<>|]+/g, '-').replace(/\s+/g, '-').slice(0, 120) || 'report';
  }
}
