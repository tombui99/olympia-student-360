import { Component, OnInit, signal } from '@angular/core';
import { NgFor } from '@angular/common';
import { EChartsCoreOption } from 'echarts/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { DashboardService } from '../api/api/dashboard.service';
import { MetricCardDto } from '../api/model/models';
import { LanguageService } from '../core/language/language.service';

@Component({
  selector: 'app-dashboard',
  imports: [NgFor, NgxEchartsDirective],
  template: `
    <section class="space-y-7">
      <div class="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">{{ languageService.t('dashboard.overview') }}</p>
          <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ languageService.t('dashboard.title') }}</h2>
          <p class="mt-2 max-w-2xl text-[#6f6877]">
            {{ languageService.t('dashboard.description') }}
          </p>
        </div>
        <div class="rounded-md border border-[#eadb9a] bg-[#fff8df] px-4 py-3 text-sm font-medium text-[#7a5b12]">
          {{ languageService.t('dashboard.status') }}
        </div>
      </div>

      <div class="grid gap-5 md:grid-cols-2 xl:grid-cols-4">
        <article
          *ngFor="let metric of metrics()"
          class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]"
        >
          <p class="text-sm font-medium text-[#6f6877]">{{ languageService.t(metric.label) }}</p>
          <p class="mt-4 text-4xl font-semibold text-[#32124f]">{{ metric.value }}</p>
          <p class="mt-4 text-sm text-[#8a6811]">{{ languageService.t(metric.delta) }}</p>
        </article>
      </div>

      <div class="grid gap-5 xl:grid-cols-[1.5fr_1fr]">
        <section class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <div class="flex items-center justify-between">
            <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('dashboard.analyticsCanvas') }}</h3>
            <span class="rounded bg-[#f3c246]/25 px-3 py-1 text-xs font-semibold text-[#7a5b12]">ECharts</span>
          </div>
          <div class="mt-6 rounded-lg border border-[#ebe6ef] bg-[#faf9f6] p-3">
            <div
              echarts
              [options]="overviewOptions"
              class="h-80 w-full"
              role="img"
              [attr.aria-label]="languageService.t('dashboard.analyticsCanvas')"
            ></div>
          </div>
        </section>

        <section class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('dashboard.dataSources') }}</h3>
          <div class="mt-5 space-y-4">
            <div class="rounded-md border border-[#ebe6ef] p-4">
              <p class="font-medium text-[#32124f]">{{ languageService.t('dashboard.canvas') }}</p>
              <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('dashboard.canvasStatus') }}</p>
            </div>
            <div class="rounded-md border border-[#ebe6ef] p-4">
              <p class="font-medium text-[#32124f]">{{ languageService.t('dashboard.reportFiles') }}</p>
              <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('dashboard.reportFilesStatus') }}</p>
            </div>
            <div class="rounded-md border border-[#ebe6ef] p-4">
              <p class="font-medium text-[#32124f]">{{ languageService.t('dashboard.schoolSystems') }}</p>
              <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('dashboard.schoolSystemsStatus') }}</p>
            </div>
          </div>
        </section>
      </div>
    </section>
  `,
})
export class DashboardComponent implements OnInit {
  readonly overviewOptions: EChartsCoreOption = {
    color: ['#32124f', '#b38116', '#31706f', '#7b4aa4'],
    tooltip: {
      trigger: 'axis',
      axisPointer: { type: 'cross' },
    },
    legend: {
      bottom: 0,
      icon: 'roundRect',
      textStyle: { color: '#5f5368' },
    },
    grid: {
      top: 32,
      right: 24,
      bottom: 64,
      left: 48,
    },
    toolbox: {
      right: 8,
      feature: {
        dataZoom: { yAxisIndex: 'none' },
        restore: {},
        saveAsImage: {},
      },
    },
    xAxis: {
      type: 'category',
      boundaryGap: false,
      data: ['Aug', 'Sep', 'Oct', 'Nov', 'Dec', 'Jan'],
      axisLine: { lineStyle: { color: '#d8d0df' } },
      axisLabel: { color: '#6f6877' },
    },
    yAxis: {
      type: 'value',
      axisLabel: { color: '#6f6877' },
      splitLine: { lineStyle: { color: '#ebe6ef' } },
    },
    dataZoom: [
      {
        type: 'inside',
      },
    ],
    series: [
      {
        name: 'Attendance',
        type: 'line',
        smooth: true,
        symbolSize: 8,
        areaStyle: { opacity: 0.12 },
        data: [88, 91, 90, 93, 92, 94],
      },
      {
        name: 'Assignment completion',
        type: 'line',
        smooth: true,
        symbolSize: 8,
        areaStyle: { opacity: 0.08 },
        data: [72, 76, 79, 81, 84, 87],
      },
      {
        name: 'Wellbeing check-ins',
        type: 'bar',
        barMaxWidth: 28,
        data: [68, 74, 83, 91, 109, 126],
      },
    ],
  };

  metrics = signal<MetricCardDto[]>([
    { label: 'metrics.students', value: '-', delta: 'metrics.awaitingWarehouse' },
    { label: 'metrics.attendance', value: '-', delta: 'metrics.comingSoon' },
    { label: 'metrics.academicAlerts', value: '-', delta: 'metrics.comingSoon' },
    { label: 'metrics.engagement', value: '-', delta: 'metrics.comingSoon' },
  ]);

  constructor(
    private readonly dashboardService: DashboardService,
    readonly languageService: LanguageService,
  ) {}

  ngOnInit() {
    this.dashboardService.apiDashboardSummaryGet().subscribe({
      next: () => undefined,
      error: () => undefined,
    });
  }
}
