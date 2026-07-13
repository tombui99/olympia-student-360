import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { EChartsCoreOption } from 'echarts/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { AuthenticationService } from '../core/auth/auth.service';
import { LanguageService } from '../core/language/language.service';
import { CohortStats, StudentsService } from './students.service';

@Component({
  selector: 'app-cohorts',
  imports: [NgxEchartsDirective],
  template: `
    <section class="space-y-7">
      <div>
        <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">
          {{ languageService.t('cohorts.eyebrow') }}
        </p>
        <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">
          {{ languageService.t('cohorts.title') }}
        </h2>
        <p class="mt-2 max-w-3xl text-[#6f6877]">{{ scopeHint() }}</p>
      </div>

      @if (error()) {
        <p class="rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">{{ error() }}</p>
      }

      @if (loading()) {
        <p class="rounded-md border border-[#ebe6ef] bg-white px-4 py-10 text-center text-sm text-[#7c7284]">
          {{ languageService.t('cohorts.loading') }}
        </p>
      } @else {
        <section class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('cohorts.byGrade') }}</h3>
          <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('cohorts.chartHint') }}</p>
          <div
            echarts
            class="mt-4 h-72 w-full cursor-pointer"
            [options]="gradeChart()"
            role="img"
            [attr.aria-label]="languageService.t('cohorts.byGrade')"
            (chartClick)="onBarClick('grade', $event)"
          ></div>
          <div class="mt-4 overflow-x-auto">
            <table class="w-full min-w-[720px] text-left">
              <thead class="bg-[#faf9f6] text-xs uppercase tracking-[0.12em] text-[#7c7284]">
                <tr>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('cohorts.cohort') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('cohorts.students') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('cohorts.atRisk') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('cohorts.high') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('cohorts.avgBehaviours') }}</th>
                  <th class="px-4 py-3 font-semibold"></th>
                </tr>
              </thead>
              <tbody class="divide-y divide-[#ebe6ef]">
                @for (cohort of grades(); track cohort.key) {
                  <tr class="text-sm">
                    <td class="px-4 py-3 font-semibold text-[#32124f]">{{ gradeLabel(cohort) }}</td>
                    <td class="px-4 py-3 text-[#6f6877]">{{ cohort.studentCount }}</td>
                    <td class="px-4 py-3">
                      <span class="rounded-full bg-[#fff8e1] px-2.5 py-0.5 text-xs font-bold text-[#7a5b12]">
                        {{ cohort.atRiskCount }}
                      </span>
                    </td>
                    <td class="px-4 py-3">
                      <span class="rounded-full bg-red-50 px-2.5 py-0.5 text-xs font-bold text-red-700">
                        {{ cohort.highRiskCount }}
                      </span>
                    </td>
                    <td class="px-4 py-3 text-[#6f6877]">{{ cohort.avgBehaviours }}</td>
                    <td class="px-4 py-3 text-right">
                      <button
                        class="text-xs font-semibold text-[#5b287d] transition hover:text-[#32124f]"
                        type="button"
                        (click)="viewCohort('grade', cohort.key)"
                      >
                        {{ languageService.t('cohorts.viewStudents') }} →
                      </button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>

        <section class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('cohorts.byHomeroom') }}</h3>
          <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('cohorts.chartHint') }}</p>
          <div
            echarts
            class="mt-4 h-72 w-full cursor-pointer"
            [options]="homeroomChart()"
            role="img"
            [attr.aria-label]="languageService.t('cohorts.byHomeroom')"
            (chartClick)="onBarClick('homeroom', $event)"
          ></div>
          <div class="mt-4 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
            @for (cohort of homerooms(); track cohort.key) {
              <button
                class="rounded-lg border border-[#ebe6ef] p-4 text-left transition hover:border-[#d3c6db] hover:bg-[#fdfcff]"
                type="button"
                (click)="viewCohort('homeroom', cohort.key)"
              >
                <div class="flex items-center justify-between">
                  <p class="font-semibold text-[#32124f]">{{ cohort.label }}</p>
                  <span class="rounded-full bg-[#fff8e1] px-2.5 py-0.5 text-xs font-bold text-[#7a5b12]">
                    {{ cohort.atRiskCount }} {{ languageService.t('cohorts.atRiskShort') }}
                  </span>
                </div>
                <p class="mt-2 text-xs text-[#7c7284]">
                  {{ cohort.studentCount }} {{ languageService.t('cohorts.students') }} ·
                  {{ cohort.avgBehaviours }} {{ languageService.t('cohorts.avgShort') }} ·
                  {{ cohort.totalProjects }} {{ languageService.t('cohorts.projects') }}
                </p>
              </button>
            }
          </div>
        </section>
      }
    </section>
  `,
})
export class CohortsComponent implements OnInit {
  private readonly studentsService = inject(StudentsService);
  private readonly authService = inject(AuthenticationService);
  private readonly router = inject(Router);
  readonly languageService = inject(LanguageService);

