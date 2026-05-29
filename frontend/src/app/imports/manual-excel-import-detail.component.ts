import { Component, computed, signal } from '@angular/core';
import { NgClass, NgFor, NgIf } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthenticationService } from '../core/auth/auth.service';
import { LanguageService } from '../core/language/language.service';
import {
  ImportModule,
  ImportRecord,
  ValidationResult,
  importModules,
} from './import-template-data';

@Component({
  selector: 'app-manual-excel-import-detail',
  imports: [NgClass, NgFor, NgIf, RouterLink],
  template: `
    <section *ngIf="module()" class="space-y-7">
      <a
        class="inline-flex items-center gap-2 rounded-md border border-[#d8d0df] px-4 py-2 text-sm font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
        routerLink="/imports"
      >
        <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
          <path d="M19 12H5" />
          <path d="M12 19l-7-7 7-7" />
        </svg>
        {{ languageService.t('imports.backToImports') }}
      </a>

      <div class="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">{{ languageService.t('imports.eyebrow') }}</p>
          <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ languageService.t(module()!.titleKey) }}</h2>
          <p class="mt-2 max-w-3xl text-[#6f6877]">{{ languageService.t(module()!.descriptionKey) }}</p>
        </div>
        <span class="rounded-full border border-[#f3c246]/50 bg-[#fff8e1] px-4 py-2 text-sm font-semibold text-[#7a5b12]">
          {{ languageService.t('imports.mockStatus') }}
        </span>
      </div>

      <section class="grid gap-5 xl:grid-cols-[minmax(0,0.9fr)_minmax(0,1.1fr)]">
        <article class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">{{ languageService.t('imports.templateWorkspace') }}</p>
          <h3 class="mt-2 text-2xl font-semibold text-[#32124f]">{{ languageService.t('imports.standardizedTemplate') }}</h3>
          <p class="mt-2 text-sm leading-6 text-[#6f6877]">{{ languageService.t('imports.templateHint') }}</p>

          <div class="mt-5 rounded-md border border-[#ebe6ef] bg-[#faf9f6] p-4">
            <p class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.requiredColumns') }}</p>
            <div class="mt-3 flex flex-wrap gap-2">
              <span
                *ngFor="let header of module()!.headers"
                class="rounded bg-white px-3 py-1 text-xs font-semibold text-[#6f6877]"
              >
                {{ header }}
              </span>
            </div>
          </div>

          <div class="mt-5 grid gap-3 sm:grid-cols-2">
            <div
              *ngFor="let check of validationChecks"
              class="rounded-md border border-[#ebe6ef] bg-white p-4"
            >
              <p class="text-sm font-semibold text-[#32124f]">{{ languageService.t(check.title) }}</p>
              <p class="mt-2 text-xs leading-5 text-[#7c7284]">{{ languageService.t(check.body) }}</p>
            </div>
          </div>

          <div class="mt-6 flex flex-wrap gap-3">
            <button
              class="rounded-md border border-[#d8d0df] px-4 py-3 text-sm font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
              type="button"
              (click)="downloadTemplate()"
            >
              {{ languageService.t('imports.downloadTemplate') }}
            </button>
            <label class="cursor-pointer rounded-md bg-[#32124f] px-4 py-3 text-sm font-semibold text-white transition hover:bg-[#4b2070]">
              {{ languageService.t('imports.chooseFile') }}
              <input class="hidden" type="file" accept=".xls,.csv,.tsv,.txt" (change)="handleFile($event)" />
            </label>
          </div>

          <p class="mt-4 text-xs leading-5 text-[#7c7284]">{{ languageService.t('imports.fileHint') }}</p>
        </article>

        <article class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <div class="flex flex-wrap items-start justify-between gap-4">
            <div>
              <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">{{ languageService.t('imports.validationTitle') }}</p>
              <h3 class="mt-2 text-2xl font-semibold text-[#32124f]">{{ validationHeading() }}</h3>
            </div>
            <span
              *ngIf="validationResult()"
              class="rounded-full px-4 py-2 text-sm font-semibold"
              [ngClass]="validationResult()?.status === 'Valid' ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'"
            >
              {{ validationResult()?.status === 'Valid' ? languageService.t('imports.valid') : languageService.t('imports.rejected') }}
            </span>
          </div>

          <div class="mt-5 grid gap-3 sm:grid-cols-2">
            <div class="rounded-md border border-[#ebe6ef] bg-[#faf9f6] p-4">
              <p class="text-sm text-[#7c7284]">{{ languageService.t('imports.validRows') }}</p>
              <p class="mt-2 text-2xl font-semibold text-[#32124f]">{{ validationResult()?.validRows ?? '-' }}</p>
            </div>
            <div class="rounded-md border border-[#ebe6ef] bg-[#faf9f6] p-4">
              <p class="text-sm text-[#7c7284]">{{ languageService.t('imports.invalidRows') }}</p>
              <p class="mt-2 text-2xl font-semibold text-[#32124f]">{{ validationResult()?.invalidRows ?? '-' }}</p>
            </div>
          </div>

          <div *ngIf="!validationResult()" class="mt-5 rounded-md border border-dashed border-[#d8d0df] bg-[#faf9f6] p-8 text-center">
            <p class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.awaitingUploadTitle') }}</p>
            <p class="mt-2 text-sm text-[#6f6877]">{{ languageService.t('imports.awaitingUploadBody') }}</p>
          </div>

          <div *ngIf="validationResult()?.errors?.length" class="mt-5 rounded-md bg-red-50 p-4 text-sm text-red-700">
            <p class="font-semibold">{{ languageService.t('imports.issuesFound') }}</p>
            <ul class="mt-2 space-y-1">
              <li *ngFor="let error of validationResult()?.errors">{{ error }}</li>
            </ul>
          </div>
        </article>
      </section>

      <section class="rounded-lg border border-[#ebe6ef] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
        <div class="border-b border-[#ebe6ef] p-6">
          <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('imports.moduleHistoryTitle') }}</h3>
          <p class="mt-1 text-sm text-[#6f6877]">{{ historyHint() }}</p>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full min-w-[820px] text-left">
            <thead class="bg-[#faf9f6] text-xs uppercase tracking-[0.12em] text-[#7c7284]">
              <tr>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('imports.fileName') }}</th>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('imports.importedBy') }}</th>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('imports.importedAt') }}</th>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('imports.result') }}</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-[#ebe6ef]">
              <tr *ngFor="let record of visibleModuleRecords()" class="text-sm">
                <td class="px-6 py-4 font-semibold text-[#32124f]">{{ record.fileName }}</td>
                <td class="px-6 py-4 text-[#6f6877]">{{ record.importedByName }}</td>
                <td class="px-6 py-4 text-[#6f6877]">{{ formatDate(record.importedAt) }}</td>
                <td class="px-6 py-4">
                  <span
                    class="rounded px-3 py-1 text-xs font-semibold"
                    [ngClass]="record.status === 'Valid' ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'"
                  >
                    {{ record.status === 'Valid' ? languageService.t('imports.valid') : languageService.t('imports.rejected') }}
                  </span>
                  <p class="mt-1 text-xs text-[#7c7284]">
                    {{ record.validRows }} {{ languageService.t('imports.validRowsShort') }} / {{ record.invalidRows }} {{ languageService.t('imports.invalidRowsShort') }}
                  </p>
                </td>
              </tr>
              <tr *ngIf="visibleModuleRecords().length === 0">
                <td class="px-6 py-8 text-center text-sm text-[#7c7284]" colspan="4">
                  {{ languageService.t('imports.noHistory') }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </section>
  `,
})
export class ManualExcelImportDetailComponent {
  private readonly validStudentIds = new Set(['S1001', 'S1002', 'S1003', 'S1004', 'S1005']);

