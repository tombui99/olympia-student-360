import { Component, computed, inject, Input, OnChanges, OnDestroy, OnInit, signal, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router, RouterLink } from '@angular/router';
import { firstValueFrom, Subscription } from 'rxjs';
import { LanguageService } from '../core/language/language.service';
import { ToastService } from '../core/toast/toast.service';
import { ReportExcelExportService, ReportExportLimitError } from './report-excel-export.service';
import { ReportExportButtonComponent } from './report-export-button.component';
import { SearchableMultiSelectComponent } from './searchable-multi-select.component';
import {
  AssessmentCategory,
  LeadershipBreakdownRow,
  LeadershipFinalCourseScores,
  LeadershipFinalScoreBreakdown,
  LeadershipReportKey,
  LeadershipReportOptions,
  LeadershipReportQuery,
  LeadershipReportResult,
  LeadershipReportsService,
} from './leadership-reports.service';

export type LeadershipMenuReportKey = 'school-learning-engagement' | 'school-assessment-results';

interface BreakdownPanel {
  key: keyof LeadershipReportResult['breakdowns'];
  titleKey: string;
  rows: LeadershipBreakdownRow[];
}

interface FinalScorePanel {
  key: keyof Pick<LeadershipFinalCourseScores, 'byCourse' | 'byGrade' | 'byHomeroom' | 'byMonth'>;
  titleKey: string;
  rows: LeadershipFinalScoreBreakdown[];
}

