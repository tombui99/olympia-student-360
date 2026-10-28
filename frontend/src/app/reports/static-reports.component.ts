import { NgClass, NgTemplateOutlet } from '@angular/common';
import { Component, computed, HostListener, inject, OnDestroy, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, firstValueFrom, forkJoin, Observable, of } from 'rxjs';
import { LanguageService } from '../core/language/language.service';
import { AuthenticationService } from '../core/auth/auth.service';
import { ToastService } from '../core/toast/toast.service';
import {
  DashboardOverview,
  DashboardOverviewService,
} from '../dashboard/dashboard-overview.service';
import {
  AtRiskStudent,
  AtRiskStudentsPage,
  StudentListItem,
  StudentsService,
} from '../students/students.service';
import { MetricVariable, ReportBuilderService } from './report-builder.service';
import {
  defaultFilterSet,
  WorkspaceFiltersService,
  WorkspaceFilterSet,
} from '../workspace/workspace-filters.service';
import {
  SupplementalReportComponent,
  SupplementalReportDefinition,
} from './supplemental-report.component';
import { HealthReportComponent } from './health-report.component';
import { BehaviourReportComponent } from './behaviour-report.component';
import { LearningReviewReportComponent } from './learning-review-report.component';
import { LearningReportKey } from './learning-reports.service';
import { LeadershipReportComponent } from './leadership-report.component';
import { ScoreReportComponent, ScoreReportMenuKey } from './score-report.component';
import { ErpMealReportComponent } from './erp-meal-report.component';
import { ReportExcelExportService, ReportExportLimitError } from './report-excel-export.service';
import { ReportExportButtonComponent } from './report-export-button.component';
import { ActivityPeriodFilterComponent } from './activity-period-filter.component';
import { activityWeeks, activityPeriodExportFilters, ActivityPeriodSelection, normalizeActivityPeriod } from './activity-period';

type StaticReportId =
  | 'learning-diligence'
  | 'school-learning-engagement'
  | 'school-assessment-results'
  | ScoreReportMenuKey
  | 'support-classes'
  | LearningReportKey
  | 'support-students'
  | 'behaviour-analysis'
  | 'mental-wellbeing'
  | 'health-diagnoses'
  | 'health-frequent-students'
  | 'health-student-visits'
  | 'meal-ordered-no-ticket'
  | 'meal-ordered-not-eaten'
  | 'meal-no-order'
  | 'meal-coffee-frequency'
  | 'learning-projects'
  | 'student-activities'
  | 'bus-safety';
type ReportStatus = 'green' | 'amber' | 'red';
type ClassDrilldownKind = 'students' | 'support';
type CohortView = 'grade' | 'class';

interface ReportMenuItem {
  id: StaticReportId;
  titleKey: string;
  descriptionKey: string;
  supplemental?: SupplementalReportDefinition;
}

interface ReportMenuGroup {
  id: string;
  titleKey: string;
  shortLabel: string;
  /** Source feed is awaiting school data; clear when the group is ready for use. */
  awaitingData?: boolean;
  items: ReportMenuItem[];
}

interface LearningReportRow {
  indicatorKey: string;
  value: number | null;
  numerator: number | null;
  denominator: number | null;
  volumeKey: string;
  targetKey: string;
  status: ReportStatus | null;
  inverse?: boolean;
}

interface ClassDrilldownContext {
  label: string;
  grade: string;
  homeroom: string;
  view: CohortView;
  kind: ClassDrilldownKind;
}

interface SupportCohortRow {
  key: string;
  label: string;
  grade: string;
  homeroom: string;
  students: number;
  atRisk: number;
  averageCanvasScorePercentage?: number | null;
}

