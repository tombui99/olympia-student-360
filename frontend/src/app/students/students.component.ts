import { Component, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { AuthenticationService } from '../core/auth/auth.service';
import { LanguageService } from '../core/language/language.service';
import { StudentListItem, StudentsService } from './students.service';
import { riskBadgeClass } from './risk.util';

@Component({
  selector: 'app-students',
  imports: [NgClass, FormsModule, RouterLink],
  template: `
    <section class="space-y-7">
      <div>
        <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">
          {{ languageService.t('students.eyebrow') }}
        </p>
        <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">
          {{ languageService.t('students.title') }}
        </h2>
        <p class="mt-2 max-w-3xl text-[#6f6877]">{{ scopeHint() }}</p>
      </div>

      @if (error()) {
        <p class="rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">{{ error() }}</p>
      }

      <section class="rounded-lg border border-[#ebe6ef] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
        <div class="flex flex-wrap items-center justify-between gap-4 border-b border-[#ebe6ef] p-6">
          <div>
            <h3 class="text-lg font-semibold text-[#32124f]">
              {{ languageService.t('students.listTitle') }}
            </h3>
            <p class="mt-1 text-sm text-[#6f6877]">
              {{ totalCount() }} {{ languageService.t('students.studentsLabel') }}
            </p>
          </div>
          <div class="flex flex-wrap items-center gap-3">
            <label class="relative block">
              <span class="sr-only">{{ languageService.t('students.searchPlaceholder') }}</span>
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
                class="w-64 rounded-md border border-[#e1dbe6] bg-[#f8f6fa] py-2.5 pl-9 pr-3 text-sm outline-none transition focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10"
                [(ngModel)]="search"
                [placeholder]="languageService.t('students.searchPlaceholder')"
              />
            </label>
            <select
              class="rounded-md border border-[#e1dbe6] bg-[#f8f6fa] px-3 py-2.5 text-sm outline-none transition focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10"
              [(ngModel)]="gradeFilter"
            >
              <option value="">{{ languageService.t('students.allGrades') }}</option>
              @for (grade of gradeLevels(); track grade) {
                <option [value]="grade">
                  {{ languageService.t('students.grade') }} {{ grade }}
                </option>
              }
            </select>
            <label class="flex items-center gap-2 text-sm text-[#6f6877]">
              <input type="checkbox" [(ngModel)]="atRiskOnly" />
              {{ languageService.t('students.atRiskOnly') }}
            </label>
          </div>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full min-w-[980px] text-left">
            <thead class="bg-[#faf9f6] text-xs uppercase tracking-[0.12em] text-[#7c7284]">
              <tr>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('students.student') }}</th>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('students.grade') }}</th>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('students.homeroom') }}</th>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('students.teacher') }}</th>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('students.behaviours') }}</th>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('students.projects') }}</th>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('students.risk') }}</th>
                <th class="px-6 py-4 font-semibold"></th>
              </tr>
            </thead>
            <tbody class="divide-y divide-[#ebe6ef]">
              @for (student of students(); track student.id) {
                <tr class="text-sm transition hover:bg-[#fdfcff]">
                  <td class="px-6 py-4">
                    <a class="flex items-center gap-3" [routerLink]="['/students', student.id]">
                      <span class="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-[#f1edf4] text-xs font-bold text-[#5b287d]">
                        {{ initials(student.fullName) }}
                      </span>
                      <span class="min-w-0">
                        <span class="block truncate font-semibold text-[#32124f]">{{ student.fullName }}</span>
                        <span class="mt-0.5 block text-xs text-[#7c7284]">{{ student.studentCode }}</span>
                      </span>
                    </a>
                  </td>
                  <td class="px-6 py-4 text-[#6f6877]">{{ student.gradeLevel || '-' }}</td>
                  <td class="px-6 py-4 text-[#6f6877]">{{ student.homeroom || '-' }}</td>
                  <td class="px-6 py-4 text-[#6f6877]">{{ student.teacherName || '-' }}</td>
                  <td class="px-6 py-4">
                    <div class="flex flex-wrap gap-1.5">
                      <span class="rounded-full bg-emerald-50 px-2.5 py-1 text-xs font-semibold text-emerald-700">
                        {{ languageService.t('students.positiveShort') }} {{ student.positiveBehaviourCount }}
                      </span>
                      <span class="rounded-full bg-red-50 px-2.5 py-1 text-xs font-semibold text-red-700">
                        {{ languageService.t('students.concerningShort') }} {{ student.concerningBehaviourCount }}
                      </span>
                    </div>
                  </td>
                  <td class="px-6 py-4">
                    <span class="rounded-full bg-[#f1edf4] px-3 py-1 text-xs font-bold text-[#5b287d]">
                      {{ student.projectCount }}
                    </span>
                  </td>
                  <td class="px-6 py-4">
                    <span
                      class="rounded-full px-3 py-1 text-xs font-semibold"
                      [ngClass]="riskBadge(student.riskLevel)"
                    >
                      {{ languageService.t('students.riskLevel.' + student.riskLevel) }}
                    </span>
                  </td>
                  <td class="px-6 py-4 text-right">
                    <a
                      class="inline-flex items-center gap-2 rounded-md border border-[#d8d0df] px-4 py-2 text-xs font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
                      [routerLink]="['/students', student.id]"
                    >
                      {{ languageService.t('students.viewProfile') }}
                      <svg class="h-3.5 w-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                        <path d="M5 12h14" />
                        <path d="m12 5 7 7-7 7" />
                      </svg>
                    </a>
                  </td>
                </tr>
              }
              @if (loading()) {
                <tr>
                  <td class="px-6 py-10 text-center text-sm text-[#7c7284]" colspan="8">
                    {{ languageService.t('students.loading') }}
                  </td>
                </tr>
              } @else if (!students().length) {
                <tr>
                  <td class="px-6 py-10 text-center text-sm text-[#7c7284]" colspan="8">
                    {{ languageService.t('students.noStudents') }}
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
              <button class="rounded-md border border-[#d8d0df] px-4 py-2 text-xs font-semibold text-[#32124f] transition hover:bg-[#f7f3fa] disabled:cursor-not-allowed disabled:opacity-40" type="button" [disabled]="page() <= 1" (click)="previousPage()">
                {{ languageService.t('pagination.previous') }}
              </button>
              <button class="rounded-md border border-[#d8d0df] px-4 py-2 text-xs font-semibold text-[#32124f] transition hover:bg-[#f7f3fa] disabled:cursor-not-allowed disabled:opacity-40" type="button" [disabled]="page() >= totalPages()" (click)="nextPage()">
                {{ languageService.t('pagination.next') }}
              </button>
            </div>
          </div>
        }
      </section>
    </section>
  `,
})
export class StudentsComponent implements OnInit, OnDestroy {
  private readonly studentsService = inject(StudentsService);
  private readonly authService = inject(AuthenticationService);
  private readonly route = inject(ActivatedRoute);
  readonly languageService = inject(LanguageService);

  readonly students = signal<StudentListItem[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly page = signal(1);
  readonly pageSize = signal(20);
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly gradeLevels = signal<string[]>([]);
  private readonly searchSignal = signal('');
  private readonly gradeFilterSignal = signal('');
  private readonly atRiskOnlySignal = signal(false);
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

  get gradeFilter(): string {
    return this.gradeFilterSignal();
  }

  set gradeFilter(value: string) {
    this.gradeFilterSignal.set(value);
    this.loadPage(1);
  }

  get atRiskOnly(): boolean {
    return this.atRiskOnlySignal();
  }

  set atRiskOnly(value: boolean) {
    this.atRiskOnlySignal.set(value);
    this.loadPage(1);
  }

  riskBadge(level: string): string {
    return riskBadgeClass(level);
  }

  ngOnInit(): void {
    const params = this.route.snapshot.queryParamMap;
    const grade = params.get('grade');
    const homeroom = params.get('homeroom');
    if (grade) {
      this.gradeFilterSignal.set(grade);
    }
    if (homeroom) {
      this.searchSignal.set(homeroom);
    }

    this.loadPage(1);
  }

  ngOnDestroy(): void {
    if (this.searchTimer !== null) window.clearTimeout(this.searchTimer);
  }

  loadPage(page: number): void {
    const requestId = ++this.requestId;
    this.loading.set(true);
    this.error.set(null);
    this.studentsService.list(
      page, this.pageSize(), this.searchSignal(), this.gradeFilterSignal(), this.atRiskOnlySignal(),
    ).subscribe({
      next: (response) => {
        if (requestId !== this.requestId) return;
        this.students.set(response.items);
        this.page.set(response.page);
        this.totalCount.set(response.totalCount);
        this.totalPages.set(response.totalPages);
        this.gradeLevels.set(response.availableGrades);
        this.loading.set(false);
      },
      error: () => {
        if (requestId !== this.requestId) return;
        this.loading.set(false);
        this.error.set(this.languageService.t('students.loadError'));
      },
    });
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

  scopeHint(): string {
    const role = this.authService.getUserRole();
    return role === 'Admin' || role === 'Principal'
      ? this.languageService.t('students.adminScopeHint')
      : this.languageService.t('students.teacherScopeHint');
  }

  initials(name: string): string {
    return (
      name
        .trim()
        .split(/\s+/)
        .slice(-2)
        .map((part) => part[0]?.toUpperCase())
        .join('') || 'S'
    );
  }
}
