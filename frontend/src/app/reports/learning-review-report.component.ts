import { Component, computed, inject, Input, OnChanges, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, ParamMap, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { LanguageService } from '../core/language/language.service';
import { ToastService } from '../core/toast/toast.service';
import {
  LearningAssignmentOption, LearningReportKey, LearningReportOptions,
  LearningReportQuery, LearningReportResult, LearningReportsService,
} from './learning-reports.service';
import { SearchableMultiSelectComponent } from './searchable-multi-select.component';
import { ReportExcelExportService, ReportExportLimitError } from './report-excel-export.service';
import { ReportExportButtonComponent } from './report-export-button.component';
import { ActivityPeriodFilterComponent } from './activity-period-filter.component';
import { activityWeeks, ActivityPeriodSelection, normalizeActivityPeriod } from './activity-period';

@Component({
  selector: 'app-learning-review-report',
  imports: [FormsModule, SearchableMultiSelectComponent, ReportExportButtonComponent, ActivityPeriodFilterComponent],
  template: `
    <section class="relative overflow-visible rounded-xl border border-[#e7e0eb] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.07)]" [attr.aria-busy]="loading()">
      @if (loading() && result()) {
        <div class="no-print absolute inset-0 z-30 flex justify-center rounded-xl bg-white/70 pt-36" role="status">
          <div class="flex h-fit items-center gap-3 rounded-full bg-white px-5 py-3 text-sm font-semibold text-[#5f5368] shadow-md">
            <span class="h-6 w-6 animate-spin rounded-full border-2 border-[#e6dfea] border-t-[#6d3a91]"></span>
            {{ t('reports.loading') }}
          </div>
        </div>
      }
      <div class="flex flex-wrap items-start justify-between gap-3 border-b border-[#eee8f1] px-5 py-5 sm:px-6">
        <div>
          <p class="text-xs font-bold uppercase tracking-[0.14em] text-[#8a6811]">{{ periodLabel }}</p>
          <h3 class="mt-1 text-xl font-semibold text-[#32124f]">{{ t('reports.learningReview.' + reportKey + '.title') }}</h3>
          <p class="mt-1 text-sm leading-6 text-[#6f6877]">{{ t('reports.learningReview.' + reportKey + '.description') }}</p>
          @if (appliedQuery; as applied) {
            <p class="mt-2 text-xs text-[#817589]">{{ activityPeriodLabel(applied) }}</p>
          }
        </div>
        <app-report-export-button [exporting]="exporting()" [disabled]="!result()?.aggregateAvailable || !result()?.totalCount" (exportRequested)="exportExcel()" />
      </div>
      <form class="no-print grid min-w-0 gap-4 border-b border-[#eee8f1] bg-[#fdfbfe] p-5 sm:grid-cols-2 xl:grid-cols-3 [&>*]:min-w-0" (ngSubmit)="run()">
        <app-searchable-multi-select
          [label]="t('reports.leadership.subaccounts')"
          [searchPlaceholder]="t('reports.leadership.searchSubaccounts')"
          [emptySelectionLabel]="t('reports.leadership.allSubaccounts')"
          [hint]="t('reports.leadership.allSubaccounts')"
          [options]="subaccountChoices()" [selected]="selectedSubaccountOptionIds()"
          (selectedChange)="selectSubaccounts($event)" />
        <app-searchable-multi-select
          [label]="t('reports.learningReview.courses')"
          [searchPlaceholder]="t('reports.learningReview.searchCourses')"
          [emptySelectionLabel]="t(reportKey === 'reading-completion' ? 'reports.learningReview.chooseCourseFirst' : 'reports.learningReview.allCoursesShort')"
          [hint]="t(reportKey === 'reading-completion' ? 'reports.learningReview.chooseCourseFirst' : 'reports.learningReview.allCourses')"
          [options]="courseChoices()" [selected]="query.courseIds"
          (selectedChange)="selectCourses($event)" />
        <label class="grid min-w-0 content-start gap-1 self-start text-sm font-semibold text-[#32124f]">
          {{ t('reports.leadership.assessmentCategory') }}
          <select class="h-11 w-full min-w-0 max-w-full rounded-md border border-[#d8d0df] bg-white px-3 font-normal" name="assessmentCategory" [(ngModel)]="query.assessmentCategory">
            @for (category of categoryChoices(); track category) {
              <option [value]="category">{{ t('reports.leadership.category.' + category) }}</option>
            }
          </select>
        </label>
        <app-searchable-multi-select
          [label]="t('reports.learningReview.assignmentGroups')"
          [searchPlaceholder]="t('reports.learningReview.searchGroups')"
          [emptySelectionLabel]="t('reports.learningReview.allGroupsShort')"
          [hint]="t('reports.learningReview.exactGroupHint')"
          [options]="groupChoices()" [selected]="query.assignmentGroupNames"
          (selectedChange)="selectGroups($event)" />
        @if (reportKey === 'reading-completion') {
          <app-searchable-multi-select
            [label]="t('reports.learningReview.assignments')"
            [searchPlaceholder]="t('reports.learningReview.searchAssignments')"
            [emptySelectionLabel]="t('reports.learningReview.noAssignmentsSelected')"
            [hint]="t(query.courseIds.length ? 'reports.learningReview.chooseAssignments' : 'reports.learningReview.chooseCourseFirst')"
            [loading]="assignmentsLoading()"
            [options]="assignmentChoices()" [selected]="query.assignmentIds"
            (selectedChange)="selectAssignments($event)" />
        }
        @if (!activityPeriod) {
        <app-activity-period-filter class="sm:col-span-2 xl:col-span-3" [academicYear]="academicYear"
          [month]="query.month" [weekStart]="query.weekStart"
          (periodChange)="query.month = $event.month; query.weekStart = $event.weekStart" />
        }
        @if (reportKey === 'missing-frequency') {
          <label class="grid content-start gap-1 self-start text-sm font-semibold text-[#32124f]">
            {{ t('reports.learningReview.maxMissing') }}
            <input class="h-11 rounded-md border border-[#d8d0df] bg-white px-3 font-normal" type="number" min="0" max="1000" name="maxMissing" [(ngModel)]="query.maxMissing" />
          </label>
        }
        @if (reportKey === 'homework-achievement') {
          <label class="grid content-start gap-1 self-start text-sm font-semibold text-[#32124f]">
            {{ t('reports.learningReview.minScore') }}
            <input class="h-11 rounded-md border border-[#d8d0df] bg-white px-3 font-normal" type="number" min="0" max="100" name="minScore" [(ngModel)]="query.minScore" />
          </label>
        }
        @if (reportKey === 'reading-completion' || reportKey === 'grading-coverage') {
          <label class="grid content-start gap-1 self-start text-sm font-semibold text-[#32124f]">
            {{ t('reports.learningReview.minRate') }}
            <input class="h-11 rounded-md border border-[#d8d0df] bg-white px-3 font-normal" type="number" min="0" max="100" name="minCompletion" [(ngModel)]="query.minCompletion" />
          </label>
        }
        @if (reportKey === 'reading-completion' && (!query.courseIds.length || !query.assignmentIds.length)) {
          <p class="rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900 sm:col-span-2 xl:col-span-3">
            {{ t(!query.courseIds.length ? 'reports.learningReview.chooseCourseFirst' : 'reports.learningReview.chooseAssignments') }}
          </p>
        }
        <div class="flex items-end gap-2 sm:col-span-2 xl:col-span-3">
          <button class="rounded-md bg-[#32124f] px-4 py-2.5 text-sm font-semibold text-white disabled:opacity-50" type="submit" [disabled]="loading() || assignmentsLoading() || !valid()">
            {{ t('reports.learningReview.run') }}
          </button>
          <button class="rounded-md border border-[#d8d0df] bg-white px-4 py-2.5 text-sm font-semibold text-[#32124f] transition hover:bg-[#f5f0f7]" type="button" [disabled]="loading()" (click)="resetFilters()">
            {{ t('reports.clearFilters') }}
          </button>
        </div>
      </form>
      @if (error()) { <p class="m-5 rounded-md bg-red-50 p-3 text-sm text-red-700">{{ error() }}</p> }
      @if (loading() && !result()) {
        <div class="grid min-h-[360px] place-items-center" role="status">
          <div class="flex items-center gap-3 text-sm font-semibold text-[#5f5368]">
            <span class="h-7 w-7 animate-spin rounded-full border-2 border-[#e6dfea] border-t-[#6d3a91]"></span>
            {{ t('reports.loading') }}
          </div>
        </div>
      }
      @if (result(); as data) {
        @if (!data.aggregateAvailable) {
          <p class="m-5 rounded-md bg-amber-50 p-4 text-sm text-amber-800">{{ t('reports.learningReview.aggregateUnavailable') }}</p>
        } @else if (data.notice === 'select_assignments') {
          <p class="m-5 text-sm text-[#817589]">{{ t('reports.learningReview.chooseAssignments') }}</p>
        } @else {
          <p class="px-5 py-3 text-sm font-semibold text-[#32124f]">{{ data.totalCount }} {{ t('reports.learningReview.results') }}</p>
          <div class="overflow-x-auto">
            <table class="w-full min-w-[780px] border-collapse text-left text-sm">
              <thead class="bg-[#f5f0f7] text-xs uppercase tracking-[0.08em] text-[#5d486b]"><tr>
                <th class="px-5 py-3">{{ t(reportKey === 'grading-coverage' ? 'reports.learningReview.course' : 'reports.learningReview.student') }}</th>
                @if (reportKey !== 'grading-coverage') {
                  <th class="px-3 py-3">{{ t('reports.table.grade') }}</th>
                  <th class="px-3 py-3">{{ t('reports.table.class') }}</th>
                }
                @if (reportKey === 'missing-frequency' || reportKey === 'reading-completion') {
                  <th class="px-3 py-3 text-right">{{ t('reports.learningReview.missing') }}</th>
                }
                @if (reportKey === 'missing-frequency' || reportKey === 'reading-completion' || reportKey === 'grading-coverage') {
                  <th class="px-3 py-3 text-right">{{ t('reports.learningReview.submitted') }}</th>
                }
                @if (reportKey === 'grading-coverage') { <th class="px-3 py-3 text-right">{{ t('reports.learningReview.graded') }}</th> }
                @if (reportKey === 'homework-achievement') {
                  <th class="px-3 py-3 text-right">{{ t('reports.learningReview.score') }}</th>
                }
                @if (reportKey === 'reading-completion' || reportKey === 'grading-coverage') {
                  <th class="px-3 py-3 text-right">{{ t('reports.learningReview.rate') }}</th>
                }
                @if (reportKey === 'grading-coverage') {
                  <th class="px-5 py-3">{{ t('reports.table.status') }}</th>
                }
              </tr></thead>
              <tbody class="divide-y divide-[#eee8f1]">
                @for (row of data.rows; track row.studentId ?? row.name) {
                  <tr>
                    <td class="px-5 py-3 font-semibold text-[#32124f]">{{ row.name }}
                      @if (row.sisUserId) { <span class="block text-xs font-normal text-[#817589]">{{ row.sisUserId }}</span> }
                    </td>
                    @if (reportKey !== 'grading-coverage') { <td class="px-3 py-3">{{ row.grade ?? '—' }}</td><td class="px-3 py-3">{{ row.homeroom ?? '—' }}</td> }
                    @if (reportKey === 'missing-frequency' || reportKey === 'reading-completion') { <td class="px-3 py-3 text-right">{{ row.missing }}</td> }
                    @if (reportKey === 'missing-frequency' || reportKey === 'reading-completion' || reportKey === 'grading-coverage') { <td class="px-3 py-3 text-right">{{ row.submitted }}</td> }
                    @if (reportKey === 'grading-coverage') { <td class="px-3 py-3 text-right">{{ row.graded }}</td> }
                    @if (reportKey === 'homework-achievement') { <td class="px-3 py-3 text-right">{{ number(row.score) }}</td> }
                    @if (reportKey === 'reading-completion' || reportKey === 'grading-coverage') { <td class="px-3 py-3 text-right">{{ number(row.rate) }}</td> }
                    @if (reportKey === 'grading-coverage') { <td class="px-5 py-3">{{ t('reports.learningReview.status.' + row.status) }}</td> }
                  </tr>
                }
              </tbody>
            </table>
          </div>
          @if (!data.rows.length) { <p class="p-5 text-sm text-[#817589]">{{ t('reports.noData') }}</p> }
          @if (data.totalCount > 0) {
            <div class="no-print flex flex-wrap items-center justify-between gap-3 border-t border-[#eee8f1] px-5 py-4 text-sm text-[#5d486b]">
              <span>{{ t('pagination.page') }} {{ data.page }} / {{ totalPages() }} · {{ data.totalCount }} {{ t('reports.learningReview.results') }}</span>
              <div class="flex gap-2">
                <button class="rounded-md border border-[#d8d0df] px-3 py-2 font-semibold text-[#32124f] disabled:opacity-40" type="button" [disabled]="loading() || data.page <= 1" (click)="changePage(data.page - 1)">{{ t('pagination.previous') }}</button>
                <button class="rounded-md border border-[#d8d0df] px-3 py-2 font-semibold text-[#32124f] disabled:opacity-40" type="button" [disabled]="loading() || data.page >= totalPages()" (click)="changePage(data.page + 1)">{{ t('pagination.next') }}</button>
              </div>
            </div>
          }
        }
      }
    </section>
  `,
})
export class LearningReviewReportComponent implements OnInit, OnChanges, OnDestroy {
  @Input({ required: true }) reportKey!: LearningReportKey;
  @Input({ required: true }) academicYear = '';
  @Input({ required: true }) term = 'all';
  @Input({ required: true }) periodLabel = '';
  @Input() activityPeriod: ActivityPeriodSelection | null = null;
  private readonly reports = inject(LearningReportsService);
  private readonly language = inject(LanguageService);
  private readonly excel = inject(ReportExcelExportService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly options = signal<LearningReportOptions | null>(null);
  readonly assignments = signal<LearningAssignmentOption[]>([]);
  readonly assignmentsLoading = signal(false);
  readonly courseChoices = computed(() => (this.options()?.courses ?? [])
    .map(course => ({ value: course.id, label: course.name })));
  readonly subaccountChoices = computed(() => (this.options()?.subaccounts ?? [])
    .map(account => ({ value: account.id, label: account.name })));
  readonly groupChoices = computed(() => (this.options()?.assignmentGroups ?? [])
    .map(group => ({ value: group, label: group })));
  readonly assignmentChoices = computed(() => {
    const courses = new Map((this.options()?.courses ?? []).map(course => [course.id, course.name]));
    return this.assignments().map(assignment => ({
      value: assignment.id, label: assignment.name, detail: courses.get(assignment.courseId),
    }));
  });
  readonly result = signal<LearningReportResult | null>(null);
  readonly totalPages = computed(() => Math.max(1,
    Math.ceil((this.result()?.totalCount ?? 0) / (this.result()?.pageSize ?? 15))));
  readonly loading = signal(false);
  readonly exporting = signal(false);
  readonly error = signal<string | null>(null);
  query: LearningReportQuery = this.defaultQuery();
  private loadedKey: LearningReportKey | null = null;
  private requestVersion = 0;
  appliedQuery: LearningReportQuery | null = null;
  private assignmentRequestVersion = 0;
  private assignmentTimer?: ReturnType<typeof setTimeout>;

  ngOnInit(): void {
    this.reports.options().subscribe({
      next: value => { this.options.set(value); this.initializeReport(); },
      error: () => this.error.set(this.t('reports.loadError')),
    });
  }
  ngOnChanges(): void {
    if (this.loadedKey && this.loadedKey !== this.reportKey) this.initializeReport();
    else if (this.loadedKey) {
      this.query.academicYear = this.academicYear;
      this.query.term = this.term;
      Object.assign(this.query, normalizeActivityPeriod(this.academicYear, this.activityPeriod ?? this.query));
      this.query.page = 1;
      this.syncQueryToUrl();
      this.fetchPage();
    }
  }
  ngOnDestroy(): void {
    if (this.assignmentTimer) clearTimeout(this.assignmentTimer);
    this.assignmentRequestVersion++;
    this.requestVersion++;
  }
  t(key: string): string { return this.language.t(key); }
  number(value: number | null): string { return value === null ? '—' : `${value.toFixed(1)}%`; }
  weekLabel(query: LearningReportQuery): string {
    return activityWeeks(query.academicYear).find(week => week.value === query.weekStart)?.label
      ?? this.t('reports.activityPeriod.allWeeks');
  }
  activityPeriodLabel(query: LearningReportQuery): string {
    return `${this.t('reports.learningReview.month')}: ${query.month ?? this.t('reports.learningReview.allMonths')} · ${this.t('reports.activityPeriod.week')}: ${this.weekLabel(query)}`;
  }
  valid(): boolean {
    return !!this.academicYear && this.query.maxMissing >= 0 && this.query.minScore >= 0 && this.query.minScore <= 100
      && this.query.minCompletion >= 0 && this.query.minCompletion <= 100
      && (this.reportKey !== 'reading-completion' ||
        (this.query.courseIds.length > 0 && this.query.assignmentIds.length > 0));
  }
  selectCourses(values: Array<string | number>): void {
    this.query.courseIds = values.map(Number);
    this.onCoursesChange();
  }
  selectSubaccounts(values: Array<string | number>): void {
    this.query.subaccountIds = values.map(Number);
  }
  selectedSubaccountOptionIds(): number[] {
    return this.selectedSubaccountOptions(this.query.subaccountIds).map(account => account.id);
  }
  categoryChoices(): LearningReportQuery['assessmentCategory'][] {
    return (this.options()?.assessmentCategories ?? [])
      .filter((category): category is LearningReportQuery['assessmentCategory'] =>
        category === 'all' || category === 'HS1' || category === 'HS2' ||
        category === 'HS3' || category === 'GPA');
  }
  selectGroups(values: Array<string | number>): void {
    this.query.assignmentGroupNames = values.map(String);
  }
  selectAssignments(values: Array<string | number>): void {
    this.query.assignmentIds = values.map(Number);
  }
  onCoursesChange(): void {
    const selected = this.query.courseIds;
    const version = ++this.assignmentRequestVersion;
    if (this.assignmentTimer) clearTimeout(this.assignmentTimer);
    if (this.reportKey !== 'reading-completion') { this.assignmentsLoading.set(false); return; }
    if (!selected.length) {
      this.assignmentsLoading.set(false);
      this.assignments.set([]);
      this.query.assignmentIds = [];
      return;
    }
    this.assignmentsLoading.set(true);
    this.assignmentTimer = setTimeout(() => {
      this.reports.assignments(selected).subscribe({
        next: items => {
          if (version !== this.assignmentRequestVersion) return;
          this.assignmentsLoading.set(false);
          this.assignments.set(items);
          const ids = new Set(items.map(item => item.id));
          this.query.assignmentIds = this.query.assignmentIds.filter(id => ids.has(id));
        },
        error: () => {
          if (version !== this.assignmentRequestVersion) return;
          this.assignmentsLoading.set(false);
          this.error.set(this.t('reports.loadError'));
        },
      });
    }, 250);
  }
  run(): void {
    if (!this.valid()) return;
    this.query.page = 1;
    this.syncQueryToUrl();
    this.fetchPage();
  }
  resetFilters(): void {
    if (this.assignmentTimer) clearTimeout(this.assignmentTimer);
    this.assignmentRequestVersion++;
    this.requestVersion++;
    this.assignments.set([]);
    this.assignmentsLoading.set(false);
    this.error.set(null);
    this.appliedQuery = null;
    this.query = {
      ...this.defaultQuery(),
      reportKey: this.reportKey,
      academicYear: this.academicYear,
      term: this.term,
    };
    this.syncQueryToUrl();
    if (this.reportKey === 'reading-completion') {
      this.result.set(null);
      this.loading.set(false);
      return;
    }
    this.fetchPage();
  }
  changePage(page: number): void {
    if (page < 1 || page > this.totalPages() || this.loading()) return;
    if (this.appliedQuery && JSON.stringify({ ...this.appliedQuery, page: 1 }) !==
      JSON.stringify({ ...this.query, page: 1 })) {
      this.run();
      return;
    }
    this.query.page = page;
    this.syncQueryToUrl();
    this.fetchPage();
  }
  async exportExcel(): Promise<void> {
    const applied = this.appliedQuery;
    if (!applied || this.exporting()) return;
    this.exporting.set(true);
    try {
      const rows = await this.excel.collectPages(async (page, pageSize) => {
        const response = await firstValueFrom(this.reports.run({ ...applied, page, pageSize, export: true }));
        return {
          items: response.rows,
          totalCount: response.totalCount,
          totalPages: Math.ceil(response.totalCount / response.pageSize),
        };
      });
      const title = this.t(`reports.learningReview.${this.reportKey}.title`);
      const courseNames = (this.options()?.courses ?? [])
        .filter(course => applied.courseIds.includes(course.id)).map(course => course.name);
      const subaccountNames = this.selectedSubaccountOptions(applied.subaccountIds)
        .map(account => account.name);
      const assignmentNames = this.assignments()
        .filter(assignment => applied.assignmentIds.includes(assignment.id)).map(assignment => assignment.name);
      await this.excel.download({
        fileName: `${title}-${this.academicYear}-${this.term}`,
        title,
        period: this.periodLabel,
        filters: [
          [this.t('reports.learningReview.month'), applied.month ?? this.t('reports.learningReview.allMonths')],
          [this.t('reports.activityPeriod.week'), this.weekLabel(applied)],
          [this.t('reports.learningReview.courses'), courseNames.length ? courseNames.join(', ') : this.t('reports.learningReview.allCoursesShort')],
          [this.t('reports.leadership.subaccounts'), subaccountNames.length ? subaccountNames.join(', ') : this.t('reports.leadership.allSubaccounts')],
          [this.t('reports.leadership.assessmentCategory'), this.t('reports.leadership.category.' + applied.assessmentCategory)],
          [this.t('reports.learningReview.assignmentGroups'), applied.assignmentGroupNames.length ? applied.assignmentGroupNames.join(', ') : this.t('reports.learningReview.allGroupsShort')],
          [this.t('reports.learningReview.assignments'), assignmentNames.length ? assignmentNames.join(', ') : this.t('reports.learningReview.noAssignmentsSelected')],
          [this.t('reports.learningReview.maxMissing'), applied.maxMissing],
          [this.t('reports.learningReview.minScore'), applied.minScore],
          [this.t('reports.learningReview.minRate'), applied.minCompletion],
        ],
        columns: [
          { header: this.t(this.reportKey === 'grading-coverage' ? 'reports.learningReview.course' : 'reports.learningReview.student'), value: row => row.name },
          { header: 'SIS ID', value: row => row.sisUserId },
          { header: this.t('reports.table.grade'), value: row => row.grade },
          { header: this.t('reports.table.class'), value: row => row.homeroom },
          { header: this.t('reports.learningReview.missing'), value: row => row.missing },
          { header: this.t('reports.learningReview.submitted'), value: row => row.submitted },
          { header: this.t('reports.learningReview.graded'), value: row => row.graded },
          { header: this.t('reports.learningReview.score'), value: row => row.score, format: 'percentage' },
          { header: this.t('reports.learningReview.rate'), value: row => row.rate, format: 'percentage' },
          { header: this.t('reports.table.status'), value: row => this.t(`reports.learningReview.status.${row.status}`) },
        ],
        rows,
      });
    } catch (error) {
      this.toast.error(this.t(error instanceof ReportExportLimitError ? 'reports.exportLimit' : 'reports.exportError'));
    } finally {
      this.exporting.set(false);
    }
  }
  private fetchPage(): void {
    if (!this.valid()) { this.result.set(null); this.loading.set(false); return; }
    const version = ++this.requestVersion;
    const query = { ...this.query, reportKey: this.reportKey, academicYear: this.academicYear, term: this.term };
    this.loading.set(true);
    this.error.set(null);
    this.reports.run(query).subscribe({
      next: value => {
        if (version !== this.requestVersion) return;
        this.appliedQuery = { ...query, page: value.page };
        this.result.set(value);
        this.loading.set(false);
        if (value.page !== query.page) {
          this.query.page = value.page;
          this.syncQueryToUrl();
        }
      },
      error: () => { if (version === this.requestVersion) { this.error.set(this.t('reports.loadError')); this.loading.set(false); } },
    });
  }
  private initializeReport(): void {
    if (!this.reportKey) return;
    this.requestVersion++;
    this.loadedKey = this.reportKey;
    this.appliedQuery = null;
    this.result.set(null);
    this.error.set(null);
    this.query = this.queryFromUrl();
    this.onCoursesChange();
    if (this.reportKey !== 'reading-completion' || this.query.assignmentIds.length) this.fetchPage();
    else this.loading.set(false);
  }
  private queryFromUrl(): LearningReportQuery {
    const defaults = this.defaultQuery();
    const params = this.route.snapshot.queryParamMap;
    const query = { ...defaults, reportKey: this.reportKey, academicYear: this.academicYear, term: this.term };
    if (params.get('lrReport') !== this.reportKey) return query;
    query.courseIds = this.idsFromUrl(params, 'lrCourse', 100);
    query.subaccountIds = this.idsFromUrl(params, 'lrSubaccount', 100);
    query.assignmentIds = this.idsFromUrl(params, 'lrAssignment', 200);
    query.assignmentGroupNames = [...new Set(params.getAll('lrGroup')
      .map(value => value.trim()).filter(value => value.length > 0 && value.length <= 255))].slice(0, 100);
    const category = params.get('lrCategory');
    if (category === 'HS1' || category === 'HS2' || category === 'HS3' || category === 'GPA') query.assessmentCategory = category;
    query.month = params.has('lrMonth') ? this.numberFromUrl(params, 'lrMonth', 0, 1, 12, true) || null : null;
    query.weekStart = params.get('lrWeekStart');
    Object.assign(query, normalizeActivityPeriod(this.academicYear, query));
    if (this.activityPeriod) Object.assign(query, this.activityPeriod);
    query.maxMissing = this.numberFromUrl(params, 'lrMaxMissing', defaults.maxMissing, 0, 1000, true);
    query.minScore = this.numberFromUrl(params, 'lrMinScore', defaults.minScore, 0, 100);
    query.minCompletion = this.numberFromUrl(params, 'lrMinCompletion', defaults.minCompletion, 0, 100);
    query.page = this.numberFromUrl(params, 'lrPage', 1, 1, 100000, true);
    return query;
  }
  private idsFromUrl(params: ParamMap, name: string, limit: number): number[] {
    return [...new Set(params.getAll(name).map(Number)
      .filter(value => Number.isSafeInteger(value) && value > 0))].slice(0, limit);
  }
  private selectedSubaccountOptions(ids: number[]) {
    const selected = new Set(ids);
    return (this.options()?.subaccounts ?? []).filter(account =>
      selected.has(account.id) || account.accountIds.some(id => selected.has(id)));
  }
  private numberFromUrl(params: ParamMap, name: string, fallback: number,
    min: number, max: number, integer = false): number {
    const raw = params.get(name);
    if (raw === null) return fallback;
    const value = Number(raw);
    return Number.isFinite(value) && value >= min && value <= max && (!integer || Number.isInteger(value))
      ? value : fallback;
  }
  private syncQueryToUrl(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        report: this.reportKey,
        ...(this.activityPeriod ? { activityMonth: this.activityPeriod.month, activityWeekStart: this.activityPeriod.weekStart } : {}),
        lrReport: this.reportKey,
        lrCourse: this.query.courseIds.length ? this.query.courseIds : null,
        lrSubaccount: this.query.subaccountIds.length ? this.query.subaccountIds : null,
        lrCategory: this.query.assessmentCategory === 'all' ? null : this.query.assessmentCategory,
        lrGroup: this.query.assignmentGroupNames.length ? this.query.assignmentGroupNames : null,
        lrAssignment: this.query.assignmentIds.length ? this.query.assignmentIds : null,
        lrMonth: this.query.month,
        lrWeekStart: this.query.weekStart,
        lrMaxMissing: this.query.maxMissing,
        lrMinScore: this.query.minScore,
        lrMinCompletion: this.query.minCompletion,
        lrPage: this.query.page > 1 ? this.query.page : null,
      },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }
  private defaultQuery(): LearningReportQuery {
    return { reportKey: 'missing-frequency', academicYear: '', term: 'all', month: null, weekStart: null,
      courseIds: [], subaccountIds: [], assignmentIds: [], maxMissing: 2, minScore: 80, minCompletion: 90,
      assignmentGroupNames: [], assessmentCategory: 'all', page: 1,
      ...(this.activityPeriod ?? {}),
    };
  }
}