interface ClassDrilldownPage {
  items: StudentListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

@Component({
  selector: 'app-static-reports',
  imports: [FormsModule, NgClass, NgTemplateOutlet, RouterLink, SupplementalReportComponent, HealthReportComponent, BehaviourReportComponent, LearningReviewReportComponent, LeadershipReportComponent, ScoreReportComponent, ErpMealReportComponent, ReportExportButtonComponent, ActivityPeriodFilterComponent],
  template: `
    <section class="print-region space-y-6">
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div>
          <div class="flex flex-wrap items-center gap-2">
            <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">
              {{ languageService.t('reports.eyebrow') }}
            </p>
            <span class="rounded-full border border-[#d9cfe0] bg-white px-2.5 py-1 text-[10px] font-bold uppercase tracking-[0.1em] text-[#6d3a91]">
              {{ languageService.t('reports.readOnly') }}
            </span>
          </div>
          <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">
            {{ languageService.t('reports.title') }}
          </h2>
          <p class="mt-2 max-w-3xl text-[#6f6877]">
            {{ languageService.t('reports.description') }}
          </p>
        </div>
        <button
          class="no-print inline-flex items-center gap-2 rounded-md border border-[#d8d0df] bg-white px-4 py-2.5 text-sm font-semibold text-[#32124f] shadow-sm transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
          type="button"
          (click)="printReport()"
        >
          <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
            <path d="M6 9V2h12v7" />
            <path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2" />
            <rect width="12" height="8" x="6" y="14" />
          </svg>
          {{ languageService.t('reports.print') }}
        </button>
      </div>

      <div
        class="report-layout grid items-start gap-5"
        [class.report-library-collapsed]="reportMenuCollapsed()"
      >
        <aside class="report-library no-print overflow-hidden rounded-xl border border-[#e7e0eb] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.07)]">
          <div
            class="flex min-h-[73px] items-center gap-3 border-b border-[#eee8f1] bg-[#faf8fb] px-3 py-3"
            [class.justify-center]="reportMenuCollapsed()"
          >
            @if (!reportMenuCollapsed()) {
              <div class="min-w-0 flex-1 px-2">
                <p class="text-xs font-bold uppercase tracking-[0.14em] text-[#8a6811]">
                  {{ languageService.t('reports.library') }}
                </p>
                <p class="mt-1 text-sm text-[#746b7b]">{{ languageService.t('reports.libraryHint') }}</p>
              </div>
            }
            <button
              class="grid h-9 w-9 shrink-0 place-items-center rounded-md border border-[#ddd3e3] bg-white text-[#5d2d7b] transition hover:border-[#b69ac6] hover:bg-[#f4eef7] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#6d3a91]"
              type="button"
              [attr.aria-label]="languageService.t(reportMenuCollapsed() ? 'reports.library.expand' : 'reports.library.collapse')"
              [attr.title]="languageService.t(reportMenuCollapsed() ? 'reports.library.expand' : 'reports.library.collapse')"
              [attr.aria-expanded]="!reportMenuCollapsed()"
              (click)="toggleReportMenu()"
            >
              <svg
                class="h-4 w-4 transition-transform duration-200"
                [class.rotate-180]="reportMenuCollapsed()"
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                stroke-width="2.4"
                stroke-linecap="round"
                stroke-linejoin="round"
                aria-hidden="true"
              >
                <path d="m15 18-6-6 6-6" />
              </svg>
            </button>
          </div>
          <nav class="space-y-1 p-2" [attr.aria-label]="languageService.t('reports.library')">
            @for (group of visibleReportGroups(); track group.id) {
              <section class="overflow-hidden rounded-lg border border-transparent" [class.border-[#e7e0eb]]="isReportGroupExpanded(group.id) && !reportMenuCollapsed()">
                <button
                  class="flex w-full items-center justify-between gap-3 rounded-lg px-3 py-3 text-left transition hover:bg-[#f7f3f9] focus-visible:outline-2 focus-visible:outline-offset-1 focus-visible:outline-[#6d3a91]"
                  [class.justify-center]="reportMenuCollapsed()"
                  [class.rounded-b-none]="isReportGroupExpanded(group.id) && !reportMenuCollapsed()"
                  [class.bg-[#f7f3f9]]="isReportGroupExpanded(group.id) && !reportMenuCollapsed()"
                  type="button"
                  [attr.aria-expanded]="isReportGroupExpanded(group.id)"
                  [attr.aria-label]="languageService.t(group.titleKey) + (group.awaitingData ? ' — ' + languageService.t('reports.awaitingData.badge') : '')"
                  [attr.title]="group.awaitingData ? languageService.t('reports.awaitingData.description') : reportMenuCollapsed() ? languageService.t(group.titleKey) : null"
                  (click)="toggleReportGroup(group.id)"
                >
                  @if (reportMenuCollapsed()) {
                    <span class="relative grid h-8 w-10 place-items-center rounded-md bg-[#f2ecf5] text-[10px] font-bold text-[#6d3a91]">
                      {{ group.shortLabel }}
                      @if (group.awaitingData) {
                        <span class="absolute -right-0.5 -top-0.5 h-2.5 w-2.5 rounded-full bg-red-600 ring-2 ring-white" aria-hidden="true"></span>
                      }
                    </span>
                  } @else {
                    <span class="flex min-w-0 flex-1 flex-wrap items-center gap-x-2 gap-y-1.5">
                      <span class="text-[11px] font-bold uppercase tracking-[0.11em] text-[#76500a]">{{ languageService.t(group.titleKey) }}</span>
                      @if (group.awaitingData) {
                        <span class="inline-flex whitespace-nowrap rounded-full bg-red-50 px-2 py-0.5 text-[10px] font-semibold text-red-700 ring-1 ring-inset ring-red-200">{{ languageService.t('reports.awaitingData.badge') }}</span>
                      }
                    </span>
                    <svg
                      class="h-4 w-4 shrink-0 text-[#7d6b87] transition-transform duration-200"
                      [class.rotate-180]="isReportGroupExpanded(group.id)"
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      stroke-width="2.25"
                      stroke-linecap="round"
                      stroke-linejoin="round"
                      aria-hidden="true"
                    >
                      <path d="m6 9 6 6 6-6" />
                    </svg>
                  }
                </button>
                @if (isReportGroupExpanded(group.id) && !reportMenuCollapsed()) {
                  <div class="space-y-1 border-t border-[#eee8f1] bg-white p-1.5">
                    @for (report of group.items; track report.id; let reportIndex = $index) {
                      <button
                        class="group flex w-full items-start gap-3 rounded-md px-3 py-3 text-left transition"
                        type="button"
                        [class.bg-[#32124f]]="activeReport() === report.id"
                        [class.text-white]="activeReport() === report.id"
                        [class.text-[#32124f]]="activeReport() !== report.id"
                        [class.hover:bg-[#f5f0f7]]="activeReport() !== report.id"
                        [attr.aria-current]="activeReport() === report.id ? 'page' : null"
                        (click)="selectReport(report.id)"
                      >
                        <span
                          class="grid h-7 w-7 shrink-0 place-items-center rounded-md text-xs font-bold"
                          [class.bg-[#f3c246]]="activeReport() === report.id"
                          [class.text-[#32124f]]="activeReport() === report.id"
                          [class.bg-[#f2ecf5]]="activeReport() !== report.id"
                          [class.text-[#6d3a91]]="activeReport() !== report.id"
                        >{{ reportIndex + 1 }}</span>
                        <span class="min-w-0">
                          <span class="block text-sm font-semibold">{{ languageService.t(report.titleKey) }}</span>
                          <span
                            class="mt-1 block text-xs leading-5"
                            [class.text-[#d8cbe2]]="activeReport() === report.id"
                            [class.text-[#817589]]="activeReport() !== report.id"
                          >{{ languageService.t(report.descriptionKey) }}</span>
                        </span>
                      </button>
                    }
                  </div>
                }
              </section>
            }
          </nav>
        </aside>

        <main class="min-w-0 space-y-4">
          <section class="no-print rounded-xl border border-[#e7e0eb] bg-white p-4 shadow-[0_12px_35px_rgba(50,18,79,0.07)]">
            <div class="flex flex-wrap items-end gap-3">
              <label class="grid min-w-[180px] flex-1 gap-1.5 sm:flex-none">
                <span class="text-[10px] font-bold uppercase tracking-[0.1em] text-[#746b7b]">
                  {{ languageService.t('reports.academicYear') }}
                </span>
                <select
                  class="rounded-md border border-[#d8d0df] bg-white px-3 py-2.5 text-sm font-semibold text-[#32124f] outline-none transition focus:border-[#8a5aa7] focus:ring-2 focus:ring-[#eee5f3] disabled:opacity-60"
                  [(ngModel)]="selectedYear"
                  (ngModelChange)="onPeriodChange()"
                  [disabled]="loading()"
                >
                  @for (year of years(); track year) {
                    <option [value]="year">{{ formatYear(year) }}</option>
                  }
                </select>
              </label>
              <label class="grid min-w-[180px] flex-1 gap-1.5 sm:flex-none">
                <span class="text-[10px] font-bold uppercase tracking-[0.1em] text-[#746b7b]">
                  {{ languageService.t('reports.term') }}
                </span>
                <select
                  class="rounded-md border border-[#d8d0df] bg-white px-3 py-2.5 text-sm font-semibold text-[#32124f] outline-none transition focus:border-[#8a5aa7] focus:ring-2 focus:ring-[#eee5f3] disabled:opacity-60"
                  [(ngModel)]="selectedTerm"
                  (ngModelChange)="onPeriodChange()"
                  [disabled]="loading()"
                >
                  <option value="all">{{ languageService.t('reports.allTerms') }}</option>
                  @for (term of terms(); track term) {
                    <option [value]="term">{{ termLabel(term) }}</option>
                  }
                </select>
              </label>
              @if (supportsActivityPeriod()) {
                <app-activity-period-filter class="block w-full sm:w-auto sm:flex-none" [academicYear]="selectedYear"
                  [month]="activityPeriod.month" [weekStart]="activityPeriod.weekStart" [showHint]="false" [disabled]="loading() || reportLoading()"
                  (periodChange)="onActivityPeriodChange($event)" />
              }
              <div class="ml-auto flex min-h-10 items-center gap-2 rounded-md px-3 text-xs"
                [ngClass]="activeReportAwaitingData() ? 'bg-red-50 text-red-700' : 'bg-[#f7f4f8] text-[#746b7b]'">
                <span class="h-2 w-2 rounded-full" [ngClass]="activeReportAwaitingData() ? 'bg-red-600' : 'bg-emerald-500'"></span>
                {{ languageService.t(activeReportAwaitingData() ? 'reports.awaitingData.badge' : 'reports.liveData') }}
              </div>
            </div>
            @if (supportsActivityPeriod()) {
              <p class="mt-3 text-xs leading-5 text-[#817589]">{{ languageService.t(activityPeriodHintKey()) }}</p>
            }
            @if (activeReport() === 'support-students') {
              <p class="mt-2 text-xs leading-5 text-[#817589]">{{ languageService.t('reports.activityPeriod.supportSignalsHint') }}</p>
            }
            @if (activeReport() === 'support-classes') {
              <p class="mt-3 text-xs leading-5 text-[#817589]">{{ languageService.t('reports.activityPeriod.semesterOnly') }}</p>
            }
            @if (activeReport() === 'school-current-course-score') {
              <p class="mt-3 text-xs leading-5 text-[#817589]">{{ languageService.t('reports.activityPeriod.currentScoreOnly') }}</p>
            }
          </section>

          @if (activeReportAwaitingData()) {
            <section class="rounded-xl border border-red-200 bg-red-50 px-5 py-4" role="status">
              <p class="text-sm font-semibold text-red-800">{{ languageService.t('reports.awaitingData.title') }}</p>
              <p class="mt-1 text-sm leading-6 text-red-700">{{ languageService.t('reports.awaitingData.description') }}</p>
            </section>
          }

          <div class="min-h-[560px]" [attr.aria-busy]="loading() || reportLoading()">
          @if (loading()) {
            <section class="grid min-h-[560px] place-items-center rounded-xl border border-[#e7e0eb] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.07)]" role="status">
              <div class="text-center">
                <span class="mx-auto block h-10 w-10 animate-spin rounded-full border-4 border-[#e6dfea] border-t-[#6d3a91]"></span>
                <p class="mt-4 text-sm font-semibold text-[#5f5368]">{{ languageService.t('reports.loading') }}</p>
              </div>
            </section>
          } @else if (loadFailed()) {
            <section class="rounded-xl border border-red-200 bg-red-50 p-8 text-center">
              <p class="font-semibold text-red-800">{{ languageService.t('reports.loadError') }}</p>
              <button class="no-print mt-4 rounded-md bg-red-700 px-4 py-2 text-sm font-semibold text-white" type="button" (click)="loadReportData()">
                {{ languageService.t('reports.retry') }}
              </button>
            </section>
          } @else if (reportLoading()) {
            <section class="flex min-h-[560px] items-center justify-center gap-3 rounded-xl border border-[#e7e0eb] bg-white p-6 text-sm font-semibold text-[#5f5368] shadow-[0_12px_35px_rgba(50,18,79,0.07)]" role="status">
              <span class="block h-6 w-6 animate-spin rounded-full border-2 border-[#e6dfea] border-t-[#6d3a91]"></span>
              {{ languageService.t('reports.loading') }}
            </section>
          } @else {
            @switch (activeReport()) {
              @case ('learning-diligence') {
                <ng-container [ngTemplateOutlet]="learningDiligence"></ng-container>
              }
              @case ('support-classes') {
                <ng-container [ngTemplateOutlet]="supportClasses"></ng-container>
              }
              @case ('missing-frequency') { <app-learning-review-report reportKey="missing-frequency" [academicYear]="selectedYear" [term]="selectedTerm" [periodLabel]="periodLabel()" [activityPeriod]="activityPeriod" /> }
              @case ('homework-achievement') { <app-learning-review-report reportKey="homework-achievement" [academicYear]="selectedYear" [term]="selectedTerm" [periodLabel]="periodLabel()" [activityPeriod]="activityPeriod" /> }
              @case ('reading-completion') { <app-learning-review-report reportKey="reading-completion" [academicYear]="selectedYear" [term]="selectedTerm" [periodLabel]="periodLabel()" [activityPeriod]="activityPeriod" /> }
              @case ('grading-coverage') { <app-learning-review-report reportKey="grading-coverage" [academicYear]="selectedYear" [term]="selectedTerm" [periodLabel]="periodLabel()" [activityPeriod]="activityPeriod" /> }
              @case ('school-learning-engagement') { <app-leadership-report reportKey="school-learning-engagement" [academicYear]="selectedYear" [term]="selectedTerm" [periodLabel]="periodLabel()" [activityPeriod]="activityPeriod" /> }
              @case ('school-assessment-results') { <app-leadership-report reportKey="school-assessment-results" [academicYear]="selectedYear" [term]="selectedTerm" [periodLabel]="periodLabel()" [activityPeriod]="activityPeriod" /> }
              @case ('school-subject-grade-average') { <app-score-report reportKey="school-subject-grade-average" [academicYear]="selectedYear" [term]="selectedTerm" [periodLabel]="periodLabel()" [activityPeriod]="activityPeriod" /> }
              @case ('subject-band-by-subject') { <app-score-report reportKey="subject-band-by-subject" [academicYear]="selectedYear" [term]="selectedTerm" [periodLabel]="periodLabel()" [activityPeriod]="activityPeriod" /> }
              @case ('subject-band-by-grade') { <app-score-report reportKey="subject-band-by-grade" [academicYear]="selectedYear" [term]="selectedTerm" [periodLabel]="periodLabel()" [activityPeriod]="activityPeriod" /> }
              @case ('subject-band-by-class') { <app-score-report reportKey="subject-band-by-class" [academicYear]="selectedYear" [term]="selectedTerm" [periodLabel]="periodLabel()" [activityPeriod]="activityPeriod" /> }
              @case ('school-score-histogram') { <app-score-report reportKey="school-score-histogram" [academicYear]="selectedYear" [term]="selectedTerm" [periodLabel]="periodLabel()" [activityPeriod]="activityPeriod" /> }
              @case ('subject-assessment-average') { <app-score-report reportKey="subject-assessment-average" [academicYear]="selectedYear" [term]="selectedTerm" [periodLabel]="periodLabel()" [activityPeriod]="activityPeriod" /> }
              @case ('school-current-course-score') { <app-score-report reportKey="school-current-course-score" [academicYear]="selectedYear" [term]="selectedTerm" [periodLabel]="periodLabel()" [activityPeriod]="activityPeriod" /> }
              @case ('subject-class-results') { <app-score-report reportKey="subject-class-results" [academicYear]="selectedYear" [term]="selectedTerm" [periodLabel]="periodLabel()" [activityPeriod]="activityPeriod" /> }
              @case ('school-top-students') { <app-score-report reportKey="school-top-students" [academicYear]="selectedYear" [term]="selectedTerm" [periodLabel]="periodLabel()" [activityPeriod]="activityPeriod" /> }
              @case ('school-bottom-students') { <app-score-report reportKey="school-bottom-students" [academicYear]="selectedYear" [term]="selectedTerm" [periodLabel]="periodLabel()" [activityPeriod]="activityPeriod" /> }
              @case ('support-students') {
                <ng-container [ngTemplateOutlet]="supportStudents"></ng-container>
              }
              @case ('behaviour-analysis') {
                <app-behaviour-report [academicYear]="selectedYear" [term]="selectedTerm" [periodLabel]="periodLabel()" [activityPeriod]="activityPeriod" />
              }
              @case ('health-diagnoses') {
                <app-health-report report="diagnoses" [academicYear]="selectedYear" [term]="selectedTerm" [periodLabel]="periodLabel()" [activityPeriod]="activityPeriod" />
              }
              @case ('health-frequent-students') {
                <app-health-report report="frequent-students" [academicYear]="selectedYear" [term]="selectedTerm" [periodLabel]="periodLabel()" [activityPeriod]="activityPeriod" />
              }
              @case ('health-student-visits') {
                <app-health-report report="student-visits" [academicYear]="selectedYear" [term]="selectedTerm" [periodLabel]="periodLabel()" [activityPeriod]="activityPeriod" />
              }
              @case ('meal-ordered-no-ticket') { <app-erp-meal-report report="ordered-no-ticket" [academicYear]="selectedYear" [term]="selectedTerm" [periodLabel]="periodLabel()" [activityPeriod]="activityPeriod" /> }
              @case ('meal-ordered-not-eaten') { <app-erp-meal-report report="ordered-not-eaten" [academicYear]="selectedYear" [term]="selectedTerm" [periodLabel]="periodLabel()" [activityPeriod]="activityPeriod" /> }
              @case ('meal-no-order') { <app-erp-meal-report report="no-order" [academicYear]="selectedYear" [term]="selectedTerm" [periodLabel]="periodLabel()" [activityPeriod]="activityPeriod" /> }
              @case ('meal-coffee-frequency') { <app-erp-meal-report report="coffee-frequency" [academicYear]="selectedYear" [term]="selectedTerm" [periodLabel]="periodLabel()" [activityPeriod]="activityPeriod" /> }
              @default {
                @if (activeSupplementalReport(); as report) {
                  <app-supplemental-report
                    [report]="report"
                    [academicYear]="selectedYear"
                    [term]="selectedTerm"
                    [periodLabel]="periodLabel()"
                    [activityPeriod]="activityPeriod"
                  />
                }
              }
            }
          }
          </div>
        </main>
      </div>
    </section>

    <ng-template #reportHeader let-titleKey="titleKey" let-descriptionKey="descriptionKey">
      <div class="border-b border-[#eee8f1] px-5 py-5 sm:px-6">
        <div class="flex flex-wrap items-start justify-between gap-3">
          <div>
            <p class="text-xs font-bold uppercase tracking-[0.14em] text-[#8a6811]">{{ periodLabel() }}</p>
            <h3 class="mt-1 text-xl font-semibold text-[#32124f]">{{ languageService.t(titleKey) }}</h3>
            <p class="mt-1 max-w-3xl text-sm leading-6 text-[#6f6877]">{{ languageService.t(descriptionKey) }}</p>
          </div>
          <div class="flex flex-col items-end gap-2">
            <app-report-export-button [exporting]="exporting()" [disabled]="!canExportStaticReport()" (exportRequested)="exportStaticReport()" />
            <span class="rounded-full bg-[#f2ecf5] px-3 py-1 text-xs font-semibold text-[#6d3a91]">
              {{ languageService.t('reports.fixedDefinition') }}
            </span>
          </div>
        </div>
      </div>
    </ng-template>

    <ng-template #learningDiligence>
      <section class="overflow-hidden rounded-xl border border-[#e7e0eb] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.07)]">
        <ng-container *ngTemplateOutlet="reportHeader; context: { titleKey: 'reports.learning.title', descriptionKey: 'reports.learning.longDescription' }"></ng-container>
        <p class="border-b border-[#eee8f1] px-5 py-3 text-xs text-[#817589]">{{ activityPeriodLabel() }}</p>
        <div class="border-b border-[#eee8f1] bg-[#fdfbfe] px-5 py-4 sm:px-6">
          <div class="flex flex-wrap items-center justify-between gap-3">
            <div>
              <p class="text-sm font-semibold text-[#32124f]">{{ languageService.t('reports.canvasAverage') }}</p>
              <p class="mt-1 text-xs text-[#817589]">{{ languageService.t('reports.canvasAverageHint') }}</p>
              <p class="mt-1 text-xs text-[#817589]">{{ languageService.t('reports.activityPeriod.courseScoreHint') }}</p>
            </div>
            <p class="text-2xl font-semibold text-[#6d3a91]">{{ formatPercent(averageCanvasScorePercentage()) }}</p>
          </div>
        </div>
        <div class="overflow-x-auto">
          <table class="w-full min-w-[760px] border-collapse text-left">
            <thead class="bg-[#f5f0f7] text-xs uppercase tracking-[0.08em] text-[#5d486b]">
              <tr>
                <th class="px-5 py-3.5 font-bold">{{ languageService.t('reports.table.indicator') }}</th>
                <th class="px-4 py-3.5 text-right font-bold">{{ languageService.t('reports.table.result') }}</th>
                <th class="px-4 py-3.5 text-right font-bold">{{ languageService.t('reports.table.volume') }}</th>
                <th class="px-4 py-3.5 font-bold">{{ languageService.t('reports.table.benchmark') }}</th>
                <th class="px-5 py-3.5 font-bold">{{ languageService.t('reports.table.status') }}</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-[#eee8f1]">
              @for (row of learningRows(); track row.indicatorKey) {
                <tr class="transition hover:bg-[#fdfbfe]">
                  <td class="px-5 py-4">
                    <p class="font-semibold text-[#32124f]">{{ languageService.t(row.indicatorKey) }}</p>
                    <p class="mt-1 text-xs text-[#817589]">{{ languageService.t(row.indicatorKey + '.hint') }}</p>
                  </td>
                  <td class="px-4 py-4 text-right text-lg font-semibold text-[#32124f]">{{ formatPercent(row.value) }}</td>
                  <td class="px-4 py-4 text-right text-sm text-[#625969]">{{ formatVolume(row) }}</td>
                  <td class="px-4 py-4 text-sm font-medium text-[#625969]">{{ languageService.t(row.targetKey) }}</td>
                  <td class="px-5 py-4"><span class="rounded-full px-2.5 py-1 text-xs font-bold" [ngClass]="statusBadgeClass(row.status)">{{ statusLabel(row.status) }}</span></td>
                </tr>
              }
            </tbody>
          </table>
        </div>
        <p class="border-t border-[#eee8f1] bg-[#faf8fb] px-5 py-3 text-xs leading-5 text-[#817589]">
          {{ languageService.t('reports.learning.note') }}
        </p>
      </section>
    </ng-template>

    <ng-template #supportClasses>
      <section class="overflow-hidden rounded-xl border border-[#e7e0eb] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.07)]">
        <ng-container *ngTemplateOutlet="reportHeader; context: { titleKey: 'reports.classes.title', descriptionKey: 'reports.classes.longDescription' }"></ng-container>
        <div class="no-print flex flex-wrap items-center justify-between gap-3 border-b border-[#eee8f1] bg-[#fdfbfe] px-5 py-3 sm:px-6">
          <div class="flex min-w-0 flex-1 flex-wrap items-center gap-3">
            <div class="inline-flex rounded-lg border border-[#ddd3e3] bg-white p-1" role="tablist" [attr.aria-label]="languageService.t('reports.classes.viewLabel')">
              <button
                class="rounded-md px-4 py-2 text-sm font-semibold transition"
                [class.bg-[#32124f]]="cohortView() === 'grade'"
                [class.text-white]="cohortView() === 'grade'"
                [class.text-[#625969]]="cohortView() !== 'grade'"
                type="button"
                role="tab"
                [attr.aria-selected]="cohortView() === 'grade'"
                (click)="selectCohortView('grade')"
              >{{ languageService.t('reports.classes.gradeView') }}</button>
              <button
                class="rounded-md px-4 py-2 text-sm font-semibold transition"
                [class.bg-[#32124f]]="cohortView() === 'class'"
                [class.text-white]="cohortView() === 'class'"
                [class.text-[#625969]]="cohortView() !== 'class'"
                type="button"
                role="tab"
                [attr.aria-selected]="cohortView() === 'class'"
                (click)="selectCohortView('class')"
              >{{ languageService.t('reports.classes.classView') }}</button>
            </div>
            <label class="relative min-w-[220px] flex-1 sm:max-w-sm">
              <span class="sr-only">{{ languageService.t('reports.classes.search') }}</span>
              <svg class="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[#8c8193]" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/></svg>
              <input
                class="w-full rounded-md border border-[#d8d0df] bg-white py-2.5 pl-10 pr-9 text-sm outline-none transition focus:border-[#8a5aa7] focus:ring-2 focus:ring-[#eee5f3]"
                type="search"
                [ngModel]="classSearch()"
                (ngModelChange)="onClassSearchChange($event)"
                [placeholder]="languageService.t('reports.classes.searchPlaceholder')"
              />
              @if (classSearch()) {
                <button
                  class="absolute right-2 top-1/2 grid h-6 w-6 -translate-y-1/2 place-items-center rounded-full text-[#746b7b] hover:bg-[#f2ecf5] hover:text-[#32124f]"
                  type="button"
                  [attr.aria-label]="languageService.t('reports.classes.clearSearch')"
                  (click)="clearClassSearch()"
                >×</button>
              }
            </label>
          </div>
          <button
            class="inline-flex items-center gap-2 rounded-md border border-[#d8d0df] bg-white px-3.5 py-2 text-sm font-semibold text-[#32124f] transition hover:border-[#b69ac6] hover:bg-[#f4eef7]"
            type="button"
            (click)="openClassSettings()"
          >
            <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><circle cx="12" cy="12" r="3"/><path d="M19.4 15a1.7 1.7 0 0 0 .34 1.88l.06.06-2.12 2.12-.06-.06a1.7 1.7 0 0 0-1.88-.34 1.7 1.7 0 0 0-1.04 1.56V20h-3v-.08a1.7 1.7 0 0 0-1.04-1.56 1.7 1.7 0 0 0-1.88.34l-.06.06-2.12-2.12.06-.06A1.7 1.7 0 0 0 7 14.7a1.7 1.7 0 0 0-1.56-1.04H5v-3h.44A1.7 1.7 0 0 0 7 9.62a1.7 1.7 0 0 0-.34-1.88l-.06-.06 2.12-2.12.06.06A1.7 1.7 0 0 0 10.66 6a1.7 1.7 0 0 0 1.04-1.56V4h3v.44A1.7 1.7 0 0 0 15.74 6a1.7 1.7 0 0 0 1.88-.34l.06-.06 2.12 2.12-.06.06a1.7 1.7 0 0 0-.34 1.88 1.7 1.7 0 0 0 1.56 1.04H21v3h-.04A1.7 1.7 0 0 0 19.4 15Z"/></svg>
            {{ languageService.t('reports.classes.settings.open') }}
          </button>
        </div>
        @if (filteredClassRows().length) {
          <div class="overflow-x-auto">
            <table class="w-full min-w-[880px] border-collapse text-left">
              <thead class="bg-[#f5f0f7] text-xs uppercase tracking-[0.08em] text-[#5d486b]">
                <tr>
                  <th class="px-5 py-3.5 font-bold">{{ languageService.t(cohortView() === 'grade' ? 'reports.table.grade' : 'reports.table.class') }}</th>
                  <th class="px-4 py-3.5 text-right font-bold">{{ languageService.t('reports.table.students') }}</th>
                  <th class="px-4 py-3.5 text-right font-bold">{{ languageService.t('reports.table.canvasAverage') }}</th>
                  <th class="px-4 py-3.5 text-right font-bold">{{ languageService.t('reports.table.needSupport') }}</th>
                  <th class="px-4 py-3.5 text-right font-bold">{{ languageService.t('reports.table.riskRate') }}</th>
                  <th class="px-4 py-3.5 font-bold">{{ languageService.t('reports.table.status') }}</th>
                  <th class="px-5 py-3.5 font-bold">{{ languageService.t('reports.table.followUp') }}</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-[#eee8f1]">
                @for (row of paginatedClassRows(); track row.key) {
                  <tr class="transition hover:bg-[#fdfbfe]">
                    <td class="px-5 py-4">
                      <p class="font-semibold text-[#32124f]">{{ row.label }}</p>
                      @if (cohortView() === 'class') {
                        <p class="mt-1 text-xs text-[#817589]">{{ languageService.t('reports.grade') }} {{ row.grade }}</p>
                      }
                    </td>
                    <td class="px-4 py-4 text-right text-sm text-[#625969]">
                      <button
                        class="rounded px-1.5 py-1 font-semibold text-[#5d2d7b] underline decoration-[#bca9c8] decoration-dotted underline-offset-4 transition hover:bg-[#f3edf7] hover:decoration-solid focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#6d3a91] disabled:cursor-default disabled:text-[#9a91a3] disabled:no-underline"
                        type="button"
                        [disabled]="row.students === 0"
                        [attr.aria-label]="classDrilldownOpenLabel(row, 'students')"
                        [attr.title]="classDrilldownOpenLabel(row, 'students')"
                        (click)="openClassDrilldown(row, 'students')"
                      >{{ row.students }}</button>
                    </td>
                    <td class="px-4 py-4 text-right text-sm font-semibold text-[#32124f]">{{ formatPercent(row.averageCanvasScorePercentage ?? null) }}</td>
                    <td class="px-4 py-4 text-right text-sm font-semibold text-[#32124f]">
                      <button
                        class="rounded px-1.5 py-1 font-semibold text-[#5d2d7b] underline decoration-[#bca9c8] decoration-dotted underline-offset-4 transition hover:bg-[#f3edf7] hover:decoration-solid focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#6d3a91] disabled:cursor-default disabled:text-[#9a91a3] disabled:no-underline"
                        type="button"
                        [disabled]="row.atRisk === 0"
                        [attr.aria-label]="classDrilldownOpenLabel(row, 'support')"
                        [attr.title]="classDrilldownOpenLabel(row, 'support')"
                        (click)="openClassDrilldown(row, 'support')"
                      >{{ row.atRisk }}</button>
                    </td>
                    <td class="px-4 py-4 text-right text-sm font-semibold text-[#32124f]">{{ classRiskRate(row) }}</td>
                    <td class="px-4 py-4"><span class="rounded-full px-2.5 py-1 text-xs font-bold" [ngClass]="statusBadgeClass(classStatus(row))" [attr.title]="classStatusExplanation(row)">{{ statusLabel(classStatus(row)) }}</span></td>
                    <td class="px-5 py-4 text-sm text-[#625969]">{{ classFollowUpKey(row) }}</td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <div class="no-print flex flex-wrap items-center justify-between gap-3 border-t border-[#eee8f1] px-5 py-4">
            <div class="flex items-center gap-3 text-xs text-[#6f6877]">
              <span>{{ classPageLabel() }}</span>
              <label class="flex items-center gap-2">
                {{ languageService.t('pagination.perPage') }}
                <select class="rounded-md border border-[#d8d0df] bg-white px-2 py-1.5 text-xs outline-none focus:border-[#6d3a91]" [value]="classPageSize()" (change)="changeClassPageSize($event)">
                  <option value="10">10</option><option value="20">20</option><option value="50">50</option>
                </select>
              </label>
            </div>
            <div class="flex gap-2">
              <button class="rounded-md border border-[#d8d0df] px-4 py-2 text-xs font-semibold text-[#32124f] disabled:opacity-40" type="button" [disabled]="classPage() <= 1" (click)="previousClassPage()">{{ languageService.t('pagination.previous') }}</button>
              <button class="rounded-md border border-[#d8d0df] px-4 py-2 text-xs font-semibold text-[#32124f] disabled:opacity-40" type="button" [disabled]="classPage() >= classTotalPages()" (click)="nextClassPage()">{{ languageService.t('pagination.next') }}</button>
            </div>
          </div>
        } @else if (classRows().length) {
          <div class="grid min-h-56 place-items-center p-8 text-center text-sm text-[#817589]">{{ languageService.t('reports.classes.noMatches') }}</div>
        } @else {
          <div class="grid min-h-56 place-items-center p-8 text-center text-sm text-[#817589]">{{ languageService.t('reports.noData') }}</div>
        }
        <p class="border-t border-[#eee8f1] bg-[#faf8fb] px-5 py-3 text-xs leading-5 text-[#817589]">{{ classRulesSummary() }}</p>
      </section>
    </ng-template>

    <ng-template #supportStudents>
      <section class="relative overflow-hidden rounded-xl border border-[#e7e0eb] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.07)]" [attr.aria-busy]="studentsLoading()">
        <ng-container *ngTemplateOutlet="reportHeader; context: { titleKey: 'reports.students.title', descriptionKey: 'reports.students.longDescription' }"></ng-container>
        <div class="no-print border-b border-[#eee8f1] px-5 py-4 sm:px-6">
          <label class="relative block max-w-md">
            <span class="sr-only">{{ languageService.t('reports.search') }}</span>
            <svg class="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[#8c8193]" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/></svg>
            <input class="w-full rounded-md border border-[#d8d0df] py-2.5 pl-10 pr-9 text-sm outline-none focus:border-[#8a5aa7] focus:ring-2 focus:ring-[#eee5f3]" type="search" [ngModel]="studentSearch()" (ngModelChange)="onStudentSearchChange($event)" [placeholder]="languageService.t('reports.search')" />
            @if (studentSearch()) {
              <button class="absolute right-2 top-1/2 grid h-6 w-6 -translate-y-1/2 place-items-center rounded-full text-[#746b7b] hover:bg-[#f2ecf5] hover:text-[#32124f]" type="button" [attr.aria-label]="languageService.t('reports.clearFilters')" (click)="clearStudentSearch()">×</button>
            }
          </label>
        </div>
        @if (studentsLoading() && studentTotalCount() > 0) {
          <div class="no-print absolute inset-0 z-10 flex justify-center bg-white/70 pt-36" role="status">
            <div class="flex h-fit items-center gap-3 rounded-full bg-white px-5 py-3 text-sm font-semibold text-[#5f5368] shadow-md"><span class="h-6 w-6 animate-spin rounded-full border-2 border-[#e6dfea] border-t-[#6d3a91]"></span>{{ languageService.t('reports.loading') }}</div>
          </div>
        }
        @if (studentsLoadFailed() && studentTotalCount() > 0) {
          <div class="m-5 rounded-md bg-red-50 p-3 text-sm text-red-700" role="alert">{{ languageService.t('reports.loadError') }} <button class="ml-2 font-semibold underline" type="button" (click)="loadSupportStudents(studentPage())">{{ languageService.t('reports.retry') }}</button></div>
        }
        @if (studentsLoading() && studentTotalCount() === 0) {
          <div class="grid min-h-56 place-items-center p-8" role="status">
            <span class="block h-9 w-9 animate-spin rounded-full border-4 border-[#e6dfea] border-t-[#6d3a91]"></span>
          </div>
        } @else if (studentsLoadFailed() && studentTotalCount() === 0) {
          <div class="grid min-h-56 place-items-center p-8 text-center">
            <div><p class="text-sm text-red-700">{{ languageService.t('reports.loadError') }}</p><button class="mt-3 rounded-md border border-red-200 px-4 py-2 text-xs font-semibold text-red-700" type="button" (click)="loadSupportStudents(studentPage())">{{ languageService.t('reports.retry') }}</button></div>
          </div>
        } @else if (filteredStudents().length) {
          <div class="overflow-x-auto">
            <table class="w-full min-w-[940px] border-collapse text-left">
              <thead class="bg-[#f5f0f7] text-xs uppercase tracking-[0.08em] text-[#5d486b]">
                <tr>
                  <th class="px-5 py-3.5 font-bold">{{ languageService.t('reports.table.student') }}</th>
                  <th class="px-4 py-3.5 font-bold">{{ languageService.t('reports.table.class') }}</th>
                  <th class="px-4 py-3.5 text-right font-bold">{{ languageService.t('reports.table.canvasAverage') }}</th>
                  <th class="px-4 py-3.5 font-bold">{{ languageService.t('reports.table.riskLevel') }}</th>
                  <th class="px-4 py-3.5 text-right font-bold">{{ languageService.t('reports.table.signals') }}</th>
                  <th class="px-4 py-3.5 font-bold">{{ languageService.t('reports.table.progress') }}</th>
                  <th class="no-print px-5 py-3.5 text-right font-bold">{{ languageService.t('reports.table.action') }}</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-[#eee8f1]">
                @for (student of filteredStudents(); track student.id) {
                  <tr class="transition hover:bg-[#fdfbfe]">
                    <td class="px-5 py-4"><p class="font-semibold text-[#32124f]">{{ student.fullName }}</p><p class="mt-1 text-xs text-[#817589]">{{ student.sisUserId }}</p></td>
                    <td class="px-4 py-4 text-sm text-[#625969]">{{ student.homeroom || (languageService.t('reports.grade') + ' ' + (student.gradeLevel || '—')) }}</td>
                    <td class="px-4 py-4 text-right text-sm font-semibold text-[#32124f]">{{ formatPercent(student.averageCanvasScorePercentage ?? null) }}</td>
                    <td class="px-4 py-4"><span class="rounded-full px-2.5 py-1 text-xs font-bold" [ngClass]="riskBadgeClass(student.riskLevel)">{{ riskLabel(student.riskLevel) }}</span></td>
                    <td class="px-4 py-4 text-right text-sm font-semibold text-[#32124f]">{{ student.riskFlagCount }}</td>
                    <td class="px-4 py-4 text-sm text-[#625969]">{{ languageService.t('reports.students.progressReview') }}</td>
                    <td class="no-print px-5 py-4 text-right"><a class="text-sm font-semibold text-[#6d3a91] hover:underline" [routerLink]="['/students', student.id]">{{ languageService.t('reports.viewStudent') }}</a></td>
                  </tr>
                }
              </tbody>
            </table>
          </div>
          <div class="no-print flex flex-wrap items-center justify-between gap-3 border-t border-[#eee8f1] px-5 py-4">
            <div class="flex items-center gap-3 text-xs text-[#6f6877]">
              <span>{{ studentPageLabel() }}</span>
              <label class="flex items-center gap-2">
                {{ languageService.t('pagination.perPage') }}
                <select class="rounded-md border border-[#d8d0df] bg-white px-2 py-1.5 text-xs outline-none focus:border-[#6d3a91]" [value]="studentPageSize()" (change)="changeStudentPageSize($event)">
                  <option value="10">10</option><option value="20">20</option><option value="50">50</option>
                </select>
              </label>
            </div>
            <div class="flex gap-2">
              <button class="rounded-md border border-[#d8d0df] px-4 py-2 text-xs font-semibold text-[#32124f] disabled:opacity-40" type="button" [disabled]="studentPage() <= 1" (click)="loadSupportStudents(studentPage() - 1)">{{ languageService.t('pagination.previous') }}</button>
              <button class="rounded-md border border-[#d8d0df] px-4 py-2 text-xs font-semibold text-[#32124f] disabled:opacity-40" type="button" [disabled]="studentPage() >= studentTotalPages()" (click)="loadSupportStudents(studentPage() + 1)">{{ languageService.t('pagination.next') }}</button>
            </div>
          </div>
        } @else {
          <div class="grid min-h-56 place-items-center p-8 text-center text-sm text-[#817589]">{{ languageService.t('reports.noStudents') }}</div>
        }
        <p class="border-t border-[#eee8f1] bg-[#faf8fb] px-5 py-3 text-xs leading-5 text-[#817589]">{{ languageService.t('reports.students.note') }}</p>
      </section>
    </ng-template>

    @if (classDrilldownMounted()) {
      <div class="no-print fixed inset-0 z-50" [class.pointer-events-none]="!classDrilldownOpen()">
        <button
          class="absolute inset-0 bg-[#24182f]/35 transition-opacity duration-300"
          [class.opacity-100]="classDrilldownOpen()"
          [class.opacity-0]="!classDrilldownOpen()"
          type="button"
          [attr.aria-label]="languageService.t('reports.drilldown.close')"
          (click)="closeClassDrilldown()"
        ></button>

        <aside
          class="absolute right-0 top-0 flex h-full w-full max-w-lg flex-col bg-white shadow-[0_24px_80px_rgba(36,24,47,0.28)] transition-transform duration-300 ease-out"
          [class.translate-x-0]="classDrilldownOpen()"
          [class.translate-x-full]="!classDrilldownOpen()"
          role="dialog"
          aria-modal="true"
          [attr.aria-label]="classDrilldownTitle()"
        >
          <div class="flex items-start justify-between gap-4 border-b border-[#ebe6ef] px-6 py-5">
            <div class="min-w-0">
              <p class="text-xs font-bold uppercase tracking-[0.16em] text-[#8a6811]">
                {{ languageService.t('reports.drilldown.eyebrow') }}
              </p>
              <h3 class="mt-2 text-xl font-semibold text-[#32124f]">{{ classDrilldownTitle() }}</h3>
              <p class="mt-1 text-sm text-[#6f6877]">
                {{ classDrilldownContext()?.label }} · {{ periodLabel() }}
              </p>
            </div>
            <button
              class="grid h-9 w-9 shrink-0 place-items-center rounded-full border border-[#d8d0df] text-xl text-[#32124f] transition hover:bg-[#f4eef7]"
              type="button"
              [attr.aria-label]="languageService.t('reports.drilldown.close')"
              (click)="closeClassDrilldown()"
            >×</button>
          </div>

          <div class="border-b border-[#ebe6ef] bg-[#faf9f6] px-6 py-3">
            <label class="relative block">
              <span class="sr-only">{{ languageService.t('reports.drilldown.searchPlaceholder') }}</span>
              <svg class="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[#8b8193]" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><circle cx="11" cy="11" r="7"/><path d="m20 20-3.5-3.5"/></svg>
              <input
                class="w-full rounded-md border border-[#d8d0df] bg-white py-2.5 pl-9 pr-9 text-sm text-[#32124f] outline-none transition placeholder:text-[#9a91a3] focus:border-[#6d3a91] focus:ring-2 focus:ring-[#6d3a91]/10"
                type="search"
                [ngModel]="classDrilldownSearch()"
                (ngModelChange)="onClassDrilldownSearch($event)"
                [placeholder]="languageService.t('reports.drilldown.searchPlaceholder')"
              />
              @if (classDrilldownSearch()) {
                <button
                  class="absolute right-2 top-1/2 grid h-7 w-7 -translate-y-1/2 place-items-center rounded-full text-sm text-[#6f6877] hover:bg-[#f1edf4]"
                  type="button"
                  [attr.aria-label]="languageService.t('reports.drilldown.clearSearch')"
                  (click)="clearClassDrilldownSearch()"
                >×</button>
              }
            </label>
          </div>

          <div class="min-h-0 flex-1 overflow-y-auto px-6 py-5">
            @if (classDrilldownLoading()) {
              <div class="grid min-h-48 place-items-center" role="status">
                <div class="text-center">
                  <span class="mx-auto block h-9 w-9 animate-spin rounded-full border-4 border-[#e6dfea] border-t-[#6d3a91]"></span>
                  <p class="mt-3 text-sm text-[#7c7284]">{{ languageService.t('reports.drilldown.loading') }}</p>
                </div>
              </div>
            } @else if (classDrilldownLoadFailed()) {
              <div class="rounded-md border border-red-200 bg-red-50 p-5 text-center">
                <p class="text-sm text-red-700">{{ languageService.t('reports.drilldown.loadError') }}</p>
                <button class="mt-3 rounded-md border border-red-200 bg-white px-4 py-2 text-xs font-semibold text-red-700" type="button" (click)="loadClassDrilldown(classDrilldownPage())">{{ languageService.t('reports.retry') }}</button>
              </div>
            } @else {
              <ul class="space-y-2">
                @for (student of classDrilldownStudents(); track student.id) {
                  <li>
                    <a
                      class="flex items-center gap-3 rounded-lg border border-[#ebe6ef] p-3 transition hover:border-[#d3c6db] hover:bg-[#fdfcff]"
                      [routerLink]="['/students', student.id]"
                      (click)="closeClassDrilldown()"
                    >
                      <span class="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-[#f1edf4] text-xs font-bold text-[#5b287d]">{{ studentInitials(student.fullName) }}</span>
                      <span class="min-w-0 flex-1">
                        <span class="block truncate text-sm font-semibold text-[#32124f]">{{ student.fullName }}</span>
                        <span class="mt-0.5 block truncate text-xs text-[#7c7284]">{{ student.sisUserId }} · {{ student.homeroom || (languageService.t('reports.grade') + ' ' + (student.gradeLevel || '—')) }}</span>
                      </span>
                      <span class="shrink-0 rounded-full px-2.5 py-1 text-xs font-bold" [ngClass]="riskBadgeClass(student.riskLevel)">{{ riskLabel(student.riskLevel) }}</span>
                      <span aria-hidden="true" class="text-[#6d3a91]">→</span>
                    </a>
                  </li>
                } @empty {
                  <li class="rounded-md border border-dashed border-[#d8d0df] bg-[#faf9f6] p-8 text-center text-sm text-[#7c7284]">{{ languageService.t('reports.drilldown.empty') }}</li>
                }
              </ul>
            }
          </div>

          @if (!classDrilldownLoading() && !classDrilldownLoadFailed() && classDrilldownTotalCount() > 0) {
            <div class="flex items-center justify-between gap-3 border-t border-[#ebe6ef] px-6 py-4">
              <span class="text-xs text-[#6f6877]">{{ classDrilldownPageLabel() }}</span>
              <div class="flex gap-2">
                <button class="rounded-md border border-[#d8d0df] px-3 py-2 text-xs font-semibold text-[#32124f] disabled:cursor-not-allowed disabled:opacity-40" type="button" [disabled]="classDrilldownPage() <= 1" (click)="loadClassDrilldown(classDrilldownPage() - 1)">{{ languageService.t('pagination.previous') }}</button>
                <button class="rounded-md border border-[#d8d0df] px-3 py-2 text-xs font-semibold text-[#32124f] disabled:cursor-not-allowed disabled:opacity-40" type="button" [disabled]="classDrilldownPage() >= classDrilldownTotalPages()" (click)="loadClassDrilldown(classDrilldownPage() + 1)">{{ languageService.t('pagination.next') }}</button>
              </div>
            </div>
          }
        </aside>
      </div>
    }

    @if (classSettingsOpen()) {
      <div class="no-print fixed inset-0 z-[70] grid place-items-center p-4">
        <button class="absolute inset-0 bg-[#24182f]/45" type="button" [attr.aria-label]="languageService.t('reports.classes.settings.close')" (click)="closeClassSettings()"></button>
        <section class="relative flex max-h-[90vh] w-full max-w-2xl flex-col overflow-hidden rounded-xl bg-white shadow-[0_28px_90px_rgba(36,24,47,0.3)]" role="dialog" aria-modal="true" [attr.aria-label]="languageService.t('reports.classes.settings.title')">
          <div class="flex items-start justify-between gap-4 border-b border-[#ebe6ef] px-6 py-5">
            <div>
              <p class="text-xs font-bold uppercase tracking-[0.16em] text-[#8a6811]">{{ languageService.t('reports.classes.settings.eyebrow') }}</p>
              <h3 class="mt-1 text-2xl font-semibold text-[#32124f]">{{ languageService.t('reports.classes.settings.title') }}</h3>
              <p class="mt-1 max-w-xl text-sm leading-6 text-[#6f6877]">{{ languageService.t('reports.classes.settings.hint') }}</p>
            </div>
            <button class="grid h-9 w-9 shrink-0 place-items-center rounded-full border border-[#d8d0df] text-xl text-[#32124f] hover:bg-[#f4eef7]" type="button" [attr.aria-label]="languageService.t('reports.classes.settings.close')" (click)="closeClassSettings()">×</button>
          </div>

          <div class="min-h-0 overflow-y-auto p-6">
            <section class="rounded-lg border border-[#e7e0eb] bg-[#faf8fb] p-4">
              <p class="text-sm font-semibold text-[#32124f]">{{ languageService.t('reports.classes.settings.formulaTitle') }}</p>
              <p class="mt-1 text-sm leading-6 text-[#625969]">{{ languageService.t('reports.classes.settings.formula') }}</p>
            </section>

            <div class="mt-5 grid gap-3 sm:grid-cols-3">
              <div class="rounded-lg border border-emerald-200 bg-emerald-50 p-4">
                <span class="rounded-full bg-emerald-100 px-2.5 py-1 text-xs font-bold text-emerald-800">{{ languageService.t('reports.status.green') }}</span>
                <p class="mt-3 text-sm font-semibold text-emerald-900">≤ {{ formatThreshold(draftGreenMaxRate) }}</p>
                <p class="mt-1 text-xs leading-5 text-emerald-800">{{ languageService.t('reports.classes.settings.greenHint') }}</p>
              </div>
              <div class="rounded-lg border border-amber-200 bg-amber-50 p-4">
                <span class="rounded-full bg-amber-100 px-2.5 py-1 text-xs font-bold text-amber-800">{{ languageService.t('reports.status.amber') }}</span>
                <p class="mt-3 text-sm font-semibold text-amber-900">&gt; {{ formatThreshold(draftGreenMaxRate) }} – ≤ {{ formatThreshold(draftAmberMaxRate) }}</p>
                <p class="mt-1 text-xs leading-5 text-amber-800">{{ languageService.t('reports.classes.settings.amberHint') }}</p>
              </div>
              <div class="rounded-lg border border-red-200 bg-red-50 p-4">
                <span class="rounded-full bg-red-100 px-2.5 py-1 text-xs font-bold text-red-800">{{ languageService.t('reports.status.red') }}</span>
                <p class="mt-3 text-sm font-semibold text-red-900">&gt; {{ formatThreshold(draftAmberMaxRate) }}</p>
                <p class="mt-1 text-xs leading-5 text-red-800">{{ languageService.t('reports.classes.settings.redHint') }}</p>
              </div>
            </div>

            <fieldset class="mt-5 rounded-lg border border-[#e7e0eb] p-5">
              <legend class="px-2 text-sm font-semibold text-[#32124f]">{{ languageService.t('reports.classes.settings.thresholds') }}</legend>
              <div class="grid gap-4 sm:grid-cols-2">
                <label class="text-sm font-semibold text-[#4f4756]">
                  {{ languageService.t('reports.classes.settings.greenMax') }}
                  <div class="relative mt-2">
                    <input class="w-full rounded-md border border-[#d8d0df] px-3 py-2.5 pr-9 outline-none focus:border-[#6d3a91] focus:ring-2 focus:ring-[#6d3a91]/10" type="number" min="0" max="99.9" step="0.1" [(ngModel)]="draftGreenMaxRate" />
                    <span class="absolute right-3 top-1/2 -translate-y-1/2 text-sm text-[#817589]">%</span>
                  </div>
                </label>
                <label class="text-sm font-semibold text-[#4f4756]">
                  {{ languageService.t('reports.classes.settings.amberMax') }}
                  <div class="relative mt-2">
                    <input class="w-full rounded-md border border-[#d8d0df] px-3 py-2.5 pr-9 outline-none focus:border-[#6d3a91] focus:ring-2 focus:ring-[#6d3a91]/10" type="number" min="0.1" max="100" step="0.1" [(ngModel)]="draftAmberMaxRate" />
                    <span class="absolute right-3 top-1/2 -translate-y-1/2 text-sm text-[#817589]">%</span>
                  </div>
                </label>
              </div>
              @if (!classSettingsValid()) {
                <p class="mt-3 text-xs font-medium text-red-700">{{ languageService.t('reports.classes.settings.validation') }}</p>
              }
            </fieldset>
          </div>

          <div class="flex flex-wrap items-center justify-between gap-3 border-t border-[#ebe6ef] bg-[#faf9f6] px-6 py-4">
            <button class="rounded-md px-3 py-2 text-sm font-semibold text-[#6d3a91] hover:bg-[#f2ecf5]" type="button" (click)="resetClassSettings()">{{ languageService.t('reports.classes.settings.reset') }}</button>
            <div class="flex gap-3">
              <button class="rounded-md border border-[#d8d0df] px-4 py-2.5 text-sm font-semibold text-[#32124f]" type="button" (click)="closeClassSettings()">{{ languageService.t('workspace.cancel') }}</button>
              <button class="rounded-md bg-[#32124f] px-4 py-2.5 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-40" type="button" [disabled]="!classSettingsValid()" (click)="saveClassSettings()">{{ languageService.t('reports.classes.settings.apply') }}</button>
            </div>
          </div>
        </section>
      </div>
    }
  `,
  styles: `
    :host { display: block; }
    table th, table td { vertical-align: middle; }
    .report-layout {
      grid-template-columns: minmax(0, 1fr);
      transition: grid-template-columns 180ms ease;
    }
    .report-library {
      width: 280px;
      max-width: 100%;
      transition: width 180ms ease;
    }
    .report-library-collapsed .report-library { width: 76px; }
    .report-menu-button-collapsed {
      justify-content: center;
      padding-inline: 0.25rem;
    }
    @media (min-width: 1280px) {
      .report-layout { grid-template-columns: 280px minmax(0, 1fr); }
      .report-layout.report-library-collapsed { grid-template-columns: 76px minmax(0, 1fr); }
      .report-library { width: 100%; }
    }
    @media print {
      aside, .no-print { display: none !important; }
      .report-layout { display: block; }
      section { box-shadow: none !important; }
    }
  `,
})
export class StaticReportsComponent implements OnInit, OnDestroy {
  private readonly reportBuilder = inject(ReportBuilderService);
  private readonly overviewService = inject(DashboardOverviewService);
  private readonly studentsService = inject(StudentsService);
  private readonly workspaceFilters = inject(WorkspaceFiltersService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly languageService = inject(LanguageService);
  private readonly authService = inject(AuthenticationService);
  private readonly excel = inject(ReportExcelExportService);
  private readonly toast = inject(ToastService);

  readonly reportGroups: ReportMenuGroup[] = [
    {
      id: 'school-leadership', titleKey: 'reports.group.schoolLeadership', shortLabel: 'BGH', items: [
        { id: 'school-subject-grade-average', titleKey: 'reports.score.subjectGradeAverage.title', descriptionKey: 'reports.score.subjectGradeAverage.menu' },
        { id: 'school-score-histogram', titleKey: 'reports.score.histogram.title', descriptionKey: 'reports.score.histogram.menu' },
        { id: 'school-current-course-score', titleKey: 'reports.score.currentCourseScore.title', descriptionKey: 'reports.score.currentCourseScore.menu' },
        { id: 'school-top-students', titleKey: 'reports.score.topStudents.title', descriptionKey: 'reports.score.topStudents.menu' },
        { id: 'school-bottom-students', titleKey: 'reports.score.bottomStudents.title', descriptionKey: 'reports.score.bottomStudents.menu' },
        { id: 'school-learning-engagement', titleKey: 'reports.leadership.engagement.title', descriptionKey: 'reports.leadership.engagement.menu' },
      ],
    },
    {
      id: 'subject-leadership', titleKey: 'reports.group.subjectLeadership', shortLabel: 'BCM', items: [
        { id: 'subject-band-by-subject', titleKey: 'reports.score.subjectDistribution.title', descriptionKey: 'reports.score.subjectDistribution.menu' },
        { id: 'subject-band-by-grade', titleKey: 'reports.score.gradeDistribution.title', descriptionKey: 'reports.score.gradeDistribution.menu' },
        { id: 'subject-band-by-class', titleKey: 'reports.score.classDistribution.title', descriptionKey: 'reports.score.classDistribution.menu' },
        { id: 'subject-assessment-average', titleKey: 'reports.score.assessmentAverage.title', descriptionKey: 'reports.score.assessmentAverage.menu' },
        { id: 'subject-class-results', titleKey: 'reports.score.classSubjectResults.title', descriptionKey: 'reports.score.classSubjectResults.menu' },
        { id: 'school-assessment-results', titleKey: 'reports.leadership.assessment.title', descriptionKey: 'reports.leadership.assessment.menu' },
      ],
    },
    {
      id: 'learning', titleKey: 'reports.group.learning', shortLabel: 'HT', items: [
        { id: 'learning-diligence', titleKey: 'reports.learning.title', descriptionKey: 'reports.learning.description' },
        { id: 'support-classes', titleKey: 'reports.classes.title', descriptionKey: 'reports.classes.description' },
        { id: 'missing-frequency', titleKey: 'reports.learningReview.missing-frequency.title', descriptionKey: 'reports.learningReview.missing-frequency.menu' },
        { id: 'homework-achievement', titleKey: 'reports.learningReview.homework-achievement.title', descriptionKey: 'reports.learningReview.homework-achievement.menu' },
        { id: 'reading-completion', titleKey: 'reports.learningReview.reading-completion.title', descriptionKey: 'reports.learningReview.reading-completion.menu' },
        { id: 'grading-coverage', titleKey: 'reports.learningReview.grading-coverage.title', descriptionKey: 'reports.learningReview.grading-coverage.menu' },
      ],
    },
    {
      id: 'support', titleKey: 'reports.group.support', shortLabel: 'HT', items: [
        { id: 'support-students', titleKey: 'reports.students.title', descriptionKey: 'reports.students.description' },
      ],
    },
    {
      id: 'behaviour', titleKey: 'reports.group.behaviour', shortLabel: 'NN', items: [
        {
          id: 'behaviour-analysis',
          titleKey: 'reports.behaviour.title',
          descriptionKey: 'reports.behaviour.menuDescription',
        },
      ],
    },
    {
      id: 'mental', titleKey: 'reports.group.mental', shortLabel: 'TL', awaitingData: true, items: [
        this.supplementalMenuItem('mental-wellbeing', 'mentalWellbeing', {
          moduleId: 'student-mental-health', dimension: 'field', dimensionField: 'risk_level',
          primaryField: 'concern_type', secondaryField: 'risk_level', dateField: 'record_date', attentionMode: 'mental-health',
        }),
      ],
    },
    {
      id: 'health', titleKey: 'reports.group.health', shortLabel: 'YT', awaitingData: true, items: [
        { id: 'health-diagnoses', titleKey: 'reports.health.diagnoses.title', descriptionKey: 'reports.health.diagnoses.menuDescription' },
        { id: 'health-frequent-students', titleKey: 'reports.health.frequent-students.title', descriptionKey: 'reports.health.frequent-students.menuDescription' },
        { id: 'health-student-visits', titleKey: 'reports.health.student-visits.title', descriptionKey: 'reports.health.student-visits.menuDescription' },
      ],
    },
    {
      id: 'meals', titleKey: 'reports.group.meals', shortLabel: 'SA', items: [
        { id: 'meal-ordered-no-ticket', titleKey: 'reports.meals.ordered-no-ticket.title', descriptionKey: 'reports.meals.ordered-no-ticket.description' },
        { id: 'meal-ordered-not-eaten', titleKey: 'reports.meals.ordered-not-eaten.title', descriptionKey: 'reports.meals.ordered-not-eaten.description' },
        { id: 'meal-no-order', titleKey: 'reports.meals.no-order.title', descriptionKey: 'reports.meals.no-order.description' },
        { id: 'meal-coffee-frequency', titleKey: 'reports.meals.coffee-frequency.title', descriptionKey: 'reports.meals.coffee-frequency.description' },
      ],
    },
    {
      id: 'projects', titleKey: 'reports.group.projects', shortLabel: 'DA', awaitingData: true, items: [
        this.supplementalMenuItem('learning-projects', 'learningProjects', {
          moduleId: 'student-projects', dimension: 'field', dimensionField: 'status',
          primaryField: 'project_name', secondaryField: 'status', dateField: 'activity_date', attentionMode: 'project',
        }),
        this.supplementalMenuItem('student-activities', 'studentActivities', {
          moduleId: 'student-projects', dimension: 'field', dimensionField: 'subject',
          primaryField: 'project_name', secondaryField: 'teacher_notes', dateField: 'activity_date', attentionMode: 'project',
        }),
      ],
    },
    {
      id: 'bus', titleKey: 'reports.group.bus', shortLabel: 'XB', awaitingData: true, items: [
        this.supplementalMenuItem('bus-safety', 'busSafety', {
          moduleId: 'student-bus-attendance', dimension: 'field', dimensionField: 'violation_type',
          primaryField: 'violation_type', secondaryField: 'route_name', dateField: 'attendance_date', attentionMode: 'bus',
        }),
      ],
    },
  ];

  readonly visibleReportGroups = computed(() => this.reportGroups.filter((group) => {
    if (group.id === 'mental' || group.id === 'health') return this.authService.canAccess('health');
    if (group.id === 'bus') return this.authService.canAccess('bus');
    return this.authService.canAccess('academic');
  }).sort((a, b) => Number(a.awaitingData ?? false) - Number(b.awaitingData ?? false)));

  readonly activeReport = signal<StaticReportId>('learning-diligence');
  readonly activeReportAwaitingData = computed(() => this.visibleReportGroups().some(group =>
    group.awaitingData && group.items.some(report => report.id === this.activeReport()),
  ));
  readonly reportMenuCollapsed = signal(false);
  readonly expandedReportGroups = signal<ReadonlySet<string>>(new Set());
  readonly years = signal<string[]>([]);
  readonly terms = signal<string[]>([]);
  readonly metrics = signal<MetricVariable[]>([]);
  readonly overview = signal<DashboardOverview | null>(null);
  readonly supportStudents = signal<AtRiskStudent[]>([]);
  readonly loading = signal(true);
  readonly reportLoading = signal(false);
  readonly exporting = signal(false);
  readonly loadFailed = signal(false);
  readonly studentsLoading = signal(false);
  readonly studentsLoadFailed = signal(false);
  readonly studentPage = signal(1);
  readonly studentPageSize = signal(20);
  readonly studentTotalCount = signal(0);
  readonly studentTotalPages = signal(0);
  readonly classPage = signal(1);
  readonly classPageSize = signal(10);
  readonly classSearch = signal('');
  readonly cohortView = signal<CohortView>('grade');
  readonly classSettingsOpen = signal(false);
  readonly greenMaxRate = signal(0);
  readonly amberMaxRate = signal(10);
  draftGreenMaxRate = 0;
  draftAmberMaxRate = 10;
  readonly classDrilldownMounted = signal(false);
  readonly classDrilldownOpen = signal(false);
  readonly classDrilldownLoading = signal(false);
  readonly classDrilldownLoadFailed = signal(false);
  readonly classDrilldownContext = signal<ClassDrilldownContext | null>(null);
  readonly classDrilldownStudents = signal<StudentListItem[]>([]);
  readonly classDrilldownSearch = signal('');
  readonly classDrilldownPage = signal(1);
  readonly classDrilldownTotalCount = signal(0);
  readonly classDrilldownTotalPages = signal(0);
  selectedYear = '';
  activityPeriod: ActivityPeriodSelection = { month: null, weekStart: null };
  selectedTerm = 'all';
  readonly studentSearch = signal('');
  private currentFilters: WorkspaceFilterSet = defaultFilterSet();
  private requestId = 0;
  private initialLoadFinished = false;
  private studentRequestId = 0;
  private studentSearchTimer: number | undefined;
  private classDrilldownRequestId = 0;
  private classDrilldownSearchTimer: number | undefined;
  private classDrilldownUnmountTimer: number | undefined;
  private readonly classDrilldownPageSize = 20;

  periodLabel(): string {
    const year = this.selectedYear;
    const term = this.selectedTerm;
    if (!year) return this.languageService.t('reports.currentPeriod');
    return `${this.formatYear(year)} · ${this.termLabel(term)}`;
  }

  readonly classRows = computed<SupportCohortRow[]>(() => {
    const overview = this.overview();
    if (!overview) return [];
    if (this.cohortView() === 'class') {
      return (overview.classDistribution ?? []).map((row) => ({
        key: `class:${row.grade}:${row.homeroom}`,
        label: row.homeroom === '—' ? this.languageService.t('reports.classes.unassignedClass') : row.homeroom,
        grade: row.grade,
        homeroom: row.homeroom,
        students: row.students,
        atRisk: row.atRisk,
        averageCanvasScorePercentage: row.averageCanvasScorePercentage,
      }));
    }
    return overview.gradeDistribution.map((row) => ({
      key: `grade:${row.grade}`,
      label: `${this.languageService.t('reports.grade')} ${row.grade}`,
      grade: row.grade,
      homeroom: '',
      students: row.students,
      atRisk: row.atRisk,
      averageCanvasScorePercentage: row.averageCanvasScorePercentage,
    }));
  });

  readonly filteredClassRows = computed(() => {
    const term = this.classSearch().trim().toLocaleLowerCase();
    if (!term) return this.classRows();
    return this.classRows().filter((row) =>
      row.label.toLocaleLowerCase().includes(term)
      || row.grade.toLocaleLowerCase().includes(term)
      || `${this.languageService.t('reports.grade')} ${row.grade}`.toLocaleLowerCase().includes(term),
    );
  });

  readonly classTotalPages = computed(() =>
    Math.max(1, Math.ceil(this.filteredClassRows().length / this.classPageSize())),
  );

  readonly paginatedClassRows = computed(() => {
    const start = (this.classPage() - 1) * this.classPageSize();
    return this.filteredClassRows().slice(start, start + this.classPageSize());
  });

  readonly filteredStudents = computed(() => this.supportStudents());

  readonly learningRows = computed<LearningReportRow[]>(() => {
    const submitted = this.metricValue('canvas-assignments-submitted');
    const graded = this.metricValue('canvas-submitted-assignments-graded');
    const missing = this.metricValue('canvas-assignments-missing');
    const late = this.metricValue('canvas-assignments-late');
    const expected = submitted === null || missing === null ? null : submitted + missing;
    return [
      this.learningRow('reports.learning.completion', submitted, expected, 'reports.volume.completion', 'reports.target.completion', false),
      this.learningRow('reports.learning.onTime', submitted === null || late === null ? null : Math.max(0, submitted - late), submitted, 'reports.volume.onTime', 'reports.target.onTime', false),
      this.learningRow('reports.learning.graded', graded, submitted, 'reports.volume.graded', 'reports.target.graded', false),
      this.learningRow('reports.learning.missing', missing, expected, 'reports.volume.missing', 'reports.target.missing', true),
    ];
  });

  readonly averageCanvasScorePercentage = computed(() =>
    this.metricValue('canvas-current-score-avg'),
  );

  readonly activeSupplementalReport = computed(() =>
    this.visibleReportGroups().flatMap((group) => group.items)
      .find((report) => report.id === this.activeReport())?.supplemental ?? null,
  );

  ngOnInit(): void {
    this.restoreReportStateFromQuery();
    this.loadClassStatusSettings();
    forkJoin({
      options: this.workspaceFilters.periodOptions().pipe(catchError(() => of({ academicYears: [] as string[], terms: [] as string[] }))),
      filters: this.workspaceFilters.load().pipe(catchError(() => of(defaultFilterSet()))),
    }).subscribe(({ options, filters }) => {
      this.years.set(options.academicYears);
      this.terms.set(options.terms);
      this.selectedYear = options.academicYears.includes(filters.year) ? filters.year : options.academicYears[0] ?? '';
      this.selectedTerm = filters.semester === 'all' || options.terms.includes(filters.semester) ? filters.semester : 'all';
      const params = this.route.snapshot.queryParamMap;
      const legacyPrefix = this.activeReport() === 'learning-diligence' ? 'ld'
        : params.get('lrReport') === this.activeReport() ? 'lr'
        : params.get('scoreReport') === this.activeReport() ? 'score' : 'lead';
      this.activityPeriod = normalizeActivityPeriod(this.selectedYear, {
        month: params.has('activityMonth') ? Number(params.get('activityMonth'))
          : params.has(legacyPrefix + 'Month') ? Number(params.get(legacyPrefix + 'Month')) : null,
        weekStart: params.get('activityWeekStart') ?? params.get(legacyPrefix + 'WeekStart'),
      });
      this.currentFilters = { ...filters, year: this.selectedYear, semester: this.selectedTerm };
      this.loadReportData();
    });
  }

  ngOnDestroy(): void {
    if (this.studentSearchTimer !== undefined) window.clearTimeout(this.studentSearchTimer);
    if (this.classDrilldownSearchTimer !== undefined) window.clearTimeout(this.classDrilldownSearchTimer);
    if (this.classDrilldownUnmountTimer !== undefined) window.clearTimeout(this.classDrilldownUnmountTimer);
  }

  @HostListener('document:keydown.escape')
  closeClassDrilldownOnEscape(): void {
    if (this.classSettingsOpen()) this.closeClassSettings();
    else if (this.classDrilldownMounted()) this.closeClassDrilldown();
  }

  selectReport(report: StaticReportId): void {
    const changed = report !== this.activeReport();
    this.activeReport.set(report);
    const group = this.visibleReportGroups().find((candidate) => candidate.items.some((item) => item.id === report));
    if (group && !this.expandedReportGroups().has(group.id)) {
      this.expandedReportGroups.update((expanded) => new Set(expanded).add(group.id));
    }
    this.syncReportStateToQuery(changed);
    if (changed) this.loadReportData();
  }

  toggleReportMenu(): void {
    this.reportMenuCollapsed.update((collapsed) => !collapsed);
    this.syncReportStateToQuery();
  }

  isReportGroupExpanded(groupId: string): boolean {
    return this.expandedReportGroups().has(groupId);
  }

  toggleReportGroup(groupId: string): void {
    if (this.reportMenuCollapsed()) this.reportMenuCollapsed.set(false);
    this.expandedReportGroups.update((expanded) => {
      const next = new Set(expanded);
      if (next.has(groupId)) next.delete(groupId);
      else next.add(groupId);
      return next;
    });
    this.syncReportStateToQuery();
  }

  selectCohortView(view: CohortView): void {
    this.cohortView.set(view);
    this.classPage.set(1);
    this.syncReportStateToQuery();
  }

  onClassSearchChange(value: string): void {
    this.classSearch.set(value);
    this.classPage.set(1);
  }

  clearClassSearch(): void {
    this.classSearch.set('');
    this.classPage.set(1);
  }

  openClassSettings(): void {
    this.draftGreenMaxRate = this.greenMaxRate();
    this.draftAmberMaxRate = this.amberMaxRate();
    this.classSettingsOpen.set(true);
  }

  closeClassSettings(): void {
    this.classSettingsOpen.set(false);
  }

  resetClassSettings(): void {
    this.draftGreenMaxRate = 0;
    this.draftAmberMaxRate = 10;
  }

  classSettingsValid(): boolean {
    return Number.isFinite(this.draftGreenMaxRate)
      && Number.isFinite(this.draftAmberMaxRate)
      && this.draftGreenMaxRate >= 0
      && this.draftAmberMaxRate > this.draftGreenMaxRate
      && this.draftAmberMaxRate <= 100;
  }

  saveClassSettings(): void {
    if (!this.classSettingsValid()) return;
    this.greenMaxRate.set(Number(this.draftGreenMaxRate));
    this.amberMaxRate.set(Number(this.draftAmberMaxRate));
    window.localStorage.setItem('reports.classStatusThresholds', JSON.stringify({
      greenMaxRate: this.greenMaxRate(),
      amberMaxRate: this.amberMaxRate(),
    }));
    this.closeClassSettings();
  }

  openClassDrilldown(row: SupportCohortRow, kind: ClassDrilldownKind): void {
    if ((kind === 'students' ? row.students : row.atRisk) <= 0) return;
    if (this.classDrilldownUnmountTimer !== undefined) {
      window.clearTimeout(this.classDrilldownUnmountTimer);
      this.classDrilldownUnmountTimer = undefined;
    }
    this.classDrilldownContext.set({
      label: row.label,
      grade: row.grade,
      homeroom: row.homeroom,
      view: this.cohortView(),
      kind,
    });
    this.classDrilldownSearch.set('');
    this.classDrilldownStudents.set([]);
    this.classDrilldownPage.set(1);
    this.classDrilldownTotalCount.set(0);
    this.classDrilldownTotalPages.set(0);
    this.classDrilldownMounted.set(true);
    window.requestAnimationFrame(() => this.classDrilldownOpen.set(true));
    this.loadClassDrilldown(1);
  }

  closeClassDrilldown(): void {
    if (this.classDrilldownSearchTimer !== undefined) {
      window.clearTimeout(this.classDrilldownSearchTimer);
      this.classDrilldownSearchTimer = undefined;
    }
    this.classDrilldownRequestId++;
    this.classDrilldownOpen.set(false);
    this.classDrilldownUnmountTimer = window.setTimeout(() => {
      this.classDrilldownMounted.set(false);
      this.classDrilldownContext.set(null);
      this.classDrilldownUnmountTimer = undefined;
    }, 300);
  }

  loadClassDrilldown(page: number): void {
    const context = this.classDrilldownContext();
    if (!context || page < 1 || (this.classDrilldownTotalPages() > 0 && page > this.classDrilldownTotalPages())) return;

    const requestId = ++this.classDrilldownRequestId;
    this.classDrilldownLoading.set(true);
    this.classDrilldownLoadFailed.set(false);
    const request$: Observable<ClassDrilldownPage> = context.kind === 'support'
      ? this.studentsService.atRisk(
          page,
          this.classDrilldownPageSize,
          this.classDrilldownSearch(),
          context.grade,
          this.selectedYear,
          this.selectedTerm,
          context.homeroom,
        )
      : this.studentsService.list(
          page,
          this.classDrilldownPageSize,
          this.classDrilldownSearch(),
          context.grade,
          false,
          context.homeroom,
        );

    request$.subscribe({
      next: (response) => {
        if (requestId !== this.classDrilldownRequestId) return;
        this.classDrilldownStudents.set(response.items);
        this.classDrilldownPage.set(response.page);
        this.classDrilldownTotalCount.set(response.totalCount);
        this.classDrilldownTotalPages.set(response.totalPages);
        this.classDrilldownLoading.set(false);
      },
      error: () => {
        if (requestId !== this.classDrilldownRequestId) return;
        this.classDrilldownLoadFailed.set(true);
        this.classDrilldownLoading.set(false);
      },
    });
  }

  onClassDrilldownSearch(value: string): void {
    this.classDrilldownSearch.set(value);
    if (this.classDrilldownSearchTimer !== undefined) window.clearTimeout(this.classDrilldownSearchTimer);
    this.classDrilldownSearchTimer = window.setTimeout(() => {
      this.classDrilldownSearchTimer = undefined;
      this.loadClassDrilldown(1);
    }, 300);
  }

  clearClassDrilldownSearch(): void {
    if (this.classDrilldownSearchTimer !== undefined) {
      window.clearTimeout(this.classDrilldownSearchTimer);
      this.classDrilldownSearchTimer = undefined;
    }
    this.classDrilldownSearch.set('');
    this.loadClassDrilldown(1);
  }

  classDrilldownTitle(): string {
    const key = this.classDrilldownContext()?.kind === 'support'
      ? 'reports.drilldown.supportTitle'
      : 'reports.drilldown.studentsTitle';
    return this.languageService.t(key);
  }

  classDrilldownOpenLabel(row: SupportCohortRow, kind: ClassDrilldownKind): string {
    const key = kind === 'support'
      ? 'reports.drilldown.openCohortSupport'
      : 'reports.drilldown.openCohortStudents';
    return this.interpolate(key, { group: row.label });
  }

  classDrilldownPageLabel(): string {
    return this.pageLabel(
      this.classDrilldownPage(),
      this.classDrilldownTotalPages(),
      this.classDrilldownTotalCount(),
    );
  }

  studentInitials(name: string): string {
    return name.trim().split(/\s+/).slice(-2).map((part) => part[0]?.toUpperCase()).join('') || 'S';
  }

  onStudentSearchChange(value: string): void {
    this.studentSearch.set(value);
    if (this.studentSearchTimer !== undefined) window.clearTimeout(this.studentSearchTimer);
    this.studentSearchTimer = window.setTimeout(() => this.loadSupportStudents(1), 300);
  }

  clearStudentSearch(): void {
    if (this.studentSearchTimer !== undefined) window.clearTimeout(this.studentSearchTimer);
    this.studentSearchTimer = undefined;
    this.studentSearch.set('');
    this.loadSupportStudents(1);
  }

  loadSupportStudents(page: number): void {
    const requestId = ++this.studentRequestId;
    this.studentsLoading.set(true);
    this.studentsLoadFailed.set(false);
    this.studentsService.atRisk(
      page,
      this.studentPageSize(),
      this.studentSearch(),
      '',
      this.selectedYear,
      this.selectedTerm,
      '', false, this.activityPeriod,
    ).subscribe({
      next: (response) => {
        if (requestId !== this.studentRequestId) return;
        this.applyStudentPage(response);
        this.studentsLoading.set(false);
      },
      error: () => {
        if (requestId !== this.studentRequestId) return;
        this.studentsLoadFailed.set(true);
        this.studentsLoading.set(false);
      },
    });
  }

  changeStudentPageSize(event: Event): void {
    this.studentPageSize.set(Number((event.target as HTMLSelectElement).value));
    this.loadSupportStudents(1);
  }

  changeClassPageSize(event: Event): void {
    this.classPageSize.set(Number((event.target as HTMLSelectElement).value));
    this.classPage.set(1);
  }

  previousClassPage(): void {
    if (this.classPage() > 1) this.classPage.update((page) => page - 1);
  }

  nextClassPage(): void {
    if (this.classPage() < this.classTotalPages()) this.classPage.update((page) => page + 1);
  }

  studentPageLabel(): string {
    return this.pageLabel(this.studentPage(), this.studentTotalPages(), this.studentTotalCount());
  }

  classPageLabel(): string {
    return this.pageLabel(this.classPage(), this.classTotalPages(), this.filteredClassRows().length);
  }

  private pageLabel(page: number, pages: number, total: number): string {
    return this.languageService.t('pagination.pageOf')
      .replace('{page}', String(page))
      .replace('{pages}', String(pages))
      .replace('{total}', String(total));
  }

  private applyStudentPage(response: AtRiskStudentsPage): void {
    this.supportStudents.set(response.items);
    this.studentPage.set(response.page);
    this.studentTotalCount.set(response.totalCount);
    this.studentTotalPages.set(response.totalPages);
  }

  onPeriodChange(): void {
    this.activityPeriod = normalizeActivityPeriod(this.selectedYear, this.activityPeriod);
    this.currentFilters = { ...this.currentFilters, year: this.selectedYear, semester: this.selectedTerm };
    this.workspaceFilters.save(this.currentFilters);
    this.syncReportStateToQuery();
    this.loadReportData();
  }

  onActivityPeriodChange(selection: ActivityPeriodSelection): void {
    this.activityPeriod = selection;
    this.syncReportStateToQuery();
    this.loadReportData();
  }

  supportsActivityPeriod(): boolean {
    const report = this.activeReport();
    return report !== 'support-classes' && report !== 'school-current-course-score';
  }

  activityPeriodHintKey(): string {
    return this.reportGroups.some(group => ['learning', 'school-leadership', 'subject-leadership'].includes(group.id)
      && group.items.some(item => item.id === this.activeReport()))
      ? 'reports.activityPeriod.hint' : 'reports.activityPeriod.recordsHint';
  }

  diligenceWeekLabel(): string {
    return activityWeeks(this.selectedYear).find(week => week.value === this.activityPeriod.weekStart)?.label
      ?? this.languageService.t('reports.activityPeriod.allWeeks');
  }

  activityPeriodLabel(): string {
    return `${this.languageService.t('reports.learningReview.month')}: ${this.activityPeriod.month ?? this.languageService.t('reports.learningReview.allMonths')} · ${this.languageService.t('reports.activityPeriod.week')}: ${this.diligenceWeekLabel()}`;
  }

  loadReportData(): void {
    const requestId = ++this.requestId;
    const report = this.activeReport();
    const needsMetrics = report === 'learning-diligence';
    const needsOverview = report === 'support-classes';
    const needsStudents = report === 'support-students';

    // Learning review, behaviour, health, and supplemental reports own their data requests. Render immediately
    // instead of blocking on unrelated Canvas, overview, and at-risk queries.
    if (!needsMetrics && !needsOverview && !needsStudents) {
      this.loading.set(false);
      this.reportLoading.set(false);
      this.loadFailed.set(false);
      this.initialLoadFinished = true;
      return;
    }

    this.loading.set(!this.initialLoadFinished);
    this.reportLoading.set(this.initialLoadFinished);
    this.loadFailed.set(false);

    const params: Record<string, string> = {
      metricIds: [
        'canvas-assignments-submitted',
        'canvas-submitted-assignments-graded',
        'canvas-assignments-missing',
        'canvas-assignments-late',
        'canvas-current-score-avg',
      ].join(','),
      ...(this.selectedYear ? { year: this.selectedYear } : {}),
      ...(this.selectedTerm && this.selectedTerm !== 'all' ? { term: this.selectedTerm } : {}),
      ...(needsMetrics && this.activityPeriod.month !== null ? { month: String(this.activityPeriod.month) } : {}),
      ...(needsMetrics && this.activityPeriod.weekStart ? { weekStart: this.activityPeriod.weekStart } : {}),
    };

    forkJoin({
      metrics: needsMetrics && this.authService.canAccess('academic')
        ? this.reportBuilder.loadMetricVariables(params) : of(null as MetricVariable[] | null),
      overview: needsOverview
        ? this.overviewService.overview(this.selectedYear || undefined, this.selectedTerm)
        : of(null as DashboardOverview | null),
      students: needsStudents && this.authService.canAccess('academic') ? this.studentsService.atRisk(
        1,
        this.studentPageSize(),
        this.studentSearch(),
        '',
        this.selectedYear,
        this.selectedTerm,
        '', false, this.activityPeriod,
      ) : of(null as AtRiskStudentsPage | null),
    }).subscribe({
      next: ({ metrics, overview, students }) => {
        if (requestId !== this.requestId) return;
        if (metrics) this.metrics.set(metrics);
        if (overview) {
          this.overview.set(overview);
          this.classPage.set(1);
          if (!this.years().includes(overview.periodYear)) this.years.update((years) => [overview.periodYear, ...years]);
          this.selectedYear = overview.periodYear;
          this.selectedTerm = overview.periodTerm;
        }
        if (students) this.applyStudentPage(students);
        this.initialLoadFinished = true;
        this.loading.set(false);
        this.reportLoading.set(false);
      },
      error: () => {
        if (requestId !== this.requestId) return;
        this.initialLoadFinished = true;
        this.loadFailed.set(true);
        this.loading.set(false);
        this.reportLoading.set(false);
      },
    });
  }

  private restoreReportStateFromQuery(): void {
    const params = this.route.snapshot.queryParamMap;
    const groups = this.visibleReportGroups();
    const availableReports = groups.flatMap((group) => group.items.map((item) => item.id));
    const legacyBehaviourReports = new Set([
      'culture-evaluation', 'discipline-compliance', 'school-culture',
      'facility-culture', 'cafeteria-culture', 'behaviour-audit',
    ]);
    const requestedValue = params.get('report');
    const requestedReport = (requestedValue && legacyBehaviourReports.has(requestedValue)
      ? 'behaviour-analysis'
      : requestedValue) as StaticReportId | null;
    const activeReport = requestedReport && availableReports.includes(requestedReport)
      ? requestedReport
      : availableReports.includes(this.activeReport()) ? this.activeReport() : availableReports[0];
    if (!activeReport) return;
    this.activeReport.set(activeReport);

    const requestedCohort = params.get('cohort');
    if (requestedCohort === 'grade' || requestedCohort === 'class') {
      this.cohortView.set(requestedCohort);
    }
    this.reportMenuCollapsed.set(params.get('library') === 'collapsed');

    const requestedGroups = params.get('groups');
    if (requestedGroups === 'none') {
      this.expandedReportGroups.set(new Set());
      return;
    }

    const availableGroups = new Set(groups.map((group) => group.id));
    if (requestedGroups) {
      this.expandedReportGroups.set(new Set(
        requestedGroups.split(',').filter((groupId) => availableGroups.has(groupId)),
      ));
      return;
    }

    if (requestedReport && availableReports.includes(requestedReport)) {
      const activeGroup = groups.find((group) => group.items.some((item) => item.id === activeReport));
      if (activeGroup) this.expandedReportGroups.set(new Set([activeGroup.id]));
    }
  }

  private syncReportStateToQuery(clearLearningFilters = false): void {
    const expandedGroups = this.reportGroups
      .filter((group) => this.expandedReportGroups().has(group.id))
      .map((group) => group.id);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        report: this.activeReport(),
        groups: expandedGroups.length ? expandedGroups.join(',') : 'none',
        cohort: this.cohortView(),
        library: this.reportMenuCollapsed() ? 'collapsed' : null,
        activityMonth: this.activityPeriod.month,
        activityWeekStart: this.activityPeriod.weekStart,
        ldMonth: null, ldWeekStart: null,
        ...(clearLearningFilters ? {
          lrReport: null, lrCourse: null, lrGroup: null, lrAssignment: null,
          lrSubaccount: null, lrCategory: null,
          lrMonth: null, lrWeekStart: null, lrMaxMissing: null, lrMinScore: null,
          lrMinCompletion: null, lrPage: null,
        } : {}),
      },
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }

