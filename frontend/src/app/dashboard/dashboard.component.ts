import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { EChartsCoreOption } from 'echarts/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { catchError, forkJoin, of } from 'rxjs';
import { LanguageService } from '../core/language/language.service';
import { riskBadgeClass, riskDotClass } from '../students/risk.util';
import { DashboardAttentionStudent, DashboardOverview, DashboardOverviewService } from './dashboard-overview.service';
import { defaultFilterSet, WorkspaceFiltersService, WorkspaceFilterSet } from '../workspace/workspace-filters.service';

interface RiskRow {
  level: 'High' | 'Medium' | 'None';
  count: number;
}

@Component({
  selector: 'app-dashboard',
  imports: [NgClass, FormsModule, RouterLink, NgxEchartsDirective],
  template: `
    <section class="space-y-7">
      <div class="flex flex-wrap items-start justify-between gap-4 lg:flex-nowrap">
        <div class="min-w-0 flex-1">
          <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">{{ languageService.t('dashboard.overview') }}</p>
          <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ languageService.t('dashboard.title') }}</h2>
          <p class="mt-2 max-w-2xl text-[#6f6877]">{{ languageService.t('dashboard.description') }}</p>
        </div>
        <div class="w-full shrink-0 self-start rounded-lg border border-[#ebe6ef] bg-white p-4 shadow-[0_10px_30px_rgba(50,18,79,0.07)] sm:w-auto sm:min-w-[22rem]">
          <div class="flex items-center gap-3 border-b border-[#f0ebf2] pb-3">
            <span class="grid h-9 w-9 shrink-0 place-items-center rounded-md bg-[#fff4ce] text-[#8a6811]">
              <svg class="h-5 w-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <rect x="3" y="4" width="18" height="18" rx="2" />
                <path d="M16 2v4M8 2v4M3 10h18" />
              </svg>
            </span>
            <div>
              <p class="text-xs font-semibold uppercase tracking-[0.12em] text-[#8a6811]">
                {{ languageService.t('dashboard.period.label') }}
              </p>
              <p class="mt-0.5 text-xs text-[#7c7284]">{{ periodLabel() }}</p>
            </div>
          </div>
          <div class="mt-3 grid grid-cols-2 gap-3">
              <label class="grid min-w-0 gap-1.5">
                <span class="text-[10px] font-semibold uppercase tracking-[0.08em] text-[#746b7b]">
                  {{ languageService.t('dashboard.period.year') }}
                </span>
                <select
                  class="w-full min-w-0 rounded-md border border-[#d8d0df] bg-white px-3 py-2.5 text-sm font-semibold text-[#32124f] outline-none transition focus:border-[#8a5aa7] focus:ring-2 focus:ring-[#eee5f3] disabled:cursor-wait disabled:opacity-60"
                  [(ngModel)]="selectedYear"
                  (ngModelChange)="onPeriodChange()"
                  [disabled]="periodLoading() || !years().length"
                >
                  @for (year of years(); track year) {
                    <option [value]="year">{{ formatYear(year) }}</option>
                  }
                </select>
              </label>
              <label class="grid min-w-0 gap-1.5">
                <span class="text-[10px] font-semibold uppercase tracking-[0.08em] text-[#746b7b]">
                  {{ languageService.t('dashboard.period.term') }}
                </span>
                <select
                  class="w-full min-w-0 rounded-md border border-[#d8d0df] bg-white px-3 py-2.5 text-sm font-semibold text-[#32124f] outline-none transition focus:border-[#8a5aa7] focus:ring-2 focus:ring-[#eee5f3] disabled:cursor-wait disabled:opacity-60"
                  [(ngModel)]="selectedTerm"
                  (ngModelChange)="onPeriodChange()"
                  [disabled]="periodLoading() || !selectedYear"
                >
                  <option value="all">{{ languageService.t('dashboard.period.term.all') }}</option>
                  @for (term of terms(); track term) {
                    <option [value]="term">
                      {{ termLabel(term) }}
                    </option>
                  }
                </select>
              </label>
          </div>
        </div>
      </div>

      <p class="-mt-3 text-xs text-[#9a91a3]">{{ languageService.t('dashboard.period.rosterNote') }}</p>

      @if (periodLoading()) {
        <div
          class="grid min-h-72 place-items-center rounded-lg border border-[#ebe6ef] bg-white p-10 shadow-[0_12px_35px_rgba(50,18,79,0.08)]"
          role="status"
          aria-live="polite"
        >
          <div class="text-center">
            <span class="mx-auto block h-10 w-10 animate-spin rounded-full border-4 border-[#e6dfea] border-t-[#6d3a91]"></span>
            <p class="mt-4 text-sm font-semibold text-[#5f5368]">
              {{ languageService.t('dashboard.loading') }}
            </p>
          </div>
        </div>
      } @else if (loadFailed() && !overview()) {
        <div class="rounded-lg border border-red-200 bg-red-50 p-8 text-center text-sm text-red-700">
          {{ languageService.t('dashboard.loadError') }}
        </div>
      } @else {
      <div class="grid gap-5 md:grid-cols-2 xl:grid-cols-4">
        <article class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <p class="text-sm font-medium text-[#6f6877]">{{ languageService.t('dashboard.cards.students') }}</p>
          <p class="mt-4 text-4xl font-semibold text-[#32124f]">{{ overview()?.totalStudents ?? '—' }}</p>
          <p class="mt-4 text-sm text-[#8a6811]">{{ languageService.t('dashboard.cards.studentsSub') }}</p>
        </article>
        <article class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <p class="text-sm font-medium text-[#6f6877]">{{ languageService.t('dashboard.cards.atRisk') }}</p>
          <p class="mt-4 text-4xl font-semibold text-[#32124f]">{{ overview()?.atRiskTotal ?? '—' }}</p>
          <p class="mt-4 text-sm text-[#8a6811]">{{ atRiskSub() }}</p>
        </article>
        <article class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <p class="text-sm font-medium text-[#6f6877]">{{ languageService.t('dashboard.cards.behaviours') }}</p>
          <p class="mt-4 text-4xl font-semibold text-[#32124f]">{{ overview()?.totalBehaviours ?? '—' }}</p>
          <p class="mt-4 text-sm text-[#8a6811]">{{ behavioursSub() }}</p>
        </article>
        <article class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <p class="text-sm font-medium text-[#6f6877]">{{ languageService.t('dashboard.cards.projects') }}</p>
          <p class="mt-4 text-4xl font-semibold text-[#32124f]">{{ overview()?.activeProjects ?? '—' }}</p>
          <p class="mt-4 text-sm text-[#8a6811]">{{ projectsSub() }}</p>
        </article>
      </div>

      <section class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
        <div class="flex flex-wrap items-center justify-between gap-3">
          <div>
            <h3 class="text-lg font-semibold text-[#32124f]">
              {{ languageService.t('dashboard.attentionTitle') }}
            </h3>
            <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('dashboard.attentionHint') }}</p>
          </div>
          <a
            class="inline-flex items-center gap-2 rounded-md border border-[#d8d0df] px-4 py-2 text-sm font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
            routerLink="/at-risk"
          >
            {{ languageService.t('dashboard.viewAllAtRisk') }}
            <svg class="h-3.5 w-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <path d="M5 12h14" />
              <path d="m12 5 7 7-7 7" />
            </svg>
          </a>
        </div>

        <div class="mt-5 space-y-2">
          @for (student of topAtRisk(); track student.id) {
            <a
              class="flex items-center gap-3 rounded-md border border-[#ebe6ef] p-3 transition hover:border-[#d3c6db] hover:bg-[#fdfcff]"
              [routerLink]="['/students', student.id]"
            >
              <span class="h-2.5 w-2.5 shrink-0 rounded-full" [ngClass]="riskDot(student.riskLevel)"></span>
              <span class="min-w-0 flex-1">
                <span class="block truncate text-sm font-semibold text-[#32124f]">{{ student.fullName }}</span>
                <span class="mt-0.5 block truncate text-xs text-[#7c7284]">
                  {{ student.sisUserId }} · {{ languageService.t('dashboard.grade') }}
                  {{ student.gradeLevel || '-' }} · {{ student.homeroom || '-' }}
                </span>
              </span>
              <span class="shrink-0 rounded-full px-2.5 py-0.5 text-xs font-semibold" [ngClass]="riskBadge(student.riskLevel)">
                {{ languageService.t('dashboard.riskLevel.' + student.riskLevel) }}
              </span>
            </a>
          }
          @if (!atRiskLoading() && !topAtRisk().length) {
            <p
              class="rounded-md border border-dashed border-[#d5e8cf] bg-[#f3faf1] p-6 text-center text-sm text-[#3f6f24]"
            >
              {{ languageService.t('dashboard.noAtRisk') }}
            </p>
          }
          @if (atRiskLoading()) {
            <p class="py-6 text-center text-sm text-[#7c7284]">
              {{ languageService.t('dashboard.attentionLoading') }}
            </p>
          }
        </div>
      </section>

      <div class="grid gap-5 xl:grid-cols-[1.5fr_1fr]">
        <section class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <div class="flex flex-wrap items-center justify-between gap-2">
            <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('dashboard.trendTitle') }}</h3>
            <span class="rounded-full bg-[#f1edf4] px-3 py-1 text-xs font-semibold text-[#5b287d]">
              {{ periodLabel() }}
            </span>
          </div>
          <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('dashboard.trendHint') }}</p>
          <div
            echarts
            [options]="trendOptions()"
            class="mt-5 h-72 w-full"
            role="img"
            [attr.aria-label]="languageService.t('dashboard.trendTitle')"
          ></div>
        </section>

        <section class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('dashboard.riskOverview') }}</h3>
          <div class="mt-5 space-y-2">
            @for (row of riskRows(); track row.level) {
              <div class="flex items-center gap-3 rounded-md border border-[#ebe6ef] px-4 py-3">
                <span class="h-2.5 w-2.5 shrink-0 rounded-full" [ngClass]="riskDot(row.level)"></span>
                <span class="flex-1 text-sm font-medium text-[#4a3f55]">
                  {{ languageService.t('dashboard.riskLevel.' + row.level) }}
                </span>
                <span class="text-sm font-semibold text-[#32124f]">{{ row.count }}</span>
              </div>
            }
          </div>

          <h4 class="mt-6 text-sm font-semibold uppercase tracking-[0.12em] text-[#7c7284]">
            {{ languageService.t('dashboard.byGrade') }}
          </h4>
          <div class="mt-3 space-y-2">
            @for (grade of overview()?.gradeDistribution ?? []; track grade.grade) {
              <a
                class="flex items-center gap-3 rounded-md border border-[#ebe6ef] px-4 py-2.5 text-sm transition hover:border-[#d3c6db] hover:bg-[#fdfcff]"
                [routerLink]="['/students']"
                [queryParams]="{ grade: grade.grade }"
              >
                <span class="flex-1 font-medium text-[#32124f]">
                  {{ languageService.t('dashboard.gradeLabel') }} {{ grade.grade }}
                </span>
                <span class="text-xs text-[#7c7284]">{{ grade.students }} {{ languageService.t('dashboard.studentsShort') }}</span>
                @if (grade.atRisk > 0) {
                  <span class="rounded-full bg-[#fff8e1] px-2.5 py-0.5 text-xs font-bold text-[#7a5b12]">
                    {{ grade.atRisk }} {{ languageService.t('dashboard.atRiskShort') }}
                  </span>
                }
              </a>
            }
          </div>
        </section>
      </div>
      }
    </section>
  `,
})
export class DashboardComponent implements OnInit {
  private readonly overviewService = inject(DashboardOverviewService);
  private readonly workspaceFilters = inject(WorkspaceFiltersService);
  readonly languageService = inject(LanguageService);

