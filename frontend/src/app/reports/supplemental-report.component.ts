import { NgClass } from '@angular/common';
import { Component, computed, inject, Input, OnChanges, signal, SimpleChanges } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import {
  ModuleRecord,
  ModuleRecordsService,
} from '../module-records/module-records.service';
import { LanguageService } from '../core/language/language.service';

export type SupplementalModuleId =
  | 'student-behaviours'
  | 'student-projects'
  | 'student-mental-health'
  | 'student-bus-attendance';

export type SupplementalDimension = 'cohort' | 'enteredBy' | 'category' | 'field';
export type SupplementalAttentionMode = 'behaviour' | 'project' | 'mental-health' | 'bus';

export interface SupplementalReportDefinition {
  id: string;
  titleKey: string;
  descriptionKey: string;
  moduleId: SupplementalModuleId;
  dimension: SupplementalDimension;
  dimensionField?: string;
  primaryField: string;
  secondaryField: string;
  dateField: string;
  attentionMode: SupplementalAttentionMode;
}

interface DistributionRow {
  label: string;
  records: number;
  students: number;
  share: number;
}

@Component({
  selector: 'app-supplemental-report',
  imports: [NgClass, RouterLink],
  template: `
    <section class="overflow-hidden rounded-xl border border-[#e7e0eb] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.07)]">
      <div class="border-b border-[#eee8f1] px-5 py-5 sm:px-6">
        <div class="flex flex-wrap items-start justify-between gap-3">
          <div>
            <p class="text-xs font-bold uppercase tracking-[0.14em] text-[#8a6811]">{{ periodLabel }}</p>
            <h3 class="mt-2 text-xl font-semibold text-[#32124f]">{{ languageService.t(report.titleKey) }}</h3>
            <p class="mt-1 max-w-3xl text-sm leading-6 text-[#6f6877]">{{ languageService.t(report.descriptionKey) }}</p>
          </div>
          <span class="rounded-full bg-[#f2ecf5] px-3 py-1 text-xs font-semibold text-[#6d3a91]">
            {{ sourceLabel() }}
          </span>
        </div>
      </div>

      @if (loading()) {
        <div class="grid min-h-72 place-items-center" role="status">
          <div class="text-center">
            <span class="mx-auto block h-9 w-9 animate-spin rounded-full border-4 border-[#e6dfea] border-t-[#6d3a91]"></span>
            <p class="mt-3 text-sm font-semibold text-[#5f5368]">{{ languageService.t('reports.loading') }}</p>
          </div>
        </div>
      } @else if (loadFailed()) {
        <div class="grid min-h-64 place-items-center p-8 text-center">
          <div>
            <p class="text-sm font-semibold text-red-700">{{ languageService.t('reports.loadError') }}</p>
            <button class="mt-4 rounded-md border border-red-200 px-4 py-2 text-xs font-semibold text-red-700" type="button" (click)="load()">
              {{ languageService.t('reports.retry') }}
            </button>
          </div>
        </div>
      } @else {
        <div class="grid gap-px border-b border-[#eee8f1] bg-[#eee8f1] sm:grid-cols-3">
          <div class="bg-white p-5">
            <p class="text-[10px] font-bold uppercase tracking-[0.12em] text-[#817589]">{{ languageService.t('reports.supplemental.records') }}</p>
            <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ formatNumber(totalCount()) }}</p>
          </div>
          <div class="bg-white p-5">
            <p class="text-[10px] font-bold uppercase tracking-[0.12em] text-[#817589]">{{ languageService.t('reports.supplemental.students') }}</p>
            <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ formatNumber(distinctStudents()) }}</p>
          </div>
          <div class="bg-white p-5">
            <p class="text-[10px] font-bold uppercase tracking-[0.12em] text-[#817589]">{{ languageService.t('reports.supplemental.attention') }}</p>
            <p class="mt-2 text-3xl font-semibold" [ngClass]="attentionCount() > 0 ? 'text-amber-700' : 'text-emerald-700'">{{ formatNumber(attentionCount()) }}</p>
          </div>
        </div>

        @if (records().length) {
          <div class="grid xl:grid-cols-[minmax(0,0.9fr)_minmax(0,1.5fr)]">
            <div class="border-b border-[#eee8f1] p-5 xl:border-b-0 xl:border-r sm:p-6">
              <h4 class="text-sm font-semibold text-[#32124f]">{{ languageService.t('reports.supplemental.breakdown') }}</h4>
              <p class="mt-1 text-xs leading-5 text-[#817589]">{{ dimensionLabel() }}</p>
              <div class="mt-4 overflow-hidden rounded-lg border border-[#e7e0eb]">
                <table class="w-full border-collapse text-left">
                  <thead class="bg-[#f5f0f7] text-[10px] uppercase tracking-[0.08em] text-[#5d486b]">
                    <tr>
                      <th class="px-3 py-3 font-bold">{{ languageService.t('reports.supplemental.group') }}</th>
                      <th class="px-3 py-3 text-right font-bold">{{ languageService.t('reports.supplemental.records') }}</th>
                      <th class="px-3 py-3 text-right font-bold">%</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-[#eee8f1]">
                    @for (row of distribution(); track row.label) {
                      <tr>
                        <td class="px-3 py-3">
                          <p class="text-sm font-semibold text-[#32124f]">{{ row.label }}</p>
                          <p class="mt-0.5 text-[11px] text-[#817589]">{{ formatNumber(row.students) }} {{ languageService.t('reports.supplemental.students').toLowerCase() }}</p>
                        </td>
                        <td class="px-3 py-3 text-right text-sm font-semibold text-[#32124f]">{{ formatNumber(row.records) }}</td>
                        <td class="px-3 py-3 text-right text-xs text-[#6f6877]">{{ formatPercent(row.share) }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>

            <div class="min-w-0">
              <div class="px-5 pb-3 pt-5 sm:px-6 sm:pt-6">
                <h4 class="text-sm font-semibold text-[#32124f]">{{ languageService.t('reports.supplemental.recent') }}</h4>
                <p class="mt-1 text-xs text-[#817589]">{{ languageService.t('reports.supplemental.recentHint') }}</p>
              </div>
              <div class="overflow-x-auto">
                <table class="w-full min-w-[720px] border-collapse text-left">
                  <thead class="bg-[#f5f0f7] text-[10px] uppercase tracking-[0.08em] text-[#5d486b]">
                    <tr>
                      <th class="px-5 py-3 font-bold">{{ languageService.t('reports.supplemental.date') }}</th>
                      <th class="px-4 py-3 font-bold">{{ languageService.t('reports.table.student') }}</th>
                      <th class="px-4 py-3 font-bold">{{ languageService.t('reports.supplemental.detail') }}</th>
                      <th class="px-5 py-3 text-right font-bold">{{ languageService.t('reports.table.action') }}</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-[#eee8f1]">
                    @for (record of recentRecords(); track record.id) {
                      <tr class="transition hover:bg-[#fdfbfe]">
                        <td class="whitespace-nowrap px-5 py-4 text-xs text-[#6f6877]">{{ formatDate(record.values[report.dateField]) }}</td>
                        <td class="px-4 py-4">
                          <p class="text-sm font-semibold text-[#32124f]">{{ record.studentName }}</p>
                          <p class="mt-0.5 text-xs text-[#817589]">{{ cohortLabel(record) }}</p>
                        </td>
                        <td class="px-4 py-4">
                          <p class="text-sm font-semibold text-[#32124f]">{{ value(record, report.primaryField) }}</p>
                          <p class="mt-0.5 max-w-sm truncate text-xs text-[#817589]">{{ value(record, report.secondaryField) }}</p>
                        </td>
                        <td class="px-5 py-4 text-right">
                          <a class="text-xs font-semibold text-[#6d3a91] hover:underline" [routerLink]="['/students', record.studentProfileId]">{{ languageService.t('reports.viewStudent') }}</a>
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </div>
          </div>
        } @else {
          <div class="grid min-h-64 place-items-center p-8 text-center text-sm text-[#817589]">{{ languageService.t('reports.noData') }}</div>
        }
        <p class="border-t border-[#eee8f1] bg-[#faf8fb] px-5 py-3 text-xs leading-5 text-[#817589]">
          {{ languageService.t('reports.supplemental.note') }}
        </p>
      }
    </section>
  `,
})
export class SupplementalReportComponent implements OnChanges {
  @Input({ required: true }) report!: SupplementalReportDefinition;
  @Input() academicYear = '';
  @Input() term = 'all';
  @Input() periodLabel = '';