  private supplementalMenuItem(
    id: StaticReportId,
    translationName: string,
    definition: Omit<SupplementalReportDefinition, 'id' | 'titleKey' | 'descriptionKey'>,
  ): ReportMenuItem {
    const titleKey = `reports.${translationName}.title`;
    const descriptionKey = `reports.${translationName}.description`;
    return {
      id,
      titleKey,
      descriptionKey,
      supplemental: { id, titleKey, descriptionKey, ...definition },
    };
  }

  private loadClassStatusSettings(): void {
    try {
      const stored = window.localStorage.getItem('reports.classStatusThresholds');
      if (!stored) return;
      const value = JSON.parse(stored) as { greenMaxRate?: unknown; amberMaxRate?: unknown };
      const green = Number(value.greenMaxRate);
      const amber = Number(value.amberMaxRate);
      if (Number.isFinite(green) && Number.isFinite(amber) && green >= 0 && amber > green && amber <= 100) {
        this.greenMaxRate.set(green);
        this.amberMaxRate.set(amber);
      }
    } catch {
      window.localStorage.removeItem('reports.classStatusThresholds');
    }
  }

  private interpolate(key: string, values: Record<string, string | number>): string {
    return Object.entries(values).reduce(
      (text, [name, value]) => text.replaceAll(`{${name}}`, String(value)),
      this.languageService.t(key),
    );
  }

