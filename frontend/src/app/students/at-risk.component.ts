import { Component, inject, OnInit, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuthenticationService } from '../core/auth/auth.service';
import { LanguageService } from '../core/language/language.service';
import { AtRiskStudent, RiskFlag, StudentsService } from './students.service';
import { riskBadgeClass, riskDotClass } from './risk.util';

@Component({
  selector: 'app-at-risk',
  imports: [NgClass, RouterLink],
  template: `
    <section class="space-y-7">
      <div>
        <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">
          {{ languageService.t('atRisk.eyebrow') }}
        </p>
        <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">
          {{ languageService.t('atRisk.title') }}
        </h2>
        <p class="mt-2 max-w-3xl text-[#6f6877]">{{ scopeHint() }}</p>
      </div>

      @if (error()) {
        <p class="rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">{{ error() }}</p>
      }

      <div class="grid gap-4 sm:grid-cols-2">
        <article class="rounded-lg border border-red-200 bg-red-50/60 p-5">
          <p class="text-sm font-medium text-red-700">{{ languageService.t('atRisk.high') }}</p>
          <p class="mt-2 text-3xl font-semibold text-red-700">{{ highRiskCount() }}</p>
        </article>
        <article class="rounded-lg border border-[#efe0b0] bg-[#fff8e1] p-5">
          <p class="text-sm font-medium text-[#7a5b12]">{{ languageService.t('atRisk.medium') }}</p>
          <p class="mt-2 text-3xl font-semibold text-[#7a5b12]">{{ mediumRiskCount() }}</p>
        </article>
      </div>

      <section class="rounded-lg border border-[#ebe6ef] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
        <div class="flex flex-wrap items-center justify-between gap-3 border-b border-[#ebe6ef] p-6">
          <div>
            <h3 class="text-lg font-semibold text-[#32124f]">
              {{ languageService.t('atRisk.listTitle') }}
            </h3>
            <p class="mt-1 text-sm text-[#6f6877]">
              {{ totalCount() }} {{ languageService.t('atRisk.flaggedLabel') }}
            </p>
          </div>
          <p class="max-w-md text-xs leading-5 text-[#9a91a3]">
            {{ languageService.t('atRisk.rulesNote') }}
          </p>
        </div>

        <div class="divide-y divide-[#ebe6ef]">
          @for (student of students(); track student.id) {
            <a
              class="flex flex-wrap items-start gap-4 p-5 transition hover:bg-[#fdfcff]"
              [routerLink]="['/students', student.id]"
            >
              <span
                class="mt-1 h-3 w-3 shrink-0 rounded-full"
                [ngClass]="riskDot(student.riskLevel)"
              ></span>
              <span class="min-w-0 flex-1">
                <span class="flex flex-wrap items-center gap-2">
                  <span class="text-sm font-semibold text-[#32124f]">{{ student.fullName }}</span>
                  <span
                    class="rounded-full px-2.5 py-0.5 text-xs font-semibold"
                    [ngClass]="riskBadge(student.riskLevel)"
                  >
                    {{ languageService.t('atRisk.level.' + student.riskLevel) }}
                  </span>
                </span>
                <span class="mt-1 block text-xs text-[#7c7284]">
                  {{ student.studentCode }} · {{ languageService.t('atRisk.grade') }}
                  {{ student.gradeLevel || '-' }} · {{ student.homeroom || '-' }}
                  @if (student.teacherName) {
                    · {{ student.teacherName }}
                  }
                </span>
                <span class="mt-3 flex flex-wrap gap-2">
                  @for (flag of student.flags; track flag.code) {
                    <span
                      class="rounded-md px-2.5 py-1 text-xs font-medium"
                      [ngClass]="riskBadge(flag.severity)"
                    >
                      {{ flagLabel(flag) }}
                    </span>
                  }
                </span>
              </span>
              <span class="mt-1 flex shrink-0 items-center gap-2 self-center text-xs font-semibold text-[#5b287d]">
                {{ languageService.t('atRisk.openProfile') }}
                <svg class="h-3.5 w-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                  <path d="M5 12h14" />
                  <path d="m12 5 7 7-7 7" />
                </svg>
              </span>
            </a>
          }
          @if (loading()) {
            <p class="p-10 text-center text-sm text-[#7c7284]">
              {{ languageService.t('atRisk.loading') }}
            </p>
          } @else if (!students().length) {
            <div class="p-12 text-center">
              <div class="mx-auto grid h-14 w-14 place-items-center rounded-full bg-emerald-50 text-2xl">
                ✓
              </div>
              <p class="mt-4 text-lg font-semibold text-[#32124f]">
                {{ languageService.t('atRisk.emptyTitle') }}
              </p>
              <p class="mt-2 text-sm text-[#6f6877]">
                {{ languageService.t('atRisk.emptyBody') }}
              </p>
            </div>
          }
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
export class AtRiskComponent implements OnInit {
  private readonly studentsService = inject(StudentsService);
  private readonly authService = inject(AuthenticationService);
  readonly languageService = inject(LanguageService);

  readonly students = signal<AtRiskStudent[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly highRiskCount = signal(0);
  readonly mediumRiskCount = signal(0);

  ngOnInit(): void {
    this.loadPage(1);
  }

  loadPage(page: number): void {
    this.loading.set(true);
    this.error.set(null);
    this.studentsService.atRisk(page, this.pageSize()).subscribe({
      next: (response) => {
        this.students.set(response.items);
        this.page.set(response.page);
        this.totalCount.set(response.totalCount);
        this.totalPages.set(response.totalPages);
        this.highRiskCount.set(response.highRiskCount);
        this.mediumRiskCount.set(response.mediumRiskCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set(this.languageService.t('atRisk.loadError'));
      },
    });
  }

  previousPage(): void {
    if (this.page() > 1) {
      this.loadPage(this.page() - 1);
    }
  }

  nextPage(): void {
    if (this.page() < this.totalPages()) {
      this.loadPage(this.page() + 1);
    }
  }

  changePageSize(event: Event): void {
    this.pageSize.set(Number((event.target as HTMLSelectElement).value));
    this.loadPage(1);
  }

  pageLabel(): string {
    return this.languageService
      .t('pagination.pageOf')
      .replace('{page}', String(this.page()))
      .replace('{pages}', String(this.totalPages()))
      .replace('{total}', String(this.totalCount()));
  }

  scopeHint(): string {
    const role = this.authService.getUserRole();
    return role === 'Admin' || role === 'Principal'
      ? this.languageService.t('atRisk.adminScopeHint')
      : this.languageService.t('atRisk.teacherScopeHint');
  }

  flagLabel(flag: RiskFlag): string {
    return this.languageService
      .t('atRisk.flags.' + flag.code)
      .replace('{value}', String(flag.value));
  }

  riskBadge(level: string): string {
    return riskBadgeClass(level);
  }

  riskDot(level: string): string {
    return riskDotClass(level);
  }
}
