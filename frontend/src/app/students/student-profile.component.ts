import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EChartsCoreOption } from 'echarts/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { LanguageService } from '../core/language/language.service';
import { RiskFlag, StudentProfileDetail, StudentsService } from './students.service';
import { riskBadgeClass } from './risk.util';

@Component({
  selector: 'app-student-profile',
  imports: [NgClass, NgxEchartsDirective, RouterLink],
  template: `
    <section class="space-y-7">
      <div class="flex flex-wrap items-center justify-between gap-3">
        <a
          class="inline-flex items-center gap-2 rounded-md border border-[#d8d0df] px-4 py-2 text-sm font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
          routerLink="/students"
        >
          <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <path d="M19 12H5" />
            <path d="M12 19l-7-7 7-7" />
          </svg>
          {{ languageService.t('studentProfile.backToStudents') }}
        </a>
        @if (student(); as profile) {
          <a
            class="inline-flex items-center gap-2 rounded-md bg-[#32124f] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[#4b2070]"
            [routerLink]="['/students', profile.id, 'digest']"
          >
            <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
              <path d="M14 2v6h6" />
            </svg>
            {{ languageService.t('studentProfile.generateDigest') }}
          </a>
        }
      </div>

      @if (loading()) {
        <p class="rounded-md border border-[#ebe6ef] bg-white px-4 py-10 text-center text-sm text-[#7c7284]">
          {{ languageService.t('studentProfile.loading') }}
        </p>
      } @else if (error()) {
        <p class="rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">{{ error() }}</p>
      } @else if (student(); as profile) {
        <div class="flex flex-wrap items-center justify-between gap-5 rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <div class="flex min-w-0 items-center gap-5">
            <span class="grid h-16 w-16 shrink-0 place-items-center rounded-full bg-[#32124f] text-xl font-bold text-[#f3c246]">
              {{ initials(profile.fullName) }}
            </span>
            <div class="min-w-0">
              <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">
                {{ languageService.t('studentProfile.eyebrow') }}
              </p>
              <h2 class="mt-1 truncate text-3xl font-semibold text-[#32124f]">{{ profile.fullName }}</h2>
              <p class="mt-1 text-sm text-[#6f6877]">{{ profile.studentCode }}</p>
            </div>
          </div>
          <dl class="grid grid-cols-2 gap-x-8 gap-y-3 text-sm sm:grid-cols-3">
            <div>
              <dt class="text-xs uppercase tracking-[0.12em] text-[#7c7284]">{{ languageService.t('studentProfile.grade') }}</dt>
              <dd class="mt-1 font-semibold text-[#32124f]">{{ profile.gradeLevel || '-' }}</dd>
            </div>
            <div>
              <dt class="text-xs uppercase tracking-[0.12em] text-[#7c7284]">{{ languageService.t('studentProfile.homeroom') }}</dt>
              <dd class="mt-1 font-semibold text-[#32124f]">{{ profile.homeroom || '-' }}</dd>
            </div>
            <div>
              <dt class="text-xs uppercase tracking-[0.12em] text-[#7c7284]">{{ languageService.t('studentProfile.teacher') }}</dt>
              <dd class="mt-1 font-semibold text-[#32124f]">
                {{ profile.teacherName || languageService.t('studentProfile.noTeacher') }}
              </dd>
            </div>
          </dl>
        </div>

        <section class="rounded-lg border border-[#ebe6ef] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <div class="border-b border-[#ebe6ef] p-6">
            <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('studentProfile.parents.title') }}</h3>
            <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('studentProfile.parents.hint') }}</p>
          </div>
          <div class="grid gap-4 p-5 md:grid-cols-2">
            @for (parent of profile.parents; track parent.id) {
              <article class="rounded-md border border-[#ebe6ef] bg-[#faf9f6] p-5">
                <div class="flex flex-wrap items-start justify-between gap-2">
                  <h4 class="font-semibold text-[#32124f]">{{ parent.fullName }}</h4>
                  @if (parent.relationship) {
                    <span class="rounded-full bg-[#eee8f3] px-3 py-1 text-xs font-semibold text-[#5b287d]">
                      {{ parent.relationship }}
                    </span>
                  }
                </div>
                <dl class="mt-4 grid gap-3 text-sm sm:grid-cols-3">
                  <div>
                    <dt class="text-xs uppercase tracking-[0.1em] text-[#7c7284]">{{ languageService.t('studentProfile.parents.ageGroup') }}</dt>
                    <dd class="mt-1 font-medium text-[#32124f]">{{ parent.ageGroup || '—' }}</dd>
                  </div>
                  <div>
                    <dt class="text-xs uppercase tracking-[0.1em] text-[#7c7284]">{{ languageService.t('studentProfile.parents.occupation') }}</dt>
                    <dd class="mt-1 font-medium text-[#32124f]">{{ parent.occupation || '—' }}</dd>
                  </div>
                  <div>
                    <dt class="text-xs uppercase tracking-[0.1em] text-[#7c7284]">{{ languageService.t('studentProfile.parents.sector') }}</dt>
                    <dd class="mt-1 font-medium text-[#32124f]">{{ sectorLabel(parent.employmentSector) }}</dd>
                  </div>
                </dl>
                @if (parent.phoneNumber || parent.email) {
                  <div class="mt-4 flex flex-wrap gap-x-5 gap-y-1 border-t border-[#e5dee9] pt-3 text-sm text-[#6f6877]">
                    @if (parent.phoneNumber) { <span>{{ parent.phoneNumber }}</span> }
                    @if (parent.email) { <span>{{ parent.email }}</span> }
                  </div>
                }
              </article>
            } @empty {
              <p class="rounded-md border border-dashed border-[#d8d0df] bg-[#faf9f6] p-8 text-center text-sm text-[#7c7284] md:col-span-2">
                {{ languageService.t('studentProfile.parents.empty') }}
              </p>
            }
          </div>
        </section>

        @if (profile.riskLevel !== 'None') {
          <div
            class="rounded-lg border p-5"
            [ngClass]="riskBanner(profile.riskLevel)"
          >
            <div class="flex flex-wrap items-center gap-3">
              <span class="text-sm font-semibold uppercase tracking-[0.14em]">
                {{ languageService.t('studentProfile.risk.banner') }}
              </span>
              <span
                class="rounded-full px-3 py-1 text-xs font-semibold"
                [ngClass]="riskBadge(profile.riskLevel)"
              >
                {{ languageService.t('studentProfile.risk.level.' + profile.riskLevel) }}
              </span>
            </div>
            <ul class="mt-3 space-y-1.5">
              @for (flag of profile.flags; track flag.code) {
                <li class="flex items-start gap-2 text-sm">
                  <span class="mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full bg-current opacity-60"></span>
                  {{ flagLabel(flag) }}
                </li>
              }
            </ul>
          </div>
        }

        <div class="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
          <article class="rounded-lg border border-[#ebe6ef] bg-white p-5 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
            <p class="text-sm text-[#7c7284]">{{ languageService.t('studentProfile.stats.behaviours') }}</p>
            <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ profile.summary.totalBehaviours }}</p>
            <p class="mt-1 text-xs text-[#7c7284]">{{ languageService.t('studentProfile.stats.behavioursHint') }}</p>
          </article>
          <article class="rounded-lg border border-[#ebe6ef] bg-white p-5 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
            <p class="text-sm text-[#7c7284]">{{ languageService.t('studentProfile.stats.recentBehaviours') }}</p>
            <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ profile.summary.behavioursLast30Days }}</p>
            <p class="mt-1 text-xs text-[#7c7284]">{{ languageService.t('studentProfile.stats.recentBehavioursHint') }}</p>
          </article>
          <article class="rounded-lg border border-[#ebe6ef] bg-white p-5 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
            <p class="text-sm text-[#7c7284]">{{ languageService.t('studentProfile.stats.projects') }}</p>
            <p class="mt-2 text-3xl font-semibold text-[#32124f]">
              {{ profile.summary.activeProjects }}
              <span class="text-base font-medium text-[#7c7284]">/ {{ profile.summary.totalProjects }}</span>
            </p>
            <p class="mt-1 text-xs text-[#7c7284]">{{ languageService.t('studentProfile.stats.projectsHint') }}</p>
          </article>
        </div>

        <div>
          <article class="rounded-lg border border-[#ebe6ef] bg-white p-6 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
            <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('studentProfile.trendTitle') }}</h3>
            <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('studentProfile.trendHint') }}</p>
            <div echarts class="mt-4 h-64 w-full" [options]="trendOptions()" role="img"></div>
          </article>
        </div>

        <div class="grid gap-5 xl:grid-cols-2">
          <article class="rounded-lg border border-[#ebe6ef] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
            <div class="border-b border-[#ebe6ef] p-6">
              <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('studentProfile.behavioursTitle') }}</h3>
              <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('studentProfile.behavioursHint') }}</p>
            </div>
            <div class="max-h-[420px] overflow-y-auto">
              <table class="w-full text-left">
                <thead class="sticky top-0 bg-[#faf9f6] text-xs uppercase tracking-[0.12em] text-[#7c7284]">
                  <tr>
                    <th class="px-6 py-3 font-semibold">{{ languageService.t('studentProfile.date') }}</th>
                    <th class="px-6 py-3 font-semibold">{{ languageService.t('studentProfile.type') }}</th>
                    <th class="px-6 py-3 font-semibold">{{ languageService.t('studentProfile.notes') }}</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-[#ebe6ef]">
                  @for (behaviour of profile.behaviours; track behaviour.id) {
                    <tr class="text-sm">
                      <td class="whitespace-nowrap px-6 py-3 text-[#6f6877]">{{ formatDate(behaviour.incidentDate) }}</td>
                      <td class="px-6 py-3 font-semibold text-[#32124f]">{{ behaviour.behaviourType }}</td>
                      <td class="px-6 py-3 text-[#6f6877]">
                        {{ behaviour.notes || '-' }}
                        @if (behaviour.recordedByName) {
                          <span class="mt-0.5 block text-xs text-[#9a91a3]">
                            {{ languageService.t('studentProfile.recordedBy') }} {{ behaviour.recordedByName }}
                          </span>
                        }
                      </td>
                    </tr>
                  } @empty {
                    <tr>
                      <td class="px-6 py-10 text-center text-sm text-[#7c7284]" colspan="3">
                        {{ languageService.t('studentProfile.noBehaviours') }}
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </article>

          <article class="rounded-lg border border-[#ebe6ef] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
            <div class="border-b border-[#ebe6ef] p-6">
              <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('studentProfile.projectsTitle') }}</h3>
              <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('studentProfile.projectsHint') }}</p>
            </div>
            <div class="max-h-[420px] space-y-3 overflow-y-auto p-5">
              @for (project of profile.projects; track project.id) {
                <div class="rounded-md border border-[#ebe6ef] p-4 transition hover:border-[#d3c6db] hover:bg-[#fdfcff]">
                  <div class="flex flex-wrap items-start justify-between gap-3">
                    <div class="min-w-0">
                      <p class="font-semibold text-[#32124f]">{{ project.projectName }}</p>
                      <p class="mt-0.5 text-xs text-[#7c7284]">
                        {{ project.subject || '-' }} · {{ formatDate(project.createdAt) }}
                      </p>
                    </div>
                    <span
                      class="rounded-full px-3 py-1 text-xs font-semibold"
                      [ngClass]="statusClass(project.status)"
                    >
                      {{ project.status }}
                    </span>
                  </div>
                  @if (project.teacherNotes) {
                    <p class="mt-3 text-sm text-[#6f6877]">{{ project.teacherNotes }}</p>
                  }
                </div>
              } @empty {
                <p class="rounded-md border border-dashed border-[#d8d0df] bg-[#faf9f6] p-8 text-center text-sm text-[#7c7284]">
                  {{ languageService.t('studentProfile.noProjects') }}
                </p>
              }
            </div>
          </article>
        </div>

        <section class="rounded-lg border border-[#ebe6ef] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <div class="border-b border-[#ebe6ef] p-6">
            <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('studentProfile.health.title') }}</h3>
            <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('studentProfile.health.hint') }}</p>
          </div>
          <div class="grid gap-4 p-5 lg:grid-cols-2">
            @for (incident of profile.healthIncidents; track incident.id) {
              <article class="rounded-md border border-[#ebe6ef] bg-[#faf9f6] p-5">
                <div class="flex flex-wrap items-start justify-between gap-3">
                  <div>
                    <p class="font-semibold text-[#32124f]">{{ healthOptionLabel('type', incident.incidentType) }}</p>
                    <p class="mt-1 text-xs text-[#7c7284]">{{ formatDate(incident.incidentDate) }} · {{ incident.academicYear }}</p>
                  </div>
                  <span class="rounded-full px-3 py-1 text-xs font-semibold" [ngClass]="severityClass(incident.severityLevel)">
                    {{ languageService.t('studentProfile.health.severity') }} {{ incident.severityLevel }} — {{ languageService.t('imports.manual.severity.' + incident.severityLevel) }}
                  </span>
                </div>
                <dl class="mt-4 grid gap-3 text-sm sm:grid-cols-2">
                  <div>
                    <dt class="text-xs uppercase tracking-[0.1em] text-[#7c7284]">{{ languageService.t('studentProfile.health.bodyArea') }}</dt>
                    <dd class="mt-1 font-medium text-[#32124f]">{{ incident.bodyArea ? healthOptionLabel('bodyArea', incident.bodyArea) : '—' }}</dd>
                  </div>
                  <div>
                    <dt class="text-xs uppercase tracking-[0.1em] text-[#7c7284]">{{ languageService.t('studentProfile.health.location') }}</dt>
                    <dd class="mt-1 font-medium text-[#32124f]">{{ incident.location ? healthOptionLabel('location', incident.location) : '—' }}</dd>
                  </div>
                </dl>
                <div class="mt-4 border-t border-[#e5dee9] pt-4 text-sm">
                  <p><span class="font-semibold text-[#32124f]">{{ languageService.t('studentProfile.health.cause') }}:</span> <span class="text-[#6f6877]">{{ incident.cause }}</span></p>
                  @if (incident.actionTaken) {
                    <p class="mt-2"><span class="font-semibold text-[#32124f]">{{ languageService.t('studentProfile.health.actionTaken') }}:</span> <span class="text-[#6f6877]">{{ incident.actionTaken }}</span></p>
                  }
                  @if (incident.recordedByName) {
                    <p class="mt-2 text-xs text-[#9a91a3]">{{ languageService.t('studentProfile.recordedBy') }} {{ incident.recordedByName }}</p>
                  }
                </div>
              </article>
            } @empty {
              <p class="rounded-md border border-dashed border-[#d8d0df] bg-[#faf9f6] p-8 text-center text-sm text-[#7c7284] lg:col-span-2">
                {{ languageService.t('studentProfile.health.empty') }}
              </p>
            }
          </div>
        </section>
      }
    </section>
  `,
})
export class StudentProfileComponent implements OnInit {
  private readonly studentsService = inject(StudentsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly languageService = inject(LanguageService);

  readonly student = signal<StudentProfileDetail | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);