  private metricValue(id: string): number | null {
    return this.metrics().find((metric) => metric.id === id)?.value ?? null;
  }

  private learningRow(
    indicatorKey: string,
    numerator: number | null,
    denominator: number | null,
    volumeKey: string,
    targetKey: string,
    inverse: boolean,
  ): LearningReportRow {
    const value = numerator === null || denominator === null || denominator <= 0 ? null : numerator / denominator * 100;
    return { indicatorKey, value, numerator, denominator, volumeKey, targetKey, status: this.learningStatus(value, inverse), inverse };
  }

  private learningStatus(value: number | null, inverse: boolean): ReportStatus | null {
    if (value === null) return null;
    if (inverse) return value <= 5 ? 'green' : value <= 12 ? 'amber' : 'red';
    return value >= 90 ? 'green' : value >= 75 ? 'amber' : 'red';
  }

  classRiskRate(row: SupportCohortRow): string {
    return row.students > 0 ? this.formatPercent(row.atRisk / row.students * 100) : '—';
  }

  classStatus(row: SupportCohortRow): ReportStatus {
    const rate = row.students > 0 ? row.atRisk / row.students * 100 : 0;
    return rate <= this.greenMaxRate() ? 'green' : rate <= this.amberMaxRate() ? 'amber' : 'red';
  }