@Component({
  selector: 'app-leadership-report',
  imports: [FormsModule, RouterLink, SearchableMultiSelectComponent, ReportExportButtonComponent],
  template: `
    <section class="relative overflow-visible rounded-xl border border-[#e7e0eb] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.07)]" [attr.aria-busy]="loading() || optionsLoading()">
      <header class="border-b border-[#eee8f1] px-5 py-5 sm:px-6">
        <div class="flex flex-wrap items-start justify-between gap-4">
          <div>
            <p class="text-xs font-bold uppercase tracking-[0.14em] text-[#8a6811]">{{ periodLabel }}</p>
            <h3 class="mt-1 text-xl font-semibold text-[#32124f]">{{ t(titleKey()) }}</h3>
            <p class="mt-1 max-w-3xl text-sm leading-6 text-[#6f6877]">{{ t(descriptionKey()) }}</p>
          </div>
          <div class="flex flex-col items-start gap-2 sm:items-end">
            <app-report-export-button [exporting]="exporting()" [disabled]="loading() || optionsLoading() || !!error() || !data()?.totalCount || !data()?.aggregateAvailable" (exportRequested)="exportExcel()" />
            <span class="rounded-full bg-[#f2ecf5] px-3 py-1 text-xs font-semibold text-[#5d2d7b]">{{ t('reports.leadership.source') }}</span>
          </div>
        </div>
      </header>

      <form class="no-print border-b border-[#eee8f1] bg-[#fdfbfe] px-5 py-5 sm:px-6" (ngSubmit)="run(1, true)">
        <div class="mb-4 flex flex-wrap items-baseline justify-between gap-2">
          <h4 class="text-sm font-semibold text-[#32124f]">{{ t('reports.leadership.filters') }}</h4>
        </div>
        @if (optionsError()) {
          <div class="mb-4 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900" role="alert">
            {{ t('reports.loadError') }}
            <button class="ml-2 font-semibold underline" type="button" (click)="loadOptions()">{{ t('reports.retry') }}</button>
          </div>
        }
        <div class="grid min-w-0 items-start gap-4 md:grid-cols-2 xl:grid-cols-3 [&>*]:min-w-0">
          <app-searchable-multi-select
            [label]="t('reports.leadership.subaccounts')"
            [searchPlaceholder]="t('reports.leadership.searchSubaccounts')"
            [emptySelectionLabel]="t('reports.leadership.allSubaccounts')"
            [options]="subaccountChoices()" [selected]="selectedSubaccountOptionIds()"
            (selectedChange)="selectSubaccounts($event)" />
          <label class="grid min-w-0 gap-1 text-sm font-semibold text-[#32124f]">
            {{ t('reports.leadership.grade') }}
            <select class="h-11 w-full min-w-0 max-w-full rounded-md border border-[#d8d0df] bg-white px-3 font-normal" name="leadershipGrade" [(ngModel)]="query.grade" (ngModelChange)="onGradeChange()">
              <option value="">{{ t('reports.leadership.allGrades') }}</option>
              @for (grade of gradeChoices(); track grade) { <option [value]="grade">{{ grade }}</option> }
            </select>
          </label>
          <label class="grid min-w-0 gap-1 text-sm font-semibold text-[#32124f]">
            {{ t('reports.leadership.homeroom') }}
            <select class="h-11 w-full min-w-0 max-w-full rounded-md border border-[#d8d0df] bg-white px-3 font-normal" name="leadershipHomeroom" [(ngModel)]="query.homeroom" (ngModelChange)="onHomeroomChange()">
              <option value="">{{ t('reports.leadership.allHomerooms') }}</option>
              @for (homeroom of homeroomChoices(); track homeroom) { <option [value]="homeroom">{{ homeroom }}</option> }
            </select>
          </label>
          <label class="grid min-w-0 gap-1 text-sm font-semibold text-[#32124f]">
            {{ t('reports.leadership.assessmentCategory') }}
            <select class="h-11 w-full min-w-0 max-w-full rounded-md border border-[#d8d0df] bg-white px-3 font-normal" name="leadershipCategory" [(ngModel)]="query.assessmentCategory">
              @for (category of categoryChoices(); track category) {
                <option [value]="category">{{ t('reports.leadership.category.' + category) }}</option>
              }
            </select>
          </label>
        </div>
        <div class="mt-5 flex flex-wrap items-center gap-2">
          <button type="submit" class="rounded-md bg-[#32124f] px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-[#4a1b73] disabled:opacity-50" [disabled]="loading() || optionsLoading() || !academicYear">{{ t('reports.leadership.run') }}</button>
          <button type="button" class="rounded-md border border-[#d8d0df] bg-white px-4 py-2.5 text-sm font-semibold text-[#32124f] transition hover:bg-[#f5f0f7]" (click)="resetFilters()">{{ t('reports.leadership.reset') }}</button>
        </div>
      </form>

      @if (loading() && data()) {
        <div class="no-print absolute inset-0 z-30 flex justify-center rounded-xl bg-white/70 pt-40" role="status">
          <div class="flex h-fit items-center gap-3 rounded-full bg-white px-5 py-3 text-sm font-semibold text-[#5f5368] shadow-md">
            <span class="h-6 w-6 animate-spin rounded-full border-2 border-[#e6dfea] border-t-[#6d3a91]"></span>{{ t('reports.loading') }}
          </div>
        </div>
      }
      @if (error()) {
        <div class="m-5 rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-800" role="alert">
          {{ error() }}
          <button class="ml-2 font-semibold underline" type="button" (click)="run(currentPage(), true)">{{ t('reports.retry') }}</button>
        </div>
      }
      @if (loading() && !data()) {
        <div class="grid min-h-80 place-items-center" role="status">
          <div class="flex items-center gap-3 text-sm font-semibold text-[#625969]"><span class="h-7 w-7 animate-spin rounded-full border-2 border-[#e6dfea] border-t-[#6d3a91]"></span>{{ t('reports.loading') }}</div>
        </div>
      } @else if (data(); as result) {
        @if (!result.aggregateAvailable) {
          <div class="m-5 rounded-lg border border-amber-200 bg-amber-50 p-5 text-sm leading-6 text-amber-900">{{ t('reports.leadership.unavailable') }}</div>
        } @else {
          <div class="grid gap-px border-b border-[#eee8f1] bg-[#eee8f1] sm:grid-cols-2 xl:grid-cols-4">
            <div class="bg-white px-5 py-5"><p class="text-[10px] font-bold uppercase tracking-[0.12em] text-[#817589]">{{ t('reports.leadership.students') }}</p><p class="mt-1 text-2xl font-semibold text-[#32124f]">{{ formatNumber(result.summary.studentCount) }}</p></div>
            <div class="bg-white px-5 py-5"><p class="text-[10px] font-bold uppercase tracking-[0.12em] text-[#817589]">{{ t('reports.leadership.courses') }}</p><p class="mt-1 text-2xl font-semibold text-[#32124f]">{{ formatNumber(result.summary.courseCount) }}</p></div>
            <div class="bg-white px-5 py-5"><p class="text-[10px] font-bold uppercase tracking-[0.12em] text-[#817589]">{{ t('reports.leadership.activities') }}</p><p class="mt-1 text-2xl font-semibold text-[#32124f]">{{ formatNumber(result.summary.activityCount) }}</p></div>
            <div class="bg-white px-5 py-5"><p class="text-[10px] font-bold uppercase tracking-[0.12em] text-[#817589]">{{ t(primaryLabelKey()) }}</p><p class="mt-1 text-2xl font-semibold text-[#32124f]">{{ formatPercent(primarySummaryValue(result)) }}</p></div>
          </div>
          <div class="grid gap-px border-b border-[#eee8f1] bg-[#eee8f1] sm:grid-cols-2 xl:grid-cols-6">
            <div class="bg-[#fdfbfe] px-5 py-4"><p class="text-xs text-[#817589]">{{ t('reports.leadership.submitted') }}</p><p class="mt-1 text-lg font-semibold text-[#32124f]">{{ formatNumber(result.summary.submitted) }}</p></div>
            <div class="bg-[#fdfbfe] px-5 py-4"><p class="text-xs text-[#817589]">{{ t('reports.leadership.missing') }}</p><p class="mt-1 text-lg font-semibold text-[#ad3444]">{{ formatNumber(result.summary.missing) }}</p></div>
            <div class="bg-[#fdfbfe] px-5 py-4"><p class="text-xs text-[#817589]">{{ t('reports.leadership.late') }}</p><p class="mt-1 text-lg font-semibold text-[#996b11]">{{ formatNumber(result.summary.late) }}</p></div>
            <div class="bg-[#fdfbfe] px-5 py-4"><p class="text-xs text-[#817589]">{{ t('reports.leadership.graded') }}</p><p class="mt-1 text-lg font-semibold text-[#32124f]">{{ formatNumber(result.summary.graded) }}</p></div>
            <div class="bg-[#fdfbfe] px-5 py-4"><p class="text-xs text-[#817589]">{{ t('reports.leadership.gradingRate') }}</p><p class="mt-1 text-lg font-semibold text-[#32124f]">{{ formatPercent(result.summary.gradingRatePercent) }}</p></div>
            <div class="bg-[#fdfbfe] px-5 py-4"><p class="text-xs text-[#817589]">{{ t('reports.leadership.averageScore') }}</p><p class="mt-1 text-lg font-semibold text-[#32124f]">{{ formatPercent(result.summary.averageScorePercent) }}</p></div>
          </div>
          <p class="border-b border-[#eee8f1] bg-[#faf8fb] px-5 py-3 text-xs leading-5 text-[#756b7c] sm:px-6">{{ t('reports.leadership.methodology') }}</p>

          @if (reportKey === 'school-assessment-results' && result.finalCourseScores; as finalScores) {
            <section class="border-b border-[#e7d9b5] bg-[#fffaf0]">
              <div class="flex flex-wrap items-start justify-between gap-3 px-5 pb-4 pt-6 sm:px-6">
                <div>
                  <p class="text-[10px] font-bold uppercase tracking-[0.14em] text-[#8a6811]">Canvas</p>
                  <h4 class="mt-1 text-lg font-semibold text-[#32124f]">{{ t('reports.leadership.finalScores.title') }}</h4>
                  <p class="mt-1 max-w-3xl text-sm leading-6 text-[#756b7c]">{{ t('reports.leadership.finalScores.description') }}</p>
                </div>
                <app-report-export-button [disabled]="!finalScores.summary.scoreCount" (exportRequested)="exportFinalScores(finalScores)" />
              </div>
              <div class="mx-5 grid gap-px overflow-hidden rounded-lg border border-[#eadfca] bg-[#eadfca] sm:grid-cols-2 xl:grid-cols-4 sm:mx-6">
                <div class="bg-white px-4 py-4"><p class="text-[10px] font-bold uppercase tracking-[0.12em] text-[#817589]">{{ t('reports.leadership.students') }}</p><p class="mt-1 text-xl font-semibold text-[#32124f]">{{ formatNumber(finalScores.summary.studentCount) }}</p></div>
                <div class="bg-white px-4 py-4"><p class="text-[10px] font-bold uppercase tracking-[0.12em] text-[#817589]">{{ t('reports.leadership.courses') }}</p><p class="mt-1 text-xl font-semibold text-[#32124f]">{{ formatNumber(finalScores.summary.courseCount) }}</p></div>
                <div class="bg-white px-4 py-4"><p class="text-[10px] font-bold uppercase tracking-[0.12em] text-[#817589]">{{ t('reports.leadership.finalScores.scoreCount') }}</p><p class="mt-1 text-xl font-semibold text-[#32124f]">{{ formatNumber(finalScores.summary.scoreCount) }}</p></div>
                <div class="bg-white px-4 py-4"><p class="text-[10px] font-bold uppercase tracking-[0.12em] text-[#817589]">{{ t('reports.leadership.finalScores.average') }}</p><p class="mt-1 text-xl font-semibold text-[#32124f]">{{ formatPercent(finalScores.summary.averageScorePercent) }}</p></div>
              </div>
              @if (finalScores.summary.scoreCount > 0) {
                <div class="grid gap-4 p-5 xl:grid-cols-2 sm:p-6">
                  @for (panel of finalScorePanels(finalScores); track panel.key) {
                    <section class="min-w-0 overflow-hidden rounded-lg border border-[#eadfca] bg-white">
                      <div class="flex items-center justify-between border-b border-[#eee8f1] px-4 py-3">
                        <h5 class="text-sm font-semibold text-[#32124f]">{{ t(panel.titleKey) }}</h5>
                        <span class="text-xs text-[#817589]">{{ panel.rows.length }}</span>
                      </div>
                      <div class="max-h-[330px] divide-y divide-[#f0ebf2] overflow-y-auto">
                        @for (row of panel.rows; track row.key) {
                          <div class="grid grid-cols-[minmax(0,1fr)_88px] items-center gap-4 px-4 py-3">
                            <div class="min-w-0">
                              <p class="truncate text-sm font-semibold text-[#32124f]" [title]="row.label">{{ panel.key === 'byMonth' && row.key === 'Undated' ? t('reports.leadership.undated') : row.label }}</p>
                              <p class="mt-0.5 text-xs text-[#817589]">{{ formatNumber(row.scoreCount) }} {{ t('reports.leadership.finalScores.scoreCount') }} · {{ formatNumber(row.studentCount) }} {{ t('reports.leadership.students') }}</p>
                              <div class="mt-2 h-1.5 overflow-hidden rounded-full bg-[#f3eee4]"><div class="h-full rounded-full bg-[#b78928]" [style.width.%]="percentWidth(row.averageScorePercent)"></div></div>
                            </div>
                            <span class="text-right text-sm font-bold text-[#32124f]">{{ formatPercent(row.averageScorePercent) }}</span>
                          </div>
                        }
                      </div>
                    </section>
                  }
                </div>
              } @else {
                <p class="px-5 py-7 text-sm text-[#817589] sm:px-6">{{ t('reports.leadership.finalScores.noData') }}</p>
              }
            </section>
          }

          @if (result.totalCount === 0) {
            <div class="grid min-h-52 place-items-center px-6 py-10 text-center text-sm text-[#817589]">{{ t('reports.leadership.noRows') }}</div>
          } @else {
            <div class="grid gap-4 bg-[#faf8fb] p-5 xl:grid-cols-2">
              @for (panel of breakdownPanels(result); track panel.key) {
                <section class="min-w-0 overflow-hidden rounded-lg border border-[#e7e0eb] bg-white">
                  <div class="flex items-center justify-between border-b border-[#eee8f1] px-4 py-3">
                    <h4 class="text-sm font-semibold text-[#32124f]">{{ t(panel.titleKey) }}</h4>
                    <span class="text-xs text-[#817589]">{{ panel.rows.length }}</span>
                  </div>
                  @if (panel.rows.length) {
                    <div class="max-h-[330px] divide-y divide-[#f0ebf2] overflow-y-auto">
                      @for (row of panel.rows; track row.key) {
                        <div class="grid grid-cols-[minmax(0,1fr)_88px] items-center gap-4 px-4 py-3">
                          <div class="min-w-0">
                            <p class="truncate text-sm font-semibold text-[#32124f]" [title]="row.label">{{ breakdownLabel(panel.key, row) }}</p>
                            <p class="mt-0.5 text-xs text-[#817589]">{{ formatNumber(row.studentCount) }} {{ t('reports.leadership.students') }} · {{ formatNumber(row.courseCount) }} {{ t('reports.leadership.courses') }}</p>
                            <div class="mt-2 h-1.5 overflow-hidden rounded-full bg-[#eee8f1]"><div class="h-full rounded-full bg-[#6d3a91]" [style.width.%]="barWidth(row)"></div></div>
                          </div>
                          <span class="text-right text-sm font-bold text-[#32124f]">{{ formatPercent(primaryValue(row)) }}</span>
                        </div>
                      }
                    </div>
                  } @else {
                    <p class="px-4 py-8 text-center text-sm text-[#817589]">{{ t('reports.leadership.noRows') }}</p>
                  }
                </section>
              }
            </div>

            <section class="border-t border-[#eee8f1]">
              <div class="flex flex-wrap items-center justify-between gap-2 px-5 py-4 sm:px-6">
                <div><h4 class="text-base font-semibold text-[#32124f]">{{ t('reports.leadership.detail') }}</h4><p class="mt-0.5 text-xs text-[#817589]">{{ formatNumber(result.totalCount) }} {{ t('reports.leadership.pageResults') }}</p></div>
                <span class="rounded-full bg-[#f2ecf5] px-3 py-1 text-xs font-semibold text-[#5d2d7b]">{{ t(primaryLabelKey()) }}</span>
              </div>
              <div class="overflow-x-auto">
                <table class="w-full min-w-[1160px] border-collapse text-left text-sm">
                  <thead class="bg-[#f5f0f7] text-[10px] uppercase tracking-[0.08em] text-[#5d486b]"><tr>
                    <th class="px-5 py-3">{{ t('reports.leadership.students') }}</th>
                    <th class="px-3 py-3">{{ t('reports.leadership.grade') }}</th>
                    <th class="px-3 py-3">{{ t('reports.leadership.homeroom') }}</th>
                    <th class="px-3 py-3">{{ t('reports.leadership.byCourse') }}</th>
                    <th class="px-3 py-3">{{ t('reports.leadership.subaccounts') }}</th>
                    <th class="px-3 py-3">{{ t('reports.leadership.category') }}</th>
                    <th class="px-3 py-3">{{ t('reports.leadership.group') }}</th>
                    <th class="px-3 py-3 text-right">{{ t('reports.leadership.activities') }}</th>
                    <th class="px-3 py-3 text-right">{{ t('reports.leadership.submitted') }}</th>
                    <th class="px-3 py-3 text-right">{{ t('reports.leadership.missing') }}</th>
                    <th class="px-3 py-3 text-right">{{ t('reports.leadership.late') }}</th>
                    <th class="px-5 py-3 text-right">{{ t('reports.leadership.averageScore') }}</th>
                  </tr></thead>
                  <tbody class="divide-y divide-[#eee8f1]">
                    @for (row of result.rows; track $index) {
                      <tr class="hover:bg-[#fdfbfe]">
                        <td class="px-5 py-3 font-semibold text-[#32124f]">
                          @if (row.studentId) { <a class="hover:text-[#6d3a91] hover:underline" [routerLink]="['/students', row.studentId]">{{ row.name }}</a> }
                          @else { {{ row.name }} }
                          @if (row.sisUserId) { <span class="block text-xs font-normal text-[#817589]">{{ row.sisUserId }}</span> }
                        </td>
                        <td class="px-3 py-3">{{ row.grade || '—' }}</td>
                        <td class="px-3 py-3">{{ row.homeroom || '—' }}</td>
                        <td class="px-3 py-3 font-medium text-[#32124f]">{{ row.courseName }}</td>
                        <td class="px-3 py-3 text-[#625969]">{{ row.subaccountName || '—' }}</td>
                        <td class="px-3 py-3 text-[#625969]">{{ row.assessmentCategory ? categoryLabel(row.assessmentCategory) : reportKey === 'school-learning-engagement' ? t('reports.leadership.allCategories') : '—' }}</td>
                        <td class="px-3 py-3 text-[#625969]">{{ row.assignmentGroupName || '—' }}</td>
                        <td class="px-3 py-3 text-right">{{ formatNumber(row.activityCount) }}</td>
                        <td class="px-3 py-3 text-right">{{ formatNumber(row.submitted) }}</td>
                        <td class="px-3 py-3 text-right">{{ formatNumber(row.missing) }}</td>
                        <td class="px-3 py-3 text-right">{{ formatNumber(row.late) }}</td>
                        <td class="px-5 py-3 text-right font-semibold text-[#32124f]">{{ formatPercent(row.averageScorePercent) }}</td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
              <div class="no-print flex flex-wrap items-center justify-between gap-3 border-t border-[#eee8f1] px-5 py-4 text-sm text-[#5d486b] sm:px-6">
                <span>{{ t('pagination.page') }} {{ result.page }} / {{ totalPages() }} · {{ formatNumber(result.totalCount) }} {{ t('reports.leadership.pageResults') }}</span>
                <div class="flex gap-2">
                  <button class="rounded-md border border-[#d8d0df] px-3 py-2 font-semibold text-[#32124f] disabled:opacity-40" type="button" [disabled]="loading() || result.page <= 1" (click)="changePage(result.page - 1)">{{ t('pagination.previous') }}</button>
                  <button class="rounded-md border border-[#d8d0df] px-3 py-2 font-semibold text-[#32124f] disabled:opacity-40" type="button" [disabled]="loading() || result.page >= totalPages()" (click)="changePage(result.page + 1)">{{ t('pagination.next') }}</button>
                </div>
              </div>
            </section>
          }
        }
      }
    </section>
  `,
})
export class LeadershipReportComponent implements OnInit, OnChanges, OnDestroy {
  @Input({ required: true }) reportKey: LeadershipMenuReportKey = 'school-learning-engagement';
  @Input({ required: true }) academicYear = '';
  @Input({ required: true }) term = 'all';
  @Input({ required: true }) periodLabel = '';

