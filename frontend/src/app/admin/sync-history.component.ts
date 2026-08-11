import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { BASE_PATH } from '../api/variables';
import { LanguageService } from '../core/language/language.service';

interface SyncRun {
  id: number;
  source: string;
  workflow: string;
  status: 'running' | 'completed' | 'failed' | 'cancelled';
  startedAtUtc: string;
  completedAtUtc: string | null;
  error: string | null;
  courses: number | null;
  assignments: number | null;
  enrollments: number | null;
  scores: number | null;
  submissions: number | null;
}

interface SyncHistoryPage {
  items: SyncRun[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
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
                  <td class="px-6 py-4 text-[#6f6877]">{{ workflowLabel(run.workflow) }}</td>
                  <td class="px-6 py-4 text-[#6f6877]">{{ formatDate(run.startedAtUtc) }}</td>
                  <td class="px-6 py-4">
                    <span class="rounded px-3 py-1 text-xs font-semibold" [class]="statusClass(run.status)" [title]="run.error ?? ''">
                      {{ statusLabel(run.status) }}
                    </span>
                  </td>
                </tr>
              }
              @if (loading()) {
                <tr>
                  <td class="px-6 py-12 text-center text-sm text-[#7c7284]" colspan="4">
                    {{ languageService.t('syncHistory.loading') }}
                  </td>
                </tr>
              } @else if (error()) {
                <tr>
                  <td class="px-6 py-12 text-center text-sm text-[#a23535]" colspan="4">
                    {{ languageService.t('syncHistory.loadError') }}
                  </td>
                </tr>
              } @else if (!runs.length) {
                <tr>
                  <td class="px-6 py-12 text-center text-sm text-[#7c7284]" colspan="4">
                    {{ languageService.t('syncHistory.empty') }}
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        @if (totalPages() > 1) {
          <div class="flex items-center justify-between border-t border-[#ebe6ef] px-6 py-4 text-sm text-[#6f6877]">
            <span>{{ languageService.t('syncHistory.page') }} {{ page() }} / {{ totalPages() }}</span>
            <div class="flex gap-2">
              <button type="button" class="rounded border border-[#d9d1df] px-3 py-2 disabled:opacity-40" [disabled]="page() <= 1 || loading()" (click)="load(page() - 1)">
                {{ languageService.t('syncHistory.previous') }}
              </button>
              <button type="button" class="rounded border border-[#d9d1df] px-3 py-2 disabled:opacity-40" [disabled]="page() >= totalPages() || loading()" (click)="load(page() + 1)">
                {{ languageService.t('syncHistory.next') }}
              </button>
            </div>
          </div>
        }
      </section>
    </section>
  `,
})
export class SyncHistoryComponent implements OnInit {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  runs: SyncRun[] = [];
  readonly loading = signal(false);
  readonly error = signal(false);
  readonly page = signal(1);
  readonly totalPages = signal(0);

  constructor(readonly languageService: LanguageService) {}

  ngOnInit(): void {
    this.load();
  }

  load(page = 1): void {
    this.loading.set(true);
    this.error.set(false);
    const params = new HttpParams().set('page', page).set('pageSize', 20);
    this.http
      .get<SyncHistoryPage>(`${this.basePath}/api/sync-history`, { params })
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: (response) => {
          this.runs = response.items;
          this.page.set(response.page);
          this.totalPages.set(response.totalPages);
        },
        error: () => this.error.set(true),
      });
  }

  formatDate(value: string): string {
    const locale = this.languageService.language() === 'vi' ? 'vi-VN' : 'en-GB';
    return new Date(value).toLocaleString(locale, { dateStyle: 'medium', timeStyle: 'medium' });
  }

  workflowLabel(workflow: string): string {
    const key = `syncHistory.workflow.${workflow}`;
    const translated = this.languageService.t(key);
    return translated === key ? workflow : translated;
  }

  statusLabel(status: SyncRun['status']): string {
    return this.languageService.t(`syncHistory.status.${status}`);
  }

  statusClass(status: SyncRun['status']): string {
    if (status === 'completed') return 'bg-[#eef6ee] text-[#3f6f24]';
    if (status === 'running') return 'bg-[#eef4fb] text-[#285f91]';
    if (status === 'cancelled') return 'bg-[#f5f1e8] text-[#80631b]';
    return 'bg-[#fbeeee] text-[#a23535]';
  }
}
