import { DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime, distinctUntilChanged, Subject } from 'rxjs';
import { AuthenticationService } from '../core/auth/auth.service';
import { LanguageService } from '../core/language/language.service';
import { StudentCommendation, StudentsService } from './students.service';
import { ClassificationSettingsModalComponent } from './classification-settings-modal.component';

@Component({
  selector: 'app-commendations',
  imports: [ClassificationSettingsModalComponent, DatePipe, FormsModule, RouterLink],
  template: `
    <section class="space-y-7">
      <div class="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p class="text-sm uppercase tracking-[0.18em] text-[#31706f]">{{ languageService.t('commendations.eyebrow') }}</p>
          <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ languageService.t('commendations.title') }}</h2>
          <p class="mt-2 max-w-3xl text-[#6f6877]">{{ scopeHint() }}</p>
        </div>
        @if (canManageSettings()) {
          <button class="rounded-md border border-[#31706f] bg-white px-4 py-2.5 text-sm font-semibold text-[#245b59] hover:bg-[#f2faf7]" type="button" (click)="settingsOpen.set(true)">
            {{ languageService.t('classificationSettings.commendation.button') }}
          </button>
        }
      </div>

      @if (error()) {
        <p class="rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">{{ error() }}</p>
      }

      <div class="grid gap-4 sm:grid-cols-3">
        <article class="rounded-lg border border-[#cfe4dd] bg-[#f2faf7] p-5">
          <p class="text-sm font-medium text-[#31706f]">{{ languageService.t('commendations.total') }}</p>
          <p class="mt-2 text-3xl font-semibold text-[#245b59]">{{ totalCommendationCount() }}</p>
        </article>
        <article class="rounded-lg border border-[#e0d7e7] bg-white p-5">
          <p class="text-sm font-medium text-[#6f6877]">{{ languageService.t('commendations.studentsRecognised') }}</p>
          <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ recognisedStudentCount() }}</p>
        </article>
        <article class="rounded-lg border border-[#efe0b0] bg-[#fffaf0] p-5">
          <p class="text-sm font-medium text-[#7a5b12]">{{ languageService.t('commendations.last30Days') }}</p>
          <p class="mt-2 text-3xl font-semibold text-[#7a5b12]">{{ recentCount() }}</p>
        </article>
      </div>

      <section class="overflow-hidden rounded-lg border border-[#ebe6ef] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
        <div class="flex flex-wrap items-end justify-between gap-4 border-b border-[#ebe6ef] p-6">
          <div>
            <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('commendations.listTitle') }}</h3>
            <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('commendations.listHint') }}</p>
          </div>
          <div class="flex w-full flex-wrap gap-3 sm:w-auto">
            <label class="min-w-0 flex-1 sm:w-72 sm:flex-none">
              <span class="sr-only">{{ languageService.t('commendations.search') }}</span>
              <input
                class="w-full rounded-md border border-[#d8d0df] px-4 py-2.5 text-sm outline-none transition focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10"
                [ngModel]="searchTerm()"
                (ngModelChange)="onSearchChange($event)"
                [placeholder]="languageService.t('commendations.search')"
              />
            </label>
            <select
              class="rounded-md border border-[#d8d0df] bg-white px-4 py-2.5 text-sm text-[#34243d] outline-none focus:border-[#6d3a91]"
              [ngModel]="selectedGrade()"
              (ngModelChange)="changeGrade($event)"
            >
              <option value="">{{ languageService.t('commendations.allGrades') }}</option>
              @for (grade of grades(); track grade) {
                <option [value]="grade">{{ languageService.t('commendations.grade') }} {{ grade }}</option>
              }
            </select>
          </div>
        </div>

        <div class="divide-y divide-[#ebe6ef]">
          @for (commendation of commendations(); track commendation.id) {
            <article class="flex flex-wrap gap-4 p-5 transition hover:bg-[#fbfefc]">
              <div class="grid h-11 w-11 shrink-0 place-items-center rounded-full bg-[#e2f2ec] text-xl" aria-hidden="true">★</div>
              <div class="min-w-0 flex-1">
                <div class="flex flex-wrap items-center gap-x-3 gap-y-1">
                  <a class="font-semibold text-[#32124f] hover:text-[#6d3a91] hover:underline" [routerLink]="['/students', commendation.studentId]">
                    {{ commendation.studentName }}
                  </a>
                  <span class="rounded-full bg-[#e8f5ef] px-2.5 py-0.5 text-xs font-semibold text-[#31706f]">
                    {{ languageService.t('commendations.qualified') }}
                  </span>
                </div>
                <p class="mt-1 text-xs text-[#7c7284]">
                  {{ commendation.sisUserId }} · {{ languageService.t('commendations.grade') }} {{ commendation.gradeLevel || '-' }} · {{ commendation.homeroom || '-' }}
                </p>
                @if (commendation.notes) {
                  <p class="mt-3 text-sm leading-6 text-[#4f4756]">{{ commendation.notes }}</p>
                }
                <div class="mt-3 flex flex-wrap gap-2 text-xs">
                  @if (commendation.positiveBehaviourCount > 0) {
                    <span class="rounded-md bg-[#e8f5ef] px-2.5 py-1 font-medium text-[#245b59]">{{ languageService.t('commendations.positiveBehaviours').replace('{value}', commendation.positiveBehaviourCount.toString()) }}</span>
                  }
                  @if (commendation.averageCanvasGrade != null) {
                    <span class="rounded-md bg-[#f1edf4] px-2.5 py-1 font-medium text-[#5b287d]">{{ languageService.t('commendations.canvasGrade').replace('{value}', commendation.averageCanvasGrade.toFixed(1)) }}</span>
                  }
                  @if (commendation.busViolationCount > 0) {
                    <span class="rounded-md bg-red-50 px-2.5 py-1 font-medium text-red-700">{{ languageService.t('commendations.busViolations').replace('{value}', commendation.busViolationCount.toString()) }}</span>
                  }
                </div>
                @if (commendation.recordedByName) {
                  <p class="mt-2 text-xs text-[#9a91a3]">
                    {{ languageService.t('commendations.recordedBy') }} {{ commendation.recordedByName }}
                  </p>
                }
              </div>
              <time class="shrink-0 text-xs font-medium text-[#7c7284]" [attr.datetime]="commendation.incidentDate">
                {{ commendation.incidentDate | date: 'mediumDate' }}
              </time>
            </article>
          }
          @if (loading()) {
            <p class="p-10 text-center text-sm text-[#7c7284]">{{ languageService.t('commendations.loading') }}</p>
          } @else if (!commendations().length) {
            <div class="p-12 text-center">
              <div class="mx-auto grid h-14 w-14 place-items-center rounded-full bg-[#eef6ee] text-2xl">★</div>
              <p class="mt-4 text-lg font-semibold text-[#32124f]">{{ languageService.t('commendations.emptyTitle') }}</p>
              <p class="mt-2 text-sm text-[#6f6877]">{{ languageService.t('commendations.emptyBody') }}</p>
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
                  [ngModel]="pageSize()"
                  (ngModelChange)="changePageSize($event)"
                >
                  <option [ngValue]="10">10</option>
                  <option [ngValue]="20">20</option>
                  <option [ngValue]="50">50</option>
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
      <app-classification-settings-modal
        [open]="settingsOpen()"
        mode="commendation"
        (closed)="settingsOpen.set(false)"
        (saved)="loadPage(1)"
      />
    </section>
  `,
})
export class CommendationsComponent implements OnInit {
  private readonly studentsService = inject(StudentsService);
  private readonly authService = inject(AuthenticationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly searchChanges = new Subject<string>();
  readonly languageService = inject(LanguageService);

  readonly commendations = signal<StudentCommendation[]>([]);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly searchTerm = signal('');
  readonly selectedGrade = signal('');
  readonly grades = signal<string[]>([]);
  readonly page = signal(1);
  readonly pageSize = signal(10);
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly totalCommendationCount = signal(0);
  readonly recognisedStudentCount = signal(0);
  readonly recentCount = signal(0);
  readonly academicYear = signal('');
  readonly term = signal('');
  readonly settingsOpen = signal(false);

  ngOnInit(): void {
    this.searchChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.loadPage(1));
    this.loadPage(1);
  }