  private readonly reports = inject(LeadershipReportsService);
  private readonly language = inject(LanguageService);
  private readonly excel = inject(ReportExcelExportService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly options = signal<LeadershipReportOptions | null>(null);
  readonly data = signal<LeadershipReportResult | null>(null);
  readonly loading = signal(false);
  readonly optionsLoading = signal(true);
  readonly optionsError = signal(false);
  readonly exporting = signal(false);
  readonly error = signal<string | null>(null);
  subaccountChoices(): Array<{ value: number; label: string; detail?: string }> {
    const constrained = !!(this.query.grade || this.query.homeroom);
    const grades = this.gradesForCurrentCohortSelection();
    return (this.options()?.subaccounts ?? [])
      .filter(account => !constrained || account.gradeLevels.some(grade => grades.has(grade)))
      .map(account => ({ value: account.id, label: account.name }));
  }
  selectedSubaccountOptionIds(): number[] {
    return this.selectedSubaccountOptions(this.query.subaccountIds).map(account => account.id);
  }
  readonly totalPages = computed(() => Math.max(1, Math.ceil((this.data()?.totalCount ?? 0) / (this.data()?.pageSize ?? 20))));
  query: LeadershipReportQuery = this.defaultQuery();
  private initialized = false;
  private destroyed = false;
  private requestVersion = 0;
  private optionsSubscription?: Subscription;
  private runSubscription?: Subscription;
  private pendingRequestKey: string | null = null;
  private appliedRequestKey: string | null = null;
  private appliedQuery: LeadershipReportQuery | null = null;

  ngOnInit(): void {
    this.initialized = true;
    this.query = this.queryFromUrl();
    // The report only needs the selected values from the URL. Fetch filter choices in
    // parallel so a slow options query does not delay the first report result.
    this.run(this.query.page);
    this.loadOptions();
  }

  loadOptions(): void {
    this.optionsSubscription?.unsubscribe();
    this.optionsLoading.set(true);
    this.optionsError.set(false);
    this.optionsSubscription = this.reports.options().subscribe({
      next: options => {
        if (this.destroyed) return;
        this.options.set(options);
        this.optionsLoading.set(false);
        if (this.normalizeCohortSelection()) this.run(1, true);
      },
      error: () => {
        if (this.destroyed) return;
        this.optionsLoading.set(false);
        this.optionsError.set(true);
      },
    });
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!this.initialized) return;
    if (!changes['reportKey'] && !changes['academicYear'] && !changes['term']) return;
    if (this.query.reportKey !== this.apiReportKey()) this.query = this.queryFromUrl();
    this.query.reportKey = this.apiReportKey();
    this.query.academicYear = this.academicYear;
    this.query.term = this.term;
    this.run();
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    this.requestVersion++;
    this.optionsSubscription?.unsubscribe();
    this.runSubscription?.unsubscribe();
  }