  module = signal<ImportModule | null>(null);
  validationResult = signal<ValidationResult | null>(null);
  selectedFileName = signal<string | null>(null);
  records = signal<ImportRecord[]>([]);

  validationChecks = [
    { title: 'imports.checks.template', body: 'imports.checks.templateBody' },
    { title: 'imports.checks.studentId', body: 'imports.checks.studentIdBody' },
    { title: 'imports.checks.required', body: 'imports.checks.requiredBody' },
    { title: 'imports.checks.date', body: 'imports.checks.dateBody' },
  ];

  visibleModuleRecords = computed(() => {
    const module = this.module();
    if (!module) {
      return [];
    }

    const role = this.authService.getUserRole();
    const moduleRecords = this.records().filter((record) => record.moduleId === module.id);
    if (role === 'Admin' || role === 'Principal') {
      return moduleRecords;
    }

    const username = this.currentUsername();
    return moduleRecords.filter((record) => record.importedBy === username);
  });

  constructor(
    route: ActivatedRoute,
    router: Router,
    private readonly authService: AuthenticationService,
    readonly languageService: LanguageService,
  ) {
    const moduleId = route.snapshot.paramMap.get('moduleId');
    const module = importModules.find((item) => item.id === moduleId);
    if (!module) {
      router.navigate(['/imports']);
      return;
    }

    this.module.set(module);
  }

