import { Component, EventEmitter, HostListener, inject, Input, OnInit, Output, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { LanguageService } from '../core/language/language.service';
import { ImportHistoryService } from './import-history.service';
import { ImportModule, ValidationResult, importModules } from './import-template-data';
import { ImportModuleField, ImportModuleFieldsService } from './import-module-fields.service';

type ParsedImportFile = {
  result: ValidationResult;
  rows: Record<string, string>[];
};

@Component({
  selector: 'app-excel-import-sheet',
  imports: [NgClass],
  styles: `
    :host { position: fixed; inset: 0; z-index: 70; }
    .sheet-backdrop { animation: fade-in 160ms ease-out; }
    .sheet-panel { animation: slide-in 220ms ease-out; }
    @keyframes fade-in { from { opacity: 0; } to { opacity: 1; } }
    @keyframes slide-in { from { transform: translateX(100%); } to { transform: translateX(0); } }
  `,
  template: `
    <div class="sheet-backdrop absolute inset-0 bg-[#1d1025]/35" (click)="close.emit()" aria-hidden="true"></div>
    <aside
      class="sheet-panel absolute inset-y-0 right-0 flex w-full max-w-2xl flex-col bg-white shadow-[-18px_0_45px_rgba(50,18,79,0.18)]"
      role="dialog"
      aria-modal="true"
      [attr.aria-label]="languageService.t('imports.excel.title')"
    >
      <header class="flex items-start justify-between gap-4 border-b border-[#ebe6ef] px-6 py-5">
        <div>
          <p class="text-xs font-semibold uppercase tracking-[0.18em] text-[#8a6811]">
            {{ languageService.t('imports.excel.eyebrow') }}
          </p>
          <h2 class="mt-2 text-2xl font-semibold text-[#32124f]">
            {{ languageService.t(module()!.titleKey) }}
          </h2>
          <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('imports.excel.hint') }}</p>
        </div>
        <button
          class="grid h-10 w-10 shrink-0 place-items-center rounded-full border border-[#ded6e4] text-xl text-[#5d5265] hover:bg-[#f7f3fa]"
          type="button"
          (click)="close.emit()"
          [attr.aria-label]="languageService.t('imports.manual.close')"
        >×</button>
      </header>

      <div class="flex-1 overflow-y-auto px-6 py-6">
        <section class="rounded-lg border border-[#ebe6ef] bg-[#faf9f6] p-5">
          <div class="flex flex-wrap items-start justify-between gap-4">
            <div>
              <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('imports.standardizedTemplate') }}</h3>
              <p class="mt-1 max-w-lg text-sm leading-6 text-[#6f6877]">{{ languageService.t('imports.templateHint') }}</p>
            </div>
            <button
              class="rounded-md border border-[#d8d0df] bg-white px-4 py-2.5 text-sm font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
              type="button"
              (click)="downloadTemplate()"
            >
              {{ languageService.t('imports.downloadTemplate') }}
            </button>
          </div>

          <p class="mt-5 text-xs font-semibold uppercase tracking-[0.12em] text-[#7c7284]">
            {{ languageService.t('imports.requiredColumns') }}
          </p>
          <div class="mt-3 flex flex-wrap gap-2">
            @for (header of effectiveHeaders(); track header) {
              <span class="rounded bg-white px-3 py-1 text-xs font-semibold text-[#6f6877]">{{ header }}</span>
            }
          </div>
        </section>

        <section class="mt-5 rounded-lg border border-[#ebe6ef] p-5">
          <div class="flex flex-wrap items-center justify-between gap-4">
            <div>
              <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('imports.excel.selectFile') }}</h3>
              <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('imports.fileHint') }}</p>
            </div>
            <label
              class="cursor-pointer rounded-md bg-[#32124f] px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-[#4b2070]"
              [class.pointer-events-none]="importing()"
              [class.opacity-50]="importing()"
            >
              {{ importing() ? languageService.t('imports.excel.importing') : languageService.t('imports.chooseFile') }}
              <input class="hidden" type="file" accept=".xls,.csv,.tsv,.txt" (change)="handleFile($event)" [disabled]="importing()" />
            </label>
          </div>

          @if (selectedFileName()) {
            <div class="mt-5 rounded-md border border-[#ebe6ef] bg-[#faf9f6] p-4">
              <div class="flex flex-wrap items-center justify-between gap-3">
                <p class="font-semibold text-[#32124f]">{{ selectedFileName() }}</p>
                @if (validationResult()) {
                  <span
                    class="rounded-full px-3 py-1 text-xs font-semibold"
                    [ngClass]="validationResult()?.status === 'Valid' ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'"
                  >
                    {{ validationResult()?.status === 'Valid' ? languageService.t('imports.excel.imported') : languageService.t('imports.rejected') }}
                  </span>
                }
              </div>
              @if (validationResult()?.errors?.length) {
                <ul class="mt-3 space-y-1 text-sm text-red-700">
                  @for (error of validationResult()!.errors; track error) {
                    <li>{{ error }}</li>
                  }
                </ul>
              }
            </div>
          }
        </section>
      </div>

      <footer class="flex items-center justify-end border-t border-[#ebe6ef] bg-[#faf9f6] px-6 py-4">
        <button class="rounded-md border border-[#d8d0df] px-5 py-2.5 text-sm font-semibold text-[#32124f] hover:bg-white" type="button" (click)="close.emit()">
          {{ languageService.t('imports.manual.close') }}
        </button>
      </footer>
    </aside>
  `,
})
export class ExcelImportSheetComponent implements OnInit {
  @Input({ required: true }) moduleId!: string;
  @Output() readonly close = new EventEmitter<void>();
  @Output() readonly imported = new EventEmitter<string>();

  private readonly importHistoryService = inject(ImportHistoryService);
  private readonly fieldsService = inject(ImportModuleFieldsService);
  readonly languageService = inject(LanguageService);
  readonly selectedFileName = signal<string | null>(null);
  readonly validationResult = signal<ValidationResult | null>(null);
  readonly importing = signal(false);
  readonly customFields = signal<ImportModuleField[]>([]);

  ngOnInit(): void {
    this.fieldsService.list(this.moduleId).subscribe({
      next: (fields) => this.customFields.set(fields),
      error: () => {},
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.close.emit();
  }

  module(): ImportModule | undefined {
    return importModules.find((candidate) => candidate.id === this.moduleId);
  }

  /** Standard module headers plus admin-defined custom field keys. */
  effectiveHeaders(): string[] {
    return [...(this.module()?.headers ?? []), ...this.customFields().map((f) => f.fieldKey)];
  }

  private customFieldKeys(): string[] {
    return this.customFields().map((f) => f.fieldKey);
  }

  downloadTemplate(): void {
    const module = this.module();
    if (!module) {
      return;
    }

    const content = this.effectiveHeaders().map((cell) => this.escapeCell(cell)).join('\t');
    const blob = new Blob([content], { type: 'application/vnd.ms-excel;charset=utf-8' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = `${module.id}-template.xls`;
    link.click();
    URL.revokeObjectURL(link.href);
  }

  handleFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }

    this.selectedFileName.set(file.name);
    this.validationResult.set(null);
    const reader = new FileReader();
    reader.onload = () => {
      const parsed = this.parseFile(String(reader.result ?? ''));
      if (parsed.result.status === 'Rejected') {
        this.validationResult.set(parsed.result);
        return;
      }
      this.importFile(file.name, parsed.rows);
      return;
    };
    reader.onerror = () => this.validationResult.set({
      status: 'Rejected',
      errors: [this.languageService.t('imports.errors.readFailed')],
      validRows: 0,
      invalidRows: 0,
    });
    reader.readAsText(file);
  }

  private importFile(fileName: string, rows: Record<string, string>[]): void {
    this.importing.set(true);
    this.importHistoryService.importExcel(this.moduleId, fileName, rows, this.customFieldKeys()).subscribe({
      next: () => {
        this.validationResult.set({ status: 'Valid', errors: [], validRows: rows.length, invalidRows: 0 });
        this.imported.emit(this.moduleId);
      },
      error: (error) => {
        const errors = Array.isArray(error?.error?.errors)
          ? error.error.errors
          : [error?.error?.message || this.languageService.t('imports.errors.importFailed')];
        this.validationResult.set({ status: 'Rejected', errors, validRows: 0, invalidRows: rows.length });
        this.importing.set(false);
      },
    });
  }

  private parseFile(rawContent: string): ParsedImportFile {
    const module = this.module();
    if (!module) {
      return this.rejected(this.languageService.t('imports.errors.templateMismatch'));
    }

    const rows = rawContent
      .replace(/^\uFEFF/, '')
      .split(/\r?\n/)
      .map((row) => row.trim())
      .filter(Boolean)
      .map((row) => row.split(row.includes('\t') ? '\t' : ',').map((cell) => cell.trim()));
    const headers = this.effectiveHeaders();
    const headerRow = rows[0] ?? [];
    if (headerRow.join('|') !== headers.join('|')) {
      return this.rejected(this.languageService.t('imports.errors.templateMismatch'), Math.max(rows.length - 1, 0));
    }

    // Required columns = standard required headers + any required custom fields.
    const requiredHeaders = [
      ...module.requiredHeaders,
      ...this.customFields().filter((f) => f.required).map((f) => f.fieldKey),
    ];
    const errors: string[] = [];
    const requiredIndexes = requiredHeaders.map((header) => headers.indexOf(header));
    const dateIndexes = headers
      .map((header, index) => ({ header, index }))
      .filter(({ header, index }) =>
        header.endsWith('_date') ||
        this.customFields().some((f) => f.fieldKey === header && f.fieldType === 'date' && index >= 0))
      .map(({ index }) => index);
    rows.slice(1).forEach((row, index) => {
      const rowNumber = index + 2;
      requiredIndexes.forEach((requiredIndex) => {
        if (!row[requiredIndex]) {
          errors.push(this.languageService.t('imports.errors.requiredMissing')
            .replace('{row}', String(rowNumber))
            .replace('{column}', headers[requiredIndex]));
        }
      });
      dateIndexes.forEach((dateIndex) => {
        if (dateIndex >= 0 && row[dateIndex] && !/^\d{4}-\d{2}-\d{2}$/.test(row[dateIndex])) {
          errors.push(this.languageService.t('imports.errors.invalidDate')
            .replace('{row}', String(rowNumber))
            .replace('{column}', headers[dateIndex]));
        }
      });
    });

    if (rows.length <= 1) {
      return this.rejected(this.languageService.t('imports.errors.noRows'));
    }
    if (errors.length > 0) {
      return { result: { status: 'Rejected', errors, validRows: 0, invalidRows: rows.length - 1 }, rows: [] };
    }

    const importRows = rows.slice(1).map((row) =>
      Object.fromEntries(headers.map((header, index) => [header, row[index] ?? ''])),
    );
    return { result: { status: 'Valid', errors: [], validRows: importRows.length, invalidRows: 0 }, rows: importRows };
  }

  private rejected(error: string, invalidRows = 0): ParsedImportFile {
    return { result: { status: 'Rejected', errors: [error], validRows: 0, invalidRows }, rows: [] };
  }

  private escapeCell(value: string): string {
    return value.includes('\t') || value.includes('\n') ? `"${value.replace(/"/g, '""')}"` : value;
  }
}
