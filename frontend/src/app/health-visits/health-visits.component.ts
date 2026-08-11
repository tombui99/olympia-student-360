import { NgClass } from '@angular/common';
import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LanguageService } from '../core/language/language.service';
import {
  HealthVisit,
  HealthVisitFilters,
  HealthVisitsService,
} from './health-visits.service';

@Component({
  selector: 'app-health-visits',
  imports: [FormsModule, NgClass, RouterLink],
  template: `
    <section class="space-y-7">
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">
            {{ languageService.t('healthVisits.eyebrow') }}
          </p>
          <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">
            {{ languageService.t('healthVisits.title') }}
          </h2>
          <p class="mt-2 max-w-3xl text-[#6f6877]">
            {{ languageService.t('healthVisits.description') }}
          </p>
        </div>
        <span class="rounded-full bg-[#eef3ff] px-3 py-1.5 text-xs font-semibold text-[#4969b2]">
          {{ languageService.t('healthVisits.source') }}
        </span>
      </div>

      <section class="rounded-lg border border-[#e6dfeb] bg-white p-5 shadow-[0_12px_35px_rgba(50,18,79,0.06)]">
        <div class="flex flex-wrap items-center justify-between gap-3">
          <h3 class="font-semibold text-[#32124f]">{{ languageService.t('healthVisits.filters') }}</h3>
          <button
            class="rounded-md border border-[#d8d0df] px-3 py-2 text-xs font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1] disabled:cursor-not-allowed disabled:opacity-40"
            type="button"
            [disabled]="!hasFilters()"
            (click)="clearFilters()"
          >
            {{ languageService.t('healthVisits.clearFilters') }}
          </button>
        </div>

        <div class="mt-4 grid gap-4 md:grid-cols-2 xl:grid-cols-5">
          <label class="xl:col-span-2">
            <span class="mb-1.5 block text-xs font-bold uppercase tracking-[0.1em] text-[#776c80]">
              {{ languageService.t('healthVisits.search') }}
            </span>
            <span class="relative block">
              <svg
                class="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[#94889d]"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2"
                aria-hidden="true"
              >
                <circle cx="11" cy="11" r="7"></circle>
                <path d="m20 20-3.5-3.5"></path>
              </svg>
              <input
                class="w-full rounded-md border border-[#ddd5e3] bg-[#faf9fb] py-2.5 pl-9 pr-3 text-sm outline-none transition focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10"
                [(ngModel)]="search"
                [placeholder]="languageService.t('healthVisits.searchPlaceholder')"
              />
            </span>
          </label>

          <label>
            <span class="mb-1.5 block text-xs font-bold uppercase tracking-[0.1em] text-[#776c80]">
              {{ languageService.t('healthVisits.class') }}
            </span>
            <select
              class="w-full rounded-md border border-[#ddd5e3] bg-[#faf9fb] px-3 py-2.5 text-sm outline-none transition focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10"
              [(ngModel)]="classFilter"
            >
              <option value="">{{ languageService.t('healthVisits.allClasses') }}</option>
              @for (className of availableClasses(); track className) {
                <option [value]="className">{{ className }}</option>
              }
            </select>
          </label>

          <label>
            <span class="mb-1.5 block text-xs font-bold uppercase tracking-[0.1em] text-[#776c80]">
              {{ languageService.t('healthVisits.classification') }}
            </span>
            <select
              class="w-full rounded-md border border-[#ddd5e3] bg-[#faf9fb] px-3 py-2.5 text-sm outline-none transition focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10"
              [(ngModel)]="classificationFilter"
            >
              <option value="">{{ languageService.t('healthVisits.allClassifications') }}</option>
              @for (value of availableClassifications(); track value) {
                <option [value]="value">{{ value }}</option>
              }
            </select>
          </label>

          <div class="grid grid-cols-2 gap-3 md:col-span-2 xl:col-span-1">
            <label>
              <span class="mb-1.5 block text-xs font-bold uppercase tracking-[0.1em] text-[#776c80]">
                {{ languageService.t('healthVisits.from') }}
              </span>
              <input
                class="w-full rounded-md border border-[#ddd5e3] bg-[#faf9fb] px-3 py-2.5 text-sm outline-none transition focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10"
                type="date"
                [(ngModel)]="fromFilter"
              />
            </label>
            <label>
              <span class="mb-1.5 block text-xs font-bold uppercase tracking-[0.1em] text-[#776c80]">
                {{ languageService.t('healthVisits.to') }}
              </span>
              <input
                class="w-full rounded-md border border-[#ddd5e3] bg-[#faf9fb] px-3 py-2.5 text-sm outline-none transition focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10"
                type="date"
                [(ngModel)]="toFilter"
              />
            </label>
          </div>
        </div>
      </section>

      @if (error()) {
        <p class="rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">{{ error() }}</p>
      }

      <section class="overflow-hidden rounded-lg border border-[#e6dfeb] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
        <div class="flex flex-wrap items-center justify-between gap-3 border-b border-[#e6dfeb] px-6 py-5">
          <div>
            <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('healthVisits.registerTitle') }}</h3>
            <p class="mt-1 text-sm text-[#6f6877]">
              {{ totalCount() }} {{ languageService.t('healthVisits.records') }}
            </p>
          </div>
        </div>

        <div
          #topScroll
          class="h-4 overflow-x-scroll border-b border-[#ebe6ef] bg-[#faf9f6]"
          role="region"
          tabindex="0"
          [attr.aria-label]="languageService.t('healthVisits.horizontalScroll')"
          (scroll)="syncHorizontalScroll($event, tableScroll)"
        >
          <div class="h-px min-w-[1420px]" aria-hidden="true"></div>
        </div>

        <div
          #tableScroll
          class="overflow-x-auto"
          (scroll)="syncHorizontalScroll($event, topScroll)"
        >
          <table class="w-full min-w-[1420px] text-left">
            <thead class="bg-[#faf9f6] text-xs uppercase tracking-[0.1em] text-[#7c7284]">
              <tr>
                <th class="px-5 py-4 font-semibold">{{ languageService.t('healthVisits.examTime') }}</th>
                <th class="px-5 py-4 font-semibold">{{ languageService.t('healthVisits.student') }}</th>
                <th class="px-5 py-4 font-semibold">{{ languageService.t('healthVisits.symptoms') }}</th>
                <th class="px-5 py-4 font-semibold">{{ languageService.t('healthVisits.diagnosis') }}</th>
                <th class="px-5 py-4 font-semibold">{{ languageService.t('healthVisits.classification') }}</th>
                <th class="px-5 py-4 font-semibold">{{ languageService.t('healthVisits.treatment') }}</th>
                <th class="px-5 py-4 font-semibold">{{ languageService.t('healthVisits.medication') }}</th>
                <th class="px-5 py-4 font-semibold">{{ languageService.t('healthVisits.examinedBy') }}</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-[#ebe6ef]">
              @for (visit of visits(); track visit.id) {
                <tr class="align-top text-sm transition hover:bg-[#fdfcff]">
                  <td class="whitespace-nowrap px-5 py-4 text-[#6f6877]">{{ formatDateTime(visit.examTime) }}</td>
                  <td class="px-5 py-4">
                    @if (visit.studentProfileId) {
                      <a class="font-semibold text-[#32124f] hover:text-[#6d3a91] hover:underline" [routerLink]="['/students', visit.studentProfileId]">
                        {{ visit.fullName }}
                      </a>
                    } @else {
                      <span class="font-semibold text-[#32124f]">{{ visit.fullName }}</span>
                    }
                    <span class="mt-1 block text-xs text-[#8b8193]">
                      {{ visit.sisUserId || languageService.t('healthVisits.noSISUserId') }}
                      @if (visit.className) { · {{ visit.className }} }
                    </span>
                  </td>
                  <td class="max-w-56 px-5 py-4 text-[#4e2a64]">{{ visit.symptoms || '—' }}</td>
                  <td class="max-w-56 px-5 py-4 text-[#6f6877]">{{ visit.diagnosis || '—' }}</td>
                  <td class="px-5 py-4">
                    <span class="whitespace-nowrap rounded-full px-2.5 py-1 text-xs font-semibold" [ngClass]="classificationClass(visit.classification)">
                      {{ visit.classification || '—' }}
                    </span>
                  </td>
                  <td class="max-w-64 px-5 py-4 text-[#6f6877]">{{ visit.treatment || '—' }}</td>
                  <td class="px-5 py-4 text-[#6f6877]">{{ visit.medication || '—' }}</td>
                  <td class="whitespace-nowrap px-5 py-4 text-[#6f6877]">{{ visit.examinedBy || '—' }}</td>
                </tr>
              }
              @if (loading()) {
                <tr>
                  <td class="px-6 py-12 text-center text-sm text-[#7c7284]" colspan="8">
                    {{ languageService.t('healthVisits.loading') }}
                  </td>
                </tr>
              } @else if (!visits().length) {
                <tr>
                  <td class="px-6 py-12 text-center text-sm text-[#7c7284]" colspan="8">
                    {{ languageService.t('healthVisits.empty') }}
                  </td>
                </tr>
              }
            </tbody>
          </table>
        </div>

        @if (!loading() && totalCount() > 0) {
          <div class="flex flex-wrap items-center justify-between gap-3 border-t border-[#ebe6ef] px-5 py-4">
            <div class="flex items-center gap-2 text-xs text-[#6f6877]">
              <span>{{ pageLabel() }}</span>
              <label class="ml-2 flex items-center gap-2">
                {{ languageService.t('pagination.perPage') }}
                <select
                  class="rounded-md border border-[#d8d0df] bg-white px-2 py-1.5 text-xs outline-none focus:border-[#6d3a91]"
                  [value]="pageSize()"
                  (change)="changePageSize($event)"
                >
                  <option value="10">10</option>
                  <option value="20">20</option>
                  <option value="50">50</option>
                </select>
              </label>
            </div>
            <div class="flex gap-2">
              <button
                class="rounded-md border border-[#d8d0df] px-4 py-2 text-xs font-semibold text-[#32124f] transition hover:bg-[#f7f3fa] disabled:cursor-not-allowed disabled:opacity-40"
                type="button"
                [disabled]="page() <= 1"
                (click)="previousPage()"
              >
                {{ languageService.t('pagination.previous') }}
              </button>
              <button
                class="rounded-md border border-[#d8d0df] px-4 py-2 text-xs font-semibold text-[#32124f] transition hover:bg-[#f7f3fa] disabled:cursor-not-allowed disabled:opacity-40"
                type="button"
                [disabled]="page() >= totalPages()"
                (click)="nextPage()"
              >
                {{ languageService.t('pagination.next') }}
              </button>
            </div>
          </div>
        }
      </section>
    </section>
  `,
})
export class HealthVisitsComponent implements OnInit, OnDestroy {
  private readonly healthVisitsService = inject(HealthVisitsService);
  readonly languageService = inject(LanguageService);