  downloadTemplate() {
    const module = this.module();
    if (!module) {
      return;
    }

    const rows = [module.headers];
    const content = rows.map((row) => row.map((cell) => this.escapeCell(cell)).join('\t')).join('\n');
    const blob = new Blob([content], { type: 'application/vnd.ms-excel;charset=utf-8' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = `${module.id}-template.xls`;
    link.click();
    URL.revokeObjectURL(link.href);
  }

  handleFile(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';

    if (!file) {
      return;
    }

    this.selectedFileName.set(file.name);
    const reader = new FileReader();
    reader.onload = () => {
      const result = this.validateFile(String(reader.result ?? ''));
      this.validationResult.set(result);
      this.addRecord(file.name, result);
    };
    reader.onerror = () => {
      const result: ValidationResult = {
        status: 'Rejected',
        errors: [this.languageService.t('imports.errors.readFailed')],
        validRows: 0,
        invalidRows: 0,
      };
      this.validationResult.set(result);
      this.addRecord(file.name, result);
    };
    reader.readAsText(file);
  }

  validationHeading(): string {
    const fileName = this.selectedFileName();
    return fileName || this.languageService.t('imports.awaitingFile');
  }

  historyHint(): string {
    const role = this.authService.getUserRole();
    return role === 'Admin' || role === 'Principal'
      ? this.languageService.t('imports.historyAllHint')
      : this.languageService.t('imports.historyTeacherHint');
  }

  formatDate(value: string): string {
    return new Intl.DateTimeFormat(undefined, {
      dateStyle: 'medium',
      timeStyle: 'short',
      timeZone: 'Asia/Ho_Chi_Minh',
    }).format(new Date(value));
  }

  private validateFile(rawContent: string): ValidationResult {
    const module = this.module();
    if (!module) {
      return { status: 'Rejected', errors: [this.languageService.t('imports.errors.templateMismatch')], validRows: 0, invalidRows: 0 };
    }

    const rows = rawContent
      .replace(/^\uFEFF/, '')
      .split(/\r?\n/)
      .map((row) => row.trim())
      .filter(Boolean)
      .map((row) => row.split(row.includes('\t') ? '\t' : ',').map((cell) => cell.trim()));

    const errors: string[] = [];
    const headerRow = rows[0] ?? [];

    if (headerRow.join('|') !== module.headers.join('|')) {
      errors.push(this.languageService.t('imports.errors.templateMismatch'));
      return { status: 'Rejected', errors, validRows: 0, invalidRows: Math.max(rows.length - 1, 0) };
    }

    let validRows = 0;
    let invalidRows = 0;
    const requiredIndexes = module.requiredHeaders.map((header) => module.headers.indexOf(header));
    const studentIdIndex = module.headers.indexOf('student_id');
    const dateIndex = module.headers.findIndex((header) => header.endsWith('_date'));

    rows.slice(1).forEach((row, index) => {
      const rowNumber = index + 2;
      const rowErrors: string[] = [];
      const studentId = row[studentIdIndex];

      if (!studentId || !this.validStudentIds.has(studentId)) {
        rowErrors.push(
          this.languageService
            .t('imports.errors.invalidStudentId')
            .replace('{row}', String(rowNumber))
            .replace('{studentId}', studentId || '-'),
        );
      }

      requiredIndexes.forEach((requiredIndex) => {
        if (!row[requiredIndex]) {
          rowErrors.push(
            this.languageService
              .t('imports.errors.requiredMissing')
              .replace('{row}', String(rowNumber))
              .replace('{column}', module.headers[requiredIndex]),
          );
        }
      });

      if (dateIndex >= 0 && row[dateIndex] && !/^\d{4}-\d{2}-\d{2}$/.test(row[dateIndex])) {
        rowErrors.push(
          this.languageService
            .t('imports.errors.invalidDate')
            .replace('{row}', String(rowNumber))
            .replace('{column}', module.headers[dateIndex]),
        );
      }

      if (rowErrors.length > 0) {
        invalidRows += 1;
        errors.push(...rowErrors);
      } else {
        validRows += 1;
      }
    });

    if (validRows + invalidRows === 0) {
      errors.push(this.languageService.t('imports.errors.noRows'));
    }

    return {
      status: errors.length === 0 ? 'Valid' : 'Rejected',
      errors,
      validRows,
      invalidRows,
    };
  }

  private addRecord(fileName: string, result: ValidationResult) {
    const module = this.module();
    if (!module) {
      return;
    }

    const record: ImportRecord = {
      id: `${Date.now()}-${Math.random().toString(36).slice(2)}`,
      moduleId: module.id,
      moduleTitleKey: module.titleKey,
      fileName,
      importedAt: new Date().toISOString(),
      importedBy: this.currentUsername(),
      importedByName: this.authService.getFullName() || this.currentUsername(),
      status: result.status,
      validRows: result.validRows,
      invalidRows: result.invalidRows,
    };

    const nextRecords = [record, ...this.records()].slice(0, 50);
    this.records.set(nextRecords);
  }

  private currentUsername(): string {
    return this.authService.getUsername() || 'unknown';
  }

  private escapeCell(value: string): string {
    return value.includes('\t') || value.includes('\n') ? `"${value.replace(/"/g, '""')}"` : value;
  }
}
