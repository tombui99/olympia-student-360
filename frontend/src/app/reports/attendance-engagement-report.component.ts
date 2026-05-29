import { Component } from '@angular/core';
import { NgFor } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LanguageService } from '../core/language/language.service';

@Component({
  selector: 'app-attendance-engagement-report',
  imports: [NgFor, RouterLink],
  template: `
    <section class="space-y-7">
      <div class="flex flex-wrap items-end justify-between gap-4">
        <div>
          <a routerLink="/reports" class="inline-flex items-center gap-2 text-sm font-semibold text-[#7a5b12] transition hover:text-[#32124f]">
            <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <path d="M19 12H5" />
              <path d="M12 19l-7-7 7-7" />
            </svg>
            {{ languageService.t('reportDetail.backToReports') }}
          </a>
          <p class="mt-4 text-sm uppercase tracking-[0.18em] text-[#8a6811]">{{ languageService.t('reportDetail.eyebrow') }}</p>
          <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ languageService.t('reports.attendance.title') }}</h2>
          <p class="mt-2 max-w-2xl text-[#6f6877]">{{ languageService.t('reports.attendance.description') }}</p>
        </div>
        <div class="rounded-md border border-[#eadb9a] bg-[#fff8df] px-4 py-3 text-sm font-medium text-[#7a5b12]">
          {{ languageService.t('reportDetail.placeholderBadge') }}
        </div>
      </div>

      <div class="grid gap-5 lg:grid-cols-[1fr_1fr_1fr]">
        <article *ngFor="let metric of metrics" class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <p class="text-sm font-medium text-[#6f6877]">{{ languageService.t(metric.labelKey) }}</p>
          <p class="mt-4 text-4xl font-semibold text-[#32124f]">-</p>
          <p class="mt-4 text-sm text-[#8a6811]">{{ languageService.t('metrics.comingSoon') }}</p>
        </article>
      </div>

      <div class="grid gap-5 xl:grid-cols-[1.2fr_1fr]">
        <section class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('attendanceReport.timeline') }}</h3>
          <div class="mt-6 grid min-h-96 place-items-center rounded-lg border border-dashed border-[#d8d0df] bg-[#faf9f6] p-8 text-center">
            <div>
              <p class="text-xl font-semibold text-[#32124f]">{{ languageService.t('attendanceReport.timelineEmptyTitle') }}</p>
              <p class="mt-3 max-w-lg text-sm leading-6 text-[#6f6877]">{{ languageService.t('attendanceReport.timelineEmptyBody') }}</p>
            </div>
          </div>
        </section>

        <section class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('attendanceReport.cohorts') }}</h3>
          <div class="mt-5 space-y-4">
            <div *ngFor="let cohort of cohorts" class="rounded-md border border-[#ebe6ef] p-4">
              <p class="font-medium text-[#32124f]">{{ languageService.t(cohort) }}</p>
              <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('metrics.awaitingWarehouse') }}</p>
            </div>
          </div>
        </section>
      </div>
    </section>
  `,
})
export class AttendanceEngagementReportComponent {
  readonly metrics = [
    { labelKey: 'attendanceReport.metrics.attendanceRate' },
    { labelKey: 'attendanceReport.metrics.absenceAlerts' },
    { labelKey: 'attendanceReport.metrics.engagementSignals' },
  ];

  readonly cohorts = [
    'attendanceReport.cohort.grade',
    'attendanceReport.cohort.homeroom',
    'attendanceReport.cohort.support',
  ];

  constructor(readonly languageService: LanguageService) {}
}