  t(key: string): string { return this.language.t(key); }
  titleKey(): string { return this.reportKey === 'school-learning-engagement' ? 'reports.leadership.engagement.title' : 'reports.leadership.assessment.title'; }
  descriptionKey(): string { return this.reportKey === 'school-learning-engagement' ? 'reports.leadership.engagement.description' : 'reports.leadership.assessment.description'; }
  currentPage(): number { return this.data()?.page ?? 1; }
  formatNumber(value: number): string { return Number.isFinite(value) ? new Intl.NumberFormat(this.language.language() === 'vi' ? 'vi-VN' : 'en-US').format(value) : '—'; }
  formatPercent(value: number | null): string { return value == null || !Number.isFinite(value) ? '—' : `${value.toFixed(1)}%`; }
  categoryChoices(): AssessmentCategory[] {
    return (this.options()?.assessmentCategories ?? [])
      .filter((category): category is AssessmentCategory =>
        category === 'all' || category === 'HS1' || category === 'HS2' ||
        category === 'HS3' || category === 'GPA');
  }
  gradeChoices(): string[] {
    const rows = this.options()?.cohorts ?? [];
    if (rows.length === 0) return this.options()?.grades ?? [];
    const subaccountGrades = this.selectedSubaccountGrades();
    return this.sortedUnique(rows.filter(row =>
      (!this.query.homeroom || row.homeroom === this.query.homeroom) &&
      (!subaccountGrades.size || subaccountGrades.has(row.grade)))
      .map(row => row.grade));
  }
  homeroomChoices(): string[] {
    const rows = this.options()?.cohorts ?? [];
    if (rows.length === 0) return this.options()?.homerooms ?? [];
    const subaccountGrades = this.selectedSubaccountGrades();
    return this.sortedUnique(rows.filter(row =>
      (!this.query.grade || row.grade === this.query.grade) &&
      (!subaccountGrades.size || subaccountGrades.has(row.grade)))
      .map(row => row.homeroom));
  }
  selectSubaccounts(values: Array<string | number>): void {
    this.query.subaccountIds = values.map(Number).filter(Number.isSafeInteger);
    if (this.query.homeroom && !this.homeroomChoices().includes(this.query.homeroom)) this.query.homeroom = '';
    if (this.query.grade && !this.gradeChoices().includes(this.query.grade)) this.query.grade = '';
  }
  onGradeChange(): void {
    this.clearInvalidSubaccounts();
    if (this.query.homeroom && !this.homeroomChoices().includes(this.query.homeroom)) this.query.homeroom = '';
  }
  onHomeroomChange(): void {
    this.clearInvalidSubaccounts();
    if (this.query.grade && !this.gradeChoices().includes(this.query.grade)) this.query.grade = '';
  }

