import { Component } from '@angular/core';
import { NgFor } from '@angular/common';
import { LanguageService } from '../core/language/language.service';

@Component({
  selector: 'app-sync-history',
  imports: [NgFor],
  template: `
    <section class="space-y-7">
      <div class="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">{{ languageService.t('syncHistory.eyebrow') }}</p>
          <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ languageService.t('syncHistory.title') }}</h2>
          <p class="mt-2 max-w-3xl text-[#6f6877]">{{ languageService.t('syncHistory.description') }}</p>
        </div>
        <span class="rounded-full border border-[#f3c246]/50 bg-[#fff8e1] px-4 py-2 text-sm font-semibold text-[#7a5b12]">
          {{ languageService.t('syncHistory.status') }}
        </span>
      </div>

      <section class="grid gap-4 md:grid-cols-3">
        <article
          *ngFor="let metric of metrics"
          class="rounded-lg border border-[#ebe6ef] bg-white p-5 shadow-[0_12px_35px_rgba(50,18,79,0.08)]"
        >
          <p class="text-sm font-medium text-[#7c7284]">{{ languageService.t(metric.label) }}</p>
          <p class="mt-3 text-2xl font-semibold text-[#32124f]">{{ languageService.t(metric.value) }}</p>
          <p class="mt-2 text-sm text-[#8a6811]">{{ languageService.t(metric.hint) }}</p>
        </article>
      </section>

      <section class="rounded-lg border border-[#ebe6ef] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
        <div class="border-b border-[#ebe6ef] p-6">
          <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('syncHistory.tableTitle') }}</h3>
          <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('syncHistory.tableHint') }}</p>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full min-w-[820px] text-left">
            <thead class="bg-[#faf9f6] text-xs uppercase tracking-[0.12em] text-[#7c7284]">
              <tr>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('syncHistory.source') }}</th>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('syncHistory.workflow') }}</th>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('syncHistory.lastRun') }}</th>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('syncHistory.result') }}</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-[#ebe6ef]">
              <tr *ngFor="let row of rows" class="text-sm">
                <td class="px-6 py-4">
                  <p class="font-semibold text-[#32124f]">{{ languageService.t(row.source) }}</p>
                  <p class="mt-1 text-xs text-[#7c7284]">{{ languageService.t(row.detail) }}</p>
                </td>
                <td class="px-6 py-4 text-[#6f6877]">{{ languageService.t(row.workflow) }}</td>
                <td class="px-6 py-4 text-[#6f6877]">{{ languageService.t(row.lastRun) }}</td>
                <td class="px-6 py-4">
                  <span class="rounded bg-[#f3c246]/25 px-3 py-1 text-xs font-semibold text-[#7a5b12]">
                    {{ languageService.t(row.result) }}
                  </span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <section class="rounded-lg border border-[#ebe6ef] bg-[#32124f] p-6 text-white shadow-[0_12px_35px_rgba(50,18,79,0.12)]">
        <p class="text-sm uppercase tracking-[0.18em] text-[#f3c246]">{{ languageService.t('syncHistory.nextStepLabel') }}</p>
        <h3 class="mt-3 text-xl font-semibold">{{ languageService.t('syncHistory.nextStepTitle') }}</h3>
        <p class="mt-3 max-w-4xl text-sm leading-6 text-purple-100">{{ languageService.t('syncHistory.nextStepBody') }}</p>
      </section>
    </section>
  `,
})
export class SyncHistoryComponent {
  metrics = [
    {
      label: 'syncHistory.metrics.canvas.label',
      value: 'syncHistory.metrics.canvas.value',
      hint: 'syncHistory.metrics.canvas.hint',
    },
    {
      label: 'syncHistory.metrics.schedule.label',
      value: 'syncHistory.metrics.schedule.value',
      hint: 'syncHistory.metrics.schedule.hint',
    },
    {
      label: 'syncHistory.metrics.warehouse.label',
      value: 'syncHistory.metrics.warehouse.value',
      hint: 'syncHistory.metrics.warehouse.hint',
    },
  ];

  rows = [
    {
      source: 'syncHistory.rows.canvas.source',
      detail: 'syncHistory.rows.canvas.detail',
      workflow: 'syncHistory.rows.canvas.workflow',
      lastRun: 'syncHistory.notConfigured',
      result: 'syncHistory.placeholder',
    },
    {
      source: 'syncHistory.rows.reportFiles.source',
      detail: 'syncHistory.rows.reportFiles.detail',
      workflow: 'syncHistory.rows.reportFiles.workflow',
      lastRun: 'syncHistory.notConfigured',
      result: 'syncHistory.placeholder',
    },
    {
      source: 'syncHistory.rows.future.source',
      detail: 'syncHistory.rows.future.detail',
      workflow: 'syncHistory.rows.future.workflow',
      lastRun: 'syncHistory.notConfigured',
      result: 'syncHistory.placeholder',
    },
  ];

  constructor(readonly languageService: LanguageService) {}
}