  readonly grades = signal<CohortStats[]>([]);
  readonly homerooms = signal<CohortStats[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly gradeChart = computed(() => this.buildChart(this.grades(), (cohort) => this.gradeLabel(cohort)));
  readonly homeroomChart = computed(() => this.buildChart(this.homerooms(), (cohort) => cohort.label));

  ngOnInit(): void {
    this.studentsService.cohorts().subscribe({
      next: (response) => {
        this.grades.set(response.grades);
        this.homerooms.set(response.homerooms);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set(this.languageService.t('cohorts.loadError'));
      },
    });
  }

  scopeHint(): string {
    const role = this.authService.getUserRole();
    return role === 'Admin' || role === 'Principal'
      ? this.languageService.t('cohorts.adminScopeHint')
      : this.languageService.t('cohorts.teacherScopeHint');
  }

  gradeLabel(cohort: CohortStats): string {
    if (cohort.key === '—') {
      return this.languageService.t('cohorts.unassigned');
    }
    return `${this.languageService.t('cohorts.grade')} ${cohort.key}`;
  }

  onBarClick(dimension: 'grade' | 'homeroom', event: { dataIndex?: number }): void {
    const list = dimension === 'grade' ? this.grades() : this.homerooms();
    const cohort = typeof event?.dataIndex === 'number' ? list[event.dataIndex] : undefined;
    if (cohort) {
      this.viewCohort(dimension, cohort.key);
    }
  }

  viewCohort(dimension: 'grade' | 'homeroom', key: string): void {
    if (key === '—') {
      this.router.navigate(['/students']);
      return;
    }
    const queryParams = dimension === 'grade' ? { grade: key } : { homeroom: key };
    this.router.navigate(['/students'], { queryParams });
  }

  private buildChart(cohorts: CohortStats[], labelFn: (cohort: CohortStats) => string): EChartsCoreOption {
    return {
      color: ['#c94f4f', '#31706f'],
      tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' } },
      legend: { bottom: 0, icon: 'roundRect', textStyle: { color: '#5f5368' } },
      grid: { top: 24, right: 20, bottom: 48, left: 40, containLabel: true },
      xAxis: {
        type: 'category',
        data: cohorts.map((cohort) => labelFn(cohort)),
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
          name: this.languageService.t('cohorts.atRisk'),
          type: 'bar',
          stack: 'students',
          data: cohorts.map((cohort) => cohort.atRiskCount),
          itemStyle: { borderRadius: [0, 0, 0, 0] },
          barMaxWidth: 44,
        },
        {
          name: this.languageService.t('cohorts.onTrack'),
          type: 'bar',
          stack: 'students',
          data: cohorts.map((cohort) => Math.max(0, cohort.studentCount - cohort.atRiskCount)),
          itemStyle: { borderRadius: [6, 6, 0, 0] },
          barMaxWidth: 44,
        },
      ],
    };
  }
}
