import { NgClass, NgTemplateOutlet } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { catchError, forkJoin, of } from 'rxjs';
import { LanguageService } from '../core/language/language.service';
import {
  DashboardGrade,
  DashboardOverview,
  DashboardOverviewService,
} from '../dashboard/dashboard-overview.service';
import { AtRiskStudent, AtRiskStudentsPage, StudentsService } from '../students/students.service';
import { MetricVariable, ReportBuilderService } from './report-builder.service';
import {
  defaultFilterSet,
  WorkspaceFiltersService,
  WorkspaceFilterSet,
} from '../workspace/workspace-filters.service';

type StaticReportId = 'learning-diligence' | 'support-classes' | 'support-students';
type ReportStatus = 'green' | 'amber' | 'red';

interface ReportMenuItem {
  id: StaticReportId;
  titleKey: string;
  descriptionKey: string;
}

interface LearningReportRow {
  indicatorKey: string;
  value: number | null;
  numerator: number | null;
  denominator: number | null;
  targetKey: string;
  status: ReportStatus | null;
  inverse?: boolean;
}

@Component({
  selector: 'app-static-reports',
  imports: [FormsModule, NgClass, NgTemplateOutlet, RouterLink],
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
            @for (report of reportMenu; track report.id; let index = $index) {
              <button
                class="group flex w-full items-start gap-3 rounded-lg px-3 py-3.5 text-left transition"
                [class.report-menu-button-collapsed]="reportMenuCollapsed()"
                type="button"
                [class.bg-[#32124f]]="activeReport() === report.id"
                [class.text-white]="activeReport() === report.id"
                [class.text-[#32124f]]="activeReport() !== report.id"
                [attr.aria-current]="activeReport() === report.id ? 'page' : null"
                [attr.aria-label]="reportMenuCollapsed() ? languageService.t(report.titleKey) : null"
                [attr.title]="reportMenuCollapsed() ? languageService.t(report.titleKey) : null"
                (click)="selectReport(report.id)"
              >
                <span
                  class="grid h-8 w-8 shrink-0 place-items-center rounded-md text-xs font-bold"
                  [class.bg-[#f3c246]]="activeReport() === report.id"
                  [class.text-[#32124f]]="activeReport() === report.id"
                  [class.bg-[#f2ecf5]]="activeReport() !== report.id"
                  [class.text-[#6d3a91]]="activeReport() !== report.id"
                >{{ index + 1 }}</span>
                @if (!reportMenuCollapsed()) {
                  <span class="min-w-0">
                    <span class="block text-sm font-semibold">{{ languageService.t(report.titleKey) }}</span>
                    <span
                      class="mt-1 block text-xs leading-5"
                      [class.text-[#d8cbe2]]="activeReport() === report.id"
                      [class.text-[#817589]]="activeReport() !== report.id"
                    >{{ languageService.t(report.descriptionKey) }}</span>
                  </span>
                }
              </button>
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
              <div class="ml-auto flex min-h-10 items-center gap-2 rounded-md bg-[#f7f4f8] px-3 text-xs text-[#746b7b]">
                <span class="h-2 w-2 rounded-full bg-emerald-500"></span>
                {{ languageService.t('reports.liveData') }}
              </div>
            </div>
          </section>

          @if (loading()) {
            <section class="grid min-h-80 place-items-center rounded-xl border border-[#e7e0eb] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.07)]" role="status">
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
          } @else {
            @switch (activeReport()) {
              @case ('learning-diligence') {
                <ng-container [ngTemplateOutlet]="learningDiligence"></ng-container>
              }
              @case ('support-classes') {
                <ng-container [ngTemplateOutlet]="supportClasses"></ng-container>
              }
              @case ('support-students') {
                <ng-container [ngTemplateOutlet]="supportStudents"></ng-container>
              }
            }
          }
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
          <span class="rounded-full bg-[#f2ecf5] px-3 py-1 text-xs font-semibold text-[#6d3a91]">
            {{ languageService.t('reports.fixedDefinition') }}
          </span>
        </div>
      </div>
    </ng-template>

    <ng-template #learningDiligence>
      <section class="overflow-hidden rounded-xl border border-[#e7e0eb] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.07)]">
        <ng-container *ngTemplateOutlet="reportHeader; context: { titleKey: 'reports.learning.title', descriptionKey: 'reports.learning.longDescription' }"></ng-container>
        <div class="border-b border-[#eee8f1] bg-[#fdfbfe] px-5 py-4 sm:px-6">
          <div class="flex flex-wrap items-center justify-between gap-3">
            <div>
              <p class="text-sm font-semibold text-[#32124f]">{{ languageService.t('reports.canvasAverage') }}</p>
              <p class="mt-1 text-xs text-[#817589]">{{ languageService.t('reports.canvasAverageHint') }}</p>
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
                  <td class="px-4 py-4 text-right text-sm text-[#625969]">{{ formatVolume(row.numerator, row.denominator) }}</td>
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
        @if (classRows().length) {
          <div class="overflow-x-auto">
            <table class="w-full min-w-[880px] border-collapse text-left">
              <thead class="bg-[#f5f0f7] text-xs uppercase tracking-[0.08em] text-[#5d486b]">
                <tr>
                  <th class="px-5 py-3.5 font-bold">{{ languageService.t('reports.table.grade') }}</th>
                  <th class="px-4 py-3.5 text-right font-bold">{{ languageService.t('reports.table.students') }}</th>
                  <th class="px-4 py-3.5 text-right font-bold">{{ languageService.t('reports.table.canvasAverage') }}</th>
                  <th class="px-4 py-3.5 text-right font-bold">{{ languageService.t('reports.table.needSupport') }}</th>
                  <th class="px-4 py-3.5 text-right font-bold">{{ languageService.t('reports.table.riskRate') }}</th>
                  <th class="px-4 py-3.5 font-bold">{{ languageService.t('reports.table.status') }}</th>
                  <th class="px-5 py-3.5 font-bold">{{ languageService.t('reports.table.followUp') }}</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-[#eee8f1]">
                @for (row of paginatedClassRows(); track row.grade) {
                  <tr class="transition hover:bg-[#fdfbfe]">
                    <td class="px-5 py-4 font-semibold text-[#32124f]">{{ languageService.t('reports.grade') }} {{ row.grade }}</td>
                    <td class="px-4 py-4 text-right text-sm text-[#625969]">{{ row.students }}</td>
                    <td class="px-4 py-4 text-right text-sm font-semibold text-[#32124f]">{{ formatPercent(row.averageCanvasScorePercentage ?? null) }}</td>
                    <td class="px-4 py-4 text-right text-sm font-semibold text-[#32124f]">{{ row.atRisk }}</td>
                    <td class="px-4 py-4 text-right text-sm font-semibold text-[#32124f]">{{ classRiskRate(row) }}</td>
                    <td class="px-4 py-4"><span class="rounded-full px-2.5 py-1 text-xs font-bold" [ngClass]="statusBadgeClass(classStatus(row))">{{ statusLabel(classStatus(row)) }}</span></td>
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
        } @else {
          <div class="grid min-h-56 place-items-center p-8 text-center text-sm text-[#817589]">{{ languageService.t('reports.noData') }}</div>
        }
        <p class="border-t border-[#eee8f1] bg-[#faf8fb] px-5 py-3 text-xs leading-5 text-[#817589]">{{ languageService.t('reports.classes.note') }}</p>
      </section>
    </ng-template>

    <ng-template #supportStudents>
      <section class="overflow-hidden rounded-xl border border-[#e7e0eb] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.07)]">
        <ng-container *ngTemplateOutlet="reportHeader; context: { titleKey: 'reports.students.title', descriptionKey: 'reports.students.longDescription' }"></ng-container>
        <div class="no-print border-b border-[#eee8f1] px-5 py-4 sm:px-6">
          <label class="relative block max-w-md">
            <span class="sr-only">{{ languageService.t('reports.search') }}</span>
            <svg class="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[#8c8193]" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/></svg>
            <input class="w-full rounded-md border border-[#d8d0df] py-2.5 pl-10 pr-3 text-sm outline-none focus:border-[#8a5aa7] focus:ring-2 focus:ring-[#eee5f3]" type="search" [ngModel]="studentSearch()" (ngModelChange)="onStudentSearchChange($event)" [placeholder]="languageService.t('reports.search')" />
          </label>
        </div>
        @if (studentsLoading()) {
          <div class="grid min-h-56 place-items-center p-8" role="status">
            <span class="block h-9 w-9 animate-spin rounded-full border-4 border-[#e6dfea] border-t-[#6d3a91]"></span>
          </div>
        } @else if (studentsLoadFailed()) {
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
export class StaticReportsComponent implements OnInit {
  private readonly reportBuilder = inject(ReportBuilderService);
  private readonly overviewService = inject(DashboardOverviewService);
  private readonly studentsService = inject(StudentsService);
  private readonly workspaceFilters = inject(WorkspaceFiltersService);
  readonly languageService = inject(LanguageService);

  readonly reportMenu: ReportMenuItem[] = [
    { id: 'learning-diligence', titleKey: 'reports.learning.title', descriptionKey: 'reports.learning.description' },
    { id: 'support-classes', titleKey: 'reports.classes.title', descriptionKey: 'reports.classes.description' },
    { id: 'support-students', titleKey: 'reports.students.title', descriptionKey: 'reports.students.description' },
  ];

  readonly activeReport = signal<StaticReportId>('learning-diligence');
  readonly reportMenuCollapsed = signal(false);
  readonly years = signal<string[]>([]);
  readonly terms = signal<string[]>([]);
  readonly metrics = signal<MetricVariable[]>([]);
  readonly overview = signal<DashboardOverview | null>(null);
  readonly supportStudents = signal<AtRiskStudent[]>([]);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly studentsLoading = signal(false);
  readonly studentsLoadFailed = signal(false);
  readonly studentPage = signal(1);
  readonly studentPageSize = signal(20);
  readonly studentTotalCount = signal(0);
  readonly studentTotalPages = signal(0);
  readonly classPage = signal(1);
  readonly classPageSize = signal(10);
  selectedYear = '';
  selectedTerm = 'all';
  readonly studentSearch = signal('');
  private currentFilters: WorkspaceFilterSet = defaultFilterSet();
  private requestId = 0;
  private studentRequestId = 0;
  private studentSearchTimer: number | undefined;

  readonly periodLabel = computed(() => {
    const year = this.overview()?.periodYear || this.selectedYear;
    const term = this.overview()?.periodTerm || this.selectedTerm;
    if (!year) return this.languageService.t('reports.currentPeriod');
    return `${this.formatYear(year)} · ${this.termLabel(term)}`;
  });

  readonly classRows = computed(() => this.overview()?.gradeDistribution ?? []);

  readonly classTotalPages = computed(() =>
    Math.max(1, Math.ceil(this.classRows().length / this.classPageSize())),
  );

  readonly paginatedClassRows = computed(() => {
    const start = (this.classPage() - 1) * this.classPageSize();
    return this.classRows().slice(start, start + this.classPageSize());
  });

  readonly filteredStudents = computed(() => this.supportStudents());

  readonly learningRows = computed<LearningReportRow[]>(() => {
    const submitted = this.metricValue('canvas-assignments-submitted');
    const graded = this.metricValue('canvas-assignments-graded');
    const missing = this.metricValue('canvas-assignments-missing');
    const late = this.metricValue('canvas-assignments-late');
    const expected = submitted === null || missing === null ? null : submitted + missing;
    return [
      this.learningRow('reports.learning.completion', submitted, expected, 'reports.target.completion', false),
      this.learningRow('reports.learning.onTime', submitted === null || late === null ? null : Math.max(0, submitted - late), submitted, 'reports.target.onTime', false),
      this.learningRow('reports.learning.graded', graded, submitted, 'reports.target.graded', false),
      this.learningRow('reports.learning.missing', missing, expected, 'reports.target.missing', true),
    ];
  });

  readonly averageCanvasScorePercentage = computed(() =>
    this.metricValue('canvas-current-score-avg'),
  );

  ngOnInit(): void {
    forkJoin({
      options: this.workspaceFilters.periodOptions().pipe(catchError(() => of({ academicYears: [] as string[], terms: [] as string[] }))),
      filters: this.workspaceFilters.load().pipe(catchError(() => of(defaultFilterSet()))),
    }).subscribe(({ options, filters }) => {
      this.years.set(options.academicYears);
      this.terms.set(options.terms);
      this.selectedYear = options.academicYears.includes(filters.year) ? filters.year : options.academicYears[0] ?? '';
      this.selectedTerm = filters.semester === 'all' || options.terms.includes(filters.semester) ? filters.semester : 'all';
      this.currentFilters = { ...filters, year: this.selectedYear, semester: this.selectedTerm };
      this.loadReportData();
    });
  }

  selectReport(report: StaticReportId): void {
    this.activeReport.set(report);
  }

  toggleReportMenu(): void {
    this.reportMenuCollapsed.update((collapsed) => !collapsed);
  }

  onStudentSearchChange(value: string): void {
    this.studentSearch.set(value);
    if (this.studentSearchTimer !== undefined) window.clearTimeout(this.studentSearchTimer);
    this.studentSearchTimer = window.setTimeout(() => this.loadSupportStudents(1), 300);
  }

  loadSupportStudents(page: number): void {
    const requestId = ++this.studentRequestId;
    this.studentsLoading.set(true);
    this.studentsLoadFailed.set(false);
    this.studentsService.atRisk(page, this.studentPageSize(), this.studentSearch()).subscribe({
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
    return this.pageLabel(this.classPage(), this.classTotalPages(), this.classRows().length);
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
    this.currentFilters = { ...this.currentFilters, year: this.selectedYear, semester: this.selectedTerm };
    this.workspaceFilters.save(this.currentFilters);
    this.loadReportData();
  }

  loadReportData(): void {
    const requestId = ++this.requestId;
    this.loading.set(true);
    this.loadFailed.set(false);
    const params: Record<string, string> = {
      metricIds: [
        'canvas-assignments-submitted',
        'canvas-assignments-graded',
        'canvas-assignments-missing',
        'canvas-assignments-late',
        'canvas-current-score-avg',
      ].join(','),
      ...(this.selectedYear ? { year: this.selectedYear } : {}),
      ...(this.selectedTerm && this.selectedTerm !== 'all' ? { term: this.selectedTerm } : {}),
    };

    forkJoin({
      metrics: this.reportBuilder.loadMetricVariables(params),
      overview: this.overviewService.overview(this.selectedYear || undefined, this.selectedTerm),
      students: this.studentsService.atRisk(1, this.studentPageSize(), this.studentSearch()),
    }).subscribe({
      next: ({ metrics, overview, students }) => {
        if (requestId !== this.requestId) return;
        this.metrics.set(metrics);
        this.overview.set(overview);
        this.applyStudentPage(students);
        this.classPage.set(1);
        if (!this.years().includes(overview.periodYear)) this.years.update((years) => [overview.periodYear, ...years]);
        this.selectedYear = overview.periodYear;
        this.selectedTerm = overview.periodTerm;
        this.loading.set(false);
      },
      error: () => {
        if (requestId !== this.requestId) return;
        this.loadFailed.set(true);
        this.loading.set(false);
      },
    });
  }

  private metricValue(id: string): number | null {
    return this.metrics().find((metric) => metric.id === id)?.value ?? null;
  }

  private learningRow(
    indicatorKey: string,
    numerator: number | null,
    denominator: number | null,
    targetKey: string,
    inverse: boolean,
  ): LearningReportRow {
    const value = numerator === null || denominator === null || denominator <= 0 ? null : numerator / denominator * 100;
    return { indicatorKey, value, numerator, denominator, targetKey, status: this.learningStatus(value, inverse), inverse };
  }

  private learningStatus(value: number | null, inverse: boolean): ReportStatus | null {
    if (value === null) return null;
    if (inverse) return value <= 5 ? 'green' : value <= 12 ? 'amber' : 'red';
    return value >= 90 ? 'green' : value >= 75 ? 'amber' : 'red';
  }

  classRiskRate(row: DashboardGrade): string {
    return row.students > 0 ? this.formatPercent(row.atRisk / row.students * 100) : '—';
  }

  classStatus(row: DashboardGrade): ReportStatus {
    const rate = row.students > 0 ? row.atRisk / row.students * 100 : 0;
    return rate === 0 ? 'green' : rate <= 10 ? 'amber' : 'red';
  }

  classFollowUpKey(row: DashboardGrade): string {
    return this.languageService.t(`reports.classes.followUp.${this.classStatus(row)}`);
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
    return level === 'High' ? 'bg-red-100 text-red-800' : 'bg-amber-100 text-amber-800';
  }

  riskLabel(level: string): string {
    return this.languageService.t(`reports.risk.${level}`);
  }

  formatPercent(value: number | null): string {
    if (value === null || !Number.isFinite(value)) return '—';
    const locale = this.languageService.language() === 'vi' ? 'vi-VN' : 'en-US';
    return new Intl.NumberFormat(locale, { maximumFractionDigits: 1 }).format(value) + '%';
  }

  formatVolume(numerator: number | null, denominator: number | null): string {
    if (numerator === null || denominator === null) return '—';
    return `${Math.round(numerator).toLocaleString()} / ${Math.round(denominator).toLocaleString()}`;
  }

  formatYear(year: string): string {
    return year.replace('-', '–');
  }

  termLabel(term: string): string {
    const key = `dashboard.period.term.${term}`;
    const translated = this.languageService.t(key);
    return translated === key ? term : translated;
  }

  printReport(): void {
    window.print();
  }
}
