import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EChartsCoreOption } from 'echarts/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { LanguageService } from '../core/language/language.service';
import { RiskFlag, StudentProfileDetail, StudentsService } from './students.service';
import { riskBadgeClass } from './risk.util';

type ProfileSection =
  | 'overview'
  | 'parents'
  | 'behaviours'
  | 'projects'
  | 'transport'
  | 'wellbeing'
  | 'learning';

@Component({
  selector: 'app-student-profile',
  imports: [NgClass, FormsModule, NgxEchartsDirective, RouterLink],
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

        @if (yearOptions().length > 1) {
          <div class="flex flex-wrap items-center gap-3 rounded-lg border border-[#ebe6ef] bg-white px-5 py-4 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
            <div class="min-w-0">
              <p class="text-sm font-semibold text-[#32124f]">{{ languageService.t('studentProfile.year.title') }}</p>
              <p class="text-xs text-[#7c7284]">{{ languageService.t('studentProfile.year.hint') }}</p>
            </div>
            <label class="ml-auto flex items-center gap-2 text-sm text-[#34243d]">
              <span class="text-xs uppercase tracking-[0.12em] text-[#7c7284]">{{ languageService.t('studentProfile.year.label') }}</span>
              <select
                class="rounded-md border border-[#d8d0df] bg-white px-4 py-2.5 outline-none"
                [ngModel]="selectedYear()"
                (ngModelChange)="onYearChange($event)"
              >
                <option value="">{{ languageService.t('studentProfile.year.all') }}</option>
                @for (option of yearOptions(); track option.value) {
                  <option [value]="option.value">{{ option.label }}</option>
                }
              </select>
            </label>
          </div>
        }

        <div class="grid items-start gap-6 lg:grid-cols-[220px_minmax(0,1fr)]">
          <aside class="sticky top-4 z-20 -mx-1 overflow-x-auto px-1 lg:mx-0 lg:overflow-visible lg:px-0">
            <nav
              class="flex min-w-max gap-2 rounded-lg border border-[#ebe6ef] bg-white p-2 shadow-[0_12px_35px_rgba(50,18,79,0.08)] lg:min-w-0 lg:flex-col"
              [attr.aria-label]="languageService.t('studentProfile.navigation.label')"
            >
              @for (item of profileSections(); track item.id) {
                <button
                  type="button"
                  class="group flex min-h-11 items-center gap-3 rounded-md px-3 py-2.5 text-left text-sm font-semibold transition"
                  [ngClass]="activeSection() === item.id
                    ? 'bg-[#32124f] text-white shadow-sm'
                    : 'text-[#5f5666] hover:bg-[#f7f3f9] hover:text-[#32124f]'"
                  [attr.aria-current]="activeSection() === item.id ? 'page' : null"
                  (click)="selectSection(item.id)"
                >
                  <span
                    class="h-2 w-2 shrink-0 rounded-full"
                    [ngClass]="activeSection() === item.id ? 'bg-[#f3c246]' : 'bg-[#c8bcd0] group-hover:bg-[#8b6a9e]'"
                  ></span>
                  <span class="whitespace-nowrap lg:min-w-0 lg:flex-1 lg:whitespace-normal">{{ item.label }}</span>
                  @if (item.count !== null) {
                    <span
                      class="min-w-6 rounded-full px-2 py-0.5 text-center text-[11px]"
                      [ngClass]="activeSection() === item.id ? 'bg-white/15 text-white' : 'bg-[#f1edf4] text-[#6f6877]'"
                    >{{ item.count }}</span>
                  }
                </button>
              }
            </nav>
          </aside>

          <main class="min-w-0">
        @if (activeSection() === 'parents') {
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
        }

        @if (activeSection() === 'overview') {
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
            <div class="mt-3 flex flex-wrap gap-2 text-xs font-semibold">
              <span class="rounded-full bg-emerald-50 px-2.5 py-1 text-emerald-700">
                {{ languageService.t('studentProfile.behaviourCategory.Positive') }}: {{ profile.summary.positiveBehaviours }}
              </span>
              <span class="rounded-full bg-red-50 px-2.5 py-1 text-red-700">
                {{ languageService.t('studentProfile.behaviourCategory.Concerning') }}: {{ profile.summary.concerningBehaviours }}
              </span>
            </div>
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
            <p class="mt-1 text-sm text-[#6f6877]">{{ trendHint() }}</p>
            <div echarts class="mt-4 h-64 w-full" [options]="trendOptions()" role="img"></div>
            <p class="mt-2 text-xs text-[#9a91a3]">{{ languageService.t('studentProfile.trendSource') }}</p>
          </article>
        </div>
        }

        @if (activeSection() === 'behaviours') {
        <div>
          <article class="rounded-lg border border-[#ebe6ef] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
            <div class="border-b border-[#ebe6ef] p-6">
              <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('studentProfile.behavioursTitle') }}</h3>
              <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('studentProfile.behavioursHint') }}</p>
            </div>
            <div class="overflow-x-auto">
              <table class="w-full min-w-[780px] text-left">
                <thead class="sticky top-0 bg-[#faf9f6] text-xs uppercase tracking-[0.12em] text-[#7c7284]">
                  <tr>
                    <th class="px-6 py-3 font-semibold">{{ languageService.t('studentProfile.date') }}</th>
                    <th class="px-6 py-3 font-semibold">{{ languageService.t('studentProfile.type') }}</th>
                    <th class="px-6 py-3 font-semibold">{{ languageService.t('studentProfile.notes') }}</th>
                    <th class="px-6 py-3 font-semibold">{{ languageService.t('studentProfile.createdBy') }}</th>
                  </tr>
                </thead>
                <tbody class="divide-y divide-[#ebe6ef]">
                  @for (behaviour of profile.behaviours; track behaviour.id) {
                    <tr class="text-sm" [ngClass]="behaviour.category === 'Positive' ? 'bg-emerald-50/25' : behaviour.category === 'Concerning' ? 'bg-red-50/25' : ''">
                      <td class="whitespace-nowrap px-6 py-3 text-[#6f6877]">{{ formatDate(behaviour.incidentDate) }}</td>
                      <td class="px-6 py-3">
                        <span
                          class="inline-flex rounded-full px-2.5 py-0.5 text-[11px] font-semibold"
                          [ngClass]="behaviour.category === 'Positive' ? 'bg-emerald-100 text-emerald-700' : behaviour.category === 'Concerning' ? 'bg-red-100 text-red-700' : 'bg-slate-100 text-slate-600'"
                        >
                          {{ languageService.t('studentProfile.behaviourCategory.' + behaviour.category) }}
                        </span>
                        <span class="mt-1 block font-semibold text-[#32124f]">{{ behaviour.behaviourType }}</span>
                      </td>
                      <td class="px-6 py-3 text-[#6f6877]">
                        {{ behaviour.notes || '-' }}
                        @if (customFieldEntries(behaviour).length) {
                          <span class="mt-1 flex flex-wrap gap-1.5">
                            @for (entry of customFieldEntries(behaviour); track entry.label) {
                              <span class="rounded-full bg-[#f3eff6] px-2 py-0.5 text-[11px] text-[#5b287d]">
                                <span class="font-semibold">{{ entry.label }}:</span> {{ entry.value }}
                              </span>
                            }
                          </span>
                        }
                      </td>
                      <td class="whitespace-nowrap px-6 py-3 font-medium text-[#5f5666]">
                        {{ behaviour.recordedByName || '—' }}
                      </td>
                    </tr>
                  } @empty {
                    <tr>
                      <td class="px-6 py-10 text-center text-sm text-[#7c7284]" colspan="4">
                        {{ languageService.t('studentProfile.noBehaviours') }}
                      </td>
                    </tr>
                  }
                </tbody>
              </table>
            </div>
          </article>
        </div>
        }

        @if (activeSection() === 'projects') {
        <div>
          <article class="rounded-lg border border-[#ebe6ef] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
            <div class="border-b border-[#ebe6ef] p-6">
              <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('studentProfile.projectsTitle') }}</h3>
              <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('studentProfile.projectsHint') }}</p>
            </div>
            <div class="space-y-3 p-5">
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
                  @if (customFieldEntries(project).length) {
                    <div class="mt-3 flex flex-wrap gap-1.5">
                      @for (entry of customFieldEntries(project); track entry.label) {
                        <span class="rounded-full bg-[#f3eff6] px-2 py-0.5 text-[11px] text-[#5b287d]">
                          <span class="font-semibold">{{ entry.label }}:</span> {{ entry.value }}
                        </span>
                      }
                    </div>
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
        }

        @if (activeSection() === 'transport') {
        <section class="rounded-lg border border-[#ebe6ef] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <div class="flex flex-wrap items-start justify-between gap-3 border-b border-[#ebe6ef] p-6">
            <div>
              <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('studentProfile.bus.title') }}</h3>
              <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('studentProfile.bus.hint') }}</p>
            </div>
            <span class="rounded-full bg-[#fff8e1] px-3 py-1 text-xs font-semibold text-[#8a6811]">
              {{ languageService.t('studentProfile.bus.source') }}
            </span>
          </div>
          <div class="overflow-x-auto">
            <table class="w-full min-w-[1000px] text-left">
              <thead class="sticky top-0 z-10 bg-[#faf9f6] text-xs uppercase tracking-[0.1em] text-[#7c7284]">
                <tr>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.bus.date') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.bus.direction') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.bus.issue') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.bus.busCode') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.bus.route') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.bus.notes') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.bus.recordedBy') }}</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-[#ebe6ef]">
                @for (record of profile.busAttendanceRecords; track record.id) {
                  <tr class="text-sm align-top">
                    <td class="whitespace-nowrap px-4 py-3 text-[#6f6877]">
                      {{ formatDate(record.attendanceDate) }}
                      @if (record.academicYear) {
                        <span class="mt-0.5 block text-xs text-[#9a91a3]">{{ record.academicYear }}</span>
                      }
                    </td>
                    <td class="whitespace-nowrap px-4 py-3 font-semibold text-[#32124f]">{{ busDirectionLabel(record.tripDirection) }}</td>
                    <td class="px-4 py-3">
                      <span class="rounded-full px-2.5 py-0.5 text-xs font-semibold" [ngClass]="busViolationClass(record.violationType)">
                        {{ busViolationLabel(record.violationType) }}
                      </span>
                    </td>
                    <td class="whitespace-nowrap px-4 py-3 text-[#6f6877]">{{ record.busCode || '—' }}</td>
                    <td class="whitespace-nowrap px-4 py-3 text-[#6f6877]">{{ record.routeName || '—' }}</td>
                    <td class="min-w-72 px-4 py-3 text-[#6f6877]">
                      {{ record.notes || '—' }}
                      @if (customFieldEntries(record).length) {
                        <div class="mt-2 flex flex-wrap gap-1.5">
                          @for (entry of customFieldEntries(record); track entry.label) {
                            <span class="rounded-full bg-[#f3eff6] px-2 py-0.5 text-[11px] text-[#5b287d]">
                              <span class="font-semibold">{{ entry.label }}:</span> {{ entry.value }}
                            </span>
                          }
                        </div>
                      }
                    </td>
                    <td class="whitespace-nowrap px-4 py-3 text-[#6f6877]">{{ record.recordedByName || '—' }}</td>
                  </tr>
                } @empty {
                  <tr>
                    <td class="px-4 py-8 text-center text-sm text-[#7c7284]" colspan="7">{{ languageService.t('studentProfile.bus.empty') }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>
        }

        @if (activeSection() === 'wellbeing') {
        <section class="rounded-lg border border-[#ebe6ef] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <div class="border-b border-[#ebe6ef] p-6">
            <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('studentProfile.mental.title') }}</h3>
            <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('studentProfile.mental.hint') }}</p>
          </div>
          <div class="overflow-x-auto">
            <table class="w-full min-w-[900px] text-left">
              <thead class="sticky top-0 z-10 bg-[#faf9f6] text-xs uppercase tracking-[0.1em] text-[#7c7284]">
                <tr>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.mental.recordDate') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.mental.concernType') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.mental.riskLevel') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.mental.notes') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.mental.recordedBy') }}</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-[#ebe6ef]">
                @for (record of profile.mentalHealthRecords; track record.id) {
                  <tr class="text-sm align-top">
                    <td class="whitespace-nowrap px-4 py-3 text-[#6f6877]">
                      {{ formatDate(record.recordDate) }}
                      <span class="mt-0.5 block text-xs text-[#9a91a3]">{{ record.academicYear }}</span>
                    </td>
                    <td class="px-4 py-3 font-semibold text-[#32124f]">{{ mentalConcernLabel(record.concernType) }}</td>
                    <td class="px-4 py-3">
                      <span class="rounded-full px-2.5 py-0.5 text-xs font-semibold" [ngClass]="riskClass(record.riskLevel)">
                        {{ mentalRiskLabel(record.riskLevel) }}
                      </span>
                    </td>
                    <td class="min-w-72 px-4 py-3 text-[#6f6877]">
                      {{ record.notes || '—' }}
                      @if (customFieldEntries(record).length) {
                        <div class="mt-2 flex flex-wrap gap-1.5">
                          @for (entry of customFieldEntries(record); track entry.label) {
                            <span class="rounded-full bg-[#f3eff6] px-2 py-0.5 text-[11px] text-[#5b287d]">
                              <span class="font-semibold">{{ entry.label }}:</span> {{ entry.value }}
                            </span>
                          }
                        </div>
                      }
                    </td>
                    <td class="whitespace-nowrap px-4 py-3 text-[#6f6877]">{{ record.recordedByName || '—' }}</td>
                  </tr>
                } @empty {
                  <tr>
                    <td class="px-4 py-8 text-center text-sm text-[#7c7284]" colspan="5">{{ languageService.t('studentProfile.mental.empty') }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>

        <section class="mt-5 rounded-lg border border-[#ebe6ef] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <div class="flex flex-wrap items-start justify-between gap-3 border-b border-[#ebe6ef] p-6">
            <div>
              <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('studentProfile.healthVisits.title') }}</h3>
              <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('studentProfile.healthVisits.hint') }}</p>
            </div>
            <span class="rounded-full bg-[#eef2fb] px-3 py-1 text-xs font-semibold text-[#4464ad]">
              {{ languageService.t('studentProfile.healthVisits.source') }}
            </span>
          </div>
          <div class="overflow-x-auto">
            <table class="w-full min-w-[820px] text-left">
              <thead class="sticky top-0 z-10 bg-[#faf9f6] text-xs uppercase tracking-[0.1em] text-[#7c7284]">
                <tr>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.healthVisits.examTime') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.healthVisits.symptoms') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.healthVisits.diagnosis') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.healthVisits.classification') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.healthVisits.treatment') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.healthVisits.medication') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.healthVisits.examinedBy') }}</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-[#ebe6ef]">
                @for (visit of profile.healthVisits; track visit.id) {
                  <tr class="text-sm align-top">
                    <td class="whitespace-nowrap px-4 py-3 text-[#6f6877]">{{ formatDateTime(visit.examTime) }}</td>
                    <td class="px-4 py-3 text-[#32124f]">{{ visit.symptoms || '—' }}</td>
                    <td class="px-4 py-3 text-[#6f6877]">{{ visit.diagnosis || '—' }}</td>
                    <td class="px-4 py-3">
                      <span class="rounded-full px-2.5 py-0.5 text-xs font-semibold" [ngClass]="classificationClass(visit.classification)">{{ visit.classification || '—' }}</span>
                    </td>
                    <td class="px-4 py-3 text-[#6f6877]">{{ visit.treatment || '—' }}</td>
                    <td class="px-4 py-3 text-[#6f6877]">{{ visit.medication || '—' }}</td>
                    <td class="whitespace-nowrap px-4 py-3 text-[#6f6877]">{{ visit.examinedBy || '—' }}</td>
                  </tr>
                } @empty {
                  <tr>
                    <td class="px-4 py-8 text-center text-sm text-[#7c7284]" colspan="7">{{ languageService.t('studentProfile.healthVisits.empty') }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>
        }

        @if (activeSection() === 'learning') {
        <section class="rounded-lg border border-[#ebe6ef] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <div class="border-b border-[#ebe6ef] p-6">
            <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('studentProfile.canvas.title') }}</h3>
            <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('studentProfile.canvas.hint') }}</p>
          </div>

          @if (profile.canvas.hasData) {
            @if (profile.canvas.journey.length > 1) {
              <div class="border-b border-[#ebe6ef] p-6">
                <div class="flex flex-wrap items-baseline justify-between gap-2">
                  <h4 class="text-sm font-semibold uppercase tracking-[0.12em] text-[#32124f]">
                    {{ languageService.t('studentProfile.canvas.journeyTitle') }}
                  </h4>
                  <p class="text-xs text-[#7c7284]">{{ languageService.t('studentProfile.canvas.journeyHint') }}</p>
                </div>
                <div echarts class="mt-4 h-64 w-full" [options]="canvasJourneyOptions()" role="img"></div>
              </div>
            }
            <p class="px-6 pt-5 text-xs uppercase tracking-[0.12em] text-[#8a6811]">
              {{ languageService.t('studentProfile.canvas.currentYear') }}
              <span class="font-semibold">{{ profile.canvas.academicYear || '—' }}</span>
            </p>
            <div class="grid gap-4 px-6 pb-6 pt-3 sm:grid-cols-2 xl:grid-cols-4">
              <div class="rounded-md border border-[#ebe6ef] bg-[#faf9f6] p-4">
                <p class="text-xs uppercase tracking-[0.12em] text-[#7c7284]">{{ languageService.t('studentProfile.canvas.avgCourseGrade') }}</p>
                <p class="mt-2 text-2xl font-semibold text-[#32124f]">{{ profile.canvas.averageCourseGrade ?? '—' }}{{ profile.canvas.averageCourseGrade !== null ? '%' : '' }}</p>
              </div>
              <div class="rounded-md border border-[#ebe6ef] bg-[#faf9f6] p-4">
                <p class="text-xs uppercase tracking-[0.12em] text-[#7c7284]">{{ languageService.t('studentProfile.canvas.avgAssignmentScore') }}</p>
                <p class="mt-2 text-2xl font-semibold text-[#32124f]">{{ profile.canvas.averageAssignmentScore ?? '—' }}{{ profile.canvas.averageAssignmentScore !== null ? '%' : '' }}</p>
              </div>
              <div class="rounded-md border border-[#ebe6ef] bg-[#faf9f6] p-4">
                <p class="text-xs uppercase tracking-[0.12em] text-[#7c7284]">{{ languageService.t('studentProfile.canvas.assignmentsGraded') }}</p>
                <p class="mt-2 text-2xl font-semibold text-[#32124f]">{{ profile.canvas.assignmentsGraded }}<span class="text-base font-medium text-[#7c7284]"> / {{ profile.canvas.assignmentsSubmitted }}</span></p>
                <p class="mt-1 text-xs text-[#7c7284]">{{ languageService.t('studentProfile.canvas.gradedHint') }}</p>
              </div>
              <div class="rounded-md border border-[#ebe6ef] bg-[#faf9f6] p-4">
                <p class="text-xs uppercase tracking-[0.12em] text-[#7c7284]">{{ languageService.t('studentProfile.canvas.attention') }}</p>
                <p class="mt-2 text-2xl font-semibold text-[#32124f]">
                  <span [ngClass]="profile.canvas.missingAssignments ? 'text-[#a9485d]' : 'text-[#32124f]'">{{ profile.canvas.missingAssignments }}</span>
                  <span class="text-sm font-medium text-[#7c7284]"> {{ languageService.t('studentProfile.canvas.missing') }} · </span>
                  <span [ngClass]="profile.canvas.lateSubmissions ? 'text-[#8a6811]' : 'text-[#32124f]'">{{ profile.canvas.lateSubmissions }}</span>
                  <span class="text-sm font-medium text-[#7c7284]"> {{ languageService.t('studentProfile.canvas.late') }}</span>
                </p>
                <p class="mt-1 text-xs text-[#7c7284]">{{ profile.canvas.coursesEnrolled }} {{ languageService.t('studentProfile.canvas.coursesEnrolled') }}</p>
              </div>
            </div>

            <div class="grid gap-5 border-t border-[#ebe6ef] p-6 xl:grid-cols-2">
              <div>
                <h4 class="text-sm font-semibold uppercase tracking-[0.12em] text-[#7c7284]">{{ languageService.t('studentProfile.canvas.courseGrades') }}</h4>
                <div class="mt-3 overflow-x-auto rounded-md border border-[#ebe6ef]">
                  <table class="w-full text-left">
                    <thead class="sticky top-0 z-10 bg-[#faf9f6] text-xs uppercase tracking-[0.12em] text-[#7c7284]">
                      <tr>
                        <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.canvas.course') }}</th>
                        <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.canvas.term') }}</th>
                        <th class="px-4 py-3 text-right font-semibold">{{ languageService.t('studentProfile.canvas.grade') }}</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-[#ebe6ef]">
                      @for (course of profile.canvas.courses; track $index) {
                        <tr class="text-sm">
                          <td class="px-4 py-3 font-semibold text-[#32124f]">{{ course.subject || course.courseName }}</td>
                          <td class="px-4 py-3 text-[#6f6877]">{{ course.gradingPeriodTitle || course.term || '—' }}</td>
                          <td class="px-4 py-3 text-right font-semibold text-[#32124f]">{{ course.finalScore ?? course.currentScore ?? '—' }}{{ (course.finalScore ?? course.currentScore) !== null ? '%' : '' }}</td>
                        </tr>
                      } @empty {
                        <tr>
                          <td class="px-4 py-8 text-center text-sm text-[#7c7284]" colspan="3">{{ languageService.t('studentProfile.canvas.noCourses') }}</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </div>

              <div>
                <h4 class="text-sm font-semibold uppercase tracking-[0.12em] text-[#7c7284]">{{ languageService.t('studentProfile.canvas.recentSubmissions') }}</h4>
                <div class="mt-3 space-y-2 rounded-md border border-[#ebe6ef] p-2">
                  @for (submission of profile.canvas.recentSubmissions; track $index) {
                    <div class="rounded-md border border-[#ebe6ef] p-3">
                      <div class="flex flex-wrap items-start justify-between gap-2">
                        <div class="min-w-0">
                          <p class="truncate text-sm font-semibold text-[#32124f]">{{ submission.assignmentTitle }}</p>
                          <p class="mt-0.5 truncate text-xs text-[#7c7284]">{{ submission.subject || submission.courseName || '—' }} · {{ submission.dueAt ? formatDate(submission.dueAt) : '—' }}</p>
                        </div>
                        <div class="flex items-center gap-2">
                          @if (submission.missing && !submission.excused) {
                            <span class="rounded-full bg-[#a9485d]/15 px-2.5 py-1 text-xs font-semibold text-[#a9485d]">{{ languageService.t('studentProfile.canvas.badge.missing') }}</span>
                          } @else if (submission.late) {
                            <span class="rounded-full bg-[#f3c246]/25 px-2.5 py-1 text-xs font-semibold text-[#8a6811]">{{ languageService.t('studentProfile.canvas.badge.late') }}</span>
                          } @else if (submission.excused) {
                            <span class="rounded-full bg-[#f1edf4] px-2.5 py-1 text-xs font-semibold text-[#5b287d]">{{ languageService.t('studentProfile.canvas.badge.excused') }}</span>
                          }
                          <span class="whitespace-nowrap text-sm font-semibold text-[#32124f]">{{ submission.score ?? '—' }}<span class="text-xs font-medium text-[#7c7284]">/{{ submission.pointsPossible ?? '—' }}</span></span>
                        </div>
                      </div>
                    </div>
                  } @empty {
                    <p class="rounded-md border border-dashed border-[#d8d0df] bg-[#faf9f6] p-6 text-center text-sm text-[#7c7284]">{{ languageService.t('studentProfile.canvas.noSubmissions') }}</p>
                  }
                </div>
              </div>
            </div>
          } @else {
            <p class="p-8 text-center text-sm text-[#7c7284]">{{ languageService.t('studentProfile.canvas.empty') }}</p>
          }
        </section>
        }
          </main>
        </div>
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
  readonly activeSection = signal<ProfileSection>('overview');

  readonly profileSections = computed<{ id: ProfileSection; label: string; count: number | null }[]>(
    () => {
      const profile = this.student();
      return [
        {
          id: 'overview',
          label: this.languageService.t('studentProfile.navigation.overview'),
          count: null,
        },
        {
          id: 'parents',
          label: this.languageService.t('studentProfile.navigation.parents'),
          count: profile?.parents.length ?? 0,
        },
        {
          id: 'behaviours',
          label: this.languageService.t('studentProfile.navigation.behaviours'),
          count: profile?.behaviours.length ?? 0,
        },
        {
          id: 'projects',
          label: this.languageService.t('studentProfile.navigation.projects'),
          count: profile?.projects.length ?? 0,
        },
        {
          id: 'transport',
          label: this.languageService.t('studentProfile.navigation.transport'),
          count: profile?.busAttendanceRecords.length ?? 0,
        },
        {
          id: 'wellbeing',
          label: this.languageService.t('studentProfile.navigation.wellbeing'),
          count: (profile?.mentalHealthRecords.length ?? 0) + (profile?.healthVisits.length ?? 0),
        },
        {
          id: 'learning',
          label: this.languageService.t('studentProfile.navigation.learning'),
          count: profile?.canvas.hasData ? profile.canvas.courses.length : 0,
        },
      ];
    },
  );

  readonly trendOptions = computed<EChartsCoreOption>(() => {
    const profile = this.student();
    const locale = this.languageService.language() === 'vi' ? 'vi-VN' : 'en-US';
    const monthFormat = new Intl.DateTimeFormat(locale, { month: 'short', year: 'numeric' });
    const points = profile?.behaviourTrend ?? [];
    const labels = points.map((point) => monthFormat.format(new Date(point.year, point.month - 1, 1)));

    return {
      grid: { left: 40, right: 16, top: 24, bottom: points.length > 6 ? 56 : 32 },
      tooltip: {
        trigger: 'axis',
        valueFormatter: (value: number) => `${value} ${this.languageService.t('studentProfile.trendRecords')}`,
      },
      xAxis: {
        type: 'category',
        data: labels,
        axisLabel: { color: '#6f6877', interval: 0, rotate: points.length > 6 ? 35 : 0 },
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
          name: this.languageService.t('studentProfile.trendSeries'),
          type: 'bar',
          data: points.map((point) => point.total),
          itemStyle: { color: '#f3c246', borderRadius: [6, 6, 0, 0] },
          barMaxWidth: 42,
        },
      ],
    };
  });

  readonly trendHint = computed(() => {
    const profile = this.student();
    const count = (profile?.behaviourTrend ?? []).reduce((total, point) => total + point.total, 0);
    const key = this.selectedYear() ? 'studentProfile.trendHintYear' : 'studentProfile.trendHintRecent';
    return this.languageService.t(key)
      .replace('{year}', this.selectedYear())
      .replace('{count}', String(count));
  });

  readonly canvasJourneyOptions = computed<EChartsCoreOption>(() => {
    const journey = this.student()?.canvas.journey ?? [];
    return {
      color: ['#32124f'],
      grid: { left: 44, right: 16, top: 28, bottom: 56, containLabel: true },
      tooltip: {
        trigger: 'axis',
        formatter: (params: { dataIndex: number; value: number | null }[]) => {
          const p = params[0];
          const point = journey[p.dataIndex];
          const grade = point?.averageGrade ?? p.value;
          const year = point ? ` · ${point.academicYear}` : '';
          return `${point?.label ?? ''}${year}<br/><b>${grade === null || grade === undefined ? '—' : grade + '%'}</b>`;
        },
      },
      xAxis: {
        type: 'category',
        data: journey.map((point) => point.label),
        axisLabel: { color: '#6f6877', interval: 0, rotate: journey.length > 6 ? 35 : 0 },
        axisLine: { lineStyle: { color: '#d8d0df' } },
      },
      yAxis: {
        type: 'value',
        min: 0,
        max: 100,
        axisLabel: { color: '#6f6877', formatter: '{value}%' },
        splitLine: { lineStyle: { color: '#f1edf4' } },
      },
      series: [
        {
          type: 'line',
          smooth: true,
          symbolSize: 9,
          data: journey.map((point) => point.averageGrade ?? null),
          lineStyle: { width: 3 },
          areaStyle: { opacity: 0.08 },
          markLine: {
            symbol: 'none',
            silent: true,
            lineStyle: { color: '#b38116', type: 'dashed' },
            data: [{ yAxis: 50 }],
          },
        },
      ],
    };
  });

  private studentId = 0;
  readonly selectedYear = signal<string>('');
  private readonly knownYears = signal<string[]>([]);

  /** Distinct academic years represented anywhere on the student profile, labelled by grade when known. */
  readonly yearOptions = computed<{ value: string; label: string }[]>(() => {
    const profile = this.student();
    const journey = profile?.canvas.journey ?? [];
    const seen = new Map<string, string | null | undefined>(this.knownYears().map((year) => [year, null]));
    for (const point of journey) {
      if (!seen.has(point.academicYear)) seen.set(point.academicYear, point.gradeLevel);
    }
    for (const item of [...(profile?.behaviours ?? []), ...(profile?.projects ?? []), ...(profile?.busAttendanceRecords ?? [])]) {
      if (item.academicYear && !seen.has(item.academicYear)) seen.set(item.academicYear, null);
    }
    for (const item of profile?.mentalHealthRecords ?? []) {
      if (item.academicYear && !seen.has(item.academicYear)) seen.set(item.academicYear, null);
    }
    return Array.from(seen.entries())
      .sort((a, b) => a[0].localeCompare(b[0]))
      .map(([year, grade]) => ({
        value: year,
        label: grade ? `${this.languageService.t('studentProfile.year.grade')} ${grade} · ${year}` : year,
      }));
  });

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('studentId');
    const id = Number(idParam);
    if (!idParam || !Number.isInteger(id) || id <= 0) {
      this.router.navigate(['/students']);
      return;
    }
    this.studentId = id;
    this.route.queryParamMap.subscribe((params) => {
      const requestedSection = params.get('section');
      this.activeSection.set(this.isProfileSection(requestedSection) ? requestedSection : 'overview');
    });
    this.loadStudent();
  }

  selectSection(section: ProfileSection): void {
    this.activeSection.set(section);
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { section: section === 'overview' ? null : section },
      queryParamsHandling: 'merge',
    });
  }

  onYearChange(year: string): void {
    this.selectedYear.set(year);
    this.loadStudent();
  }

  private isProfileSection(value: string | null): value is ProfileSection {
    return (
      value === 'overview' ||
      value === 'parents' ||
      value === 'behaviours' ||
      value === 'projects' ||
      value === 'transport' ||
      value === 'wellbeing' ||
      value === 'learning'
    );
  }

  private loadStudent(): void {
    this.studentsService.get(this.studentId, this.selectedYear() || undefined).subscribe({
      next: (profile) => {
        this.rememberAcademicYears(profile);
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

  private rememberAcademicYears(profile: StudentProfileDetail): void {
    const years = new Set(this.knownYears());
    for (const point of profile.canvas.journey) years.add(point.academicYear);
    for (const item of [...profile.behaviours, ...profile.projects, ...profile.busAttendanceRecords]) {
      if (item.academicYear) years.add(item.academicYear);
    }
    for (const item of profile.mentalHealthRecords) years.add(item.academicYear);
    this.knownYears.set(Array.from(years).sort());
  }

  formatDate(value: string): string {
    const locale = this.languageService.language() === 'vi' ? 'vi-VN' : 'en-US';
    return new Intl.DateTimeFormat(locale, {
      dateStyle: 'medium',
      timeZone: 'Asia/Ho_Chi_Minh',
    }).format(new Date(value));
  }

  formatDateTime(value: string): string {
    const locale = this.languageService.language() === 'vi' ? 'vi-VN' : 'en-US';
    return new Intl.DateTimeFormat(locale, {
      dateStyle: 'medium',
      timeStyle: 'short',
      timeZone: 'Asia/Ho_Chi_Minh',
    }).format(new Date(value));
  }

  /** Admin-defined custom field values on a record, with humanised labels. */
  customFieldEntries(record: { customFields?: Record<string, string> }): { label: string; value: string }[] {
    return Object.entries(record.customFields ?? {})
      .filter(([, value]) => value !== '' && value !== null && value !== undefined)
      .map(([key, value]) => ({
        label: key.replace(/_/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase()),
        value,
      }));
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
    if (normalized === 'completed' || normalized === 'hoàn thành') {
      return 'bg-green-50 text-green-700';
    }
    if (normalized === 'in progress' || normalized === 'ongoing' || normalized === 'đang thực hiện') {
      return 'bg-[#fff8e1] text-[#7a5b12]';
    }
    return 'bg-[#f1edf4] text-[#5b287d]';
  }

  mentalConcernLabel(value: string): string {
    const slug = value.toLowerCase().replace(/[\s/]+/g, '-');
    const key = `imports.mentalHealthOptions.concern.${slug}`;
    const translated = this.languageService.t(key);
    return translated === key ? value : translated;
  }

  mentalRiskLabel(value: string): string {
    const key = `imports.mentalHealthOptions.risk.${value.toLowerCase()}`;
    const translated = this.languageService.t(key);
    return translated === key ? value : translated;
  }

  busDirectionLabel(value: string): string {
    return this.busOptionLabel('direction', value);
  }

  busViolationLabel(value: string): string {
    return this.busOptionLabel('violation', value);
  }

  busViolationClass(value: string): string {
    return value.toLowerCase() === 'no seatbelt'
      ? 'bg-red-50 text-red-700'
      : 'bg-[#fff8e1] text-[#8a6811]';
  }

  private busOptionLabel(kind: 'direction' | 'violation', value: string): string {
    const slug = value.toLowerCase().replace(/[\s/]+/g, '-');
    const key = `imports.busOptions.${kind}.${slug}`;
    const translated = this.languageService.t(key);
    return translated === key ? value : translated;
  }

  riskClass(level: string): string {
    switch ((level || '').toLowerCase()) {
      case 'high':
        return 'bg-red-50 text-red-700';
      case 'medium':
        return 'bg-[#fff3cd] text-[#795b0b]';
      default:
        return 'bg-green-50 text-green-700';
    }
  }

  classificationClass(value?: string | null): string {
    const text = (value || '').toLowerCase();
    if (text.includes('nghiêm trọng') || text.includes('serious')) return 'bg-red-50 text-red-700';
    if (text.includes('theo dõi') || text.includes('follow')) return 'bg-[#fff3cd] text-[#795b0b]';
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