  resetFilters(): void {
    this.query = this.defaultQuery();
    this.run();
  }

  run(page = 1, force = false): void {
    if (!this.academicYear) {
      this.runSubscription?.unsubscribe();
      this.pendingRequestKey = null;
      this.loading.set(false);
      this.data.set(null);
      return;
    }
    const request = {
      ...this.query,
      reportKey: this.apiReportKey(), academicYear: this.academicYear, term: this.term,
      subaccountIds: [...new Set(this.query.subaccountIds)].sort((a, b) => a - b),
      page, pageSize: 20, includeSummary: page === 1 || !this.data(),
    };
    const requestKey = JSON.stringify({ ...request, includeSummary: undefined });
    if (this.loading() && this.pendingRequestKey === requestKey) return;
    if (!force && !this.loading() && !this.error() && this.data() && this.appliedRequestKey === requestKey) return;
    // Abort superseded HTTP requests so the server can stop work when it honors cancellation.
    this.runSubscription?.unsubscribe();
    const version = ++this.requestVersion;
    this.pendingRequestKey = requestKey;
    this.loading.set(true);
    this.error.set(null);
    this.syncQueryToUrl(page);
    this.runSubscription = this.reports.run(request).subscribe({
      next: result => {
        if (version !== this.requestVersion) return;
        const previous = this.data();
        this.data.set(request.includeSummary || !previous ? result : {
          ...result,
          summary: previous.summary,
          breakdowns: previous.breakdowns,
          finalCourseScores: previous.finalCourseScores,
        });
        const { includeSummary: _includeSummary, ...appliedRequest } = request;
        this.appliedQuery = { ...appliedRequest, page: result.page };
        this.appliedRequestKey = JSON.stringify(this.appliedQuery);
        this.pendingRequestKey = null;
        this.loading.set(false);
        if (result.page !== page) this.syncQueryToUrl(result.page);
      },
      error: () => {
        if (version !== this.requestVersion) return;
        this.pendingRequestKey = null;
        this.error.set(this.t('reports.loadError'));
        this.loading.set(false);
      },
    });
  }