  readonly visits = signal<HealthVisit[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly page = signal(1);
  readonly pageSize = signal(20);
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly availableClasses = signal<string[]>([]);
  readonly availableClassifications = signal<string[]>([]);

  private readonly searchSignal = signal('');
  private readonly classFilterSignal = signal('');
  private readonly classificationFilterSignal = signal('');
  private readonly fromFilterSignal = signal('');
  private readonly toFilterSignal = signal('');
  private searchTimer: number | null = null;
  private requestId = 0;

  get search(): string {
    return this.searchSignal();
  }

  set search(value: string) {
    this.searchSignal.set(value);
    if (this.searchTimer !== null) window.clearTimeout(this.searchTimer);
    this.searchTimer = window.setTimeout(() => this.loadPage(1), 300);
  }

  get classFilter(): string {
    return this.classFilterSignal();
  }

  set classFilter(value: string) {
    this.classFilterSignal.set(value);
    this.loadPage(1);
  }

  get classificationFilter(): string {
    return this.classificationFilterSignal();
  }

  set classificationFilter(value: string) {
    this.classificationFilterSignal.set(value);
    this.loadPage(1);
  }

  get fromFilter(): string {
    return this.fromFilterSignal();
  }

  set fromFilter(value: string) {
    this.fromFilterSignal.set(value);
    this.loadPage(1);
  }

  get toFilter(): string {
    return this.toFilterSignal();
  }

  set toFilter(value: string) {
    this.toFilterSignal.set(value);
    this.loadPage(1);
  }

  ngOnInit(): void {
    this.loadPage(1);
  }

  ngOnDestroy(): void {
    if (this.searchTimer !== null) window.clearTimeout(this.searchTimer);
  }

  syncHorizontalScroll(event: Event, target: HTMLElement): void {
    const source = event.currentTarget as HTMLElement;
    if (target.scrollLeft !== source.scrollLeft) {
      target.scrollLeft = source.scrollLeft;
    }
  }

  loadPage(page: number): void {
    const requestId = ++this.requestId;
    this.loading.set(true);
    this.error.set(null);

    const filters: HealthVisitFilters = {
      search: this.searchSignal(),
      className: this.classFilterSignal(),
      classification: this.classificationFilterSignal(),
      from: this.fromFilterSignal(),
      to: this.toFilterSignal(),
    };

    this.healthVisitsService.list(page, this.pageSize(), filters).subscribe({
      next: (response) => {
        if (requestId !== this.requestId) return;
        this.visits.set(response.items);
        this.page.set(response.page);
        this.totalCount.set(response.totalCount);
        this.totalPages.set(response.totalPages);
        this.availableClasses.set(response.availableClasses);
        this.availableClassifications.set(response.availableClassifications);
        this.loading.set(false);
      },
      error: () => {
        if (requestId !== this.requestId) return;
        this.loading.set(false);
        this.error.set(this.languageService.t('healthVisits.loadError'));
      },
    });
  }

  clearFilters(): void {
    if (this.searchTimer !== null) window.clearTimeout(this.searchTimer);
    this.searchSignal.set('');
    this.classFilterSignal.set('');
    this.classificationFilterSignal.set('');
    this.fromFilterSignal.set('');
    this.toFilterSignal.set('');
    this.loadPage(1);
  }

  hasFilters(): boolean {
    return Boolean(
      this.searchSignal().trim() ||
      this.classFilterSignal() ||
      this.classificationFilterSignal() ||
      this.fromFilterSignal() ||
      this.toFilterSignal(),
    );
  }

  previousPage(): void {
    if (this.page() > 1) this.loadPage(this.page() - 1);
  }

  nextPage(): void {
    if (this.page() < this.totalPages()) this.loadPage(this.page() + 1);
  }

  changePageSize(event: Event): void {
    this.pageSize.set(Number((event.target as HTMLSelectElement).value));
    this.loadPage(1);
  }

  pageLabel(): string {
    return this.languageService.t('pagination.pageOf')
      .replace('{page}', String(this.page()))
      .replace('{pages}', String(this.totalPages()))
      .replace('{total}', String(this.totalCount()));
  }

  formatDateTime(value: string): string {
    const locale = this.languageService.language() === 'vi' ? 'vi-VN' : 'en-GB';
    return new Intl.DateTimeFormat(locale, {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(value));
  }

  classificationClass(value?: string | null): string {
    const text = (value || '').toLowerCase();
    if (text.includes('nghiêm trọng') || text.includes('serious')) return 'bg-red-50 text-red-700';
    if (text.includes('theo dõi') || text.includes('follow')) return 'bg-[#fff3cd] text-[#795b0b]';
    return value ? 'bg-green-50 text-green-700' : 'bg-[#f1edf4] text-[#6f6877]';
  }
}
