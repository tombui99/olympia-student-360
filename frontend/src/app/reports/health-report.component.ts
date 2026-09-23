import { Component, HostListener, inject, Input, OnChanges, OnDestroy, signal, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LanguageService } from '../core/language/language.service';
import {
  HealthReportDetailsQuery,
  HealthReportGrouping,
  HealthReportRow,
  HealthReportsService,
  HealthReportType,
  HealthVisitReportDetail,
} from './health-reports.service';

@Component({
  selector: 'app-health-report',
  imports: [FormsModule, RouterLink],
  template: `
    <section class="overflow-hidden rounded-xl border border-[#e7e0eb] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.07)]">
      <div class="border-b border-[#eee8f1] px-5 py-5 sm:px-6">
        <div class="flex flex-wrap items-start justify-between gap-3">
          <div>
            <p class="text-xs font-bold uppercase tracking-[0.14em] text-[#8a6811]">{{ periodLabel }}</p>
            <h3 class="mt-2 text-xl font-semibold text-[#32124f]">{{ languageService.t(titleKey()) }}</h3>
            <p class="mt-1 max-w-3xl text-sm leading-6 text-[#6f6877]">{{ languageService.t(descriptionKey()) }}</p>
          </div>
          <span class="rounded-full bg-[#eef8f4] px-3 py-1 text-xs font-semibold text-emerald-800">
            {{ languageService.t('reports.health.source') }}
          </span>
        </div>
      </div>

      <div class="no-print flex flex-wrap items-center justify-between gap-3 border-b border-[#eee8f1] bg-[#fdfbfe] px-5 py-3 sm:px-6">
        <label class="relative min-w-[230px] flex-1 sm:max-w-md">
          <span class="sr-only">{{ languageService.t('reports.health.search') }}</span>
          <svg class="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[#8c8193]" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/></svg>
          <input
            class="w-full rounded-md border border-[#d8d0df] bg-white py-2.5 pl-10 pr-9 text-sm outline-none transition focus:border-[#8a5aa7] focus:ring-2 focus:ring-[#eee5f3]"
            type="search"
            [ngModel]="search()"
            (ngModelChange)="onSearchChange($event)"
            [placeholder]="languageService.t('reports.health.search')"
          />
          @if (search()) {
            <button class="absolute right-2 top-1/2 grid h-6 w-6 -translate-y-1/2 place-items-center rounded-full text-[#746b7b] hover:bg-[#f2ecf5]" type="button" [attr.aria-label]="languageService.t('reports.health.clearSearch')" (click)="clearSearch()">×</button>
          }
        </label>
        <div class="inline-flex rounded-lg border border-[#ddd3e3] bg-white p-1" role="group" [attr.aria-label]="languageService.t('reports.health.groupBy')">
          @for (option of groupingOptions; track option) {
            <button
              class="rounded-md px-3.5 py-2 text-sm font-semibold transition"
              type="button"
              [class.bg-[#32124f]]="groupBy() === option"
              [class.text-white]="groupBy() === option"
              [class.text-[#625969]]="groupBy() !== option"
              [attr.aria-pressed]="groupBy() === option"
              (click)="selectGrouping(option)"
            >{{ languageService.t('reports.health.groupBy.' + option) }}</button>
          }
        </div>
      </div>

      @if (loading()) {
        <div class="grid min-h-72 place-items-center" role="status">
          <div class="text-center"><span class="mx-auto block h-9 w-9 animate-spin rounded-full border-4 border-[#e6dfea] border-t-[#6d3a91]"></span><p class="mt-3 text-sm font-semibold text-[#5f5368]">{{ languageService.t('reports.loading') }}</p></div>
        </div>
      } @else if (loadFailed()) {
        <div class="grid min-h-64 place-items-center p-8 text-center">
          <div><p class="text-sm font-semibold text-red-700">{{ languageService.t('reports.loadError') }}</p><button class="mt-4 rounded-md border border-red-200 px-4 py-2 text-xs font-semibold text-red-700" type="button" (click)="load(page())">{{ languageService.t('reports.retry') }}</button></div>
        </div>
      } @else if (rows().length) {
        <div class="grid gap-px border-b border-[#eee8f1] bg-[#eee8f1] sm:grid-cols-2">
          <div class="bg-white px-5 py-4"><p class="text-[10px] font-bold uppercase tracking-[0.12em] text-[#817589]">{{ languageService.t('reports.health.totalVisits') }}</p><p class="mt-1 text-2xl font-semibold text-[#32124f]">{{ formatNumber(totalVisits()) }}</p></div>
          <div class="bg-white px-5 py-4"><p class="text-[10px] font-bold uppercase tracking-[0.12em] text-[#817589]">{{ languageService.t('reports.health.resultRows') }}</p><p class="mt-1 text-2xl font-semibold text-[#32124f]">{{ formatNumber(totalCount()) }}</p></div>
        </div>
        <div class="overflow-x-auto">
          <table class="w-full min-w-[860px] border-collapse text-left">
            <thead class="bg-[#f5f0f7] text-[10px] uppercase tracking-[0.08em] text-[#5d486b]">
              <tr>
                <th class="px-5 py-3.5 font-bold">{{ languageService.t('reports.health.period') }}</th>
                @if (report === 'diagnoses') {
                  <th class="px-4 py-3.5 font-bold">{{ languageService.t('reports.health.diagnosis') }}</th>
                  <th class="px-4 py-3.5 text-right font-bold">{{ languageService.t('reports.health.visits') }}</th>
                  <th class="px-4 py-3.5 text-right font-bold">{{ languageService.t('reports.health.students') }}</th>
                } @else if (report === 'frequent-students') {
                  <th class="px-4 py-3.5 font-bold">{{ languageService.t('reports.table.student') }}</th>
                  <th class="px-4 py-3.5 font-bold">{{ languageService.t('reports.health.class') }}</th>
                  <th class="px-4 py-3.5 text-right font-bold">{{ languageService.t('reports.health.visits') }}</th>
                } @else {
                  <th class="px-4 py-3.5 font-bold">{{ languageService.t('reports.health.examTime') }}</th>
                  <th class="px-4 py-3.5 font-bold">{{ languageService.t('reports.table.student') }}</th>
                  <th class="px-4 py-3.5 font-bold">{{ languageService.t('reports.health.class') }}</th>
                  <th class="px-4 py-3.5 font-bold">{{ languageService.t('reports.health.diagnosis') }}</th>
                }
                @if (report !== 'student-visits') {
                  <th class="px-4 py-3.5 font-bold">{{ languageService.t('reports.health.latestVisit') }}</th>
                }
                <th class="px-5 py-3.5 text-right font-bold">{{ languageService.t('reports.table.action') }}</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-[#eee8f1]">
              @for (row of rows(); track row.key) {
                <tr class="transition hover:bg-[#fdfbfe]">
                  <td class="whitespace-nowrap px-5 py-4 text-xs font-semibold text-[#6f6877]">{{ formatPeriod(row) }}</td>
                  @if (report === 'diagnoses') {
                    <td class="px-4 py-4 text-sm font-semibold text-[#32124f]">{{ row.diagnosis || '—' }}</td>
                    <td class="px-4 py-4 text-right text-sm font-semibold text-[#32124f]">{{ formatNumber(row.visitCount) }}</td>
                    <td class="px-4 py-4 text-right text-sm text-[#625969]">{{ formatNumber(row.studentCount) }}</td>
                  } @else if (report === 'frequent-students') {
                    <td class="px-4 py-4"><p class="text-sm font-semibold text-[#32124f]">{{ row.studentName }}</p><p class="mt-0.5 text-xs text-[#817589]">{{ row.sisUserId || '—' }}</p></td>
                    <td class="px-4 py-4 text-sm text-[#625969]">{{ row.className || '—' }}</td>
                    <td class="px-4 py-4 text-right text-sm font-semibold text-[#32124f]">{{ formatNumber(row.visitCount) }}</td>
                  } @else {
                    <td class="whitespace-nowrap px-4 py-4 text-sm text-[#625969]">{{ formatDateTime(row.latestVisit) }}</td>
                    <td class="px-4 py-4"><p class="text-sm font-semibold text-[#32124f]">{{ row.studentName }}</p><p class="mt-0.5 text-xs text-[#817589]">{{ row.sisUserId || '—' }}</p></td>
                    <td class="px-4 py-4 text-sm text-[#625969]">{{ row.className || '—' }}</td>
                    <td class="px-4 py-4 text-sm text-[#625969]">{{ row.diagnosis || '—' }}</td>
                  }
                  @if (report !== 'student-visits') {
                    <td class="whitespace-nowrap px-4 py-4 text-sm text-[#625969]">{{ formatDateTime(row.latestVisit) }}</td>
                  }
                  <td class="px-5 py-4 text-right"><button class="rounded-md border border-[#d8d0df] px-3 py-2 text-xs font-semibold text-[#5d2d7b] transition hover:border-[#b69ac6] hover:bg-[#f4eef7]" type="button" (click)="openDetails(row)">{{ languageService.t('reports.health.viewDetails') }}</button></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <div class="no-print flex flex-wrap items-center justify-between gap-3 border-t border-[#eee8f1] px-5 py-4">
          <span class="text-xs text-[#6f6877]">{{ pageLabel() }}</span>
          <div class="flex gap-2">
            <button class="rounded-md border border-[#d8d0df] px-4 py-2 text-xs font-semibold text-[#32124f] disabled:opacity-40" type="button" [disabled]="page() <= 1" (click)="load(page() - 1)">{{ languageService.t('pagination.previous') }}</button>
            <button class="rounded-md border border-[#d8d0df] px-4 py-2 text-xs font-semibold text-[#32124f] disabled:opacity-40" type="button" [disabled]="page() >= totalPages()" (click)="load(page() + 1)">{{ languageService.t('pagination.next') }}</button>
          </div>
        </div>
      } @else {
        <div class="grid min-h-64 place-items-center p-8 text-center text-sm text-[#817589]">{{ languageService.t(search() ? 'reports.health.noMatches' : 'reports.noData') }}</div>
      }
      <p class="border-t border-[#eee8f1] bg-[#faf8fb] px-5 py-3 text-xs leading-5 text-[#817589]">{{ languageService.t('reports.health.note') }}</p>
    </section>

    @if (detailsOpen()) {
      <div class="fixed inset-0 z-[90] grid place-items-center p-4 sm:p-6">
        <button class="absolute inset-0 bg-[#24182f]/45" type="button" [attr.aria-label]="languageService.t('reports.health.details.close')" (click)="closeDetails()"></button>
        <section class="relative flex max-h-[90vh] w-full max-w-7xl flex-col overflow-hidden rounded-xl bg-white shadow-[0_28px_90px_rgba(36,24,47,0.3)]" role="dialog" aria-modal="true" [attr.aria-label]="languageService.t('reports.health.details.title')">
          <header class="flex items-start justify-between gap-4 border-b border-[#ebe6ef] px-5 py-4 sm:px-6">
            <div><p class="text-xs font-bold uppercase tracking-[0.14em] text-[#8a6811]">{{ selectedRow() ? formatPeriod(selectedRow()!) : '' }}</p><h3 class="mt-1 text-xl font-semibold text-[#32124f]">{{ detailTitle() }}</h3><p class="mt-1 text-sm text-[#6f6877]">{{ detailCountLabel() }}</p></div>
            <button class="grid h-9 w-9 shrink-0 place-items-center rounded-full border border-[#d8d0df] text-xl text-[#32124f] hover:bg-[#f4eef7]" type="button" [attr.aria-label]="languageService.t('reports.health.details.close')" (click)="closeDetails()">×</button>
          </header>
          <div class="min-h-0 flex-1 overflow-auto">
            @if (detailsLoading()) {
              <div class="grid min-h-64 place-items-center"><span class="block h-9 w-9 animate-spin rounded-full border-4 border-[#e6dfea] border-t-[#6d3a91]"></span></div>
            } @else if (detailsLoadFailed()) {
              <div class="grid min-h-64 place-items-center p-8 text-center"><div><p class="text-sm font-semibold text-red-700">{{ languageService.t('reports.loadError') }}</p><button class="mt-3 rounded-md border border-red-200 px-4 py-2 text-xs font-semibold text-red-700" type="button" (click)="loadDetails(detailsPage())">{{ languageService.t('reports.retry') }}</button></div></div>
            } @else {
              <table class="w-full min-w-[1380px] border-collapse text-left">
                <thead class="sticky top-0 bg-[#f5f0f7] text-[10px] uppercase tracking-[0.08em] text-[#5d486b]"><tr><th class="px-5 py-3 font-bold">{{ languageService.t('reports.health.examTime') }}</th><th class="px-4 py-3 font-bold">{{ languageService.t('reports.table.student') }}</th><th class="px-4 py-3 font-bold">{{ languageService.t('reports.health.class') }}</th><th class="px-4 py-3 font-bold">{{ languageService.t('reports.health.classification') }}</th><th class="px-4 py-3 font-bold">{{ languageService.t('reports.health.diagnosis') }}</th><th class="px-4 py-3 font-bold">{{ languageService.t('reports.health.symptoms') }}</th><th class="px-4 py-3 font-bold">{{ languageService.t('reports.health.treatment') }}</th><th class="px-4 py-3 font-bold">{{ languageService.t('reports.health.medication') }}</th><th class="px-4 py-3 font-bold">{{ languageService.t('reports.health.examinedBy') }}</th><th class="px-5 py-3 font-bold">{{ languageService.t('reports.health.additional') }}</th></tr></thead>
                <tbody class="divide-y divide-[#eee8f1]">
                  @for (detail of details(); track detail.id) {
                    <tr class="align-top hover:bg-[#fdfbfe]"><td class="whitespace-nowrap px-5 py-4 text-xs text-[#625969]">{{ formatDateTime(detail.examTime) }}</td><td class="px-4 py-4">@if (detail.studentProfileId) { <a class="text-sm font-semibold text-[#5d2d7b] hover:underline" [routerLink]="['/students', detail.studentProfileId]">{{ detail.studentName }}</a> } @else { <span class="text-sm font-semibold text-[#32124f]">{{ detail.studentName }}</span> }<p class="mt-0.5 text-xs text-[#817589]">{{ detail.sisUserId || '—' }}</p></td><td class="px-4 py-4 text-sm text-[#625969]">{{ detail.className || '—' }}</td><td class="px-4 py-4 text-sm text-[#625969]">{{ detail.classification || '—' }}</td><td class="px-4 py-4 text-sm font-medium text-[#32124f]">{{ detail.diagnosis || '—' }}</td><td class="max-w-xs px-4 py-4 text-sm text-[#625969]">{{ detail.symptoms || '—' }}</td><td class="max-w-xs px-4 py-4 text-sm text-[#625969]">{{ detail.treatment || '—' }}</td><td class="px-4 py-4 text-sm text-[#625969]">{{ detail.medication || '—' }}</td><td class="px-4 py-4 text-sm text-[#625969]">{{ detail.examinedBy || '—' }}</td><td class="px-5 py-4 text-xs text-[#625969]">@if (customFieldEntries(detail).length) { <dl class="space-y-1">@for (field of customFieldEntries(detail); track field[0]) { <div><dt class="inline font-semibold text-[#32124f]">{{ field[0] }}:</dt> <dd class="inline">{{ field[1] }}</dd></div> }</dl> } @else { — }</td></tr>
                  }
                </tbody>
              </table>
            }
          </div>
          @if (!detailsLoading() && !detailsLoadFailed() && detailsTotalPages() > 1) {
            <footer class="flex items-center justify-between gap-3 border-t border-[#ebe6ef] bg-[#faf9f6] px-5 py-3"><span class="text-xs text-[#6f6877]">{{ detailsPageLabel() }}</span><div class="flex gap-2"><button class="rounded-md border border-[#d8d0df] px-3 py-2 text-xs font-semibold disabled:opacity-40" type="button" [disabled]="detailsPage() <= 1" (click)="loadDetails(detailsPage() - 1)">{{ languageService.t('pagination.previous') }}</button><button class="rounded-md border border-[#d8d0df] px-3 py-2 text-xs font-semibold disabled:opacity-40" type="button" [disabled]="detailsPage() >= detailsTotalPages()" (click)="loadDetails(detailsPage() + 1)">{{ languageService.t('pagination.next') }}</button></div></footer>
          }
        </section>
      </div>
    }
  `,
})
export class HealthReportComponent implements OnChanges, OnDestroy {
  @Input({ required: true }) report!: HealthReportType;
  @Input() academicYear = '';
  @Input() term = 'all';
  @Input() periodLabel = '';