  readonly overview = signal<DashboardOverview | null>(null);
  readonly topAtRisk = signal<DashboardAttentionStudent[]>([]);
  readonly atRiskLoading = signal(true);
  readonly periodLoading = signal(true);
  readonly loadFailed = signal(false);

  readonly years = signal<string[]>([]);
  readonly terms = signal<string[]>([]);
  private currentFilters: WorkspaceFilterSet = defaultFilterSet();
  selectedYear = '';
  selectedTerm = 'all';
  private overviewRequestId = 0;

  readonly periodLabel = computed(() => {
    const data = this.overview();
    if (!data) {
      return '';
    }
    const term = this.termLabel(data.periodTerm);
    return `${this.formatYear(data.periodYear)} · ${term}`;
  });

  readonly riskRows = computed<RiskRow[]>(() => {
    const data = this.overview();
    if (!data) {
      return [];
    }
    return [
      { level: 'High', count: data.atRiskHigh },
      { level: 'Medium', count: data.atRiskMedium },
      { level: 'None', count: data.onTrack },
    ];
  });

  readonly trendOptions = computed<EChartsCoreOption>(() => {
    const data = this.overview();
    const points = data?.behaviourTrend ?? [];
    const labels = points.map((point) => this.monthLabel(point.year, point.month));

    return {
      color: ['#6d3a91'],
      tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
      legend: { bottom: 0, icon: 'roundRect', textStyle: { color: '#5f5368' } },
      grid: { top: 24, right: 20, bottom: 48, left: 40, containLabel: true },
      xAxis: {
        type: 'category',
        data: labels,
        axisLine: { lineStyle: { color: '#d8d0df' } },
        axisLabel: { color: '#6f6877' },
      },
      yAxis: {
        type: 'value',
        minInterval: 1,
        axisLabel: { color: '#6f6877' },
        splitLine: { lineStyle: { color: '#ebe6ef' } },
      },
      series: [
        {
          name: this.languageService.t('dashboard.trend.activity'),
          type: 'bar',
          data: points.map((point) => point.total),
          barMaxWidth: 22,
          itemStyle: { borderRadius: [4, 4, 0, 0] },
        },
      ],
    };
  });