  classFollowUpKey(row: SupportCohortRow): string {
    return this.languageService.t(`reports.classes.followUp.${this.classStatus(row)}`);
  }

  classStatusExplanation(row: SupportCohortRow): string {
    return this.interpolate('reports.classes.settings.rowExplanation', {
      count: row.atRisk,
      total: row.students,
      rate: this.classRiskRate(row),
      status: this.statusLabel(this.classStatus(row)),
      green: this.formatThreshold(this.greenMaxRate()),
      amber: this.formatThreshold(this.amberMaxRate()),
    });
  }

  classRulesSummary(): string {
    return this.interpolate('reports.classes.settings.summary', {
      green: this.formatThreshold(this.greenMaxRate()),
      amber: this.formatThreshold(this.amberMaxRate()),
    });
  }

  statusLabel(status: ReportStatus | null): string {
    return this.languageService.t(`reports.status.${status ?? 'unavailable'}`);
  }

  statusBadgeClass(status: ReportStatus | null): string {
    if (status === 'green') return 'bg-emerald-100 text-emerald-800';
    if (status === 'amber') return 'bg-amber-100 text-amber-800';
    if (status === 'red') return 'bg-red-100 text-red-800';
    return 'bg-slate-100 text-slate-600';
  }

  riskBadgeClass(level: string): string {
    if (level === 'High') return 'bg-red-100 text-red-800';
    if (level === 'Medium') return 'bg-amber-100 text-amber-800';
    if (level === 'Low') return 'bg-sky-100 text-sky-800';
    return 'bg-emerald-100 text-emerald-800';
  }