  private readonly reports = inject(HealthReportsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly languageService = inject(LanguageService);
  readonly groupingOptions: HealthReportGrouping[] = ['week', 'month', 'year'];
  readonly groupBy = signal<HealthReportGrouping>(this.initialGrouping());
  readonly search = signal('');
  readonly rows = signal<HealthReportRow[]>([]);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly page = signal(1);
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly totalVisits = signal(0);
  readonly detailsOpen = signal(false);
  readonly selectedRow = signal<HealthReportRow | null>(null);
  readonly details = signal<HealthVisitReportDetail[]>([]);
  readonly detailsLoading = signal(false);
  readonly detailsLoadFailed = signal(false);
  readonly detailsPage = signal(1);
  readonly detailsTotalCount = signal(0);
  readonly detailsTotalPages = signal(0);
  private readonly pageSize = 20;
  private readonly detailsPageSize = 20;
  private requestId = 0;
  private detailsRequestId = 0;
  private searchTimer: number | undefined;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['report'] || changes['academicYear'] || changes['term']) {
      this.closeDetails();
      this.load(1);
    }
  }

  ngOnDestroy(): void {
    if (this.searchTimer !== undefined) window.clearTimeout(this.searchTimer);
  }

  @HostListener('document:keydown.escape')
  closeDetailsOnEscape(): void {
    if (this.detailsOpen()) this.closeDetails();
  }

  titleKey(): string {
    return `reports.health.${this.report}.title`;
  }

  descriptionKey(): string {
    return `reports.health.${this.report}.description`;
  }

  selectGrouping(grouping: HealthReportGrouping): void {
    if (this.groupBy() === grouping) return;
    this.groupBy.set(grouping);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { healthPeriod: grouping },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
    this.load(1);
  }

  onSearchChange(value: string): void {
    this.search.set(value);
    if (this.searchTimer !== undefined) window.clearTimeout(this.searchTimer);
    this.searchTimer = window.setTimeout(() => {
      this.searchTimer = undefined;
      this.load(1);
    }, 300);
  }

  clearSearch(): void {
    if (this.searchTimer !== undefined) window.clearTimeout(this.searchTimer);
    this.searchTimer = undefined;
    this.search.set('');
    this.load(1);
  }

  load(page: number): void {
    if (!this.report || !this.academicYear) return;
    const requestId = ++this.requestId;
    this.loading.set(true);
    this.loadFailed.set(false);
    this.reports.summary(this.report, this.groupBy(), this.academicYear, this.term, this.search(), page, this.pageSize).subscribe({
      next: (response) => {
        if (requestId !== this.requestId) return;
        this.rows.set(response.items);
        this.page.set(response.page);
        this.totalCount.set(response.totalCount);
        this.totalPages.set(response.totalPages);
        this.totalVisits.set(response.totalVisits);
        this.loading.set(false);
      },
      error: () => {
        if (requestId !== this.requestId) return;
        this.loadFailed.set(true);
        this.loading.set(false);
      },
    });
  }

  openDetails(row: HealthReportRow): void {
    this.selectedRow.set(row);
    this.detailsOpen.set(true);
    this.loadDetails(1);
  }

  closeDetails(): void {
    this.detailsRequestId++;
    this.detailsOpen.set(false);
    this.selectedRow.set(null);
    this.details.set([]);
    this.detailsLoading.set(false);
  }

  loadDetails(page: number): void {
    const row = this.selectedRow();
    if (!row) return;
    const query: HealthReportDetailsQuery = { periodStart: row.periodStart, periodEnd: row.periodEnd };
    if (this.report === 'diagnoses') query.diagnosis = row.diagnosis || undefined;
    else if (this.report === 'student-visits') query.visitId = row.visitId || undefined;
    else {
      query.studentProfileId = row.studentProfileId || undefined;
      query.sisUserId = row.sisUserId || undefined;
      query.studentName = row.studentName;
      query.className = row.className || undefined;
    }
    const requestId = ++this.detailsRequestId;
    this.detailsLoading.set(true);
    this.detailsLoadFailed.set(false);
    this.reports.details(query, page, this.detailsPageSize).subscribe({
      next: (response) => {
        if (requestId !== this.detailsRequestId) return;
        this.details.set(response.items);
        this.detailsPage.set(response.page);
        this.detailsTotalCount.set(response.totalCount);
        this.detailsTotalPages.set(response.totalPages);
        this.detailsLoading.set(false);
      },
      error: () => {
        if (requestId !== this.detailsRequestId) return;
        this.detailsLoadFailed.set(true);
        this.detailsLoading.set(false);
      },
    });
  }

  detailTitle(): string {
    const row = this.selectedRow();
    if (!row) return this.languageService.t('reports.health.details.title');
    return this.report === 'diagnoses' ? row.diagnosis || '—' : row.studentName;
  }

  detailCountLabel(): string {
    return this.languageService.t('reports.health.details.count').replace('{count}', this.formatNumber(this.detailsTotalCount()));
  }

  formatPeriod(row: HealthReportRow): string {
    const start = new Date(row.periodStart);
    const end = new Date(row.periodEnd);
    const locale = this.languageService.language() === 'vi' ? 'vi-VN' : 'en-GB';
    if (this.groupBy() === 'year') return String(start.getUTCFullYear());
    if (this.groupBy() === 'month') return new Intl.DateTimeFormat(locale, { month: '2-digit', year: 'numeric', timeZone: 'UTC' }).format(start);
    const lastDay = new Date(end.getTime() - 86_400_000);
    const formatter = new Intl.DateTimeFormat(locale, { day: '2-digit', month: '2-digit', year: 'numeric', timeZone: 'UTC' });
    return `${formatter.format(start)} – ${formatter.format(lastDay)}`;
  }

  formatDateTime(value: string): string {
    return new Intl.DateTimeFormat(this.languageService.language() === 'vi' ? 'vi-VN' : 'en-GB', {
      dateStyle: 'short',
      timeStyle: 'short',
    }).format(new Date(value));
  }

  formatNumber(value: number): string {
    return value.toLocaleString(this.languageService.language() === 'vi' ? 'vi-VN' : 'en-US');
  }

  customFieldEntries(detail: HealthVisitReportDetail): [string, string][] {
    return Object.entries(detail.customFields ?? {});
  }

  pageLabel(): string {
    return this.paginationLabel(this.page(), this.totalPages(), this.totalCount());
  }

  detailsPageLabel(): string {
    return this.paginationLabel(this.detailsPage(), this.detailsTotalPages(), this.detailsTotalCount());
  }

  private paginationLabel(page: number, pages: number, total: number): string {
    return this.languageService.t('pagination.pageOf')
      .replace('{page}', String(page))
      .replace('{pages}', String(pages))
      .replace('{total}', String(total));
  }

  private initialGrouping(): HealthReportGrouping {
    const value = this.route.snapshot.queryParamMap.get('healthPeriod');
    return value === 'week' || value === 'year' ? value : 'month';
  }
}