  ngOnInit() {
    forkJoin({
      options: this.workspaceFilters.options().pipe(catchError(() => of({ academicYears: [], terms: [] }))),
      filters: this.workspaceFilters.load().pipe(catchError(() => of(defaultFilterSet()))),
    }).subscribe({
      next: ({ options, filters }) => this.initializePeriod(options.academicYears, options.terms, filters),
      error: () => {
        this.currentFilters = defaultFilterSet();
        this.terms.set([]);
        this.loadOverview();
      },
    });
  }

  onPeriodChange(): void {
    this.currentFilters = { ...this.currentFilters, year: this.selectedYear, semester: this.selectedTerm };
    this.workspaceFilters.save(this.currentFilters);
    this.loadOverview();
  }

  private loadOverview(): void {
    const requestId = ++this.overviewRequestId;
    this.loadFailed.set(false);
    this.atRiskLoading.set(true);
    this.periodLoading.set(true);
    this.overviewService.overview(this.selectedYear || undefined, this.selectedTerm).subscribe({
      next: (data) => {
        if (requestId !== this.overviewRequestId) {
          return;
        }
        this.overview.set(data);
        this.topAtRisk.set(data.topAtRisk);
        this.atRiskLoading.set(false);
        this.periodLoading.set(false);
        // Reflect the period the server actually resolved (e.g. on first load).
        if (!this.years().includes(data.periodYear)) {
          this.years.update((years) => [data.periodYear, ...years]);
        }
        this.selectedYear = data.periodYear;
        this.selectedTerm = data.periodTerm;
      },
      error: () => {
        if (requestId === this.overviewRequestId) {
          this.loadFailed.set(true);
          this.atRiskLoading.set(false);
          this.periodLoading.set(false);
        }
      },
    });
  }