  riskLabel(level: string): string {
    return this.languageService.t(`reports.risk.${level}`);
  }

  formatPercent(value: number | null): string {
    if (value === null || !Number.isFinite(value)) return this.languageService.t('reports.status.unavailable');
    const locale = this.languageService.language() === 'vi' ? 'vi-VN' : 'en-US';
    return new Intl.NumberFormat(locale, { maximumFractionDigits: 1 }).format(value) + '%';
  }

  formatThreshold(value: number): string {
    return this.formatPercent(value);
  }

  formatVolume(row: LearningReportRow): string {
    if (row.numerator === null || row.denominator === null) return '—';
    const locale = this.languageService.language() === 'vi' ? 'vi-VN' : 'en-US';
    const format = (value: number) => Math.round(value).toLocaleString(locale);
    return this.languageService.t(row.volumeKey)
      .replace('{count}', format(row.numerator))
      .replace('{total}', format(row.denominator));
  }

  formatYear(year: string): string {
    return year.replace('-', '–');
  }

  termLabel(term: string): string {
    const key = `dashboard.period.term.${term}`;
    const translated = this.languageService.t(key);
    return translated === key ? term : translated;
  }

  canExportStaticReport(): boolean {
    if (this.activeReport() === 'learning-diligence') return this.learningRows().some(row => row.value !== null);
    if (this.activeReport() === 'support-classes') return this.filteredClassRows().length > 0;
    if (this.activeReport() === 'support-students') return this.studentTotalCount() > 0;
    return false;
  }

