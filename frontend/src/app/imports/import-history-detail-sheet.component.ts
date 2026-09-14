import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, signal } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { LanguageService } from '../core/language/language.service';
import {
  ImportHistoryDetailResponse,
  ImportHistoryRecordResponse,
  ImportHistoryService,
} from './import-history.service';
import { importModules } from './import-template-data';

@Component({
  selector: 'app-import-history-detail-sheet',
  imports: [NgFor, NgIf, FormsModule],
  template: `
    <div class="fixed inset-0 z-50 flex justify-end bg-[#22152c]/45" (click)="close.emit()">
      <aside
        class="flex h-full w-full max-w-3xl flex-col bg-white shadow-2xl"
        role="dialog"
        aria-modal="true"
        [attr.aria-label]="languageService.t('imports.historyDetail.title')"
        (click)="$event.stopPropagation()"
      >
        <header class="flex items-start justify-between gap-4 border-b border-[#e9e2ed] px-6 py-5 sm:px-8">
          <div>
            <p class="text-xs font-semibold uppercase tracking-[0.18em] text-[#8a6811]">
              {{ languageService.t('imports.historyDetail.eyebrow') }}
            </p>
            <h2 class="mt-2 text-2xl font-semibold text-[#32124f]">
              {{ detail() ? moduleName(detail()!.moduleId) : languageService.t('imports.historyDetail.title') }}
            </h2>
          </div>
          <button class="grid h-10 w-10 place-items-center rounded-full border border-[#ded6e4] text-xl text-[#5f5667] hover:bg-[#f7f3f8]" type="button" (click)="close.emit()">×</button>
        </header>

        <div class="min-h-0 flex-1 overflow-y-auto px-6 py-6 sm:px-8">
          <div *ngIf="loading()" class="py-16 text-center text-sm text-[#746b7b]">
            {{ languageService.t('imports.historyDetail.loading') }}
          </div>
          <div *ngIf="!loading() && loadError()" class="rounded-md border border-red-200 bg-red-50 p-4 text-sm text-red-700">
            {{ loadError() }}
          </div>

          <ng-container *ngIf="detail() as item">
            <section class="rounded-lg border border-[#e8e1ec] bg-[#faf8fb] p-4 sm:p-5">
              <div class="grid gap-4 sm:grid-cols-2">
                <label class="sm:col-span-2">
                  <span class="mb-1.5 block text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.historyDetail.reportName') }}</span>
                  <div class="flex gap-2">
                    <input class="min-w-0 flex-1 rounded-md border border-[#d8d0df] bg-white px-3 py-2.5 text-sm outline-none focus:border-[#6d3b8f]" [(ngModel)]="reportName" [disabled]="savingName()" />
                    <button class="rounded-md bg-[#32124f] px-4 py-2 text-sm font-semibold text-white disabled:opacity-50" type="button" [disabled]="savingName() || !reportName.trim()" (click)="saveName()">
                      {{ languageService.t('common.save') }}
                    </button>
                  </div>
                </label>
                <div>
                  <p class="text-xs font-semibold uppercase tracking-wide text-[#817687]">{{ languageService.t('imports.importedBy') }}</p>
                  <p class="mt-1 text-sm text-[#4f4656]">{{ item.importedByName }}</p>
                </div>
                <div>
                  <p class="text-xs font-semibold uppercase tracking-wide text-[#817687]">{{ languageService.t('imports.importedAt') }}</p>
                  <p class="mt-1 text-sm text-[#4f4656]">{{ formatDate(item.importedAt) }}</p>
                </div>
              </div>
              <div *ngIf="message()" class="mt-3 text-sm text-emerald-700">{{ message() }}</div>
              <div *ngIf="actionError()" class="mt-3 text-sm text-red-700">{{ actionError() }}</div>
            </section>

            <div class="mt-6 flex items-center justify-between gap-4">
              <div>
                <h3 class="font-semibold text-[#32124f]">{{ languageService.t('imports.historyDetail.records') }}</h3>
                <p class="mt-1 text-sm text-[#746b7b]">{{ item.records.length }} {{ languageService.t('imports.historyDetail.rows') }}</p>
              </div>
              <button class="rounded-md border border-red-200 px-3 py-2 text-sm font-semibold text-red-700 hover:bg-red-50" type="button" [disabled]="deletingHistory()" (click)="removeHistory()">
                {{ languageService.t('imports.historyDetail.deleteReport') }}
              </button>
            </div>

            <div *ngIf="item.records.length === 0" class="mt-4 rounded-lg border border-dashed border-[#d9d0df] py-12 text-center text-sm text-[#746b7b]">
              {{ languageService.t('imports.historyDetail.noRecords') }}
            </div>

            <article *ngFor="let record of item.records; trackBy: trackRecord" class="mt-4 rounded-lg border border-[#e5dfe9] p-4 sm:p-5">
              <div class="flex flex-wrap items-start justify-between gap-3 border-b border-[#eee9f1] pb-3">
                <div>
                  <h4 class="font-semibold text-[#32124f]">{{ record.studentName }}</h4>
                  <p class="mt-0.5 text-xs text-[#817687]">{{ record.sisUserId }}</p>
                </div>
                <button class="text-sm font-semibold text-red-700 hover:underline" type="button" [disabled]="rowBusy(record.id)" (click)="removeRecord(record)">
                  {{ languageService.t('common.delete') }}
                </button>
              </div>

              <div class="mt-4 grid gap-4 sm:grid-cols-2">
                <label *ngFor="let key of fieldKeys(record)" [class.sm:col-span-2]="isLongField(key)">
                  <span class="mb-1.5 block text-xs font-semibold uppercase tracking-wide text-[#655b6c]">{{ fieldLabel(key) }}</span>
                  <textarea *ngIf="isLongField(key); else standardInput" class="min-h-24 w-full rounded-md border border-[#d8d0df] px-3 py-2 text-sm outline-none focus:border-[#6d3b8f]" [(ngModel)]="record.values[key]" [disabled]="rowBusy(record.id)"></textarea>
                  <ng-template #standardInput>
                    <input class="w-full rounded-md border border-[#d8d0df] px-3 py-2 text-sm outline-none focus:border-[#6d3b8f]" [type]="isDateField(key) ? 'date' : 'text'" [(ngModel)]="record.values[key]" [disabled]="rowBusy(record.id)" />
                  </ng-template>
                </label>
              </div>
              <div class="mt-4 flex justify-end">
                <button class="rounded-md bg-[#32124f] px-4 py-2 text-sm font-semibold text-white disabled:opacity-50" type="button" [disabled]="rowBusy(record.id)" (click)="saveRecord(record)">
                  {{ rowBusy(record.id) ? languageService.t('imports.historyDetail.saving') : languageService.t('imports.historyDetail.saveRecord') }}
                </button>
              </div>
            </article>
          </ng-container>
        </div>
      </aside>
    </div>
  `,
})
export class ImportHistoryDetailSheetComponent implements OnChanges {
  @Input({ required: true }) historyId!: number;
  @Output() close = new EventEmitter<void>();
  @Output() changed = new EventEmitter<void>();
  @Output() deleted = new EventEmitter<void>();

