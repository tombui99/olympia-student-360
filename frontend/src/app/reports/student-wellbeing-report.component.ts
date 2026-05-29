import { Component } from '@angular/core';
import { NgFor } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LanguageService } from '../core/language/language.service';

@Component({
  selector: 'app-student-wellbeing-report',
  imports: [NgFor, RouterLink],
  template: `
    <section class="space-y-7">
      <div class="flex flex-wrap items-end justify-between gap-4">
        <div>
          <a routerLink="/reports" class="text-sm font-semibold text-[#7a5b12] transition hover:text-[#32124f]">
            {{ languageService.t('reportDetail.backToReports') }}
          </a>
          <p class="mt-4 text-sm uppercase tracking-[0.18em] text-[#8a6811]">{{ languageService.t('reportDetail.eyebrow') }}</p>
          <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ languageService.t('reports.wellbeing.title') }}</h2>
          <p class="mt-2 max-w-2xl text-[#6f6877]">{{ languageService.t('reports.wellbeing.description') }}</p>
        </div>
        <div class="rounded-md border border-[#eadb9a] bg-[#fff8df] px-4 py-3 text-sm font-medium text-[#7a5b12]">
          {{ languageService.t('reportDetail.placeholderBadge') }}
        </div>
      </div>

      <div class="grid gap-5 xl:grid-cols-[0.8fr_1.2fr]">
        <section class="space-y-5">
          <article *ngFor="let metric of metrics" class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
            <p class="text-sm font-medium text-[#6f6877]">{{ languageService.t(metric.labelKey) }}</p>
            <p class="mt-4 text-4xl font-semibold text-[#32124f]">-</p>
            <p class="mt-4 text-sm text-[#8a6811]">{{ languageService.t('metrics.comingSoon') }}</p>
          </article>
        </section>

        <section class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('wellbeingReport.supportMap') }}</h3>
          <div class="mt-6 grid min-h-96 place-items-center rounded-lg border border-dashed border-[#d8d0df] bg-[#faf9f6] p-8 text-center">
            <div>
              <p class="text-xl font-semibold text-[#32124f]">{{ languageService.t('wellbeingReport.mapEmptyTitle') }}</p>
              <p class="mt-3 max-w-lg text-sm leading-6 text-[#6f6877]">{{ languageService.t('wellbeingReport.mapEmptyBody') }}</p>
            </div>
          </div>
        </section>
      </div>

      <section class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
        <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('wellbeingReport.followUps') }}</h3>
        <div class="mt-5 grid gap-4 md:grid-cols-3">
          <div *ngFor="let item of followUps" class="rounded-md border border-[#ebe6ef] p-4">
            <p class="font-medium text-[#32124f]">{{ languageService.t(item) }}</p>
            <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('metrics.awaitingWarehouse') }}</p>
          </div>
        </div>
      </section>
    </section>
  `,
})
export class StudentWellbeingReportComponent {
  readonly metrics = [
    { labelKey: 'wellbeingReport.metrics.checkIns' },
    { labelKey: 'wellbeingReport.metrics.supportCases' },
    { labelKey: 'wellbeingReport.metrics.followUps' },
  ];

  readonly followUps = [
    'wellbeingReport.followUp.teacher',
    'wellbeingReport.followUp.counsellor',
    'wellbeingReport.followUp.leadership',
  ];

  constructor(readonly languageService: LanguageService) {}
}