  async exportStaticReport(): Promise<void> {
    if (!this.canExportStaticReport() || this.exporting()) return;
    this.exporting.set(true);
    try {
      const report = this.activeReport();
      if (report === 'learning-diligence') {
        const title = this.languageService.t('reports.learning.title');
        await this.excel.download({
          fileName: `${title}-${this.selectedYear}-${this.selectedTerm}`,
          title,
          period: this.periodLabel(),
          filters: [
            [this.languageService.t('reports.learningReview.month'), this.activityPeriod.month ?? this.languageService.t('reports.learningReview.allMonths')],
            [this.languageService.t('reports.activityPeriod.week'), this.diligenceWeekLabel()],
            [this.languageService.t('reports.canvasAverage'), this.languageService.t('reports.activityPeriod.courseScoreHint')],
          ],
          columns: [
            { header: this.languageService.t('reports.table.indicator'), value: row => this.languageService.t(row.indicatorKey) },
            { header: this.languageService.t('reports.table.result'), value: row => row.value },
            { header: this.languageService.t('reports.table.volume'), value: row => this.formatVolume(row) },
            { header: this.languageService.t('reports.table.benchmark'), value: row => this.languageService.t(row.targetKey) },
            { header: this.languageService.t('reports.table.status'), value: row => this.statusLabel(row.status) },
          ],
          rows: this.learningRows(),
        });
        return;
      }

      if (report === 'support-classes') {
        const title = this.languageService.t('reports.classes.title');
        await this.excel.download({
          fileName: `${title}-${this.selectedYear}-${this.selectedTerm}`,
          title,
          period: this.periodLabel(),
          filters: [
            [this.languageService.t('reports.classes.viewLabel'), this.languageService.t(this.cohortView() === 'grade' ? 'reports.classes.gradeView' : 'reports.classes.classView')],
            [this.languageService.t('reports.classes.search'), this.classSearch()],
          ],
          columns: [
            { header: this.languageService.t(this.cohortView() === 'grade' ? 'reports.table.grade' : 'reports.table.class'), value: row => row.label },
            { header: this.languageService.t('reports.table.grade'), value: row => row.grade },
            { header: this.languageService.t('reports.table.students'), value: row => row.students },
            { header: this.languageService.t('reports.table.canvasAverage'), value: row => row.averageCanvasScorePercentage, format: 'percentage' },
            { header: this.languageService.t('reports.table.needSupport'), value: row => row.atRisk },
            { header: this.languageService.t('reports.table.riskRate'), value: row => row.students ? row.atRisk / row.students * 100 : null, format: 'percentage' },
            { header: this.languageService.t('reports.table.status'), value: row => this.statusLabel(this.classStatus(row)) },
            { header: this.languageService.t('reports.table.followUp'), value: row => this.classFollowUpKey(row) },
          ],
          rows: this.filteredClassRows(),
        });
        return;
      }

      if (report === 'support-students') {
        const year = this.selectedYear;
        const term = this.selectedTerm;
        const activityPeriod = { ...this.activityPeriod };
        const search = this.studentSearch();
        const periodLabel = this.periodLabel();
        const rows = await this.excel.collectPages(async (page, pageSize) => {
          const response = await firstValueFrom(this.studentsService.atRisk(
            page, pageSize, search, '', year, term, '', true, activityPeriod,
          ));
          return { items: response.items, totalCount: response.totalCount, totalPages: response.totalPages };
        });
        const title = this.languageService.t('reports.students.title');
        await this.excel.download({
          fileName: `${title}-${year}-${term}`,
          title,
          period: periodLabel,
          filters: [...activityPeriodExportFilters(year, activityPeriod, key => this.languageService.t(key)),
            [this.languageService.t('reports.search'), search],
            [this.languageService.t('reports.table.signals'), this.languageService.t('reports.activityPeriod.supportSignalsHint')]],
          columns: [
            { header: this.languageService.t('reports.table.student'), value: row => row.fullName },
            { header: 'SIS ID', value: row => row.sisStudentCode || row.sisUserId },
            { header: this.languageService.t('reports.table.grade'), value: row => row.gradeLevel },
            { header: this.languageService.t('reports.table.class'), value: row => row.homeroom },
            { header: this.languageService.t('reports.table.canvasAverage'), value: row => row.averageCanvasScorePercentage, format: 'percentage' },
            { header: this.languageService.t('reports.table.riskLevel'), value: row => this.riskLabel(row.riskLevel) },
            { header: this.languageService.t('reports.table.signals'), value: row => row.riskFlagCount },
            { header: this.languageService.t('reports.behaviour.positive'), value: row => row.positiveBehaviourCount },
            { header: this.languageService.t('reports.behaviour.concerning'), value: row => row.concerningBehaviourCount },
            { header: this.languageService.t('reports.students.progressReview'), value: row => row.flags.map(flag => `${flag.code}: ${flag.value}`).join('; ') },
          ],
          rows,
        });
      }
    } catch (error) {
      this.toast.error(this.languageService.t(error instanceof ReportExportLimitError ? 'reports.exportLimit' : 'reports.exportError'));
    } finally {
      this.exporting.set(false);
    }
  }

  printReport(): void {
    window.print();
  }
}