  private readonly recordsService = inject(ModuleRecordsService);
  readonly languageService = inject(LanguageService);
  readonly records = signal<ModuleRecord[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  private requestId = 0;

  readonly distinctStudents = signal(0);
  readonly recentRecords = computed(() => this.records().slice(0, 20));
  readonly attentionCount = signal(0);
  readonly distribution = signal<DistributionRow[]>([]);

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['report'] || changes['academicYear'] || changes['term']) this.load();
  }

  load(): void {
    if (!this.report) return;
    const requestId = ++this.requestId;
    this.loading.set(true);
    this.loadFailed.set(false);
    forkJoin({
      records: this.recordsService.list(this.report.moduleId, 1, 20, '', this.academicYear, this.term),
      summary: this.recordsService.supplementalSummary(
        this.report.moduleId, this.report.dimensionField || this.report.primaryField,
        this.academicYear, this.term,
      ),
    }).subscribe({
      next: ({ records, summary }) => {
        if (requestId !== this.requestId) return;
        this.records.set(records.items);
        this.totalCount.set(summary.totalCount);
        this.distinctStudents.set(summary.distinctStudents);
        this.attentionCount.set(summary.attentionCount);
        this.distribution.set(summary.distribution);
        this.loading.set(false);
      },
      error: () => {
        if (requestId !== this.requestId) return;
        this.loadFailed.set(true);
        this.loading.set(false);
      },
    });
  }

  sourceLabel(): string {
    return this.languageService.t(`reports.source.${this.report.moduleId}`);
  }

  dimensionLabel(): string {
    return this.languageService.t(`reports.dimension.${this.report.dimension}`);
  }

  cohortLabel(record: ModuleRecord): string {
    return record.canvasGradeLevel
      ? `${this.languageService.t('reports.grade')} ${record.canvasGradeLevel}`
      : this.languageService.t('reports.supplemental.noCanvasCohort');
  }

  value(record: ModuleRecord, field: string): string {
    return record.values[field]?.trim() || '—';
  }

  formatDate(value: string | null | undefined): string {
    if (!value) return '—';
    const date = new Date(`${value}T00:00:00`);
    if (Number.isNaN(date.getTime())) return value;
    return new Intl.DateTimeFormat(this.languageService.language() === 'vi' ? 'vi-VN' : 'en-GB').format(date);
  }

  formatNumber(value: number): string {
    return value.toLocaleString(this.languageService.language() === 'vi' ? 'vi-VN' : 'en-US');
  }

  formatPercent(value: number): string {
    return new Intl.NumberFormat(this.languageService.language() === 'vi' ? 'vi-VN' : 'en-US', {
      maximumFractionDigits: 1,
    }).format(value) + '%';
  }

}