  loadPage(page: number): void {
    this.loading.set(true);
    this.error.set(null);
    this.studentsService
      .commendations(page, this.pageSize(), this.searchTerm(), this.selectedGrade())
      .subscribe({
      next: (response) => {
        this.commendations.set(response.items);
        this.page.set(response.page);
        this.totalCount.set(response.totalCount);
        this.totalPages.set(response.totalPages);
        this.totalCommendationCount.set(response.totalCommendationCount);
        this.recognisedStudentCount.set(response.studentsRecognisedCount);
        this.recentCount.set(response.recentCount);
        this.grades.set(response.availableGrades);
        this.academicYear.set(response.academicYear);
        this.term.set(response.term);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.error.set(this.languageService.t('commendations.loadError'));
      },
    });
  }

  onSearchChange(value: string): void {
    this.searchTerm.set(value);
    this.searchChanges.next(value.trim());
  }

  changeGrade(value: string): void {
    this.selectedGrade.set(value);
    this.loadPage(1);
  }

  changePageSize(value: number): void {
    this.pageSize.set(Number(value));
    this.loadPage(1);
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

  pageLabel(): string {
    return this.languageService
      .t('pagination.pageOf')
      .replace('{page}', String(this.page()))
      .replace('{pages}', String(this.totalPages()))
      .replace('{total}', String(this.totalCount()));
  }

  scopeHint(): string {
    const semester = this.term()
      ? this.languageService.t(`studentProfile.canvas.journeyTerm.${this.term()}`)
      : '';
    return this.languageService.t('commendations.scopeHint')
      .replace('{period}', [this.academicYear(), semester].filter(Boolean).join(' · '));
  }

  canManageSettings(): boolean {
    const role = this.authService.getUserRole();
    return role === 'Admin' || role === 'SchoolAdministration' || role === 'Principal';
  }
}
