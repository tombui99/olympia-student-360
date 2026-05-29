import { Component } from '@angular/core';
import { NgFor } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LanguageService } from '../core/language/language.service';

interface ReportCard {
  id: string;
  titleKey: string;
  descriptionKey: string;
  statusKey: string;
}

@Component({
  selector: 'app-reports',
  imports: [NgFor, RouterLink],
  template: `
    <section class="space-y-7">
      <div class="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">{{ languageService.t('reports.eyebrow') }}</p>
          <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ languageService.t('reports.title') }}</h2>
          <p class="mt-2 max-w-2xl text-[#6f6877]">
            {{ languageService.t('reports.description') }}
          </p>
        </div>
        <div class="rounded-md border border-[#eadb9a] bg-[#fff8df] px-4 py-3 text-sm font-medium text-[#7a5b12]">
          {{ languageService.t('reports.status') }}
        </div>
      </div>

      <div class="grid gap-5 md:grid-cols-2 xl:grid-cols-3">
        <a
          *ngFor="let report of reports"
          [routerLink]="['/reports', report.id]"
          class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)] transition hover:border-[#f3c246] hover:shadow-[0_18px_45px_rgba(50,18,79,0.12)]"
        >
          <div class="flex items-start justify-between gap-4">
            <div class="grid h-11 w-11 place-items-center rounded bg-[#32124f] text-sm font-bold text-[#f3c246]">
              {{ languageService.t('reports.cardInitial') }}
            </div>
            <span class="rounded bg-[#f3c246]/25 px-3 py-1 text-xs font-semibold text-[#7a5b12]">
              {{ languageService.t(report.statusKey) }}
            </span>
          </div>
          <h3 class="mt-5 text-xl font-semibold text-[#32124f]">{{ languageService.t(report.titleKey) }}</h3>
          <p class="mt-3 text-sm leading-6 text-[#6f6877]">{{ languageService.t(report.descriptionKey) }}</p>
          <p class="mt-6 text-sm font-semibold text-[#32124f]">{{ languageService.t('reports.openReport') }}</p>
        </a>
      </div>
    </section>
  `,
})
export class ReportsComponent {
  readonly reports: ReportCard[] = [
    {
      id: 'academic-overview',
      titleKey: 'reports.academic.title',
      descriptionKey: 'reports.academic.description',
      statusKey: 'reports.placeholderStatus',
    },
    {
      id: 'attendance-engagement',
      titleKey: 'reports.attendance.title',
      descriptionKey: 'reports.attendance.description',
      statusKey: 'reports.placeholderStatus',
    },
    {
      id: 'student-wellbeing',
      titleKey: 'reports.wellbeing.title',
      descriptionKey: 'reports.wellbeing.description',
      statusKey: 'reports.placeholderStatus',
    },
  ];

  constructor(readonly languageService: LanguageService) {}
}