  private initializePeriod(years: string[], terms: string[], filters: WorkspaceFilterSet): void {
    this.years.set(years);
    const availableTerms = terms;
    this.terms.set(availableTerms);

    this.selectedYear = years.includes(filters.year) ? filters.year : years[0] ?? '';
    this.selectedTerm = filters.semester === 'all' || availableTerms.includes(filters.semester)
      ? filters.semester
      : 'all';
    this.currentFilters = {
      ...filters,
      year: this.selectedYear,
      semester: this.selectedTerm,
    };
    this.loadOverview();
  }

  formatYear(year: string): string {
    return year.replace('-', '–');
  }

  termLabel(term: string): string {
    const key = `dashboard.period.term.${term}`;
    const translated = this.languageService.t(key);
    return translated === key ? term : translated;
  }

  atRiskSub(): string {
    const data = this.overview();
    if (!data) {
      return '';
    }
    return this.languageService
      .t('dashboard.cards.atRiskSub')
      .replace('{high}', String(data.atRiskHigh))
      .replace('{medium}', String(data.atRiskMedium));
  }

  behavioursSub(): string {
    const data = this.overview();
    if (!data) {
      return '';
    }
    return this.languageService
      .t('dashboard.cards.behavioursSub')
      .replace('{period}', this.periodLabel());
  }

  projectsSub(): string {
    const data = this.overview();
    if (!data) {
      return '';
    }
    return this.languageService.t('dashboard.cards.projectsSub').replace('{value}', String(data.totalProjects));
  }

  monthLabel(year: number, month: number): string {
    const locale = this.languageService.language() === 'vi' ? 'vi-VN' : 'en-US';
    return new Intl.DateTimeFormat(locale, { month: 'short' }).format(new Date(year, month - 1, 1));
  }

  riskBadge(level: string): string {
    return riskBadgeClass(level);
  }

  riskDot(level: string): string {
    return riskDotClass(level);
  }
}