  changePage(page: number): void {
    if (page < 1 || page > this.totalPages() || this.loading()) return;
    if (this.appliedQuery && JSON.stringify({ ...this.appliedQuery, page: 1 }) !==
        JSON.stringify({ ...this.query, reportKey: this.apiReportKey(), academicYear: this.academicYear,
          term: this.term, page: 1, pageSize: 20 })) {
      this.run();
      return;
    }
    this.run(page);
  }

  primaryValue(row: LeadershipBreakdownRow): number | null {
    return this.reportKey === 'school-assessment-results'
      ? row.averageScorePercent : row.completionRatePercent;
  }
  primaryLabelKey(): string {
    return this.reportKey === 'school-assessment-results'
      ? 'reports.leadership.averageScore' : 'reports.leadership.completionRate';
  }
  primarySummaryValue(result: LeadershipReportResult): number | null {
    return this.reportKey === 'school-assessment-results'
      ? result.summary.averageScorePercent : result.summary.completionRatePercent;
  }
  barWidth(row: LeadershipBreakdownRow): number { return Math.max(0, Math.min(100, this.primaryValue(row) ?? 0)); }
  percentWidth(value: number | null): number { return Math.max(0, Math.min(100, value ?? 0)); }
  breakdownLabel(panel: keyof LeadershipReportResult['breakdowns'], row: LeadershipBreakdownRow): string {
    if (panel === 'byAssessmentCategory') return this.categoryLabel(row.key);
    if (panel === 'byMonth' && row.key === 'Undated') return this.t('reports.leadership.undated');
    return row.label;
  }
  categoryLabel(category: string | null): string {
    return category && ['HS1', 'HS2', 'HS3', 'GPA'].includes(category)
      ? this.t('reports.leadership.category.' + category) : category || '—';
  }
  breakdownPanels(result: LeadershipReportResult): BreakdownPanel[] {
    const breakdowns = result.breakdowns;
    const keys: Array<keyof LeadershipReportResult['breakdowns']> = this.reportKey === 'school-learning-engagement'
      ? ['byMonth', 'bySubaccount', 'byCourse', 'byGrade', 'byHomeroom', 'byAssessmentCategory']
      : ['byAssessmentCategory', 'bySubaccount', 'byCourse', 'byGrade', 'byHomeroom', 'byMonth'];
    return keys.filter(key => breakdowns[key].length > 0)
      .map(key => ({ key, titleKey: `reports.leadership.${key}`, rows: breakdowns[key] }));
  }
  finalScorePanels(scores: LeadershipFinalCourseScores): FinalScorePanel[] {
    const keys: FinalScorePanel['key'][] = ['byCourse', 'byGrade', 'byHomeroom', 'byMonth'];
    return keys.filter(key => scores[key].length > 0)
      .map(key => ({ key, titleKey: `reports.leadership.${key}`, rows: scores[key] }));
  }