  readonly trendOptions = computed<EChartsCoreOption>(() => {
    const profile = this.student();
    const locale = this.languageService.language() === 'vi' ? 'vi-VN' : 'en-US';
    const monthFormat = new Intl.DateTimeFormat(locale, { month: 'short', year: 'numeric' });

    const months: { key: string; label: string }[] = [];
    const counts = new Map<string, number>();
    const now = new Date();
    for (let offset = 5; offset >= 0; offset--) {
      const date = new Date(now.getFullYear(), now.getMonth() - offset, 1);
      const key = `${date.getFullYear()}-${date.getMonth()}`;
      months.push({ key, label: monthFormat.format(date) });
      counts.set(key, 0);
    }

    for (const behaviour of profile?.behaviours ?? []) {
      const date = new Date(behaviour.incidentDate);
      const key = `${date.getFullYear()}-${date.getMonth()}`;
      if (counts.has(key)) {
        counts.set(key, (counts.get(key) ?? 0) + 1);
      }
    }

    return {
      grid: { left: 40, right: 16, top: 24, bottom: 32 },
      tooltip: { trigger: 'axis' },
      xAxis: {
        type: 'category',
        data: months.map((month) => month.label),
        axisLabel: { color: '#6f6877' },
        axisLine: { lineStyle: { color: '#d8d0df' } },
      },
      yAxis: {
        type: 'value',
        minInterval: 1,
        axisLabel: { color: '#6f6877' },
        splitLine: { lineStyle: { color: '#f1edf4' } },
      },
      series: [
        {
          type: 'bar',
          data: months.map((month) => counts.get(month.key) ?? 0),
          itemStyle: { color: '#f3c246', borderRadius: [6, 6, 0, 0] },
          barMaxWidth: 42,
        },
      ],
    };
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
            ? this.languageService.t('studentProfile.notFound')
            : this.languageService.t('studentProfile.loadError'),
        );
      },
    });
  }

  formatDate(value: string): string {
    const locale = this.languageService.language() === 'vi' ? 'vi-VN' : 'en-US';
    return new Intl.DateTimeFormat(locale, {
      dateStyle: 'medium',
      timeZone: 'Asia/Ho_Chi_Minh',
    }).format(new Date(value));
  }

  sectorLabel(value?: string | null): string {
    if (!value) return '—';
    const normalized = value.trim().toLowerCase();
    if (normalized === 'public' || normalized === 'private') {
      return this.languageService.t(`studentProfile.parents.sector.${normalized}`);
    }
    return value;
  }

  flagLabel(flag: RiskFlag): string {
    return this.languageService
      .t('studentProfile.risk.flags.' + flag.code)
      .replace('{value}', String(flag.value));
  }

  riskBadge(level: string): string {
    return riskBadgeClass(level);
  }

  riskBanner(level: string): string {
    switch (level) {
      case 'High':
        return 'border-red-200 bg-red-50/70 text-red-800';
      case 'Medium':
        return 'border-[#efe0b0] bg-[#fff8e1] text-[#7a5b12]';
      default:
        return 'border-[#e0d4ea] bg-[#f6f1fa] text-[#5b287d]';
    }
  }

  statusClass(status: string): string {
    const normalized = status.trim().toLocaleLowerCase();
    if (normalized === 'completed') {
      return 'bg-green-50 text-green-700';
    }
    if (normalized === 'in progress' || normalized === 'ongoing') {
      return 'bg-[#fff8e1] text-[#7a5b12]';
    }
    return 'bg-[#f1edf4] text-[#5b287d]';
  }

  healthOptionLabel(kind: 'type' | 'bodyArea' | 'location', value: string): string {
    const slug = value.toLowerCase().replace(/\s+/g, '-');
    const key = `imports.healthOptions.${kind}.${slug}`;
    const translated = this.languageService.t(key);
    return translated === key ? value : translated;
  }

  severityClass(level: number): string {
    if (level === 3) return 'bg-red-50 text-red-700';
    if (level === 2) return 'bg-[#fff3cd] text-[#795b0b]';
    return 'bg-green-50 text-green-700';
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
