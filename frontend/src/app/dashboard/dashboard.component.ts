import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { RouterLink } from '@angular/router';
import { EChartsCoreOption } from 'echarts/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { LanguageService } from '../core/language/language.service';
import { AtRiskStudent, StudentsService } from '../students/students.service';
import { riskBadgeClass, riskDotClass } from '../students/risk.util';
import { DashboardOverview, DashboardOverviewService } from './dashboard-overview.service';

interface RiskRow {
  level: 'High' | 'Medium' | 'None';
  count: number;
}

@Component({
  selector: 'app-dashboard',
  imports: [NgClass, RouterLink, NgxEchartsDirective],
  template: `
    <section class="space-y-7">
      <div>
        <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">{{ languageService.t('dashboard.overview') }}</p>
        <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ languageService.t('dashboard.title') }}</h2>
        <p class="mt-2 max-w-2xl text-[#6f6877]">{{ languageService.t('dashboard.description') }}</p>
      </div>

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
                  {{ student.studentCode }} · {{ languageService.t('dashboard.grade') }}
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
          <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('dashboard.trendTitle') }}</h3>
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
    </section>
  `,
})
export class DashboardComponent implements OnInit {
  private readonly overviewService = inject(DashboardOverviewService);
  private readonly studentsService = inject(StudentsService);
  readonly languageService = inject(LanguageService);

  readonly overview = signal<DashboardOverview | null>(null);
  readonly topAtRisk = signal<AtRiskStudent[]>([]);
  readonly atRiskLoading = signal(true);

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
    this.overviewService.overview().subscribe({
      next: (data) => this.overview.set(data),
      error: () => undefined,
    });

    this.studentsService.atRisk(1, 5).subscribe({
      next: (response) => {
        this.topAtRisk.set(response.items);
        this.atRiskLoading.set(false);
      },
      error: () => this.atRiskLoading.set(false),
    });
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
      .replace('{value}', String(data.behavioursLast30Days));
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