  exportFinalScores(scores: LeadershipFinalCourseScores): void {
    if (!scores.summary.scoreCount) return;
    const sections = this.finalScorePanels(scores);
    const rows = sections.flatMap(panel => panel.rows.map(row => ({ panel: panel.titleKey, row })));
    const title = this.t('reports.leadership.finalScores.title');
    const applied = this.appliedQuery;
    const selectedAccounts = this.selectedSubaccountOptions(applied?.subaccountIds ?? [])
      .map(account => account.name);
    this.excel.download({
      fileName: `${title}-${this.academicYear}-${this.term}`,
      title,
      period: this.periodLabel,
      filters: [
        [this.t('reports.leadership.subaccounts'), selectedAccounts.join(', ') || this.t('reports.leadership.allSubaccounts')],
        [this.t('reports.leadership.grade'), applied?.grade || this.t('reports.leadership.allGrades')],
        [this.t('reports.leadership.homeroom'), applied?.homeroom || this.t('reports.leadership.allHomerooms')],
        [this.t('reports.leadership.finalScores.average'), scores.summary.averageScorePercent],
      ],
      columns: [
        { header: this.t('reports.leadership.breakdown'), value: item => this.t(item.panel) },
        { header: this.t('reports.leadership.breakdownName'), value: item =>
          item.panel === 'reports.leadership.byMonth' && item.row.key === 'Undated'
            ? this.t('reports.leadership.undated') : item.row.label },
        { header: this.t('reports.leadership.students'), value: item => item.row.studentCount },
        { header: this.t('reports.leadership.courses'), value: item => item.row.courseCount },
        { header: this.t('reports.leadership.finalScores.scoreCount'), value: item => item.row.scoreCount },
        { header: this.t('reports.leadership.finalScores.average'), value: item => item.row.averageScorePercent },
      ],
      rows,
    });
  }

