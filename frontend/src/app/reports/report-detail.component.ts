import { Component, computed, inject, signal } from '@angular/core';
import { NgFor } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { LanguageService } from '../core/language/language.service';

interface MetricPlaceholder {
  labelKey: string;
  value: string;
  noteKey: string;
}

interface ReportDefinition {
  id: string;
  titleKey: string;
  descriptionKey: string;
}

@Component({
  selector: 'app-report-detail',
  imports: [NgFor, RouterLink],
  template: `
    <section class="space-y-7">
      <div class="flex flex-wrap items-end justify-between gap-4">
        <div>
          <a routerLink="/reports" class="text-sm font-semibold text-[#7a5b12] transition hover:text-[#32124f]">
            {{ languageService.t('reportDetail.backToReports') }}
          </a>
          <p class="mt-4 text-sm uppercase tracking-[0.18em] text-[#8a6811]">{{ languageService.t('reportDetail.eyebrow') }}</p>
          <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ languageService.t(report().titleKey) }}</h2>
          <p class="mt-2 max-w-2xl text-[#6f6877]">
            {{ languageService.t(report().descriptionKey) }}
          </p>
        </div>
        <div class="rounded-md border border-[#eadb9a] bg-[#fff8df] px-4 py-3 text-sm font-medium text-[#7a5b12]">
          {{ languageService.t('reportDetail.placeholderBadge') }}
        </div>
      </div>

      <div class="grid gap-5 md:grid-cols-2 xl:grid-cols-4">
        <article
          *ngFor="let metric of metrics"
          class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]"
        >
          <p class="text-sm font-medium text-[#6f6877]">{{ languageService.t(metric.labelKey) }}</p>
          <p class="mt-4 text-4xl font-semibold text-[#32124f]">{{ metric.value }}</p>
          <p class="mt-4 text-sm text-[#8a6811]">{{ languageService.t(metric.noteKey) }}</p>
        </article>
      </div>

      <div class="grid gap-5 xl:grid-cols-[1.5fr_1fr]">
        <section class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <div class="flex items-center justify-between gap-4">
            <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('reportDetail.chartTitle') }}</h3>
            <span class="rounded bg-[#f3c246]/25 px-3 py-1 text-xs font-semibold text-[#7a5b12]">
              {{ languageService.t('reportDetail.noData') }}
            </span>
          </div>
          <div class="mt-6 grid min-h-80 place-items-center rounded-lg border border-dashed border-[#d8d0df] bg-[#faf9f6] p-8 text-center">
            <div>
              <p class="text-xl font-semibold text-[#32124f]">{{ languageService.t('reportDetail.emptyTitle') }}</p>
              <p class="mt-3 max-w-lg text-sm leading-6 text-[#6f6877]">
                {{ languageService.t('reportDetail.emptyBody') }}
              </p>
            </div>
          </div>
        </section>

        <section class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('reportDetail.sourcesTitle') }}</h3>
          <div class="mt-5 space-y-4">
            <div class="rounded-md border border-[#ebe6ef] p-4">
              <p class="font-medium text-[#32124f]">{{ languageService.t('dashboard.canvas') }}</p>
              <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('dashboard.canvasStatus') }}</p>
            </div>
            <div class="rounded-md border border-[#ebe6ef] p-4">
              <p class="font-medium text-[#32124f]">{{ languageService.t('dashboard.reportFiles') }}</p>
              <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('dashboard.reportFilesStatus') }}</p>
            </div>
          </div>
        </section>
      </div>
    </section>
  `,
})
export class ReportDetailComponent {
  private readonly route = inject(ActivatedRoute);
  private readonly reportId = signal(this.route.snapshot.paramMap.get('reportId') ?? 'academic-overview');

  private readonly reports: ReportDefinition[] = [
    {
      id: 'academic-overview',
      titleKey: 'reports.academic.title',
      descriptionKey: 'reports.academic.description',
    },
    {
      id: 'attendance-engagement',
      titleKey: 'reports.attendance.title',
      descriptionKey: 'reports.attendance.description',
    },
    {
      id: 'student-wellbeing',
      titleKey: 'reports.wellbeing.title',
      descriptionKey: 'reports.wellbeing.description',
    },
  ];

  readonly report = computed(() => {
    return this.reports.find((item) => item.id === this.reportId()) ?? this.reports[0];
  });

  readonly metrics: MetricPlaceholder[] = [
    { labelKey: 'reportDetail.metrics.students', value: '-', noteKey: 'metrics.awaitingWarehouse' },
    { labelKey: 'reportDetail.metrics.trend', value: '-', noteKey: 'metrics.comingSoon' },
    { labelKey: 'reportDetail.metrics.risk', value: '-', noteKey: 'metrics.comingSoon' },
    { labelKey: 'reportDetail.metrics.actions', value: '-', noteKey: 'metrics.comingSoon' },
  ];

  constructor(readonly languageService: LanguageService) {}
}