  detail = signal<ImportHistoryDetailResponse | null>(null);
  loading = signal(true);
  loadError = signal<string | null>(null);
  actionError = signal<string | null>(null);
  message = signal<string | null>(null);
  savingName = signal(false);
  deletingHistory = signal(false);
  busyRows = signal<Set<number>>(new Set());
  reportName = '';

  constructor(
    private readonly service: ImportHistoryService,
    readonly languageService: LanguageService,
  ) {}

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['historyId']) this.load();
  }

  moduleName(moduleId: string): string {
    const module = importModules.find((candidate) => candidate.id === moduleId);
    return module ? this.languageService.t(module.titleKey) : moduleId;
  }

  formatDate(value: string): string {
    return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(value));
  }

  fieldKeys(record: ImportHistoryRecordResponse): string[] {
    return Object.keys(record.values);
  }

  fieldLabel(key: string): string {
    const translated = this.languageService.t(`imports.historyDetail.fields.${key}`);
    return translated === `imports.historyDetail.fields.${key}`
      ? key.replaceAll('_', ' ').replace(/\b\w/g, (letter) => letter.toUpperCase())
      : translated;
  }

  isDateField(key: string): boolean { return key.endsWith('_date'); }
  isLongField(key: string): boolean {
    return ['notes', 'teacher_notes', 'symptoms', 'diagnosis', 'treatment'].includes(key);
  }
  rowBusy(id: number): boolean { return this.busyRows().has(id); }
  trackRecord(_: number, record: ImportHistoryRecordResponse): number { return record.id; }

  saveName(): void {
    const detail = this.detail();
    if (!detail || !this.reportName.trim()) return;
    this.savingName.set(true);
    this.clearMessages();
    this.service.rename(detail.id, this.reportName.trim()).pipe(finalize(() => this.savingName.set(false))).subscribe({
      next: () => {
        detail.fileName = this.reportName.trim();
        this.message.set(this.languageService.t('imports.historyDetail.reportSaved'));
        this.changed.emit();
      },
      error: () => this.actionError.set(this.languageService.t('imports.historyDetail.saveError')),
    });
  }

  saveRecord(record: ImportHistoryRecordResponse): void {
    const detail = this.detail();
    if (!detail) return;
    this.setRowBusy(record.id, true);
    this.clearMessages();
    this.service.updateRecord(detail.id, record.id, record.values).pipe(finalize(() => this.setRowBusy(record.id, false))).subscribe({
      next: () => {
        this.message.set(this.languageService.t('imports.historyDetail.recordSaved'));
        this.changed.emit();
      },
      error: () => this.actionError.set(this.languageService.t('imports.historyDetail.saveError')),
    });
  }

  removeRecord(record: ImportHistoryRecordResponse): void {
    const detail = this.detail();
    if (!detail || !window.confirm(this.languageService.t('imports.historyDetail.confirmDeleteRecord'))) return;
    this.setRowBusy(record.id, true);
    this.clearMessages();
    this.service.deleteRecord(detail.id, record.id).pipe(finalize(() => this.setRowBusy(record.id, false))).subscribe({
      next: () => {
        detail.records = detail.records.filter((item) => item.id !== record.id);
        detail.validRows = Math.max(0, detail.validRows - 1);
        this.detail.set({ ...detail });
        this.message.set(this.languageService.t('imports.historyDetail.recordDeleted'));
        this.changed.emit();
      },
      error: () => this.actionError.set(this.languageService.t('imports.historyDetail.deleteError')),
    });
  }

  removeHistory(): void {
    const detail = this.detail();
    if (!detail || !window.confirm(this.languageService.t('imports.historyDetail.confirmDeleteReport'))) return;
    this.deletingHistory.set(true);
    this.clearMessages();
    this.service.deleteHistory(detail.id).pipe(finalize(() => this.deletingHistory.set(false))).subscribe({
      next: () => this.deleted.emit(),
      error: () => this.actionError.set(this.languageService.t('imports.historyDetail.deleteError')),
    });
  }

  private load(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.service.detail(this.historyId).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: (detail) => {
        this.detail.set(detail);
        this.reportName = detail.fileName;
      },
      error: () => this.loadError.set(this.languageService.t('imports.historyDetail.loadError')),
    });
  }

  private setRowBusy(id: number, busy: boolean): void {
    const rows = new Set(this.busyRows());
    busy ? rows.add(id) : rows.delete(id);
    this.busyRows.set(rows);
  }

  private clearMessages(): void {
    this.message.set(null);
    this.actionError.set(null);
  }
}