  async exportExcel(): Promise<void> {
    const applied = this.appliedQuery;
    if (!applied || this.exporting() || !this.data()?.totalCount) return;
    this.exporting.set(true);
    try {
      const rows = await this.excel.collectPages(async (page, pageSize) => {
        const result = await firstValueFrom(this.reports.run({ ...applied, page, pageSize,
          export: true, includeSummary: false }));
        return { items: result.rows, totalCount: result.totalCount,
          totalPages: Math.ceil(result.totalCount / result.pageSize) };
      });
      const selectedAccounts = this.selectedSubaccountOptions(applied.subaccountIds)
        .map(account => account.name);
      const title = this.t(this.titleKey());
      this.excel.download({
        fileName: `${title}-${applied.academicYear}-${applied.term}-${applied.assessmentCategory}`,
        title, period: this.periodLabel,
        filters: [
          [this.t('reports.leadership.subaccounts'), selectedAccounts.join(', ') || this.t('reports.leadership.allSubaccounts')],
          [this.t('reports.leadership.grade'), applied.grade || this.t('reports.leadership.allGrades')],
          [this.t('reports.leadership.homeroom'), applied.homeroom || this.t('reports.leadership.allHomerooms')],
          [this.t('reports.leadership.assessmentCategory'), this.t('reports.leadership.category.' + applied.assessmentCategory)],
        ],
        columns: [
          { header: 'SIS ID', value: row => row.sisUserId },
          { header: this.t('reports.leadership.students'), value: row => row.name },
          { header: this.t('reports.leadership.grade'), value: row => row.grade },
          { header: this.t('reports.leadership.homeroom'), value: row => row.homeroom },
          { header: this.t('reports.leadership.byCourse'), value: row => row.courseName },
          { header: this.t('reports.leadership.subaccounts'), value: row => row.subaccountName },
          { header: this.t('reports.leadership.category'), value: row => row.assessmentCategory },
          { header: this.t('reports.leadership.group'), value: row => row.assignmentGroupName },
          { header: this.t('reports.leadership.activities'), value: row => row.activityCount },
          { header: this.t('reports.leadership.submitted'), value: row => row.submitted },
          { header: this.t('reports.leadership.missing'), value: row => row.missing },
          { header: this.t('reports.leadership.late'), value: row => row.late },
          { header: this.t('reports.leadership.graded'), value: row => row.graded },
          { header: this.t('reports.leadership.averageScore'), value: row => row.averageScorePercent },
        ],
        rows,
      });
    } catch (error) {
      this.toast.error(this.t(error instanceof ReportExportLimitError ? 'reports.exportLimit' : 'reports.exportError'));
    } finally {
      this.exporting.set(false);
    }
  }

  private apiReportKey(): LeadershipReportKey {
    return this.reportKey === 'school-learning-engagement' ? 'learning-engagement' : 'assessment-results';
  }
  private defaultQuery(): LeadershipReportQuery {
    return { reportKey: this.apiReportKey(), academicYear: this.academicYear, term: this.term,
      grade: '', homeroom: '', subaccountIds: [], assessmentCategory: 'all',
      page: 1, pageSize: 20 };
  }

  private queryFromUrl(): LeadershipReportQuery {
    const query = this.defaultQuery();
    const params = this.route.snapshot.queryParamMap;
    if (params.get('leadReport') !== this.reportKey) return query;
    query.grade = this.shortValue(params.get('leadGrade'), 50);
    query.homeroom = this.shortValue(params.get('leadHomeroom'), 100);
    query.subaccountIds = [...new Set(params.getAll('leadAccount').map(Number)
      .filter(value => Number.isSafeInteger(value) && value > 0))].slice(0, 50);
    const category = params.get('leadCategory');
    if (this.categoryChoices().includes(category as AssessmentCategory))
      query.assessmentCategory = category as AssessmentCategory;
    const page = Number(params.get('leadPage'));
    query.page = Number.isSafeInteger(page) && page >= 1 && page <= 100000 ? page : 1;
    return query;
  }

  private shortValue(value: string | null, maximum: number): string {
    const trimmed = value?.trim() ?? '';
    return trimmed.length <= maximum ? trimmed : '';
  }

  private sortedUnique(values: string[]): string[] {
    return [...new Set(values)].sort((a, b) => a.localeCompare(b, undefined, { numeric: true }));
  }

  private selectedSubaccountGrades(): Set<string> {
    return new Set(this.selectedSubaccountOptions(this.query.subaccountIds)
      .flatMap(account => account.gradeLevels));
  }

  private gradesForCurrentCohortSelection(): Set<string> {
    const rows = this.options()?.cohorts ?? [];
    const values = rows.length
      ? rows.filter(row =>
          (!this.query.grade || row.grade === this.query.grade) &&
          (!this.query.homeroom || row.homeroom === this.query.homeroom)).map(row => row.grade)
      : (this.options()?.grades ?? []).filter(grade => !this.query.grade || grade === this.query.grade);
    return new Set(values);
  }

  private clearInvalidSubaccounts(): void {
    const available = new Set(this.subaccountChoices().map(option => Number(option.value)));
    this.query.subaccountIds = this.selectedSubaccountOptions(this.query.subaccountIds)
      .filter(account => available.has(account.id)).map(account => account.id);
  }

  private selectedSubaccountOptions(ids: number[]) {
    const selected = new Set(ids);
    return (this.options()?.subaccounts ?? []).filter(account =>
      selected.has(account.id) || account.accountIds.some(id => selected.has(id)));
  }

  private normalizeCohortSelection(): boolean {
    const before = `${this.query.grade}|${this.query.homeroom}|${this.query.subaccountIds.join(',')}`;
    this.clearInvalidSubaccounts();
    if (this.query.homeroom && !this.homeroomChoices().includes(this.query.homeroom)) this.query.homeroom = '';
    if (this.query.grade && !this.gradeChoices().includes(this.query.grade)) this.query.grade = '';
    return before !== `${this.query.grade}|${this.query.homeroom}|${this.query.subaccountIds.join(',')}`;
  }

  private syncQueryToUrl(page: number): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        leadReport: this.reportKey,
        leadGrade: this.query.grade || null,
        leadHomeroom: this.query.homeroom || null,
        leadAccount: this.query.subaccountIds.length ? this.query.subaccountIds : null,
        leadCategory: this.query.assessmentCategory === 'all' ? null : this.query.assessmentCategory,
        leadPage: page > 1 ? page : null,
      },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }
}
