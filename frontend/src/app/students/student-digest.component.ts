import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LanguageService } from '../core/language/language.service';
import { RiskFlag, StudentProfileDetail, StudentsService } from './students.service';
import { riskBadgeClass } from './risk.util';

@Component({
  selector: 'app-student-digest',
  imports: [NgClass, RouterLink],
  template: `
    <section class="space-y-6">
      <div class="no-print flex flex-wrap items-center justify-between gap-3">
        <a
          class="inline-flex items-center gap-2 rounded-md border border-[#d8d0df] px-4 py-2 text-sm font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
          [routerLink]="backLink()"
        >
          <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <path d="M19 12H5" />
            <path d="M12 19l-7-7 7-7" />
          </svg>
          {{ languageService.t('digest.backToProfile') }}
        </a>
        @if (student()) {
          <button
            class="inline-flex items-center gap-2 rounded-md bg-[#32124f] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[#4b2070]"
            type="button"
            (click)="print()"
          >
            <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <path d="M6 9V2h12v7" />
              <path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2" />
              <path d="M6 14h12v8H6z" />
            </svg>
            {{ languageService.t('digest.print') }}
          </button>
        }
      </div>

      @if (loading()) {
        <p class="rounded-md border border-[#ebe6ef] bg-white px-4 py-10 text-center text-sm text-[#7c7284]">
          {{ languageService.t('digest.loading') }}
        </p>
      } @else if (error()) {
        <p class="rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">{{ error() }}</p>
      } @else if (student(); as profile) {
        <article
          class="print-region mx-auto max-w-3xl rounded-lg border border-[#ebe6ef] bg-white p-8 shadow-[0_12px_35px_rgba(50,18,79,0.08)] print-shadow-none"
        >
          <header class="flex items-start justify-between gap-4 border-b border-[#ebe6ef] pb-5">
            <div>
              <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">
                {{ languageService.t('app.school') }}
              </p>
              <h1 class="mt-1 text-2xl font-semibold text-[#32124f]">
                {{ languageService.t('digest.title') }}
              </h1>
              <p class="mt-1 text-sm text-[#6f6877]">{{ generatedLine() }}</p>
            </div>
            <div class="grid h-14 w-14 shrink-0 place-items-center rounded bg-[#f3c246] text-2xl font-bold text-[#32124f]">
              O
            </div>
          </header>

          <section class="mt-6 grid gap-4 sm:grid-cols-2">
            <div>
              <p class="text-xs uppercase tracking-[0.12em] text-[#7c7284]">
                {{ languageService.t('digest.student') }}
              </p>
              <p class="mt-1 text-lg font-semibold text-[#32124f]">{{ profile.fullName }}</p>
              <p class="text-sm text-[#6f6877]">{{ profile.studentCode }}</p>
            </div>
            <div class="grid grid-cols-2 gap-3 text-sm">
              <div>
                <p class="text-xs uppercase tracking-[0.12em] text-[#7c7284]">
                  {{ languageService.t('digest.grade') }}
                </p>
                <p class="mt-1 font-semibold text-[#32124f]">{{ profile.gradeLevel || '-' }}</p>
              </div>
              <div>
                <p class="text-xs uppercase tracking-[0.12em] text-[#7c7284]">
                  {{ languageService.t('digest.homeroom') }}
                </p>
                <p class="mt-1 font-semibold text-[#32124f]">{{ profile.homeroom || '-' }}</p>
              </div>
              <div class="col-span-2">
                <p class="text-xs uppercase tracking-[0.12em] text-[#7c7284]">
                  {{ languageService.t('digest.teacher') }}
                </p>
                <p class="mt-1 font-semibold text-[#32124f]">
                  {{ profile.teacherName || languageService.t('digest.noTeacher') }}
                </p>
              </div>
            </div>
          </section>

          <section class="mt-6">
            <div class="flex items-center gap-3">
              <h2 class="text-sm font-semibold uppercase tracking-[0.12em] text-[#32124f]">
                {{ languageService.t('digest.statusTitle') }}
              </h2>
              <span
                class="rounded-full px-3 py-1 text-xs font-semibold"
                [ngClass]="riskBadge(profile.riskLevel)"
              >
                {{ languageService.t('digest.risk.' + profile.riskLevel) }}
              </span>
            </div>
            @if (profile.flags.length) {
              <ul class="mt-3 space-y-1.5 text-sm text-[#4a3f55]">
                @for (flag of profile.flags; track flag.code) {
                  <li class="flex items-start gap-2">
                    <span class="mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full bg-[#8a6811]"></span>
                    {{ flagLabel(flag) }}
                  </li>
                }
              </ul>
            } @else {
              <p class="mt-2 text-sm text-[#3f6f24]">{{ languageService.t('digest.noConcerns') }}</p>
            }
          </section>

          <section class="mt-6 grid grid-cols-2 gap-3 sm:grid-cols-3">
            <div class="rounded-md border border-[#ebe6ef] bg-[#faf9f6] p-4">
              <p class="text-xs text-[#7c7284]">{{ languageService.t('digest.behaviours') }}</p>
              <p class="mt-1 text-2xl font-semibold text-[#32124f]">{{ profile.summary.totalBehaviours }}</p>
            </div>
            <div class="rounded-md border border-[#ebe6ef] bg-[#faf9f6] p-4">
              <p class="text-xs text-[#7c7284]">{{ languageService.t('digest.recent') }}</p>
              <p class="mt-1 text-2xl font-semibold text-[#32124f]">{{ profile.summary.behavioursLast30Days }}</p>
            </div>
            <div class="rounded-md border border-[#ebe6ef] bg-[#faf9f6] p-4">
              <p class="text-xs text-[#7c7284]">{{ languageService.t('digest.activeProjects') }}</p>
              <p class="mt-1 text-2xl font-semibold text-[#32124f]">
                {{ profile.summary.activeProjects }}/{{ profile.summary.totalProjects }}
              </p>
            </div>
          </section>

          <section class="mt-6">
            <h2 class="text-sm font-semibold uppercase tracking-[0.12em] text-[#32124f]">
              {{ languageService.t('digest.recentBehaviours') }}
            </h2>
            @if (recentBehaviours(profile).length) {
              <ul class="mt-3 divide-y divide-[#ebe6ef] rounded-md border border-[#ebe6ef]">
                @for (behaviour of recentBehaviours(profile); track behaviour.id) {
                  <li class="flex items-start justify-between gap-3 px-4 py-2.5 text-sm">
                    <span>
                      <span class="font-semibold text-[#32124f]">{{ behaviour.behaviourType }}</span>
                      @if (behaviour.notes) {
                        <span class="block text-xs text-[#7c7284]">{{ behaviour.notes }}</span>
                      }
                    </span>
                    <span class="shrink-0 text-xs text-[#7c7284]">{{ formatDate(behaviour.incidentDate) }}</span>
                  </li>
                }
              </ul>
            } @else {
              <p class="mt-2 text-sm text-[#7c7284]">{{ languageService.t('digest.noBehaviours') }}</p>
            }
          </section>

          <section class="mt-6">
            <h2 class="text-sm font-semibold uppercase tracking-[0.12em] text-[#32124f]">
              {{ languageService.t('digest.projects') }}
            </h2>
            @if (profile.projects.length) {
              <ul class="mt-3 divide-y divide-[#ebe6ef] rounded-md border border-[#ebe6ef]">
                @for (project of profile.projects; track project.id) {
                  <li class="flex items-start justify-between gap-3 px-4 py-2.5 text-sm">
                    <span>
                      <span class="font-semibold text-[#32124f]">{{ project.projectName }}</span>
                      <span class="block text-xs text-[#7c7284]">{{ project.subject || '-' }}</span>
                    </span>
                    <span class="shrink-0 text-xs font-semibold text-[#5b287d]">{{ project.status }}</span>
                  </li>
                }
              </ul>
            } @else {
              <p class="mt-2 text-sm text-[#7c7284]">{{ languageService.t('digest.noProjects') }}</p>
            }
          </section>

          <footer class="mt-8 border-t border-[#ebe6ef] pt-4 text-xs text-[#9a91a3]">
            {{ languageService.t('digest.footer') }}
          </footer>
        </article>
      }
    </section>
  `,
})
export class StudentDigestComponent implements OnInit {
  private readonly studentsService = inject(StudentsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly languageService = inject(LanguageService);

  readonly student = signal<StudentProfileDetail | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly backLink = computed(() => {
    const profile = this.student();
    return profile ? ['/students', profile.id] : ['/students'];
  });

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('studentId');
    const id = Number(idParam);
    if (!idParam || !Number.isInteger(id) || id <= 0) {
      this.router.navigate(['/students']);
      return;
    }

    this.studentsService.get(id).subscribe({
      next: (profile) => {
        this.student.set(profile);
        this.loading.set(false);
      },
      error: (response) => {
        this.loading.set(false);
        this.error.set(
          response?.status === 404
            ? this.languageService.t('digest.notFound')
            : this.languageService.t('digest.loadError'),
        );
      },
    });
  }

  print(): void {
    window.print();
  }

  recentBehaviours(profile: StudentProfileDetail) {
    return profile.behaviours.slice(0, 8);
  }

  generatedLine(): string {
    return this.languageService
      .t('digest.generatedOn')
      .replace('{date}', this.formatDate(new Date().toISOString()));
  }

  flagLabel(flag: RiskFlag): string {
    return this.languageService
      .t('digest.flags.' + flag.code)
      .replace('{value}', String(flag.value));
  }

  riskBadge(level: string): string {
    return riskBadgeClass(level);
  }

  formatDate(value: string): string {
    const locale = this.languageService.language() === 'vi' ? 'vi-VN' : 'en-US';
    return new Intl.DateTimeFormat(locale, {
      dateStyle: 'medium',
      timeZone: 'Asia/Ho_Chi_Minh',
    }).format(new Date(value));
  }
}
