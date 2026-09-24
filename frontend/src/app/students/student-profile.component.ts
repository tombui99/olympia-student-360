import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EChartsCoreOption } from 'echarts/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { LanguageService } from '../core/language/language.service';
import { AuthenticationService } from '../core/auth/auth.service';
import { RiskFlag, StudentParent, StudentProfileDetail, StudentsService } from './students.service';
import { riskBadgeClass } from './risk.util';
import {
  CanvasSubmissionSheetKind,
  StudentCanvasSubmissionSheetComponent,
} from './student-canvas-submission-sheet.component';

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
  imports: [NgClass, FormsModule, NgxEchartsDirective, RouterLink, StudentCanvasSubmissionSheetComponent],
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
          @if (authService.canAccess('academic')) {
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
              @if (profile.foreignName) {
                <p class="mt-0.5 text-sm text-[#6f6877]">{{ profile.foreignName }}</p>
              }
              <p class="mt-1 text-sm text-[#6f6877]">
                {{ profile.sisStudentCode ? profile.sisStudentCode + ' · ' : '' }}{{ profile.sisUserId }}
              </p>
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
              <dt class="text-xs uppercase tracking-[0.12em] text-[#7c7284]">{{ languageService.t('studentProfile.gender') }}</dt>
              <dd class="mt-1 font-semibold text-[#32124f]">{{ profile.gender || '-' }}</dd>
            </div>
            <div>
              <dt class="text-xs uppercase tracking-[0.12em] text-[#7c7284]">{{ languageService.t('studentProfile.dateOfBirth') }}</dt>
              <dd class="mt-1 font-semibold text-[#32124f]">{{ profile.dateOfBirth ? formatDate(profile.dateOfBirth) : '-' }}</dd>
            </div>
            <div>
              <dt class="text-xs uppercase tracking-[0.12em] text-[#7c7284]">{{ languageService.t('studentProfile.status') }}</dt>
              <dd class="mt-1 font-semibold text-[#32124f]">{{ profile.sisStatus || '-' }}</dd>
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

        <div class="-mx-1 overflow-x-auto px-1 pb-1">
          <nav
            class="flex min-w-max gap-1 rounded-lg border border-[#ebe6ef] bg-white p-1 shadow-[0_8px_26px_rgba(50,18,79,0.06)]"
            [attr.aria-label]="languageService.t('studentProfile.navigation.label')"
          >
            @for (item of profileSections(); track item.id) {
              <button
                type="button"
                class="flex min-h-10 items-center gap-2 rounded-md px-4 py-2 text-sm font-semibold transition"
                [ngClass]="activeSection() === item.id
                  ? 'bg-[#32124f] text-white shadow-sm'
                  : 'text-[#5f5666] hover:bg-[#f7f3f9] hover:text-[#32124f]'"
                [attr.aria-current]="activeSection() === item.id ? 'page' : null"
                (click)="selectSection(item.id)"
              >
                <span class="whitespace-nowrap">{{ item.label }}</span>
                @if (item.count !== null) {
                  <span
                    class="min-w-6 rounded-full px-2 py-0.5 text-center text-[11px]"
                    [ngClass]="activeSection() === item.id ? 'bg-white/15 text-white' : 'bg-[#f1edf4] text-[#6f6877]'"
                  >{{ item.count }}</span>
                }
              </button>
            }
          </nav>
        </div>

        <main class="min-w-0 space-y-5">
        @if (activeSection() === 'parents') {
        <section class="rounded-lg border border-[#ebe6ef] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <div class="border-b border-[#ebe6ef] p-6">
            <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('studentProfile.parents.title') }}</h3>
            <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('studentProfile.parents.hint') }}</p>
          </div>
          <div class="grid gap-5 p-5 lg:grid-cols-2">
            @for (parent of profile.parents; track parent.id) {
              <article class="overflow-hidden rounded-lg border border-[#ddd3e4] bg-white shadow-[0_8px_24px_rgba(50,18,79,0.06)]">
                <div class="border-b border-[#e8e0ec] bg-gradient-to-r from-[#f5f0f8] to-[#fffaf0] px-5 py-4">
                  <p class="text-xs font-semibold uppercase tracking-[0.16em] text-[#8a6811]">
                    {{ languageService.t('studentProfile.parents.relationship') }}
                  </p>
                  <div class="mt-1 flex flex-wrap items-baseline gap-x-3 gap-y-1">
                    <h4 class="text-xl font-bold text-[#32124f]">
                      {{ relationshipLabel(parent.relationship) }}
                    </h4>
                    <p class="text-base font-semibold text-[#4e4355]">{{ parent.fullName }}</p>
                  </div>
                </div>
                <div class="space-y-5 p-5">
                  <div class="grid gap-3 sm:grid-cols-2">
                    <div class="rounded-md border border-[#eadcb5] bg-[#fff9e8] p-3.5">
                      <p class="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.12em] text-[#7a5b12]">
                        <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6A19.79 19.79 0 0 1 2.12 4.18 2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72c.13.96.36 1.9.69 2.8a2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45c.9.33 1.84.56 2.8.69A2 2 0 0 1 22 16.92z"/></svg>
                        {{ languageService.t('studentProfile.parents.phone') }}
                      </p>
                      @if (parent.phoneNumber) {
                        <a class="mt-1.5 block break-all text-base font-bold text-[#32124f] underline decoration-[#d7b44a] decoration-2 underline-offset-4" [href]="'tel:' + parent.phoneNumber">{{ parent.phoneNumber }}</a>
                      } @else {
                        <p class="mt-1.5 font-semibold text-[#7c7284]">—</p>
                      }
                    </div>
                    <div class="rounded-md border border-[#d9cce2] bg-[#f7f2fa] p-3.5">
                      <p class="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.12em] text-[#5b287d]">
                        <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z"/><path d="m22 6-10 7L2 6"/></svg>
                        {{ languageService.t('studentProfile.parents.email') }}
                      </p>
                      @if (parent.email) {
                        <a class="mt-1.5 block break-all text-base font-bold text-[#32124f] underline decoration-[#9d79b5] decoration-2 underline-offset-4" [href]="'mailto:' + parent.email">{{ parent.email }}</a>
                      } @else {
                        <p class="mt-1.5 font-semibold text-[#7c7284]">—</p>
                      }
                    </div>
                  </div>

                  <dl class="grid gap-3 text-sm sm:grid-cols-3">
                    <div>
                      <dt class="text-xs uppercase tracking-[0.1em] text-[#7c7284]">{{ languageService.t('studentProfile.parents.birthYear') }}</dt>
                      <dd class="mt-1 font-medium text-[#32124f]">{{ parent.birthYear || '—' }}</dd>
                    </div>
                    <div>
                      <dt class="text-xs uppercase tracking-[0.1em] text-[#7c7284]">{{ languageService.t('studentProfile.parents.occupation') }}</dt>
                      <dd class="mt-1 font-medium text-[#32124f]">{{ parent.occupation || '—' }}</dd>
                    </div>
                    <div>
                      <dt class="text-xs uppercase tracking-[0.1em] text-[#7c7284]">{{ languageService.t('studentProfile.parents.username') }}</dt>
                      <dd class="mt-1 font-medium text-[#32124f]">{{ parent.username || '—' }}</dd>
                    </div>
                  </dl>

                  <div class="rounded-md border border-[#e5dee9] bg-[#faf9fb] p-4">
                    <div class="flex items-start justify-between gap-4">
                      <div class="min-w-0">
                        <p class="text-sm font-semibold text-[#32124f]">{{ languageService.t('studentProfile.parents.notes') }}</p>
                        @if (parent.notes) {
                          <p class="mt-1.5 line-clamp-3 whitespace-pre-line text-sm leading-6 text-[#574d5d]">{{ parent.notes }}</p>
                          @if (parent.notesUpdatedAt) {
                            <p class="mt-2 text-xs text-[#7c7284]">
                              {{ languageService.t('studentProfile.parents.notesUpdated') }} {{ formatDateTime(parent.notesUpdatedAt) }}
                              @if (parent.notesUpdatedByName) { · {{ parent.notesUpdatedByName }} }
                            </p>
                          }
                        } @else {
                          <p class="mt-1 text-sm text-[#7c7284]">{{ languageService.t('studentProfile.parents.notesEmpty') }}</p>
                        }
                      </div>
                      <button
                        type="button"
                        class="shrink-0 rounded-md border border-[#cfc3d6] bg-white px-3.5 py-2 text-sm font-semibold text-[#32124f] transition hover:border-[#6e3b8b] hover:bg-[#f7f2fa]"
                        (click)="openParentNoteModal(parent)"
                      >
                        {{ languageService.t(parent.notes ? 'studentProfile.parents.notesEdit' : 'studentProfile.parents.notesAdd') }}
                      </button>
                    </div>
                  </div>
                </div>
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
        @if (authService.canAccess('academic')) {
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

        <article class="overflow-hidden rounded-lg border border-[#ded3e6] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <div class="flex flex-wrap items-center justify-between gap-4 border-b border-[#ebe6ef] bg-gradient-to-r from-[#f7f2fa] to-[#fffaf0] px-5 py-4">
            <div class="flex items-center gap-3">
              <span class="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-[#32124f] text-[#f3c246]">
                <svg class="h-5 w-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                  <path d="M4 19V9M10 19V5M16 19v-7M22 19H2" />
                </svg>
              </span>
              <div>
                <h3 class="font-semibold text-[#32124f]">{{ languageService.t('studentProfile.canvas.title') }}</h3>
                <p class="text-xs text-[#6f6877]">{{ languageService.t('studentProfile.canvas.hint') }}</p>
              </div>
            </div>
            <div class="flex flex-wrap items-center gap-2">
              @if (profile.canvas.academicYear) {
                <span class="rounded-full border border-[#d9cce2] bg-white px-3 py-1 text-xs font-semibold text-[#5b287d]">
                  {{ profile.canvas.academicYear }}
                </span>
              }
              @if (profile.canvas.term) {
                <span class="rounded-full border border-[#eadcb5] bg-white px-3 py-1 text-xs font-semibold text-[#7a5b12]">
                  {{ canvasTermLabel(profile.canvas.term) }}
                </span>
              }
              <button
                type="button"
                class="inline-flex items-center gap-1.5 rounded-md border border-[#6d3a91] bg-white px-3.5 py-2 text-sm font-semibold text-[#4a1b73] transition hover:bg-[#f7f1fb] focus:outline-none focus:ring-4 focus:ring-[#6d3a91]/10"
                (click)="selectSection('learning')"
              >
                {{ languageService.t('studentProfile.canvas.viewDetails') }}
                <span aria-hidden="true">→</span>
              </button>
            </div>
          </div>

          @if (profile.canvas.hasData) {
            <div class="grid gap-0 sm:grid-cols-2 lg:grid-cols-4">
              <div class="border-b border-[#ebe6ef] p-5 sm:border-r lg:border-b-0">
                <p class="text-xs uppercase tracking-[0.12em] text-[#7c7284]">{{ languageService.t('studentProfile.canvas.avgCourseGrade') }}</p>
                @if (profile.canvas.averageCourseGrade != null) {
                  <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ profile.canvas.averageCourseGrade }}%</p>
                } @else {
                  <p class="mt-2 text-base font-semibold text-[#7c7284]">{{ languageService.t('studentProfile.canvas.noGradeData') }}</p>
                }
              </div>
              <div class="border-b border-[#ebe6ef] p-5 lg:border-b-0 lg:border-r">
                <p class="text-xs uppercase tracking-[0.12em] text-[#7c7284]">{{ languageService.t('studentProfile.canvas.avgAssignmentScore') }}</p>
                @if (profile.canvas.averageAssignmentScore != null) {
                  <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ profile.canvas.averageAssignmentScore }}%</p>
                } @else {
                  <p class="mt-2 text-base font-semibold text-[#7c7284]">{{ languageService.t('studentProfile.canvas.noGradeData') }}</p>
                }
              </div>
              <div class="border-b border-[#ebe6ef] p-5 sm:border-r sm:border-b-0">
                <p class="text-xs uppercase tracking-[0.12em] text-[#7c7284]">{{ languageService.t('studentProfile.canvas.coursesEnrolled') }}</p>
                <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ profile.canvas.coursesEnrolled }}</p>
              </div>
              <div class="p-5">
                <p class="text-xs uppercase tracking-[0.12em] text-[#7c7284]">{{ languageService.t('studentProfile.canvas.assignmentsGraded') }}</p>
                <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ profile.canvas.assignmentsGraded }}</p>
              </div>
            </div>
            @if (profile.canvas.journey.length > 1) {
              <div class="border-t border-[#ebe6ef] p-6">
                <div class="flex flex-wrap items-baseline justify-between gap-2">
                  <h4 class="text-sm font-semibold uppercase tracking-[0.12em] text-[#32124f]">
                    {{ languageService.t('studentProfile.canvas.journeyTitle') }}
                  </h4>
                  <p class="text-xs text-[#7c7284]">{{ languageService.t('studentProfile.canvas.journeyHint') }}</p>
                </div>
                <div echarts class="mt-4 h-64 w-full" [options]="canvasJourneyOptions()" role="img"></div>
              </div>
            }
          } @else {
            <p class="p-6 text-sm text-[#7c7284]">{{ languageService.t('studentProfile.canvas.empty') }}</p>
          }
        </article>

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
        @if (authService.canAccess('health')) {
          <div class="grid gap-4 sm:grid-cols-2">
            <article class="rounded-lg border border-[#ebe6ef] bg-white p-5 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
              <p class="text-sm text-[#7c7284]">{{ languageService.t('dashboard.cards.healthVisits') }}</p>
              <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ profile.healthVisits.length }}</p>
            </article>
            <article class="rounded-lg border border-[#ebe6ef] bg-white p-5 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
              <p class="text-sm text-[#7c7284]">{{ languageService.t('dashboard.cards.mentalHealth') }}</p>
              <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ profile.mentalHealthRecords.length }}</p>
            </article>
          </div>
        }
        @if (authService.canAccess('bus')) {
          <article class="rounded-lg border border-[#ebe6ef] bg-white p-5 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
            <p class="text-sm text-[#7c7284]">{{ languageService.t('dashboard.cards.busRecords') }}</p>
            <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ profile.busAttendanceRecords.length }}</p>
          </article>
        }
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

        <section class="rounded-lg border border-[#ebe6ef] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
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
            <table class="w-full min-w-[1100px] text-left">
              <thead class="sticky top-0 z-10 bg-[#faf9f6] text-xs uppercase tracking-[0.1em] text-[#7c7284]">
                <tr>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.healthVisits.examTime') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.healthVisits.symptoms') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.healthVisits.diagnosis') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.healthVisits.classification') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.healthVisits.treatment') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.healthVisits.medication') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.healthVisits.notes') }}</th>
                  <th class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.healthVisits.notificationSent') }}</th>
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
                    <td class="px-4 py-3 text-[#6f6877]">{{ visit.notes || '—' }}</td>
                    <td class="px-4 py-3 text-[#6f6877]">{{ languageService.t('moduleRecords.boolean.' + visit.notificationSent) }}</td>
                    <td class="whitespace-nowrap px-4 py-3 text-[#6f6877]">{{ visit.examinedBy || '—' }}</td>
                  </tr>
                } @empty {
                  <tr>
                    <td class="px-4 py-8 text-center text-sm text-[#7c7284]" colspan="9">{{ languageService.t('studentProfile.healthVisits.empty') }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
        </section>
        }

        @if (activeSection() === 'learning') {
        <section class="rounded-lg border border-[#ebe6ef] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <div class="flex flex-wrap items-center justify-between gap-4 border-b border-[#ebe6ef] p-6">
            <div>
              <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('studentProfile.canvas.title') }}</h3>
              <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('studentProfile.canvas.hint') }}</p>
            </div>
            <a
              class="inline-flex items-center gap-2 rounded-md border border-[#6d3a91] bg-[#f7f1fb] px-4 py-2.5 text-sm font-semibold text-[#4a1b73] transition hover:bg-[#efe2f7] focus:outline-none focus:ring-4 focus:ring-[#6d3a91]/10"
              [routerLink]="['/transcripts/student', profile.id]"
            >
              <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" />
                <path d="M14 2v6h6M8 13h8M8 17h5" />
              </svg>
              {{ languageService.t('studentProfile.canvas.viewFullTranscript') }}
              <span aria-hidden="true">→</span>
            </a>
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
            <div class="flex flex-wrap items-center gap-3 px-6 pt-5">
              <label class="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.12em] text-[#8a6811]">
                <span>{{ languageService.t('studentProfile.canvas.currentYear') }}</span>
                <select
                  class="rounded-md border border-[#d8d0df] bg-white px-3 py-2 text-sm font-semibold normal-case tracking-normal text-[#32124f] outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10 disabled:cursor-wait disabled:opacity-60"
                  [attr.aria-label]="languageService.t('studentProfile.canvas.yearSelectLabel')"
                  [disabled]="canvasYearLoading()"
                  [ngModel]="selectedCanvasYear()"
                  (ngModelChange)="onCanvasYearChange($event)"
                >
                  @for (year of canvasYearOptions(); track year) {
                    <option [value]="year">{{ year }}</option>
                  }
                </select>
              </label>
              @if (canvasTermOptions().length) {
                <label class="flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.12em] text-[#8a6811]">
                  <span>{{ languageService.t('studentProfile.canvas.currentSemester') }}</span>
                  <select
                    class="rounded-md border border-[#d8d0df] bg-white px-3 py-2 text-sm font-semibold normal-case tracking-normal text-[#32124f] outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10 disabled:cursor-wait disabled:opacity-60"
                    [attr.aria-label]="languageService.t('studentProfile.canvas.semesterSelectLabel')"
                    [disabled]="canvasYearLoading()"
                    [ngModel]="selectedCanvasTerm()"
                    (ngModelChange)="onCanvasTermChange($event)"
                  >
                    @for (term of canvasTermOptions(); track term) {
                      <option [value]="term">{{ canvasTermLabel(term) }}</option>
                    }
                  </select>
                </label>
              }
              @if (canvasYearLoading()) {
                <span class="text-xs text-[#7c7284]">{{ languageService.t('studentProfile.canvas.loadingPeriod') }}</span>
              }
            </div>
            <div class="grid gap-4 px-6 pb-6 pt-3 sm:grid-cols-2 xl:grid-cols-4">
              <div class="rounded-md border border-[#ebe6ef] bg-[#faf9f6] p-4">
                <p class="text-xs uppercase tracking-[0.12em] text-[#7c7284]">{{ languageService.t('studentProfile.canvas.avgCourseGrade') }}</p>
                @if (profile.canvas.averageCourseGrade != null) {
                  <p class="mt-2 text-2xl font-semibold text-[#32124f]">{{ profile.canvas.averageCourseGrade }}%</p>
                } @else {
                  <p class="mt-2 text-base font-semibold text-[#7c7284]">{{ languageService.t('studentProfile.canvas.noGradeData') }}</p>
                }
              </div>
              <div class="rounded-md border border-[#ebe6ef] bg-[#faf9f6] p-4">
                <p class="text-xs uppercase tracking-[0.12em] text-[#7c7284]">{{ languageService.t('studentProfile.canvas.avgAssignmentScore') }}</p>
                <p class="mt-2 text-2xl font-semibold text-[#32124f]">{{ profile.canvas.averageAssignmentScore ?? '—' }}{{ profile.canvas.averageAssignmentScore !== null ? '%' : '' }}</p>
              </div>
              <button
                class="group rounded-md border border-[#ded6e4] bg-[#faf9f6] p-4 text-left transition hover:border-[#bca9c8] hover:bg-white hover:shadow-[0_6px_18px_rgba(50,18,79,0.08)] focus:outline-none focus:ring-4 focus:ring-[#6d3a91]/10"
                type="button"
                (click)="openCanvasSubmissionSheet('assignmentActivity')"
              >
                <span class="flex items-center justify-between gap-3">
                  <span class="text-xs uppercase tracking-[0.12em] text-[#7c7284]">{{ languageService.t('studentProfile.canvas.assignmentActivity') }}</span>
                  <span class="text-[#6d3a91] transition-transform group-hover:translate-x-0.5" aria-hidden="true">→</span>
                </span>
                <span class="mt-3 grid grid-cols-2 gap-3">
                  <span>
                    <span class="block text-2xl font-semibold text-[#32124f]">{{ profile.canvas.assignmentsGraded }}</span>
                    <span class="mt-0.5 block text-xs text-[#7c7284]">{{ languageService.t('studentProfile.canvas.gradedCountLabel') }}</span>
                  </span>
                  <span class="border-l border-[#ded6e4] pl-3">
                    <span class="block text-2xl font-semibold text-[#32124f]">{{ profile.canvas.assignmentsSubmitted }}</span>
                    <span class="mt-0.5 block text-xs text-[#7c7284]">{{ languageService.t('studentProfile.canvas.submittedCountLabel') }}</span>
                  </span>
                </span>
                <span class="mt-3 block text-xs font-semibold text-[#6d3a91]">{{ languageService.t('studentProfile.canvas.viewDetails') }}</span>
              </button>
              <button
                class="group rounded-md border border-[#ded6e4] bg-[#faf9f6] p-4 text-left transition hover:border-[#bca9c8] hover:bg-white hover:shadow-[0_6px_18px_rgba(50,18,79,0.08)] focus:outline-none focus:ring-4 focus:ring-[#6d3a91]/10"
                type="button"
                (click)="openCanvasSubmissionSheet('needsAttention')"
              >
                <span class="flex items-center justify-between gap-3">
                  <span class="text-xs uppercase tracking-[0.12em] text-[#7c7284]">{{ languageService.t('studentProfile.canvas.attention') }}</span>
                  <span class="text-[#6d3a91] transition-transform group-hover:translate-x-0.5" aria-hidden="true">→</span>
                </span>
                <span class="mt-3 grid grid-cols-2 gap-3">
                  <span>
                    <span class="block text-2xl font-semibold" [ngClass]="profile.canvas.missingAssignments ? 'text-[#a9485d]' : 'text-[#32124f]'">{{ profile.canvas.missingAssignments }}</span>
                    <span class="mt-0.5 block text-xs text-[#7c7284]">{{ languageService.t('studentProfile.canvas.missing') }}</span>
                  </span>
                  <span class="border-l border-[#ded6e4] pl-3">
                    <span class="block text-2xl font-semibold" [ngClass]="profile.canvas.lateSubmissions ? 'text-[#8a6811]' : 'text-[#32124f]'">{{ profile.canvas.lateSubmissions }}</span>
                    <span class="mt-0.5 block text-xs text-[#7c7284]">{{ languageService.t('studentProfile.canvas.late') }}</span>
                  </span>
                </span>
                <span class="mt-3 flex items-center justify-between gap-3 text-xs">
                  <span class="text-[#7c7284]">{{ profile.canvas.coursesEnrolled }} {{ languageService.t('studentProfile.canvas.coursesEnrolled') }}</span>
                  <span class="font-semibold text-[#6d3a91]">{{ languageService.t('studentProfile.canvas.viewDetails') }}</span>
                </span>
              </button>
            </div>

            <div class="grid items-stretch gap-5 border-t border-[#ebe6ef] p-6 xl:grid-cols-2">
              <div class="flex min-w-0 flex-col">
                <div class="flex min-h-9 flex-wrap items-center justify-between gap-3">
                  <h4 class="text-sm font-semibold uppercase tracking-[0.12em] text-[#7c7284]">{{ languageService.t('studentProfile.canvas.courseGrades') }}</h4>
                  @if (profile.canvas.courseGradesTotalPages > 1) {
                    <div class="flex flex-wrap items-center gap-3 text-xs text-[#7c7284]">
                      <span>{{ paginationSummary(profile.canvas.courseGradesPage, profile.canvas.courseGradesTotalPages, profile.canvas.courseGradesTotalCount) }}</span>
                      <div class="flex gap-2">
                        <button
                          type="button"
                          class="rounded-md border border-[#d8d0df] px-3 py-2 font-semibold text-[#32124f] transition hover:bg-[#f7f3f9] disabled:cursor-not-allowed disabled:opacity-45"
                          [disabled]="courseGradesLoading() || profile.canvas.courseGradesPage <= 1"
                          (click)="loadCourseGradesPage(profile.canvas.courseGradesPage - 1)"
                        >{{ languageService.t('pagination.previous') }}</button>
                        <button
                          type="button"
                          class="rounded-md border border-[#d8d0df] px-3 py-2 font-semibold text-[#32124f] transition hover:bg-[#f7f3f9] disabled:cursor-not-allowed disabled:opacity-45"
                          [disabled]="courseGradesLoading() || profile.canvas.courseGradesPage >= profile.canvas.courseGradesTotalPages"
                          (click)="loadCourseGradesPage(profile.canvas.courseGradesPage + 1)"
                        >{{ languageService.t('pagination.next') }}</button>
                      </div>
                    </div>
                  }
                </div>
                <div class="mt-3 flex-1 overflow-hidden rounded-md border border-[#ebe6ef]">
                  <div class="hidden grid-cols-[minmax(0,1.35fr)_minmax(0,0.9fr)_minmax(0,0.9fr)] bg-[#faf9f6] text-xs uppercase tracking-[0.12em] text-[#7c7284] md:grid">
                    <span class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.canvas.course') }}</span>
                    <span class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.canvas.enrollmentTerm') }}</span>
                    <span class="px-4 py-3 font-semibold">{{ languageService.t('studentProfile.canvas.gradingPeriod') }}</span>
                  </div>
                  <div class="space-y-2 p-2">
                    @for (course of profile.canvas.courses; track $index) {
                      <article class="rounded-md border border-[#ebe6ef] bg-white p-3 text-sm shadow-[0_1px_2px_rgba(50,18,79,0.04)] xl:flex xl:h-[8.5rem] xl:flex-col xl:justify-between">
                        <div class="grid gap-4 md:grid-cols-[minmax(0,1.35fr)_minmax(0,0.9fr)_minmax(0,0.9fr)]">
                          <div class="min-w-0 text-[#32124f]">
                            <span class="mb-1 block text-[0.68rem] font-semibold uppercase tracking-[0.12em] text-[#7c7284] md:hidden">{{ languageService.t('studentProfile.canvas.course') }}</span>
                            <span class="block font-semibold">{{ course.courseName }}</span>
                            <span class="mt-0.5 block text-xs font-normal text-[#8a808f]">{{ course.courseCode || '—' }} · {{ languageService.t('studentProfile.canvas.canvasId') }} {{ course.canvasCourseId }}</span>
                          </div>
                          <div class="min-w-0 text-[#6f6877]">
                            <span class="mb-1 block text-[0.68rem] font-semibold uppercase tracking-[0.12em] text-[#7c7284] md:hidden">{{ languageService.t('studentProfile.canvas.enrollmentTerm') }}</span>
                            <span class="block">{{ course.enrollmentTermName || '—' }}</span>
                            @if (course.enrollmentTermId) { <span class="mt-0.5 block text-xs text-[#8a808f]">{{ languageService.t('studentProfile.canvas.canvasId') }} {{ course.enrollmentTermId }}</span> }
                          </div>
                          <div class="min-w-0 text-[#6f6877]">
                            <span class="mb-1 block text-[0.68rem] font-semibold uppercase tracking-[0.12em] text-[#7c7284] md:hidden">{{ languageService.t('studentProfile.canvas.gradingPeriod') }}</span>
                            <span class="block">{{ course.gradingPeriodTitle || '—' }}</span>
                            @if (course.gradingPeriodId) { <span class="mt-0.5 block text-xs text-[#8a808f]">{{ languageService.t('studentProfile.canvas.canvasId') }} {{ course.gradingPeriodId }}</span> }
                          </div>
                        </div>
                        <div class="mt-3 flex flex-wrap items-center justify-end gap-x-8 gap-y-2 border-t border-[#ebe6ef] pt-3">
                          <div class="flex items-baseline gap-2">
                            <span class="text-xs font-semibold uppercase tracking-[0.1em] text-[#7c7284]">{{ languageService.t('studentProfile.canvas.currentScore') }}</span>
                            <span class="font-semibold text-[#32124f]">{{ course.currentScore != null ? course.currentScore + '%' : languageService.t('studentProfile.canvas.noGradeData') }}</span>
                          </div>
                          <div class="flex items-baseline gap-2">
                            <span class="text-xs font-semibold uppercase tracking-[0.1em] text-[#7c7284]">{{ languageService.t('studentProfile.canvas.finalScore') }}</span>
                            <span class="font-semibold text-[#32124f]">{{ course.finalScore != null ? course.finalScore + '%' : languageService.t('studentProfile.canvas.noGradeData') }}</span>
                          </div>
                        </div>
                      </article>
                    } @empty {
                      <p class="px-4 py-8 text-center text-sm text-[#7c7284]">{{ languageService.t('studentProfile.canvas.noCourses') }}</p>
                    }
                  </div>
                </div>
              </div>

              <div class="flex min-w-0 flex-col">
                <div class="flex min-h-9 flex-wrap items-center justify-between gap-3">
                  <h4 class="text-sm font-semibold uppercase tracking-[0.12em] text-[#7c7284]">{{ languageService.t('studentProfile.canvas.recentSubmissions') }}</h4>
                  @if (profile.canvas.recentSubmissionsTotalPages > 1) {
                    <div class="flex flex-wrap items-center gap-3 text-xs text-[#7c7284]">
                      <span>{{ paginationSummary(profile.canvas.recentSubmissionsPage, profile.canvas.recentSubmissionsTotalPages, profile.canvas.recentSubmissionsTotalCount) }}</span>
                      <div class="flex gap-2">
                        <button
                          type="button"
                          class="rounded-md border border-[#d8d0df] px-3 py-2 font-semibold text-[#32124f] transition hover:bg-[#f7f3f9] disabled:cursor-not-allowed disabled:opacity-45"
                          [disabled]="recentSubmissionsLoading() || profile.canvas.recentSubmissionsPage <= 1"
                          (click)="loadRecentSubmissionsPage(profile.canvas.recentSubmissionsPage - 1)"
                        >{{ languageService.t('pagination.previous') }}</button>
                        <button
                          type="button"
                          class="rounded-md border border-[#d8d0df] px-3 py-2 font-semibold text-[#32124f] transition hover:bg-[#f7f3f9] disabled:cursor-not-allowed disabled:opacity-45"
                          [disabled]="recentSubmissionsLoading() || profile.canvas.recentSubmissionsPage >= profile.canvas.recentSubmissionsTotalPages"
                          (click)="loadRecentSubmissionsPage(profile.canvas.recentSubmissionsPage + 1)"
                        >{{ languageService.t('pagination.next') }}</button>
                      </div>
                    </div>
                  }
                </div>
                <div class="mt-3 flex-1 space-y-2 rounded-md border border-[#ebe6ef] p-2">
                  @for (submission of profile.canvas.recentSubmissions; track $index) {
                    <div class="rounded-md border border-[#ebe6ef] p-3 xl:flex xl:h-[8.5rem] xl:flex-col xl:justify-center">
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

        @if (parentNoteModalParent(); as noteParent) {
          <div class="fixed inset-0 z-[70] grid place-items-center p-4" (keydown.escape)="closeParentNoteModal()">
            <button
              type="button"
              class="absolute inset-0 bg-[#24182f]/50 backdrop-blur-[1px]"
              [attr.aria-label]="languageService.t('studentProfile.parents.notesClose')"
              (click)="closeParentNoteModal()"
            ></button>
            <section
              class="relative flex max-h-[90vh] w-full max-w-2xl flex-col overflow-hidden rounded-xl bg-white shadow-[0_28px_90px_rgba(36,24,47,0.3)]"
              role="dialog"
              aria-modal="true"
              aria-labelledby="parent-note-modal-title"
            >
              <div class="flex items-start justify-between gap-4 border-b border-[#ebe6ef] px-6 py-5">
                <div>
                  <p class="text-xs font-semibold uppercase tracking-[0.16em] text-[#8a6811]">
                    {{ relationshipLabel(noteParent.relationship) }} · {{ noteParent.fullName }}
                  </p>
                  <h3 id="parent-note-modal-title" class="mt-1 text-2xl font-semibold text-[#32124f]">
                    {{ languageService.t('studentProfile.parents.notesModalTitle') }}
                  </h3>
                  <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('studentProfile.parents.notesHint') }}</p>
                </div>
                <button
                  type="button"
                  class="grid h-9 w-9 shrink-0 place-items-center rounded-full border border-[#d8d0df] text-xl text-[#32124f] transition hover:bg-[#f7f2fa]"
                  [attr.aria-label]="languageService.t('studentProfile.parents.notesClose')"
                  (click)="closeParentNoteModal()"
                >×</button>
              </div>

              <form class="min-h-0 overflow-y-auto" (ngSubmit)="saveParentNote(noteParent)">
                <div class="p-6">
                  <div class="rounded-md border border-[#eadcb5] bg-[#fff9e8] px-4 py-3 text-sm text-[#6d5317]">
                    {{ languageService.t('studentProfile.parents.notesSource') }}
                  </div>
                  <label class="mt-5 block" for="parent-note-modal-field">
                    <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('studentProfile.parents.notes') }}</span>
                  </label>
                  <textarea
                    id="parent-note-modal-field"
                    name="parentNote"
                    class="mt-2 min-h-44 w-full resize-y rounded-md border border-[#cfc3d6] bg-white px-3.5 py-3 text-sm text-[#34243d] outline-none transition placeholder:text-[#9b929f] focus:border-[#6e3b8b] focus:ring-2 focus:ring-[#6e3b8b]/15"
                    maxlength="2000"
                    [placeholder]="languageService.t('studentProfile.parents.notesPlaceholder')"
                    [ngModel]="parentNoteDraft(noteParent)"
                    (ngModelChange)="updateParentNoteDraft(noteParent.id, $event)"
                  ></textarea>
                  <div class="mt-2 flex items-center justify-between gap-3 text-xs">
                    <div>
                      @if (parentNoteState(noteParent.id) === 'error') {
                        <span class="font-semibold text-red-700">{{ languageService.t('studentProfile.parents.notesSaveError') }}</span>
                      } @else if (noteParent.notesUpdatedAt) {
                        <span class="text-[#7c7284]">
                          {{ languageService.t('studentProfile.parents.notesUpdated') }} {{ formatDateTime(noteParent.notesUpdatedAt) }}
                          @if (noteParent.notesUpdatedByName) { · {{ noteParent.notesUpdatedByName }} }
                        </span>
                      }
                    </div>
                    <span class="text-[#7c7284]">{{ parentNoteDraft(noteParent).length }}/2000</span>
                  </div>
                </div>
                <div class="flex justify-end gap-3 border-t border-[#ebe6ef] bg-[#faf9f6] px-6 py-4">
                  <button
                    type="button"
                    class="rounded-md border border-[#d8d0df] px-5 py-2.5 text-sm font-semibold text-[#32124f] transition hover:bg-white"
                    (click)="closeParentNoteModal()"
                  >{{ languageService.t('studentProfile.parents.notesCancel') }}</button>
                  <button
                    type="submit"
                    class="rounded-md bg-[#32124f] px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-[#4b2070] disabled:cursor-not-allowed disabled:opacity-45"
                    [disabled]="!parentNoteChanged(noteParent) || parentNoteState(noteParent.id) === 'saving'"
                  >
                    {{ parentNoteState(noteParent.id) === 'saving' ? languageService.t('studentProfile.parents.notesSaving') : languageService.t('studentProfile.parents.notesSave') }}
                  </button>
                </div>
              </form>
            </section>
          </div>
        }

        @if (canvasSheetKind(); as sheetKind) {
          <app-student-canvas-submission-sheet
            [studentId]="profile.id"
            [studentName]="profile.fullName"
            [academicYear]="profile.canvas.academicYear"
            [term]="profile.canvas.term"
            [kind]="sheetKind"
            [gradedCount]="profile.canvas.assignmentsGraded"
            [submittedCount]="profile.canvas.assignmentsSubmitted"
            [missingCount]="profile.canvas.missingAssignments"
            [lateCount]="profile.canvas.lateSubmissions"
            (close)="closeCanvasSubmissionSheet()"
          />
        }
      }
    </section>
  `,
})
export class StudentProfileComponent implements OnInit {
  private readonly studentsService = inject(StudentsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly languageService = inject(LanguageService);
  readonly authService = inject(AuthenticationService);

  readonly student = signal<StudentProfileDetail | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly courseGradesLoading = signal(false);
  readonly recentSubmissionsLoading = signal(false);
  readonly canvasYearLoading = signal(false);
  readonly canvasSheetKind = signal<CanvasSubmissionSheetKind | null>(null);
  readonly activeSection = signal<ProfileSection>('overview');
  private readonly parentNoteModalParentId = signal<number | null>(null);
  private readonly parentNoteDrafts = signal<Record<number, string>>({});
  private readonly parentNoteStates = signal<Record<number, 'idle' | 'saving' | 'saved' | 'error'>>({});
  readonly parentNoteModalParent = computed(() => {
    const parentId = this.parentNoteModalParentId();
    return parentId === null
      ? null
      : this.student()?.parents.find((parent) => parent.id === parentId) ?? null;
  });

  readonly profileSections = computed<{ id: ProfileSection; label: string; count: number | null }[]>(
    () => {
      const profile = this.student();
      const sections: { id: ProfileSection; label: string; count: number | null }[] = [
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
      return sections.filter((item) => {
        if (item.id === 'transport') return this.authService.canAccess('bus');
        if (item.id === 'wellbeing') return this.authService.canAccess('health');
        if (['parents', 'behaviours', 'projects', 'learning'].includes(item.id)) {
          return this.authService.canAccess('academic');
        }
        return true;
      });
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
    const labels = journey.map((point) => this.canvasJourneyLabel(point));
    return {
      color: ['#32124f'],
      grid: { left: 44, right: 16, top: 28, bottom: 56, containLabel: true },
      tooltip: {
        trigger: 'axis',
        formatter: (params: { dataIndex: number; value: number | null }[]) => {
          const p = params[0];
          const point = journey[p.dataIndex];
          const grade = point?.averageGrade ?? p.value;
          return `${point ? this.canvasJourneyLabel(point) : ''}<br/><b>${grade === null || grade === undefined ? '—' : grade + '%'}</b>`;
        },
      },
      xAxis: {
        type: 'category',
        data: labels,
        axisLabel: { color: '#6f6877', interval: 0, rotate: journey.length > 4 ? 28 : 0 },
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

  private canvasJourneyLabel(point: { academicYear: string; term?: string | null }): string {
    const normalizedTerm = point.term?.toLowerCase();
    const termLabel = normalizedTerm === 'hk1'
      ? this.languageService.t('studentProfile.canvas.journeyTerm.hk1')
      : normalizedTerm === 'hk2'
        ? this.languageService.t('studentProfile.canvas.journeyTerm.hk2')
        : point.term?.toUpperCase() ?? '';
    return `${point.academicYear.replace('-', '–')} · ${termLabel}`;
  }

  private studentId = 0;
  readonly selectedYear = signal<string>('');
  readonly selectedCanvasYear = signal<string>('');
  readonly selectedCanvasTerm = signal<string>('');
  private readonly knownYears = signal<string[]>([]);

  /**
   * Prefer the API's explicit Canvas-year list, but remain compatible with a backend that has
   * not restarted onto the latest contract by deriving years from the journey/current year.
   */
  readonly canvasYearOptions = computed<string[]>(() => {
    const canvas = this.student()?.canvas;
    if (!canvas) return [];

    const years = new Set(canvas.academicYears ?? []);
    for (const point of canvas.journey ?? []) years.add(point.academicYear);
    if (canvas.academicYear) years.add(canvas.academicYear);
    return Array.from(years).sort((a, b) => b.localeCompare(a));
  });

  readonly canvasTermOptions = computed<string[]>(() => {
    const canvas = this.student()?.canvas;
    const selectedYear = canvas?.academicYear ?? this.selectedCanvasYear();
    const availableTerms = new Set<string>((canvas?.availableTerms ?? [])
      .map((term) => term.toLowerCase())
      .filter((term) => term === 'hk1' || term === 'hk2'));
    for (const term of (canvas?.journey ?? [])
      .filter((point) => point.academicYear === selectedYear && point.averageGrade != null)
      .map((point) => point.term?.toLowerCase())
      .filter((term): term is string => term === 'hk1' || term === 'hk2')) {
      availableTerms.add(term);
    }
    const explicitTerm = canvas?.term?.toLowerCase();
    if (explicitTerm === 'hk1' || explicitTerm === 'hk2') availableTerms.add(explicitTerm);
    return Array.from(availableTerms).sort((a, b) => a === b ? 0 : a === 'hk1' ? -1 : 1);
  });

  /** Distinct academic years represented anywhere on the student profile, labelled by grade when known. */
  readonly yearOptions = computed<{ value: string; label: string }[]>(() => {
    const profile = this.student();
    const journey = profile?.canvas.journey ?? [];
    const seen = new Map<string, string | null | undefined>(this.knownYears().map((year) => [year, null]));
    for (const year of profile?.canvas.academicYears ?? []) {
      if (!seen.has(year)) seen.set(year, null);
    }
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

  onCanvasYearChange(year: string): void {
    const profile = this.student();
    if (!profile || !year || year === profile.canvas.academicYear || this.canvasYearLoading()) return;

    const previousYear = profile.canvas.academicYear ?? '';
    const previousTerm = profile.canvas.term ?? this.selectedCanvasTerm();
    this.selectedCanvasYear.set(year);
    this.canvasYearLoading.set(true);
    this.closeCanvasSubmissionSheet();
    this.studentsService.canvasSummary(this.studentId, year).subscribe({
      next: (canvas) => {
        this.student.update((current) => current ? { ...current, canvas } : current);
        this.selectedCanvasYear.set(canvas.academicYear ?? year);
        this.selectedCanvasTerm.set(canvas.term ?? '');
        this.rememberCanvasAcademicYears(canvas.academicYears ?? this.canvasYearOptions());
        this.canvasYearLoading.set(false);
      },
      error: () => {
        this.selectedCanvasYear.set(previousYear);
        this.selectedCanvasTerm.set(previousTerm);
        this.canvasYearLoading.set(false);
      },
    });
  }

  onCanvasTermChange(term: string): void {
    const profile = this.student();
    const year = profile?.canvas.academicYear ?? this.selectedCanvasYear();
    if (!profile || !year || !term || term === profile.canvas.term || this.canvasYearLoading()) return;

    const previousTerm = profile.canvas.term ?? this.selectedCanvasTerm();
    this.selectedCanvasTerm.set(term);
    this.canvasYearLoading.set(true);
    this.closeCanvasSubmissionSheet();
    this.studentsService.canvasSummary(this.studentId, year, term).subscribe({
      next: (canvas) => {
        this.student.update((current) => current ? { ...current, canvas } : current);
        this.selectedCanvasYear.set(canvas.academicYear ?? year);
        this.selectedCanvasTerm.set(canvas.term ?? term);
        this.rememberCanvasAcademicYears(canvas.academicYears ?? this.canvasYearOptions());
        this.canvasYearLoading.set(false);
      },
      error: () => {
        this.selectedCanvasTerm.set(previousTerm);
        this.canvasYearLoading.set(false);
      },
    });
  }

  loadCourseGradesPage(page: number): void {
    const profile = this.student();
    if (!profile || this.courseGradesLoading() || page < 1 || page > profile.canvas.courseGradesTotalPages) return;
    this.courseGradesLoading.set(true);
    this.studentsService.canvasCourseGrades(
      this.studentId,
      page,
      profile.canvas.courseGradesPageSize,
      profile.canvas.academicYear || undefined,
      profile.canvas.term,
    ).subscribe({
      next: (result) => {
        this.student.update((current) => current ? {
          ...current,
          canvas: {
            ...current.canvas,
            courses: result.items,
            courseGradesPage: result.page,
            courseGradesPageSize: result.pageSize,
            courseGradesTotalCount: result.totalCount,
            courseGradesTotalPages: result.totalPages,
          },
        } : current);
        this.courseGradesLoading.set(false);
      },
      error: () => this.courseGradesLoading.set(false),
    });
  }

  loadRecentSubmissionsPage(page: number): void {
    const profile = this.student();
    if (!profile || this.recentSubmissionsLoading() || page < 1 || page > profile.canvas.recentSubmissionsTotalPages) return;
    this.recentSubmissionsLoading.set(true);
    this.studentsService.canvasRecentSubmissions(
      this.studentId,
      page,
      profile.canvas.recentSubmissionsPageSize,
      profile.canvas.academicYear || undefined,
      profile.canvas.term,
    ).subscribe({
      next: (result) => {
        this.student.update((current) => current ? {
          ...current,
          canvas: {
            ...current.canvas,
            recentSubmissions: result.items,
            recentSubmissionsPage: result.page,
            recentSubmissionsPageSize: result.pageSize,
            recentSubmissionsTotalCount: result.totalCount,
            recentSubmissionsTotalPages: result.totalPages,
          },
        } : current);
        this.recentSubmissionsLoading.set(false);
      },
      error: () => this.recentSubmissionsLoading.set(false),
    });
  }

  openCanvasSubmissionSheet(kind: CanvasSubmissionSheetKind): void {
    this.canvasSheetKind.set(kind);
  }

  closeCanvasSubmissionSheet(): void {
    this.canvasSheetKind.set(null);
  }

  canvasTermLabel(term: string): string {
    return this.languageService.t(`studentProfile.canvas.journeyTerm.${term.toLowerCase()}`);
  }

  paginationSummary(page: number, pages: number, total: number): string {
    return this.languageService.t('pagination.pageOf')
      .replace('{page}', String(page))
      .replace('{pages}', String(pages))
      .replace('{total}', String(total));
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
        this.parentNoteDrafts.set(Object.fromEntries(profile.parents.map((parent) => [parent.id, parent.notes ?? ''])));
        this.parentNoteStates.set({});
        this.selectedCanvasYear.set(profile.canvas.academicYear ?? '');
        this.selectedCanvasTerm.set(profile.canvas.term ?? '');
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

  parentNoteDraft(parent: StudentParent): string {
    return this.parentNoteDrafts()[parent.id] ?? parent.notes ?? '';
  }

  openParentNoteModal(parent: StudentParent): void {
    this.parentNoteDrafts.update((drafts) => ({ ...drafts, [parent.id]: parent.notes ?? '' }));
    this.parentNoteStates.update((states) => ({ ...states, [parent.id]: 'idle' }));
    this.parentNoteModalParentId.set(parent.id);
  }

  closeParentNoteModal(): void {
    const parent = this.parentNoteModalParent();
    if (parent) {
      this.parentNoteDrafts.update((drafts) => ({ ...drafts, [parent.id]: parent.notes ?? '' }));
      this.parentNoteStates.update((states) => ({ ...states, [parent.id]: 'idle' }));
    }
    this.parentNoteModalParentId.set(null);
  }

  parentNoteState(parentId: number): 'idle' | 'saving' | 'saved' | 'error' {
    return this.parentNoteStates()[parentId] ?? 'idle';
  }

  parentNoteChanged(parent: StudentParent): boolean {
    return this.parentNoteDraft(parent).trim() !== (parent.notes ?? '').trim();
  }

  updateParentNoteDraft(parentId: number, notes: string): void {
    this.parentNoteDrafts.update((drafts) => ({ ...drafts, [parentId]: notes }));
    this.parentNoteStates.update((states) => ({ ...states, [parentId]: 'idle' }));
  }

  saveParentNote(parent: StudentParent): void {
    if (!this.parentNoteChanged(parent) || this.parentNoteState(parent.id) === 'saving') return;
    this.parentNoteStates.update((states) => ({ ...states, [parent.id]: 'saving' }));
    this.studentsService.saveParentNote(this.studentId, parent.id, this.parentNoteDraft(parent)).subscribe({
      next: (savedParent) => {
        this.student.update((profile) => profile ? {
          ...profile,
          parents: profile.parents.map((item) => item.id === savedParent.id ? savedParent : item),
        } : profile);
        this.parentNoteDrafts.update((drafts) => ({ ...drafts, [parent.id]: savedParent.notes ?? '' }));
        this.parentNoteStates.update((states) => ({ ...states, [parent.id]: 'saved' }));
        this.parentNoteModalParentId.set(null);
      },
      error: () => this.parentNoteStates.update((states) => ({ ...states, [parent.id]: 'error' })),
    });
  }

  private rememberAcademicYears(profile: StudentProfileDetail): void {
    const years = new Set(this.knownYears());
    for (const point of profile.canvas.journey) years.add(point.academicYear);
    for (const year of profile.canvas.academicYears ?? []) years.add(year);
    for (const item of [...profile.behaviours, ...profile.projects, ...profile.busAttendanceRecords]) {
      if (item.academicYear) years.add(item.academicYear);
    }
    for (const item of profile.mentalHealthRecords) years.add(item.academicYear);
    this.knownYears.set(Array.from(years).sort());
  }

  private rememberCanvasAcademicYears(academicYears: string[]): void {
    const years = new Set(this.knownYears());
    for (const year of academicYears) years.add(year);
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

  relationshipLabel(value?: string | null): string {
    if (!value) return this.languageService.t('studentProfile.parents.guardian');
    const normalized = value.trim().toLowerCase();
    if (normalized === 'father' || normalized === 'mother') {
      return this.languageService.t(`studentProfile.parents.relationship.${normalized}`);
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
