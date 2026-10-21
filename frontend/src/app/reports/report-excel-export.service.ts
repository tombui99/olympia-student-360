import { inject, Injectable } from '@angular/core';
import { LanguageService } from '../core/language/language.service';

export type ReportExportCell = string | number | boolean | Date | null | undefined;

export interface ReportExportColumn<T> {
  header: string;
  value: (row: T) => ReportExportCell;
  /** Percentage inputs use the report API's 0–100 scale. */
  format?: 'integer' | 'decimal' | 'percentage' | 'text';
  width?: number;
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

  async download<T>(definition: ReportExportDefinition<T>): Promise<void> {
    if (definition.rows.length > this.maximumRows)
      throw new ReportExportLimitError(definition.rows.length, this.maximumRows);
    // Load the workbook engine only when exporting; keep it out of the initial app bundle.
    const { createReportWorkbook } = await import('./report-excel-workbook');
    const workbook = createReportWorkbook(definition, this.language.t('reports.exportedAt'));
    const buffer = await workbook.xlsx.writeBuffer();
    const blob = new Blob([new Uint8Array(buffer)], {
      type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    });
    const link = document.createElement('a');
    const url = URL.createObjectURL(blob);
    link.href = url;
    link.download = `${this.safeFileName(definition.fileName)}.xlsx`;
    document.body.appendChild(link);
    try { link.click(); }
    finally {
      link.remove();
      // Allow the browser to consume the download before releasing its URL.
      setTimeout(() => URL.revokeObjectURL(url), 30_000);
    }
  }

  private safeFileName(value: string): string {
    return value.normalize('NFC').replace(/\.xlsx?$/i, '').replace(/[\\/:*?"<>|\u0000-\u001f]+/g, '-')
      .replace(/\s+/g, '-').slice(0, 120) || 'report';
  }
}
