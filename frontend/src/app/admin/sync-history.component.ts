import { Component } from '@angular/core';
import { LanguageService } from '../core/language/language.service';

interface SyncRun {
  id: string;
  source: string;
  workflow: string;
  lastRun: string;
  result: string;
}

@Component({
  selector: 'app-sync-history',
  imports: [],
  template: `
    <section class="space-y-7">
      <div>
        <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">{{ languageService.t('syncHistory.eyebrow') }}</p>
        <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ languageService.t('syncHistory.title') }}</h2>
        <p class="mt-2 max-w-3xl text-[#6f6877]">{{ languageService.t('syncHistory.description') }}</p>
      </div>

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
              @for (run of runs; track run.id) {
                <tr class="text-sm">
                  <td class="px-6 py-4 font-semibold text-[#32124f]">{{ run.source }}</td>
                  <td class="px-6 py-4 text-[#6f6877]">{{ run.workflow }}</td>
                  <td class="px-6 py-4 text-[#6f6877]">{{ run.lastRun }}</td>
                  <td class="px-6 py-4">
                    <span class="rounded bg-[#eef6ee] px-3 py-1 text-xs font-semibold text-[#3f6f24]">
                      {{ run.result }}
                    </span>
                  </td>
                </tr>
              }
              @if (!runs.length) {
                <tr>
                  <td class="px-6 py-12 text-center text-sm text-[#7c7284]" colspan="4">
                    {{ languageService.t('syncHistory.empty') }}
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>
      </section>
    </section>
  `,
})
export class SyncHistoryComponent {
  readonly runs: SyncRun[] = [];

  constructor(readonly languageService: LanguageService) {}
}
