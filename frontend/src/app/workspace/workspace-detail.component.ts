import { NgClass, NgFor, NgIf } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EChartsCoreOption } from 'echarts/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { timeout } from 'rxjs';
import { AuthenticationService } from '../core/auth/auth.service';
import { LanguageService } from '../core/language/language.service';
import {
  CanvasExploreField,
  CanvasExploreDrilldown,
  CanvasExploreDrilldownRow,
  CanvasExploreOptions,
  CanvasExploreResult,
  ChartKind,
  CustomChart,
  CustomReport,
  groupMetricVariables,
  MetricDrilldown,
  MetricDrilldownRow,
  MetricVariable,
  MetricVariableGroup,
  ReportBuilderService,
  SystemUser,
  WorkspaceAiChartSnapshot,
  WorkspaceAiReportRequest,
} from '../reports/report-builder.service';
import {
  defaultFilterSet,
  emptyFilterOptions,
  WorkspaceFiltersService,
  WorkspaceFilterSet,
} from './workspace-filters.service';

type WorkspaceSheetMode = 'edit';
type SheetKind = 'workspace' | 'chart';

interface WorkspaceDraft {
  title: string;
  description: string;

  sharedWith: string[];
}

interface ChartDraft {
  id: string | null;
  title: string;
  kind: ChartKind;
  metrics: string[];
  showBaseline: boolean;
  kpiTarget: number | null;
  /** 'metric' = simplified metric variables; 'explorer' = raw Canvas fields. */
  source: 'metric' | 'explorer';
  dataset: string;
  dimension: string;
  measure: string;
}

interface AiReportDraft {
  language: 'en' | 'vi';
  audience: 'leadership' | 'teachers' | 'parents';
  tone: 'constructive' | 'formal' | 'concise';
  detailLevel: 'brief' | 'standard' | 'detailed';
  additionalInstructions: string;
  includeRecommendations: boolean;
}

interface ChartImageExporter {
  getDataURL(options?: {
    type?: 'png';
    pixelRatio?: number;
    backgroundColor?: string;
    excludeComponents?: string[];
  }): string;
}

@Component({
  selector: 'app-workspace-detail',
  imports: [FormsModule, NgClass, NgFor, NgIf, NgxEchartsDirective, RouterLink],
  template: `
    <section class="space-y-7">
      <div
        *ngIf="workspaceLoading()"
        class="fixed inset-0 z-40 grid place-items-center bg-[#f7f4f2]/95 px-6 backdrop-blur-sm"
        role="status"
        aria-live="polite"
      >
        <div class="w-full max-w-sm rounded-2xl border border-[#e3dbe7] bg-white px-8 py-9 text-center shadow-[0_24px_70px_rgba(50,18,79,0.16)]">
          <div class="relative mx-auto h-16 w-16">
            <div class="absolute inset-0 rounded-full border-4 border-[#eee7f1]"></div>
            <div class="absolute inset-0 animate-spin rounded-full border-4 border-transparent border-t-[#6d3a91] border-r-[#f3c246]"></div>
            <div class="absolute inset-[18px] rounded-full bg-[#32124f]"></div>
          </div>
          <h2 class="mt-5 text-xl font-semibold text-[#32124f]">{{ languageService.t('workspace.loading.title') }}</h2>
          <p class="mt-2 text-sm leading-6 text-[#6f6877]">{{ languageService.t('workspace.loading.body') }}</p>
        </div>
      </div>

      <div class="no-print flex flex-wrap items-start justify-between gap-4">
        <div>
          <a
            routerLink="/workspace"
            class="text-sm font-semibold text-[#7a5b12] transition hover:text-[#32124f]"
          >
            {{ languageService.t('workspace.backToWorkspace') }}
          </a>
          <ng-container *ngIf="selectedReport() as report">
            <p class="mt-4 text-sm uppercase tracking-[0.18em] text-[#8a6811]">
              {{
                isSelectedOwner()
                  ? languageService.t('workspace.privateWorkspace')
                  : languageService.t('workspace.sharedWorkspace')
              }}
            </p>
            <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ report.title }}</h2>
            <p class="mt-2 max-w-3xl text-[#6f6877]">{{ report.description }}</p>
          </ng-container>
        </div>
        <div class="flex flex-wrap items-center gap-2" *ngIf="selectedReport()">
          <div class="relative">
            <button
              class="inline-flex items-center gap-2 rounded-md border border-[#d8d0df] px-4 py-3 text-sm font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
              type="button"
              (click)="toggleSnapshotMenu()"
            >
              <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M6 9V2h12v7" />
                <path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2" />
                <path d="M6 14h12v8H6z" />
              </svg>
              {{ languageService.t('workspace.snapshot.button') }}
              <svg class="h-3.5 w-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                <path d="M6 9l6 6 6-6" />
              </svg>
            </button>
            <button
              *ngIf="snapshotMenuOpen()"
              class="fixed inset-0 z-20 cursor-default"
              type="button"
              tabindex="-1"
              aria-hidden="true"
              (click)="closeSnapshotMenu()"
            ></button>
            <div
              *ngIf="snapshotMenuOpen()"
              class="absolute right-0 z-30 mt-1 w-56 overflow-hidden rounded-md border border-[#e4dde8] bg-white py-1 shadow-[0_18px_50px_rgba(36,24,47,0.22)]"
            >
              <button
                class="flex w-full items-center gap-3 px-4 py-2.5 text-left text-sm font-medium text-[#32124f] transition hover:bg-[#fff8e1]"
                type="button"
                (click)="takeSnapshot()"
              >
                <svg class="h-4 w-4 text-[#7c7284]" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                  <path d="M6 9V2h12v7" />
                  <path d="M6 18H4a2 2 0 0 1-2-2v-5a2 2 0 0 1 2-2h16a2 2 0 0 1 2 2v5a2 2 0 0 1-2 2h-2" />
                  <path d="M6 14h12v8H6z" />
                </svg>
                {{ languageService.t('workspace.snapshot.print') }}
              </button>
              <button
                class="flex w-full items-center gap-3 px-4 py-2.5 text-left text-sm font-medium text-[#32124f] transition hover:bg-[#fff8e1]"
                type="button"
                (click)="takeSnapshot()"
              >
                <svg class="h-4 w-4 text-[#7c7284]" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                  <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" />
                  <path d="M7 10l5 5 5-5" />
                  <path d="M12 15V3" />
                </svg>
                {{ languageService.t('workspace.snapshot.download') }}
              </button>
              <p class="border-t border-[#efeaf3] px-4 pt-2 pb-1 text-xs leading-4 text-[#9a91a3]">
                {{ languageService.t('workspace.snapshot.hint') }}
              </p>
            </div>
          </div>
          <button
            class="inline-flex items-center gap-2 rounded-md border border-[#7b4aa4] bg-[#f7f1fb] px-4 py-3 text-sm font-semibold text-[#4a1b73] transition hover:bg-[#efe2f7] disabled:cursor-not-allowed disabled:opacity-40"
            type="button"
            [disabled]="!selectedReport()?.charts?.length"
            (click)="openAiReportSheet()"
          >
            <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
              <path d="m12 3-1.4 3.6L7 8l3.6 1.4L12 13l1.4-3.6L17 8l-3.6-1.4L12 3Z" />
              <path d="m19 14-.8 2.2L16 17l2.2.8L19 20l.8-2.2L22 17l-2.2-.8L19 14Z" />
              <path d="m5 13-1 2.5L1.5 16.5 4 17.5 5 20l1-2.5 2.5-1L6 15.5 5 13Z" />
            </svg>
            {{ languageService.t('workspace.aiReport.button') }}
          </button>
          <ng-container *ngIf="isSelectedOwner()">
            <button
              class="rounded-md border border-[#d8d0df] px-4 py-3 text-sm font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
              type="button"
              (click)="startEditWorkspace()"
            >
              {{ languageService.t('workspace.manageWorkspace') }}
            </button>
            <button
              class="rounded-md bg-[#32124f] px-4 py-3 text-sm font-semibold text-white transition hover:bg-[#4a1b73]"
              type="button"
              (click)="startCreateChart()"
            >
              {{ languageService.t('workspace.addChart') }}
            </button>
          </ng-container>
        </div>
      </div>

      <section
        *ngIf="selectedReport()"
        class="no-print rounded-lg border border-[#ebe6ef] bg-white p-5 shadow-[0_12px_35px_rgba(50,18,79,0.08)]"
      >
        <div class="flex flex-wrap items-start justify-between gap-3">
          <div>
            <h3 class="text-base font-semibold text-[#32124f]">{{ languageService.t('workspace.filters.title') }}</h3>
            <p class="mt-1 text-xs text-[#7c7284]">{{ languageService.t('workspace.filters.hint') }}</p>
          </div>
          <button
            class="rounded-md border border-[#d8d0df] px-3 py-2 text-xs font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
            type="button"
            (click)="resetFilters()"
          >
            {{ languageService.t('workspace.filters.reset') }}
          </button>
        </div>

        <div class="mt-4 grid gap-4 lg:grid-cols-3">
          <fieldset class="rounded-md border border-[#ebe6ef] bg-[#faf9f6] p-3">
            <legend class="px-1 text-xs font-bold uppercase tracking-[0.12em] text-[#8a6811]">
              {{ languageService.t('workspace.filters.time') }}
            </legend>
            <div class="grid grid-cols-2 gap-2">
              <label class="filter-field">
                <span class="filter-label">{{ languageService.t('workspace.filters.fields.year') }}</span>
                <select class="filter-select" [ngModel]="filters().year" (ngModelChange)="setFilter('year', $event)">
                  <option value="">{{ languageService.t('workspace.filters.all') }}</option>
                  <option *ngFor="let year of options().academicYears" [value]="year">{{ year }}</option>
                </select>
              </label>
              <label class="filter-field">
                <span class="filter-label">{{ languageService.t('workspace.filters.fields.semester') }}</span>
                <select class="filter-select" [ngModel]="filters().semester" (ngModelChange)="setFilter('semester', $event)">
                  <option value="">{{ languageService.t('workspace.filters.all') }}</option>
                  <option *ngFor="let semester of options().terms" [value]="semester">
                    {{ semesterLabel(semester) }}
                  </option>
                </select>
              </label>
            </div>
          </fieldset>

          <fieldset class="rounded-md border border-[#ebe6ef] bg-[#faf9f6] p-3">
            <legend class="px-1 text-xs font-bold uppercase tracking-[0.12em] text-[#8a6811]">
              {{ languageService.t('workspace.filters.object') }}
            </legend>
            <div class="grid grid-cols-2 gap-2 xl:grid-cols-3">
              <label class="filter-field">
                <span class="filter-label">{{ languageService.t('workspace.filters.fields.campus') }}</span>
                <select class="filter-select" [ngModel]="filters().campus" (ngModelChange)="setFilter('campus', $event)">
                  <option value="">{{ languageService.t('workspace.filters.all') }}</option>
                  <option *ngFor="let campus of options().campuses" [value]="campus">{{ campus }}</option>
                </select>
              </label>
              <label class="filter-field">
                <span class="filter-label">{{ languageService.t('workspace.filters.fields.level') }}</span>
                <select class="filter-select" [ngModel]="filters().level" (ngModelChange)="setFilter('level', $event)">
                  <option value="">{{ languageService.t('workspace.filters.all') }}</option>
                  <option *ngFor="let level of educationLevelOptions()" [value]="level">{{ level }}</option>
                </select>
              </label>
              <label class="filter-field">
                <span class="filter-label">{{ languageService.t('workspace.filters.fields.grade') }}</span>
                <select class="filter-select" [ngModel]="filters().grade" (ngModelChange)="setFilter('grade', $event)">
                  <option value="">{{ languageService.t('workspace.filters.allGrades') }}</option>
                  <option *ngFor="let grade of gradeOptions()" [value]="grade">
                    {{ languageService.t('workspace.filters.gradePrefix') }} {{ grade }}
                  </option>
                </select>
              </label>
              <label class="filter-field">
                <span class="filter-label">{{ languageService.t('workspace.filters.fields.homeroom') }}</span>
                <select class="filter-select" [ngModel]="filters().homeroom" (ngModelChange)="setFilter('homeroom', $event)">
                  <option value="">{{ languageService.t('workspace.filters.allHomerooms') }}</option>
                  <option *ngFor="let homeroom of homeroomOptions()" [value]="homeroom">{{ homeroom }}</option>
                </select>
              </label>
              <label class="filter-field xl:col-span-2">
                <span class="filter-label">{{ languageService.t('workspace.filters.fields.student') }}</span>
                <select class="filter-select" [ngModel]="filters().studentId" (ngModelChange)="setFilter('studentId', $event)">
                  <option value="">{{ languageService.t('workspace.filters.allStudents') }}</option>
                  <option *ngFor="let student of studentOptions()" [value]="student.id">
                    {{ student.fullName }} ({{ student.sisUserId }})
                  </option>
                </select>
              </label>
            </div>
          </fieldset>

          <fieldset class="rounded-md border border-[#ebe6ef] bg-[#faf9f6] p-3">
            <legend class="px-1 text-xs font-bold uppercase tracking-[0.12em] text-[#8a6811]">
              {{ languageService.t('workspace.filters.program') }}
            </legend>
            <div class="grid grid-cols-2 gap-2">
              <label class="filter-field">
                <span class="filter-label">{{ languageService.t('workspace.filters.fields.program') }}</span>
                <select class="filter-select" [ngModel]="filters().program" (ngModelChange)="setFilter('program', $event)">
                  <option value="">{{ languageService.t('workspace.filters.all') }}</option>
                  <option *ngFor="let program of options().programs" [value]="program">{{ program }}</option>
                </select>
              </label>
              <label class="filter-field">
                <span class="filter-label">{{ languageService.t('workspace.filters.fields.subject') }}</span>
                <select class="filter-select" [ngModel]="filters().subject" (ngModelChange)="setFilter('subject', $event)">
                  <option value="">{{ languageService.t('workspace.filters.all') }}</option>
                  <option *ngFor="let subject of options().subjects" [value]="subject">{{ subject }}</option>
                </select>
              </label>
            </div>
          </fieldset>
        </div>
      </section>

      <section
        *ngIf="selectedReport() as report; else missingWorkspace"
        class="snapshot-surface rounded-lg border border-[#ebe6ef] bg-white p-5 shadow-[0_12px_35px_rgba(50,18,79,0.08)]"
      >
        <header class="snapshot-only mb-6 border-b border-[#ebe6ef] pb-5">
          <div class="flex items-start justify-between gap-4">
            <div>
              <p class="text-xs uppercase tracking-[0.18em] text-[#8a6811]">
                {{ languageService.t('app.school') }} · {{ languageService.t('workspace.snapshot.heading') }}
              </p>
              <h1 class="mt-2 text-2xl font-semibold text-[#32124f]">{{ report.title }}</h1>
              <p *ngIf="report.description" class="mt-1 max-w-3xl text-sm text-[#6f6877]">{{ report.description }}</p>
            </div>
            <p class="whitespace-nowrap text-xs text-[#7c7284]">
              {{ languageService.t('workspace.snapshot.generatedOn') }} {{ snapshotTimestamp() }}
            </p>
          </div>
          <div class="mt-4">
            <p class="text-xs font-bold uppercase tracking-[0.12em] text-[#8a6811]">
              {{ languageService.t('workspace.snapshot.filtersTitle') }}
            </p>
            <div class="mt-2 flex flex-wrap gap-2">
              <span
                *ngFor="let item of appliedFilters()"
                class="inline-flex items-center gap-1.5 rounded-full border border-[#e4dde8] bg-[#faf9f6] px-3 py-1 text-xs text-[#32124f]"
              >
                <span class="font-semibold text-[#7c7284]">{{ item.label }}:</span> {{ item.value }}
              </span>
              <span *ngIf="!appliedFilters().length" class="text-xs text-[#7c7284]">
                {{ languageService.t('workspace.snapshot.noFilters') }}
              </span>
            </div>
          </div>
        </header>

        <div class="grid gap-5 lg:grid-cols-2">
          <article
            *ngFor="let chart of orderedCharts(report)"
            class="rounded-lg border border-[#ebe6ef] bg-white p-5 shadow-[0_12px_35px_rgba(50,18,79,0.06)]"
            [ngClass]="{ 'lg:col-span-2': isWideChart(chart) }"
          >
            <div class="flex flex-wrap items-start justify-between gap-3">
              <div>
                <h3 class="text-lg font-semibold text-[#32124f]">{{ chart.title }}</h3>
                <p class="mt-1 text-xs text-[#7c7284]">
                  {{ languageService.t('workspace.chartTypeLabel') }}:
                  {{ languageService.t('workspace.chartType.' + chart.kind) }}
                </p>
              </div>
              <div class="no-print flex items-center gap-1.5">
                <button
                  class="grid h-8 w-8 place-items-center rounded-md border border-[#d8d0df] text-xs font-bold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
                  type="button"
                  [attr.aria-label]="languageService.t('workspace.moveUp')"
                  [attr.title]="languageService.t('workspace.moveUp')"
                  (click)="moveChart(report, chart, -1)"
                >
                  ↑
                </button>
                <button
                  class="grid h-8 w-8 place-items-center rounded-md border border-[#d8d0df] text-xs font-bold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
                  type="button"
                  [attr.aria-label]="languageService.t('workspace.moveDown')"
                  [attr.title]="languageService.t('workspace.moveDown')"
                  (click)="moveChart(report, chart, 1)"
                >
                  ↓
                </button>
                <button
                  *ngIf="isSelectedOwner()"
                  class="rounded-md border border-[#d8d0df] px-3 py-2 text-xs font-semibold text-[#32124f] cursor-pointer"
                  type="button"
                  (click)="startEditChart(chart)"
                >
                  {{ languageService.t('workspace.configure') }}
                </button>
              </div>
            </div>

            <div
              class="mt-5 min-h-72 rounded-md border border-dashed border-[#d8d0df] bg-[#faf9f6] p-4"
              [ngClass]="{ 'min-h-[29rem]': isWideChart(chart) }"
            >
              <ng-container *ngIf="chart.explorer">
                <div
                  echarts
                  class="w-full"
                  [ngClass]="isWideChart(chart) ? 'h-[26rem]' : 'h-72'"
                  [options]="chartOptions(chart)"
                  role="img"
                  [attr.aria-label]="chart.title"
                  (chartInit)="onChartInit(chart.id, $event)"
                  (chartClick)="onChartPointClick(chart, $event)"
                  (chartDataZoom)="onChartDataZoom(chart.id, $event)"
                ></div>
                <p *ngIf="!explorerHasRows(chart)" class="mt-3 text-center text-xs text-[#9a91a3]">
                  {{ languageService.t('workspace.explorer.noData') }}
                </p>
                <p class="mt-3 text-xs font-medium text-[#5f5368]">{{ explorerCaption(chart) }}</p>
              </ng-container>

              <ng-container *ngIf="!chart.explorer">
              <ng-container *ngIf="chart.metrics.length; else emptyChart">
                <div *ngIf="chart.kind === 'card'" class="grid gap-3 sm:grid-cols-2">
                  <button
                    *ngFor="let metric of chartMetrics(chart)"
                    class="rounded-md border border-[#ebe6ef] bg-white p-4 text-left transition hover:border-[#f3c246] hover:bg-[#fff8e1] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#f3c246]"
                    type="button"
                    [title]="languageService.t('workspace.drilldown.openHint')"
                    (click)="openDrilldown(metric.id, null)"
                  >
                    <p class="text-sm text-[#6f6877]">{{ metricLabel(metric.id) }}</p>
                    <p class="mt-2 text-3xl font-semibold text-[#32124f]">
                      {{ cardValue(metric) }}
                    </p>
                    <p *ngIf="metric.trend !== null" class="mt-2 text-sm font-medium text-[#8a6811]">{{ metric.trend }}</p>
                  </button>
                </div>

                <div
                  echarts
                  *ngIf="chart.kind !== 'card'"
                  class="w-full cursor-pointer"
                  [ngClass]="isWideChart(chart) ? 'h-[26rem]' : 'h-72'"
                  [options]="chartOptions(chart)"
                  role="img"
                  [attr.aria-label]="chart.title"
                  (chartInit)="onChartInit(chart.id, $event)"
                  (chartClick)="onChartPointClick(chart, $event)"
                  (chartDataZoom)="onChartDataZoom(chart.id, $event)"
                ></div>

                <p
                  *ngIf="chart.kind !== 'card'"
                  class="mt-3 text-xs text-[#9a91a3]"
                >
                  {{ languageService.t('workspace.drilldown.chartHint') }}
                </p>

                <div class="mt-3 flex flex-wrap gap-3">
                  <span
                    *ngFor="let metric of chartMetrics(chart); let metricIndex = index"
                    class="flex items-center gap-2 text-xs font-medium text-[#5f5368]"
                  >
                    <span
                      class="h-2.5 w-2.5 rounded-full"
                      [style.background]="seriesColor(metricIndex)"
                    ></span>
                    {{ metricLabel(metric.id) }}
                  </span>
                </div>
              </ng-container>
              <ng-template #emptyChart>
                <div class="grid min-h-56 place-items-center text-center">
                  <div>
                    <p class="font-semibold text-[#32124f]">
                      {{ languageService.t('workspace.emptyChartTitle') }}
                    </p>
                    <p class="mt-2 text-sm text-[#6f6877]">
                      {{ languageService.t('workspace.emptyChartBody') }}
                    </p>
                  </div>
                </div>
              </ng-template>
              </ng-container>
            </div>
          </article>
        </div>
      </section>

      <ng-template #missingWorkspace>
        <section
          class="grid min-h-80 place-items-center rounded-lg border border-[#ebe6ef] bg-white p-8 text-center"
        >
          <div>
            <p class="text-lg font-semibold text-[#32124f]">
              {{ languageService.t('workspace.missingTitle') }}
            </p>
            <p class="mt-2 text-sm text-[#6f6877]">
              {{ languageService.t('workspace.missingBody') }}
            </p>
          </div>
        </section>
      </ng-template>

      <div
        *ngIf="sheetMounted()"
        class="no-print fixed inset-0 z-40"
        [class.pointer-events-none]="!sheetOpen()"
      >
        <button
          class="sheet-backdrop absolute inset-0 bg-[#24182f]/35"
          [class.sheet-backdrop-open]="sheetOpen()"
          [class.sheet-backdrop-closing]="!sheetOpen()"
          type="button"
          [attr.aria-label]="languageService.t('workspace.closeSheet')"
          (click)="closeSheet()"
        ></button>

        <aside
          class="sheet-panel absolute right-0 top-0 flex h-full w-full max-w-xl flex-col bg-white shadow-[0_24px_80px_rgba(36,24,47,0.28)]"
          [class.sheet-panel-open]="sheetOpen()"
          [class.sheet-panel-closing]="!sheetOpen()"
          role="dialog"
          aria-modal="true"
        >
          <ng-container *ngIf="sheetKind() === 'workspace'; else chartSheet">
            <div class="flex items-start justify-between gap-4 border-b border-[#ebe6ef] px-6 py-5">
              <div>
                <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">
                  {{ languageService.t('workspace.eyebrow') }}
                </p>
                <h3 class="mt-2 text-xl font-semibold text-[#32124f]">
                  {{ languageService.t('workspace.manageWorkspace') }}
                </h3>
                <p class="mt-1 text-sm text-[#6f6877]">
                  {{ languageService.t('workspace.workspaceSheetHint') }}
                </p>
              </div>
              <button
                class="grid h-9 w-9 place-items-center rounded-full border border-[#d8d0df] text-lg font-semibold text-[#32124f]"
                type="button"
                (click)="closeSheet()"
              >
                x
              </button>
            </div>

            <form class="flex min-h-0 flex-1 flex-col" (ngSubmit)="saveWorkspaceDraft()">
              <div class="min-h-0 flex-1 space-y-5 overflow-y-auto px-6 py-6">
                <label class="block">
                  <span class="text-sm font-medium text-[#34243d]">{{
                    languageService.t('workspace.workspaceTitle')
                  }}</span>
                  <input
                    class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-4 py-3 outline-none"
                    name="workspaceTitle"
                    [(ngModel)]="workspaceDraft.title"
                    required
                  />
                </label>

                <label class="block">
                  <span class="text-sm font-medium text-[#34243d]">{{
                    languageService.t('workspace.workspaceDescription')
                  }}</span>
                  <textarea
                    class="mt-2 min-h-28 w-full rounded-md border border-[#d8d0df] bg-white px-4 py-3 outline-none"
                    name="workspaceDescription"
                    [(ngModel)]="workspaceDraft.description"
                  ></textarea>
                </label>

                <div class="rounded-md border border-[#ebe6ef] bg-[#faf9f6] p-4">
                  <p class="text-sm font-medium text-[#32124f]">
                    {{ languageService.t('workspace.shareWithSelectedUsers') }}
                  </p>

                  <div class="relative mt-4">
                    <button
                      class="flex w-full items-center justify-between rounded-md border border-[#d8d0df] bg-white px-4 py-3 text-left text-sm text-[#32124f]"
                      type="button"
                      (click)="shareDropdownOpen.set(!shareDropdownOpen())"
                    >
                      <span>{{ selectedShareLabel() }}</span>
                      <span>v</span>
                    </button>
                    <div
                      *ngIf="shareDropdownOpen()"
                      class="absolute left-0 right-0 z-10 mt-2 max-h-64 overflow-y-auto rounded-md border border-[#d8d0df] bg-white p-2 shadow-[0_16px_40px_rgba(50,18,79,0.16)]"
                    >
                      <label
                        *ngFor="let user of systemUsers()"
                        class="flex cursor-pointer items-start gap-3 rounded px-3 py-2 text-sm transition hover:bg-[#faf9f6]"
                      >
                        <input
                          class="mt-1"
                          type="checkbox"
                          [checked]="workspaceDraft.sharedWith.includes(user.username)"
                          (change)="toggleShareUser(user.username)"
                        />
                        <span>
                          <span class="block font-semibold text-[#32124f]">{{
                            user.fullName
                          }}</span>
                          <span class="block text-xs text-[#7c7284]"
                            >{{ user.username }}{{ user.email ? ' · ' + user.email : '' }}</span
                          >
                        </span>
                      </label>
                    </div>
                  </div>
                </div>
              </div>

              <div
                class="flex flex-wrap items-center justify-between gap-3 border-t border-[#ebe6ef] bg-[#faf9f6] px-6 py-4"
              >
                <div class="flex gap-2">
                  <button
                    class="rounded-md border border-[#d8d0df] px-4 py-3 text-sm font-semibold text-[#32124f]"
                    type="button"
                    (click)="duplicateSelected()"
                  >
                    {{ languageService.t('workspace.duplicate') }}
                  </button>
                </div>
                <div class="flex gap-2">
                  <button
                    class="rounded-md border border-[#d8d0df] px-5 py-3 text-sm font-semibold text-[#32124f]"
                    type="button"
                    (click)="closeSheet()"
                  >
                    {{ languageService.t('workspace.cancel') }}
                  </button>
                  <button
                    class="rounded-md bg-[#32124f] px-5 py-3 text-sm font-semibold text-white"
                    type="submit"
                  >
                    {{ languageService.t('workspace.save') }}
                  </button>
                </div>
              </div>
            </form>
          </ng-container>

          <ng-template #chartSheet>
            <div class="flex items-start justify-between gap-4 border-b border-[#ebe6ef] px-6 py-5">
              <div>
                <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">
                  {{ languageService.t('workspace.chart') }}
                </p>
                <h3 class="mt-2 text-xl font-semibold text-[#32124f]">
                  {{
                    chartDraft.id
                      ? languageService.t('workspace.configureChart')
                      : languageService.t('workspace.addChart')
                  }}
                </h3>
                <p class="mt-1 text-sm text-[#6f6877]">
                  {{ languageService.t('workspace.chartSheetHint') }}
                </p>
              </div>
              <button
                class="grid h-9 w-9 place-items-center rounded-full border border-[#d8d0df] text-lg font-semibold text-[#32124f]"
                type="button"
                (click)="closeSheet()"
              >
                x
              </button>
            </div>

            <form class="flex min-h-0 flex-1 flex-col" (ngSubmit)="saveChartDraft()">
              <div class="min-h-0 flex-1 space-y-5 overflow-y-auto px-6 py-6">
                <label class="block">
                  <span class="text-sm font-medium text-[#34243d]">{{
                    languageService.t('workspace.chartTitle')
                  }}</span>
                  <input
                    class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-4 py-3 outline-none"
                    name="chartTitle"
                    [(ngModel)]="chartDraft.title"
                    required
                  />
                </label>

                <label class="block">
                  <span class="text-sm font-medium text-[#34243d]">{{
                    languageService.t('workspace.chartSource')
                  }}</span>
                  <select
                    class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-4 py-3 outline-none"
                    name="chartSource"
                    [(ngModel)]="chartDraft.source"
                    (ngModelChange)="onChartSourceChange()"
                  >
                    <option value="metric">{{ languageService.t('workspace.chartSource.metric') }}</option>
                    <option value="explorer">{{ languageService.t('workspace.chartSource.explorer') }}</option>
                  </select>
                  <p class="mt-2 text-xs leading-5 text-[#7c7284]">
                    {{ languageService.t('workspace.chartSource.' + chartDraft.source + 'Hint') }}
                  </p>
                </label>

                <label class="block">
                  <span class="text-sm font-medium text-[#34243d]">{{
                    languageService.t('workspace.chartTypeLabel')
                  }}</span>
                  <select
                    class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-4 py-3 outline-none"
                    name="chartKind"
                    [(ngModel)]="chartDraft.kind"
                  >
                    <option value="bar">{{ languageService.t('workspace.chartType.bar') }}</option>
                    <option value="line">
                      {{ languageService.t('workspace.chartType.line') }}
                    </option>
                    <option value="pie">
                      {{ languageService.t('workspace.chartType.pie') }}
                    </option>
                    <option *ngIf="chartDraft.source === 'metric'" value="scatter">
                      {{ languageService.t('workspace.chartType.scatter') }}
                    </option>
                    <option *ngIf="chartDraft.source === 'metric'" value="card">
                      {{ languageService.t('workspace.chartType.card') }}
                    </option>
                  </select>
                  <p *ngIf="chartDraft.kind === 'pie'" class="mt-2 text-xs leading-5 text-[#7c7284]">
                    {{ languageService.t('workspace.chartTypeHint.pie') }}
                  </p>
                  <p *ngIf="chartDraft.kind === 'scatter'" class="mt-2 text-xs leading-5 text-[#7c7284]">
                    {{ languageService.t('workspace.chartTypeHint.scatter') }}
                  </p>
                </label>

                <div
                  *ngIf="chartDraft.source === 'explorer'"
                  class="space-y-4 rounded-md border border-[#ebe6ef] bg-[#faf9f6] p-4"
                >
                  <p class="text-sm font-medium text-[#32124f]">
                    {{ languageService.t('workspace.explorer.builderTitle') }}
                  </p>
                  <p class="text-xs text-[#7c7284]">{{ languageService.t('workspace.explorer.builderHint') }}</p>
                  <label class="block text-sm text-[#34243d]">
                    {{ languageService.t('workspace.explorer.dataset') }}
                    <select
                      class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-4 py-2.5 outline-none"
                      name="explorerDataset"
                      [(ngModel)]="chartDraft.dataset"
                      (ngModelChange)="onDatasetChange()"
                    >
                      <option *ngFor="let dataset of exploreOptions().datasets" [value]="dataset.key">
                        {{ exploreDatasetLabel(dataset.key) }}
                      </option>
                    </select>
                  </label>
                  <label class="block text-sm text-[#34243d]">
                    {{ languageService.t('workspace.explorer.dimension') }}
                    <select
                      class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-4 py-2.5 outline-none"
                      name="explorerDimension"
                      [(ngModel)]="chartDraft.dimension"
                    >
                      <option *ngFor="let field of datasetDimensions()" [value]="field.key">
                        {{ exploreFieldLabel(field.key, field.label) }}
                      </option>
                    </select>
                  </label>
                  <label class="block text-sm text-[#34243d]">
                    {{ languageService.t('workspace.explorer.measure') }}
                    <select
                      class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-4 py-2.5 outline-none"
                      name="explorerMeasure"
                      [(ngModel)]="chartDraft.measure"
                    >
                      <option *ngFor="let field of datasetMeasures()" [value]="field.key">
                        {{ exploreFieldLabel(field.key, field.label) }}
                      </option>
                    </select>
                  </label>
                </div>

                <div
                  *ngIf="chartDraft.source === 'metric' && (chartDraft.kind === 'bar' || chartDraft.kind === 'line')"
                  class="rounded-md border border-[#ebe6ef] bg-[#faf9f6] p-4"
                >
                  <p class="text-sm font-medium text-[#32124f]">
                    {{ languageService.t('workspace.referenceLines') }}
                  </p>
                  <p class="mt-1 text-xs text-[#7c7284]">
                    {{ languageService.t('workspace.referenceLinesHint') }}
                  </p>
                  <label class="mt-3 flex cursor-pointer items-center gap-3 text-sm text-[#34243d]">
                    <input
                      name="chartBaseline"
                      type="checkbox"
                      [(ngModel)]="chartDraft.showBaseline"
                    />
                    {{ languageService.t('workspace.showBaseline') }}
                  </label>
                  <label class="mt-3 block text-sm text-[#34243d]">
                    {{ languageService.t('workspace.kpiTarget') }}
                    <input
                      class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-4 py-2.5 outline-none"
                      name="chartKpiTarget"
                      type="number"
                      [(ngModel)]="chartDraft.kpiTarget"
                      [placeholder]="languageService.t('workspace.kpiTargetPlaceholder')"
                    />
                  </label>
                </div>
                <div *ngIf="chartDraft.source === 'metric'">
                  <p class="text-sm font-medium text-[#34243d]">
                    {{ languageService.t('workspace.metricVariables') }}
                  </p>
                  <p class="mt-1 text-xs text-[#7c7284]">
                    {{ languageService.t('workspace.metricGroupingHint') }}
                  </p>
                  <div class="mt-3 space-y-3">
                    <details *ngFor="let group of metricGroups(); trackBy: trackMetricGroup" class="overflow-hidden rounded-md border border-[#e1dbe6] bg-white">
                      <summary class="flex cursor-pointer list-none items-center justify-between gap-3 bg-[#f4f0f6] px-4 py-3 text-sm font-semibold text-[#32124f]">
                        <span>{{ datasetLabel(group.dataset) }}</span>
                        <span class="rounded-full bg-white px-2.5 py-0.5 text-xs text-[#6f6877]">
                          {{ selectedMetricCount(group) }}/{{ group.metrics.length }}
                        </span>
                      </summary>
                      <div class="space-y-2 border-t border-[#e1dbe6] p-3">
                        <label
                          *ngFor="let metric of group.metrics; trackBy: trackMetric"
                          class="flex cursor-pointer items-start justify-between gap-3 rounded-md border border-[#ebe6ef] bg-[#faf9f6] p-3 transition hover:border-[#f3c246]"
                        >
                          <span>
                            <span class="block text-sm font-semibold text-[#32124f]">{{ metricLabel(metric.id) }}</span>
                            <span class="mt-1 block text-xs text-[#7c7284]">{{ sourceLabel(metric.source) }}</span>
                            <span *ngIf="metric.definition" class="mt-1 block max-w-xl text-xs leading-relaxed text-[#7c7284]">
                              {{ metric.definition }}
                            </span>
                            <span *ngIf="metric.sourceFields.length" class="mt-1 block font-mono text-[11px] text-[#8a6811]">
                              {{ metric.sourceFields.join(' · ') }}
                            </span>
                          </span>
                          <input
                            class="mt-1"
                            type="checkbox"
                            [checked]="chartDraft.metrics.includes(metric.id)"
                            (change)="toggleChartMetric(metric.id)"
                          />
                        </label>
                      </div>
                    </details>
                  </div>
                </div>
              </div>
              <div
                class="flex flex-wrap items-center justify-between gap-3 border-t border-[#ebe6ef] bg-[#faf9f6] px-6 py-4"
              >
                <button
                  *ngIf="chartDraft.id"
                  class="rounded-md border border-red-200 px-4 py-3 text-sm font-semibold text-red-700"
                  type="button"
                  (click)="deleteChartDraft()"
                >
                  {{ languageService.t('workspace.delete') }}
                </button>
                <div class="ml-auto flex gap-2">
                  <button
                    class="rounded-md border border-[#d8d0df] px-5 py-3 text-sm font-semibold text-[#32124f]"
                    type="button"
                    (click)="closeSheet()"
                  >
                    {{ languageService.t('workspace.cancel') }}
                  </button>
                  <button
                    class="rounded-md bg-[#32124f] px-5 py-3 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-40"
                    type="submit"
                    [disabled]="!chartDraft.title.trim() || !isChartDraftValid()"
                  >
                    {{ languageService.t('workspace.save') }}
                  </button>
                </div>
              </div>
            </form>
          </ng-template>
        </aside>
      </div>

      <div
        *ngIf="aiReportMounted()"
        class="no-print fixed inset-0 z-50"
        [class.pointer-events-none]="!aiReportOpen()"
      >
        <button
          class="sheet-backdrop absolute inset-0 bg-[#24182f]/35"
          [class.sheet-backdrop-open]="aiReportOpen()"
          [class.sheet-backdrop-closing]="!aiReportOpen()"
          type="button"
          [attr.aria-label]="languageService.t('workspace.aiReport.close')"
          (click)="closeAiReportSheet()"
        ></button>

        <aside
          class="sheet-panel absolute right-0 top-0 flex h-full w-full max-w-xl flex-col bg-white shadow-[0_24px_80px_rgba(36,24,47,0.28)]"
          [class.sheet-panel-open]="aiReportOpen()"
          [class.sheet-panel-closing]="!aiReportOpen()"
          role="dialog"
          aria-modal="true"
          [attr.aria-label]="languageService.t('workspace.aiReport.title')"
        >
          <div class="flex items-start justify-between gap-4 border-b border-[#ebe6ef] px-6 py-5">
            <div>
              <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">
                {{ languageService.t('workspace.aiReport.eyebrow') }}
              </p>
              <h3 class="mt-2 text-2xl font-semibold text-[#32124f]">
                {{ languageService.t('workspace.aiReport.title') }}
              </h3>
              <p class="mt-1 text-sm leading-6 text-[#6f6877]">
                {{ languageService.t('workspace.aiReport.description') }}
              </p>
            </div>
            <button
              class="grid h-10 w-10 shrink-0 place-items-center rounded-full border border-[#d8d0df] text-lg font-semibold text-[#32124f]"
              type="button"
              [attr.aria-label]="languageService.t('workspace.aiReport.close')"
              (click)="closeAiReportSheet()"
            >x</button>
          </div>

          <form class="flex min-h-0 flex-1 flex-col" (ngSubmit)="generateAiReport()">
            <div class="min-h-0 flex-1 space-y-5 overflow-y-auto px-6 py-5">
              <div class="rounded-md border border-[#eadcae] bg-[#fff8e1] px-4 py-3 text-sm leading-6 text-[#74570c]">
                {{ languageService.t('workspace.aiReport.dataNotice')
                    .replace('{charts}', aiReportChartCount().toString()) }}
              </div>

              <p *ngIf="aiReportError()" class="rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">
                {{ aiReportError() }}
              </p>

              <div class="grid gap-4 sm:grid-cols-2">
                <label class="block text-sm font-medium text-[#34243d]">
                  {{ languageService.t('workspace.aiReport.language') }}
                  <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-4 py-3 outline-none focus:border-[#6d3a91]" name="aiLanguage" [(ngModel)]="aiReportDraft.language">
                    <option value="vi">{{ languageService.t('workspace.aiReport.language.vi') }}</option>
                    <option value="en">{{ languageService.t('workspace.aiReport.language.en') }}</option>
                  </select>
                </label>
                <label class="block text-sm font-medium text-[#34243d]">
                  {{ languageService.t('workspace.aiReport.audience') }}
                  <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-4 py-3 outline-none focus:border-[#6d3a91]" name="aiAudience" [(ngModel)]="aiReportDraft.audience">
                    <option value="leadership">{{ languageService.t('workspace.aiReport.audience.leadership') }}</option>
                    <option value="teachers">{{ languageService.t('workspace.aiReport.audience.teachers') }}</option>
                    <option value="parents">{{ languageService.t('workspace.aiReport.audience.parents') }}</option>
                  </select>
                </label>
                <label class="block text-sm font-medium text-[#34243d]">
                  {{ languageService.t('workspace.aiReport.tone') }}
                  <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-4 py-3 outline-none focus:border-[#6d3a91]" name="aiTone" [(ngModel)]="aiReportDraft.tone">
                    <option value="constructive">{{ languageService.t('workspace.aiReport.tone.constructive') }}</option>
                    <option value="formal">{{ languageService.t('workspace.aiReport.tone.formal') }}</option>
                    <option value="concise">{{ languageService.t('workspace.aiReport.tone.concise') }}</option>
                  </select>
                </label>
                <label class="block text-sm font-medium text-[#34243d]">
                  {{ languageService.t('workspace.aiReport.detail') }}
                  <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-4 py-3 outline-none focus:border-[#6d3a91]" name="aiDetail" [(ngModel)]="aiReportDraft.detailLevel">
                    <option value="brief">{{ languageService.t('workspace.aiReport.detail.brief') }}</option>
                    <option value="standard">{{ languageService.t('workspace.aiReport.detail.standard') }}</option>
                    <option value="detailed">{{ languageService.t('workspace.aiReport.detail.detailed') }}</option>
                  </select>
                </label>
              </div>

              <label class="block text-sm font-medium text-[#34243d]">
                {{ languageService.t('workspace.aiReport.instructions') }}
                <textarea
                  class="mt-2 min-h-28 w-full resize-y rounded-md border border-[#d8d0df] bg-white px-4 py-3 text-sm leading-6 outline-none focus:border-[#6d3a91]"
                  name="aiInstructions"
                  maxlength="1200"
                  [(ngModel)]="aiReportDraft.additionalInstructions"
                  [placeholder]="languageService.t('workspace.aiReport.instructionsPlaceholder')"
                ></textarea>
                <span class="mt-1 block text-right text-xs text-[#9a91a3]">{{ aiReportDraft.additionalInstructions.length }}/1200</span>
              </label>

              <label class="flex cursor-pointer items-start gap-3 rounded-md border border-[#e7e0eb] bg-[#faf9f6] p-4 text-sm text-[#34243d]">
                <input class="mt-1" type="checkbox" name="aiRecommendations" [(ngModel)]="aiReportDraft.includeRecommendations" />
                <span>
                  <span class="block font-semibold text-[#32124f]">{{ languageService.t('workspace.aiReport.recommendations') }}</span>
                  <span class="mt-1 block leading-5 text-[#7c7284]">{{ languageService.t('workspace.aiReport.recommendationsHint') }}</span>
                </span>
              </label>
            </div>

            <div class="flex items-center justify-end gap-2 border-t border-[#ebe6ef] bg-[#faf9f6] px-6 py-4">
              <button class="rounded-md border border-[#d8d0df] px-5 py-3 text-sm font-semibold text-[#32124f]" type="button" [disabled]="aiReportLoading()" (click)="closeAiReportSheet()">
                {{ languageService.t('workspace.cancel') }}
              </button>
              <button class="inline-flex items-center gap-2 rounded-md bg-[#32124f] px-5 py-3 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-45" type="submit" [disabled]="aiReportLoading() || aiReportChartCount() === 0">
                <svg *ngIf="aiReportLoading()" class="h-4 w-4 animate-spin" viewBox="0 0 24 24" fill="none" aria-hidden="true"><circle class="opacity-30" cx="12" cy="12" r="9" stroke="currentColor" stroke-width="3"/><path d="M21 12a9 9 0 0 0-9-9" stroke="currentColor" stroke-width="3" stroke-linecap="round"/></svg>
                {{ languageService.t(aiReportLoading() ? 'workspace.aiReport.generating' : 'workspace.aiReport.generate') }}
              </button>
            </div>
          </form>
        </aside>
      </div>

      <div
        *ngIf="drilldownMounted()"
        class="no-print fixed inset-0 z-40"
        [class.pointer-events-none]="!drilldownOpen()"
      >
        <button
          class="sheet-backdrop absolute inset-0 bg-[#24182f]/35"
          [class.sheet-backdrop-open]="drilldownOpen()"
          [class.sheet-backdrop-closing]="!drilldownOpen()"
          type="button"
          [attr.aria-label]="languageService.t('workspace.closeSheet')"
          (click)="closeDrilldown()"
        ></button>

        <aside
          class="sheet-panel absolute right-0 top-0 flex h-full w-full max-w-lg flex-col bg-white shadow-[0_24px_80px_rgba(36,24,47,0.28)]"
          [class.sheet-panel-open]="drilldownOpen()"
          [class.sheet-panel-closing]="!drilldownOpen()"
          role="dialog"
          aria-modal="true"
        >
          <div class="flex items-start justify-between gap-4 border-b border-[#ebe6ef] px-6 py-5">
            <div class="min-w-0">
              <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">
                {{ languageService.t('workspace.drilldown.eyebrow') }}
              </p>
              <h3 class="mt-2 truncate text-xl font-semibold text-[#32124f]">
                {{ drilldownMetricLabel() }}
              </h3>
              <p class="mt-1 text-sm text-[#6f6877]">
                <span *ngIf="drilldownPeriod()" class="font-semibold text-[#32124f]"
                  >{{ drilldownPeriod() }} · </span
                >{{
                  languageService.t(
                    drilldownUnavailable()
                      ? 'workspace.drilldown.aggregateSubtitle'
                      : 'workspace.drilldown.subtitle'
                  )
                }}
              </p>
            </div>
            <button
              class="grid h-9 w-9 shrink-0 place-items-center rounded-full border border-[#d8d0df] text-lg font-semibold text-[#32124f]"
              type="button"
              (click)="closeDrilldown()"
            >
              x
            </button>
          </div>

          <div class="min-h-0 flex-1 overflow-y-auto px-6 py-5">
            <p
              *ngIf="drilldownError()"
              class="rounded-md bg-red-50 px-4 py-3 text-sm text-red-700"
            >
              {{ drilldownError() }}
            </p>

            <p
              *ngIf="drilldownLoading()"
              class="py-10 text-center text-sm text-[#7c7284]"
            >
              {{ languageService.t('workspace.drilldown.loading') }}
            </p>

            <p
              *ngIf="drilldownUnavailable()"
              class="rounded-md border border-[#eadcae] bg-[#fff8e1] px-4 py-3 text-sm leading-6 text-[#74570c]"
            >
              {{ languageService.t('workspace.drilldown.aggregateOnly') }}
            </p>

            <ul
              *ngIf="!explorerDrilldown() && !drilldownLoading() && !drilldownError() && !drilldownUnavailable()"
              class="space-y-2"
            >
              <li *ngFor="let student of drilldownStudents()">
                <a
                  class="flex items-center gap-3 rounded-lg border border-[#ebe6ef] p-3 transition hover:border-[#d3c6db] hover:bg-[#fdfcff]"
                  [routerLink]="['/students', student.studentId]"
                  (click)="closeDrilldown()"
                >
                  <span
                    class="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-[#f1edf4] text-xs font-bold text-[#5b287d]"
                  >
                    {{ studentInitials(student.fullName) }}
                  </span>
                  <span class="min-w-0 flex-1">
                    <span class="block truncate text-sm font-semibold text-[#32124f]">{{
                      student.fullName
                    }}</span>
                    <span class="mt-0.5 block truncate text-xs text-[#7c7284]">
                      {{ student.sisUserId }} ·
                      {{ languageService.t('workspace.drilldown.grade') }}
                      {{ student.gradeLevel || '-' }} · {{ student.homeroom || '-' }}
                    </span>
                  </span>
                  <span
                    class="inline-flex shrink-0 items-center gap-2 rounded-full bg-[#f1edf4] px-3 py-1.5 text-sm font-bold text-[#5b287d]"
                    [title]="drilldownMetricLabel()"
                  >
                    {{ formatDrilldownValue(student.value) }}
                  </span>
                </a>
                <div
                  *ngIf="student.records.length"
                  class="ml-4 border-l-2 border-[#ebe6ef] py-2 pl-4"
                >
                  <p class="mb-2 text-xs font-semibold text-[#6f6877]">
                    {{ student.recordCount }} {{ languageService.t('workspace.drilldown.sourceRecords') }}
                  </p>
                  <div
                    *ngFor="let record of student.records"
                    class="mb-2 rounded-md bg-[#faf9f6] p-3 text-xs text-[#5f5368] last:mb-0"
                  >
                    <p *ngIf="record.date" class="mb-2 font-semibold text-[#32124f]">
                      {{ formatRecordDate(record.date) }}
                    </p>
                    <div class="grid gap-x-3 gap-y-1 sm:grid-cols-2">
                      <p *ngFor="let field of recordFields(record.fields)">
                        <span class="font-semibold">{{ field.key }}:</span> {{ field.value }}
                      </p>
                    </div>
                  </div>
                </div>
              </li>
              <li
                *ngIf="!drilldownStudents().length"
                class="rounded-md border border-dashed border-[#d8d0df] bg-[#faf9f6] p-8 text-center text-sm text-[#7c7284]"
              >
                {{ languageService.t('workspace.drilldown.empty') }}
              </li>
            </ul>

            <ul
              *ngIf="explorerDrilldown() && !drilldownLoading() && !drilldownError()"
              class="space-y-3"
            >
              <li
                *ngFor="let record of explorerDrilldownRecords()"
                class="rounded-lg border border-[#ebe6ef] p-4"
              >
                <div class="flex items-start justify-between gap-3">
                  <div>
                    <a
                      *ngIf="record.studentId; else catalogueRecord"
                      class="font-semibold text-[#32124f] hover:underline"
                      [routerLink]="['/students', record.studentId]"
                    >{{ record.fullName }}</a>
                    <ng-template #catalogueRecord>
                      <span class="font-semibold text-[#32124f]">{{ languageService.t('workspace.drilldown.sourceRecord') }}</span>
                    </ng-template>
                    <p *ngIf="record.sisUserId" class="mt-0.5 text-xs text-[#7c7284]">
                      {{ record.sisUserId }} · {{ record.gradeLevel || '-' }} · {{ record.homeroom || '-' }}
                    </p>
                  </div>
                  <span *ngIf="record.value !== null && record.value !== undefined" class="rounded-full bg-[#f1edf4] px-3 py-1 text-sm font-bold text-[#5b287d]">
                    {{ record.value }}
                  </span>
                </div>
                <p *ngIf="record.date" class="mt-3 text-xs font-semibold text-[#6f6877]">
                  {{ formatRecordDate(record.date) }}
                </p>
                <div class="mt-2 grid gap-x-3 gap-y-1 text-xs text-[#5f5368] sm:grid-cols-2">
                  <p *ngFor="let field of recordFields(record.fields)">
                    <span class="font-semibold">{{ field.key }}:</span> {{ field.value }}
                  </p>
                </div>
              </li>
              <li *ngIf="!explorerDrilldownRecords().length" class="rounded-md border border-dashed border-[#d8d0df] p-8 text-center text-sm text-[#7c7284]">
                {{ languageService.t('workspace.drilldown.empty') }}
              </li>
            </ul>
          </div>
          <div
            *ngIf="!drilldownLoading() && !drilldownError() && !drilldownUnavailable() && drilldownTotalCount() > 0"
            class="flex items-center justify-between gap-3 border-t border-[#ebe6ef] px-6 py-4"
          >
            <span class="text-xs text-[#6f6877]">{{ drilldownPageLabel() }}</span>
            <div class="flex gap-2">
              <button
                class="rounded-md border border-[#d8d0df] px-3 py-2 text-xs font-semibold text-[#32124f] disabled:cursor-not-allowed disabled:opacity-40"
                type="button"
                [disabled]="drilldownPage() <= 1"
                (click)="changeDrilldownPage(drilldownPage() - 1)"
              >{{ languageService.t('pagination.previous') }}</button>
              <button
                class="rounded-md border border-[#d8d0df] px-3 py-2 text-xs font-semibold text-[#32124f] disabled:cursor-not-allowed disabled:opacity-40"
                type="button"
                [disabled]="drilldownPage() >= drilldownTotalPages()"
                (click)="changeDrilldownPage(drilldownPage() + 1)"
              >{{ languageService.t('pagination.next') }}</button>
            </div>
          </div>
        </aside>
      </div>
    </section>
  `,
  styles: `
    .filter-field {
      display: block;
      min-width: 0;
    }

    .filter-label {
      display: block;
      margin-bottom: 0.35rem;
      font-size: 0.7rem;
      font-weight: 700;
      letter-spacing: 0.08em;
      line-height: 1rem;
      text-transform: uppercase;
      color: #6f6877;
    }

    .filter-select {
      width: 100%;
      min-width: 0;
      border-radius: 0.375rem;
      border: 1px solid #e1dbe6;
      background: #ffffff;
      padding: 0.5rem 0.6rem;
      font-size: 0.8rem;
      color: #32124f;
      outline: none;
    }

    .filter-select:focus {
      border-color: #6d3a91;
    }

    .sheet-backdrop {
      opacity: 0;
      transition: opacity 220ms ease-out;
    }

    .sheet-backdrop-open {
      opacity: 1;
    }

    .sheet-backdrop-closing {
      opacity: 0;
    }

    .sheet-panel {
      transform: translate3d(100%, 0, 0);
      backface-visibility: hidden;
      will-change: transform;
      transition: transform 260ms cubic-bezier(0.22, 1, 0.36, 1);
      contain: layout style;
    }

    .sheet-panel-open {
      transform: translate3d(0, 0, 0);
    }

    .sheet-panel-closing {
      transform: translate3d(100%, 0, 0);
    }

    @media (prefers-reduced-motion: reduce) {
      .sheet-backdrop,
      .sheet-panel {
        transition: none;
      }
    }
  `,
})
export class WorkspaceDetailComponent implements OnInit {
  private readonly authService = inject(AuthenticationService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly reportBuilderService = inject(ReportBuilderService);
  private readonly workspaceFilters = inject(WorkspaceFiltersService);
  readonly languageService = inject(LanguageService);

  readonly options = signal(emptyFilterOptions());
  readonly filters = signal<WorkspaceFilterSet>(defaultFilterSet());
  private filterOptionsLoaded = false;
  private readonly chartOrder = signal<string[]>([]);

  readonly educationLevelOptions = computed(() => {
    const campus = this.filters().campus;
    if (!campus) return this.options().educationLevels;
    return [...new Set(this.options().students
      .filter((student) => student.campus === campus)
      .map((student) => student.educationLevel)
      .filter((level): level is string => !!level))]
      .sort((a, b) => a.localeCompare(b));
  });

  readonly gradeOptions = computed(() => {
    const { campus, level } = this.filters();
    if (!campus && !level) {
      return [...this.options().gradeLevels]
        .sort((a, b) => a.localeCompare(b, undefined, { numeric: true }));
    }
    const grades = new Set(
      this.options().students
        .filter((student) => (!campus || student.campus === campus) &&
          (!level || student.educationLevel === level))
        .map((student) => student.gradeLevel)
        .filter((grade): grade is string => !!grade),
    );
    return [...grades].sort((a, b) => a.localeCompare(b, undefined, { numeric: true }));
  });

  readonly homeroomOptions = computed(() => {
    const { campus, level, grade } = this.filters();
    if (!campus && !level && !grade) {
      return [...this.options().homerooms]
        .sort((a, b) => a.localeCompare(b, undefined, { numeric: true }));
    }
    const homerooms = new Set(
      this.options().students
        .filter((student) =>
          (!campus || student.campus === campus) &&
          (!level || student.educationLevel === level) &&
          (!grade || student.gradeLevel === grade))
        .map((student) => student.homeroom)
        .filter((homeroom): homeroom is string => !!homeroom),
    );
    return [...homerooms].sort((a, b) => a.localeCompare(b, undefined, { numeric: true }));
  });

  readonly studentOptions = computed(() => {
    const { campus, level, grade, homeroom } = this.filters();
    return this.options().students.filter(
      (student) =>
        (!campus || student.campus === campus) &&
        (!level || student.educationLevel === level) &&
        (!grade || student.gradeLevel === grade) &&
        (!homeroom || student.homeroom === homeroom),
    );
  });

  readonly customReports = signal<CustomReport[]>([]);
  readonly systemUsers = signal<SystemUser[]>([]);
  readonly selectedReportId = signal<string | null>(null);
  readonly sheetMounted = signal(false);
  readonly sheetOpen = signal(false);
  readonly sheetKind = signal<SheetKind>('workspace');
  readonly workspaceSheetMode = signal<WorkspaceSheetMode>('edit');
  readonly shareDropdownOpen = signal(false);
  readonly metricVariables = this.reportBuilderService.metricVariables;
  readonly workspaceLoading = signal(true);
  private renderCycle = 0;
  private loadingDataRequests = 0;
  private readonly workspaceRequestTimeoutMs = 15_000;

  readonly drilldownMounted = signal(false);
  readonly drilldownOpen = signal(false);
  readonly drilldownLoading = signal(false);
  readonly drilldownError = signal<string | null>(null);
  readonly drilldownUnavailable = signal(false);
  readonly drilldownMetricId = signal<string | null>(null);
  readonly drilldownMetricLabel = signal<string>('');
  readonly drilldownPeriod = signal<string | null>(null);
  private readonly drilldownData = signal<MetricDrilldown | null>(null);
  private readonly explorerDrilldownData = signal<CanvasExploreDrilldown | null>(null);
  readonly explorerDrilldown = signal(false);
  private explorerDrilldownSpec: { dataset: string; dimension: string; measure: string; groupKey: string } | null = null;
  private drilldownPeriodStart: string | null = null;
  private drilldownRequestId = 0;
  private readonly drilldownPageSize = 20;
  private readonly chartZoomRanges = new Map<string, { start: number; end: number }>();
  private readonly chartImageExporters = new Map<string, ChartImageExporter>();

  readonly drilldownStudents = computed<MetricDrilldownRow[]>(
    () => this.drilldownData()?.students ?? [],
  );
  readonly explorerDrilldownRecords = computed<CanvasExploreDrilldownRow[]>(
    () => this.explorerDrilldownData()?.records ?? [],
  );
  readonly drilldownUnit = computed(() => this.drilldownData()?.unit ?? '');
  readonly drilldownPage = computed(() =>
    this.explorerDrilldown() ? this.explorerDrilldownData()?.page ?? 1 : this.drilldownData()?.page ?? 1);
  readonly drilldownTotalPages = computed(() =>
    this.explorerDrilldown() ? this.explorerDrilldownData()?.totalPages ?? 0 : this.drilldownData()?.totalPages ?? 0);
  readonly drilldownTotalCount = computed(() =>
    this.explorerDrilldown() ? this.explorerDrilldownData()?.totalCount ?? 0 : this.drilldownData()?.totalCount ?? 0);
  readonly drilldownPageLabel = computed(() => this.languageService.t('pagination.pageOf')
    .replace('{page}', String(this.drilldownPage()))
    .replace('{pages}', String(this.drilldownTotalPages()))
    .replace('{total}', String(this.drilldownTotalCount())));

  readonly snapshotMenuOpen = signal(false);
  readonly snapshotTimestamp = signal('');

  readonly aiReportMounted = signal(false);
  readonly aiReportOpen = signal(false);
  readonly aiReportLoading = signal(false);
  readonly aiReportError = signal<string | null>(null);

  readonly exploreOptions = signal<CanvasExploreOptions>({ datasets: [] });
  /** Cached explore results keyed by chart id (explorer charts fetch on filter change). */
  readonly exploreResults = signal<Record<string, CanvasExploreResult>>({});
  /** Count reportable charts without performing the expensive canvas image export. */
  readonly aiReportChartCount = computed(() => {
    const report = this.selectedReport();
    if (!report) {
      return 0;
    }

    return this.orderedCharts(report).filter((chart) => {
      if (chart.explorer) {
        return (this.exploreResults()[chart.id]?.rows.length ?? 0) > 0;
      }

      return this.chartMetrics(chart).some((metric) => metric.series.length > 0);
    }).length;
  });
  private explorerRequestVersion = 0;

  /** Active filters, resolved to human-readable label/value pairs for the snapshot header. */
  readonly appliedFilters = computed<{ label: string; value: string }[]>(() => {
    const filters = this.filters();
    const t = (key: string) => this.languageService.t(key);
    const rows: { label: string; value: string }[] = [];
    const push = (labelKey: string, value: string) => {
      if (value) rows.push({ label: t(labelKey), value });
    };

    push('workspace.filters.fields.year', filters.year);
    if (filters.semester) {
      push('workspace.filters.fields.semester', this.semesterLabel(filters.semester));
    }
    if (filters.cycle) {
      push('workspace.filters.fields.cycle', t('workspace.filters.cycle.' + filters.cycle));
    }
    push('workspace.filters.fields.campus', filters.campus);
    push('workspace.filters.fields.level', filters.level);
    if (filters.grade) {
      push('workspace.filters.fields.grade', `${t('workspace.filters.gradePrefix')} ${filters.grade}`);
    }
    push('workspace.filters.fields.homeroom', filters.homeroom);
    if (filters.studentId) {
      const student = this.studentOptions().find((item) => String(item.id) === String(filters.studentId));
      push('workspace.filters.fields.student',
        student ? `${student.fullName} (${student.sisUserId})` : filters.studentId);
    }
    push('workspace.filters.fields.program', filters.program);
    push('workspace.filters.fields.subject', filters.subject);
    return rows;
  });

  private sheetUnmountTimer: number | null = null;
  private drilldownUnmountTimer: number | null = null;
  private aiReportUnmountTimer: number | null = null;

  workspaceDraft: WorkspaceDraft = this.emptyWorkspaceDraft();
  chartDraft: ChartDraft = this.emptyChartDraft();
  aiReportDraft: AiReportDraft = this.defaultAiReportDraft();

  readonly selectedReport = computed(() => {
    const selectedId = this.selectedReportId();
    return this.customReports().find((report) => report.id === selectedId) ?? null;
  });

  ngOnInit() {
    this.selectedReportId.set(this.route.snapshot.paramMap.get('workspaceId'));
    this.customReports.set(this.reportBuilderService.loadReports());

    this.reportBuilderService.loadRemoteReports()
      .pipe(timeout(this.workspaceRequestTimeoutMs))
      .subscribe({
        next: (remoteReports) => {
          if (!remoteReports.length) {
            if (this.selectedReport()) this.loadMetrics();
            else this.workspaceLoading.set(false);
            return;
          }
          this.customReports.set(remoteReports);
          this.reportBuilderService.saveReports(remoteReports);
          const report = this.selectedReport();
          if (!report?.serverId) {
            this.filters.set(defaultFilterSet());
            this.chartOrder.set([]);
            this.loadMetrics();
            return;
          }
          this.workspaceFilters.loadWorkspace(report.serverId)
            .pipe(timeout(this.workspaceRequestTimeoutMs))
            .subscribe({
              next: (filters) => {
                this.filters.set(filters);
                this.reconcileFilters();
                this.chartOrder.set(this.workspaceFilters.loadWorkspaceChartOrder());
                this.loadMetrics();
              },
              error: () => {
                this.filters.set(defaultFilterSet());
                this.chartOrder.set([]);
                this.loadMetrics();
              },
            });
        },
        error: () => {
          if (this.selectedReport()) this.loadMetrics();
          else this.workspaceLoading.set(false);
        },
      });

    this.reportBuilderService.loadSystemUsers().subscribe((users) => this.systemUsers.set(users));
    this.reportBuilderService.loadExploreOptions().subscribe((options) => this.exploreOptions.set(options));
    this.workspaceFilters.options().subscribe((options) => {
      this.options.set(options);
      this.filterOptionsLoaded = true;
      if (this.reconcileFilters()) {
        const workspaceId = this.selectedReport()?.serverId;
        if (workspaceId) this.workspaceFilters.saveWorkspace(workspaceId, this.filters());
        if (this.selectedReport()) this.loadMetrics();
      }
    });

  }

  setFilter(key: keyof WorkspaceFilterSet, value: string): void {
    this.filters.update((current) => {
      const next = { ...current, [key]: value };
      if (key === 'campus') {
        next.level = '';
        next.grade = '';
        next.homeroom = '';
        next.studentId = '';
      } else if (key === 'level') {
        next.grade = '';
        next.homeroom = '';
        next.studentId = '';
      } else if (key === 'grade') {
        next.homeroom = '';
        next.studentId = '';
      } else if (key === 'homeroom') {
        next.studentId = '';
      }
      return next;
    });
    const workspaceId = this.selectedReport()?.serverId;
    if (workspaceId) this.workspaceFilters.saveWorkspace(workspaceId, this.filters());
    this.loadMetrics();
  }

  resetFilters(): void {
    this.filters.set(defaultFilterSet());
    const workspaceId = this.selectedReport()?.serverId;
    if (workspaceId) this.workspaceFilters.saveWorkspace(workspaceId, this.filters());
    this.loadMetrics();
  }

  private reconcileFilters(): boolean {
    if (!this.filterOptionsLoaded) return false;
    const current = this.filters();
    const available = this.options();
    const next = { ...current };

    if (next.year && !available.academicYears.includes(next.year)) next.year = '';
    if (next.semester && !available.terms.includes(next.semester)) next.semester = '';
    if (next.campus && !available.campuses.includes(next.campus)) next.campus = '';
    const campusLevels = next.campus
      ? available.students.filter((student) => student.campus === next.campus)
          .map((student) => student.educationLevel)
      : available.educationLevels;
    if (next.level && !campusLevels.includes(next.level)) next.level = '';
    if (next.program && !available.programs.includes(next.program)) next.program = '';
    if (next.subject && !available.subjects.includes(next.subject)) next.subject = '';

    const scopedStudents = available.students.filter((student) =>
      (!next.campus || student.campus === next.campus) &&
      (!next.level || student.educationLevel === next.level));
    if (next.grade && !scopedStudents.some((student) => student.gradeLevel === next.grade)) {
      next.grade = '';
    }
    if (next.homeroom && !scopedStudents.some((student) =>
      (!next.grade || student.gradeLevel === next.grade) && student.homeroom === next.homeroom)) {
      next.homeroom = '';
    }
    if (next.studentId && !scopedStudents.some((student) =>
      String(student.id) === String(next.studentId) &&
      (!next.grade || student.gradeLevel === next.grade) &&
      (!next.homeroom || student.homeroom === next.homeroom))) {
      next.studentId = '';
    }

    if (JSON.stringify(next) === JSON.stringify(current)) return false;
    this.filters.set(next);
    return true;
  }

  orderedCharts(report: CustomReport): CustomChart[] {
    const order = this.chartOrder();
    if (!order.length) {
      return report.charts;
    }
    return [...report.charts].sort((a, b) => {
      const indexA = order.indexOf(a.id);
      const indexB = order.indexOf(b.id);
      return (indexA === -1 ? order.length : indexA) - (indexB === -1 ? order.length : indexB);
    });
  }

  moveChart(report: CustomReport, chart: CustomChart, direction: -1 | 1): void {
    const charts = this.orderedCharts(report);
    const index = charts.findIndex((candidate) => candidate.id === chart.id);
    const target = index + direction;
    if (index === -1 || target < 0 || target >= charts.length) {
      return;
    }

    const ids = charts.map((candidate) => candidate.id);
    [ids[index], ids[target]] = [ids[target], ids[index]];
    this.chartOrder.set(ids);
    const workspaceId = this.selectedReport()?.serverId;
    if (workspaceId) {
      this.workspaceFilters.saveWorkspaceChartOrder(workspaceId, ids, this.filters());
    }
  }

  metricGroups(): MetricVariableGroup[] {
    return groupMetricVariables(this.metricVariables());
  }

  selectedMetricCount(group: MetricVariableGroup): number {
    return group.metrics.filter((metric) => this.chartDraft.metrics.includes(metric.id)).length;
  }

  trackMetricGroup(_index: number, group: MetricVariableGroup): string {
    return group.dataset;
  }

  trackMetric(_index: number, metric: MetricVariable): string {
    return metric.id;
  }

  private scaledSeries(metric: MetricVariable): number[] {
    return metric.series.map((point) => point.value);
  }

  private metricPeriods(metrics: MetricVariable[]): string[] {
    return Array.from(new Set(metrics.flatMap((metric) => metric.series.map((point) => point.periodStart.slice(0, 7)))))
      .map((month) => `${month}-01T00:00:00Z`)
      .sort((left, right) => new Date(left).getTime() - new Date(right).getTime());
  }

  private metricPoint(metric: MetricVariable, periodStart: string) {
    const month = periodStart.slice(0, 7);
    return metric.series.find((point) => point.periodStart.slice(0, 7) === month);
  }

  private loadMetrics(): void {
    const report = this.selectedReport();
    if (!report) {
      this.workspaceLoading.set(false);
      return;
    }

    const cycle = ++this.renderCycle;
    const params = this.filterParams();
    const explorerCharts = report.charts.filter((chart) => !!chart.explorer);

    this.workspaceLoading.set(true);
    this.loadingDataRequests = 1 + explorerCharts.length;
    this.exploreResults.set({});

    this.reportBuilderService.loadMetricVariables(params)
      .pipe(timeout(this.workspaceRequestTimeoutMs))
      .subscribe({
        next: () => this.completeChartDataRequest(cycle),
        error: () => this.completeChartDataRequest(cycle),
      });
    this.loadExplorerCharts(cycle, params, explorerCharts);
  }

  private filterParams(): Record<string, string> {
    const filters = this.filters();
    return Object.fromEntries(Object.entries({
      year: filters.year, term: filters.semester, cycle: filters.cycle, campus: filters.campus,
      level: filters.level, grade: filters.grade, homeroom: filters.homeroom,
      studentId: filters.studentId, program: filters.program, subject: filters.subject,
    }).filter(([, value]) => !!value)) as Record<string, string>;
  }

  /** Fetch aggregated data for every explorer chart in the selected report. */
  private loadExplorerCharts(
    cycle: number,
    base: Record<string, string>,
    explorerCharts: CustomChart[],
  ): void {
    const requestVersion = ++this.explorerRequestVersion;
    for (const chart of explorerCharts) {
      if (!chart.explorer) continue;
      const { dataset, dimension, measure } = chart.explorer;
      this.reportBuilderService
        .loadExplore({ ...base, dataset, dimension, measure })
        .pipe(timeout(this.workspaceRequestTimeoutMs))
        .subscribe({
          next: (result) => {
            if (cycle === this.renderCycle && requestVersion === this.explorerRequestVersion) {
              this.exploreResults.update((current) => ({ ...current, [chart.id]: result }));
            }
            this.completeChartDataRequest(cycle);
          },
          error: () => this.completeChartDataRequest(cycle),
        });
    }
  }

  private completeChartDataRequest(cycle: number): void {
    if (cycle !== this.renderCycle) return;

    this.loadingDataRequests = Math.max(0, this.loadingDataRequests - 1);
    if (this.loadingDataRequests > 0) return;

    // Data completion is the only loading boundary. Chart painting continues
    // independently and never keeps the page-blocking overlay open.
    this.workspaceLoading.set(false);
  }

  startEditWorkspace() {
    const report = this.selectedReport();
    if (!report || !this.isSelectedOwner()) {
      return;
    }

    this.workspaceDraft = {
      title: report.title,
      description: report.description,

      sharedWith: report.sharedWith.filter((username) =>
        this.systemUsers().some((user) => user.username === username),
      ),
    };
    this.openSheet('workspace');
  }

  saveWorkspaceDraft() {
    const report = this.selectedReport();
    if (!report || !this.isSelectedOwner() || !this.workspaceDraft.title.trim()) {
      return;
    }

    report.title = this.workspaceDraft.title.trim();
    report.description = this.workspaceDraft.description.trim();

    report.sharedWith = [...this.workspaceDraft.sharedWith];
    this.persistReport(report);
    this.closeSheet();
  }

  startCreateChart() {
    this.chartDraft = this.emptyChartDraft();
    this.chartDraft.title = this.languageService.t('workspace.defaultNewChartTitle');
    this.openSheet('chart');
  }

  startEditChart(chart: CustomChart) {
    this.chartDraft = {
      id: chart.id,
      title: chart.title,
      kind: chart.kind,
      metrics: [...chart.metrics],
      showBaseline: chart.showBaseline ?? false,
      kpiTarget: chart.kpiTarget ?? null,
      source: chart.explorer ? 'explorer' : 'metric',
      dataset: chart.explorer?.dataset ?? '',
      dimension: chart.explorer?.dimension ?? '',
      measure: chart.explorer?.measure ?? '',
    };
    this.openSheet('chart');
  }

  saveChartDraft() {
    const report = this.selectedReport();
    if (!report || !this.isSelectedOwner() || !this.chartDraft.title.trim() || !this.isChartDraftValid()) {
      return;
    }

    const kpiTargetRaw = this.chartDraft.kpiTarget;
    const kpiTarget =
      kpiTargetRaw === null || kpiTargetRaw === undefined || Number.isNaN(Number(kpiTargetRaw))
        ? null
        : Number(kpiTargetRaw);

    const isExplorer = this.chartDraft.source === 'explorer';
    const build = (id: string): CustomChart => ({
      id,
      title: this.chartDraft.title.trim(),
      kind: this.chartDraft.kind,
      metrics: isExplorer ? [] : [...this.chartDraft.metrics],
      showBaseline: isExplorer ? false : this.chartDraft.showBaseline,
      kpiTarget: isExplorer ? null : kpiTarget,
      explorer: isExplorer
        ? { dataset: this.chartDraft.dataset, dimension: this.chartDraft.dimension, measure: this.chartDraft.measure }
        : undefined,
    });

    if (this.chartDraft.id) {
      const draftId = this.chartDraft.id;
      report.charts = report.charts.map((chart) => (chart.id === draftId ? build(draftId) : chart));
    } else {
      report.charts = [...report.charts, build(crypto.randomUUID())];
    }

    this.persistReport(report);
    this.loadMetrics();
    this.closeSheet();
  }

  // ---- Canvas data explorer (chart builder) ----

  isChartDraftValid(): boolean {
    return this.chartDraft.source === 'explorer'
      ? !!(this.chartDraft.dataset && this.chartDraft.dimension && this.chartDraft.measure)
      : this.chartDraft.metrics.length > 0;
  }

  onChartSourceChange(): void {
    if (this.chartDraft.source === 'explorer') {
      // Explorer charts render via ECharts; card/scatter aren't supported.
      if (this.chartDraft.kind === 'card' || this.chartDraft.kind === 'scatter') {
        this.chartDraft.kind = 'bar';
      }
      this.chartDraft.metrics = [];
      const datasets = this.exploreOptions().datasets;
      if (datasets.length && !this.chartDraft.dataset) {
        this.chartDraft.dataset = datasets[0].key;
        this.onDatasetChange();
      }
    }
  }

  onDatasetChange(): void {
    const dataset = this.exploreOptions().datasets.find((item) => item.key === this.chartDraft.dataset);
    this.chartDraft.dimension = dataset?.dimensions[0]?.key ?? '';
    this.chartDraft.measure = dataset?.measures[0]?.key ?? '';
  }

  datasetDimensions(): CanvasExploreField[] {
    return this.exploreOptions().datasets.find((item) => item.key === this.chartDraft.dataset)?.dimensions ?? [];
  }

  datasetMeasures(): CanvasExploreField[] {
    return this.exploreOptions().datasets.find((item) => item.key === this.chartDraft.dataset)?.measures ?? [];
  }

  exploreDatasetLabel(key: string): string {
    return this.tr(`workspace.explorer.dataset.${key}`,
      this.exploreOptions().datasets.find((item) => item.key === key)?.label ?? key);
  }

  exploreFieldLabel(key: string, fallback: string): string {
    return this.tr(`workspace.explorer.field.${key}`, fallback);
  }

  private tr(key: string, fallback: string): string {
    const value = this.languageService.t(key);
    return value === key ? fallback : value;
  }

  semesterLabel(term: string): string {
    return this.tr(`workspace.filters.semester.${term}`, term);
  }

  deleteChartDraft() {
    const report = this.selectedReport();
    if (!report || !this.chartDraft.id || !this.isSelectedOwner()) {
      return;
    }

    report.charts = report.charts.filter((chart) => chart.id !== this.chartDraft.id);
    this.persistReport(report);
    this.closeSheet();
  }

  duplicateSelected() {
    const report = this.selectedReport();
    if (!report || !this.isSelectedOwner()) {
      return;
    }

    const copy: CustomReport = {
      ...structuredClone(report),
      id: crypto.randomUUID(),
      serverId: undefined,
      owner: this.owner(),
      title: `${report.title} ${this.languageService.t('workspace.copySuffix')}`,

      sharedWith: [],
      updatedAt: new Date().toISOString(),
      charts: report.charts.map((chart) => ({ ...chart, id: crypto.randomUUID() })),
    };

    this.customReports.set([copy, ...this.customReports()]);
    this.selectedReportId.set(copy.id);
    this.persistReport(copy);
    this.closeSheet();
    this.router.navigate(['/workspace', copy.id]);
  }

  deleteSelected() {
    const report = this.selectedReport();
    if (!report || !this.isSelectedOwner()) {
      return;
    }

    this.customReports.set(this.customReports().filter((candidate) => candidate.id !== report.id));
    this.reportBuilderService.saveReports(this.customReports());
    this.reportBuilderService.deleteRemoteReport(report).subscribe();
    this.closeSheet();
    this.router.navigate(['/workspace']);
  }

  toggleShareUser(username: string) {
    const selected = new Set(this.workspaceDraft.sharedWith);
    if (selected.has(username)) {
      selected.delete(username);
    } else {
      selected.add(username);
    }
    this.workspaceDraft.sharedWith = [...selected];
  }

  toggleChartMetric(metricId: string) {
    const selected = new Set(this.chartDraft.metrics);
    if (selected.has(metricId)) {
      selected.delete(metricId);
    } else {
      selected.add(metricId);
    }
    this.chartDraft.metrics = [...selected];
  }

  selectedShareLabel(): string {
    if (!this.workspaceDraft.sharedWith.length) {
      return this.languageService.t('workspace.selectUsers');
    }
    return this.workspaceDraft.sharedWith
      .map(
        (username) =>
          this.systemUsers().find((user) => user.username === username)?.fullName ?? username,
      )
      .join(', ');
  }

  isSelectedOwner(): boolean {
    return this.selectedReport()?.owner === this.owner();
  }

  closeSheet() {
    this.sheetOpen.set(false);
    this.shareDropdownOpen.set(false);
    this.sheetUnmountTimer = window.setTimeout(() => {
      this.sheetMounted.set(false);
      this.sheetUnmountTimer = null;
    }, 300);
  }

  openAiReportSheet(): void {
    const report = this.selectedReport();
    if (!report?.serverId || !report.charts.length) {
      return;
    }

    if (this.aiReportUnmountTimer !== null) {
      window.clearTimeout(this.aiReportUnmountTimer);
      this.aiReportUnmountTimer = null;
    }

    this.aiReportDraft = this.defaultAiReportDraft();
    this.aiReportError.set(null);
    this.aiReportMounted.set(true);
    // Give the browser one painted frame with the panel off-screen before
    // transitioning it in. This avoids a first-open layout jump.
    requestAnimationFrame(() => {
      requestAnimationFrame(() => {
        if (this.aiReportMounted()) {
          this.aiReportOpen.set(true);
        }
      });
    });
  }

  closeAiReportSheet(): void {
    if (this.aiReportLoading()) {
      return;
    }

    this.aiReportOpen.set(false);
    this.aiReportUnmountTimer = window.setTimeout(() => {
      this.aiReportMounted.set(false);
      this.aiReportUnmountTimer = null;
    }, 300);
  }

  generateAiReport(): void {
    const report = this.selectedReport();
    if (!report?.serverId || this.aiReportLoading()) {
      return;
    }

    const charts = this.buildAiChartSnapshots();
    if (!charts.length) {
      this.aiReportError.set(this.languageService.t('workspace.aiReport.noData'));
      return;
    }

    const request: WorkspaceAiReportRequest = {
      ...this.aiReportDraft,
      additionalInstructions: this.aiReportDraft.additionalInstructions.trim(),
      filters: this.appliedFilters(),
      charts,
    };

    this.aiReportLoading.set(true);
    this.aiReportError.set(null);
    this.reportBuilderService.generateAiReport(report.serverId, request).subscribe({
      next: (response) => {
        const blob = response.body;
        if (!blob) {
          this.aiReportLoading.set(false);
          this.aiReportError.set(this.languageService.t('workspace.aiReport.error'));
          return;
        }

        const disposition = response.headers.get('content-disposition');
        const filename = this.downloadFilename(disposition, report.title);
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = filename;
        document.body.appendChild(link);
        link.click();
        link.remove();
        window.setTimeout(() => URL.revokeObjectURL(url), 0);

        this.aiReportLoading.set(false);
        this.closeAiReportSheet();
      },
      error: (error: { status?: number }) => {
        this.aiReportLoading.set(false);
        this.aiReportError.set(this.languageService.t(
          error.status === 503
            ? 'workspace.aiReport.notConfigured'
            : 'workspace.aiReport.error',
        ));
      },
    });
  }

  metricLabel(metricId: string): string {
    const key = `workspace.metrics.${metricId}.label`;
    const translated = this.languageService.t(key);
    return translated === key
      ? this.metricVariables().find((metric) => metric.id === metricId)?.label ?? metricId
      : translated;
  }

  metricLineage(metric: MetricVariable): string {
    const categoryKey = `workspace.categories.${metric.category}`;
    const sourceKey = `workspace.sources.${metric.source}`;
    const translatedCategory = this.languageService.t(categoryKey);
    const translatedSource = this.languageService.t(sourceKey);
    const category = translatedCategory === categoryKey ? metric.category : translatedCategory;
    const source = translatedSource === sourceKey ? metric.source : translatedSource;
    return `${category} · ${source}`;
  }

  categoryLabel(category: string): string {
    const key = `workspace.categories.${category}`;
    const translated = this.languageService.t(key);
    return translated === key ? category : translated;
  }

  sourceLabel(source: string): string {
    const key = `workspace.sources.${source}`;
    const translated = this.languageService.t(key);
    return translated === key ? source : translated;
  }

  datasetLabel(dataset: string): string {
    const key = `workspace.datasets.${dataset}`;
    const translated = this.languageService.t(key);
    return translated === key ? dataset : translated;
  }

  chartMetrics(chart: CustomChart): MetricVariable[] {
    return chart.metrics
      .map((metricId) => this.metricVariables().find((metric) => metric.id === metricId))
      .filter((metric): metric is MetricVariable => !!metric);
  }

  explorerResult(chart: CustomChart): CanvasExploreResult | null {
    return this.exploreResults()[chart.id] ?? null;
  }

  private buildAiChartSnapshots(): WorkspaceAiChartSnapshot[] {
    const report = this.selectedReport();
    if (!report) {
      return [];
    }

    return this.orderedCharts(report).flatMap((chart) => {
      if (chart.explorer) {
        const result = this.explorerResult(chart);
        if (!result?.rows.length) {
          return [];
        }

        return [{
          chartId: chart.id,
          title: chart.title,
          chartType: this.languageService.t(`workspace.chartType.${chart.kind}`),
          source: this.exploreDatasetLabel(chart.explorer.dataset),
          imageDataUrl: this.chartSnapshotDataUrl(chart),
          series: [{
            name: this.exploreFieldLabel(chart.explorer.measure, chart.explorer.measure),
            unit: result.unit,
            points: result.rows.map((row) => ({
              label: this.explorerKeyLabel(row.key),
              value: row.value,
            })),
          }],
        }];
      }

      const metrics = this.chartMetrics(chart)
        .filter((metric) => metric.series.length > 0);
      if (!metrics.length) {
        return [];
      }

      return [{
        chartId: chart.id,
        title: chart.title,
        chartType: this.languageService.t(`workspace.chartType.${chart.kind}`),
        source: [...new Set(metrics.map((metric) => this.sourceLabel(metric.source)))].join(', '),
        imageDataUrl: this.chartSnapshotDataUrl(chart),
        series: metrics.map((metric) => ({
          name: this.metricLabel(metric.id),
          unit: metric.unit,
          points: metric.series.map((point) => ({
            label: this.periodDisplay(point.periodStart),
            value: point.value,
          })),
        })),
      }];
    });
  }

  onChartInit(chartId: string, chart: ChartImageExporter): void {
    this.chartImageExporters.set(chartId, chart);
  }

  private chartSnapshotDataUrl(chart: CustomChart): string | null {
    try {
      const dataUrl = chart.kind === 'card'
        ? this.cardChartSnapshotDataUrl(chart)
        : this.chartImageExporters.get(chart.id)?.getDataURL({
            type: 'png',
            pixelRatio: 1.5,
            backgroundColor: '#ffffff',
            excludeComponents: ['toolbox'],
          });
      return dataUrl?.startsWith('data:image/png;base64,') && dataUrl.length <= 2_000_000
        ? dataUrl
        : null;
    } catch {
      return null;
    }
  }

  private cardChartSnapshotDataUrl(chart: CustomChart): string | null {
    const metrics = this.chartMetrics(chart);
    if (!metrics.length) return null;

    const canvas = document.createElement('canvas');
    canvas.width = 1200;
    canvas.height = Math.max(420, 170 + Math.ceil(metrics.length / 2) * 190);
    const context = canvas.getContext('2d');
    if (!context) return null;

    context.fillStyle = '#ffffff';
    context.fillRect(0, 0, canvas.width, canvas.height);
    context.fillStyle = '#32124f';
    context.font = '600 36px Calibri, Arial, sans-serif';
    context.fillText(chart.title, 48, 62);

    metrics.forEach((metric, index) => {
      const column = index % 2;
      const row = Math.floor(index / 2);
      const x = 48 + column * 570;
      const y = 100 + row * 190;
      context.fillStyle = '#faf9f6';
      context.strokeStyle = '#e4dde8';
      context.lineWidth = 2;
      context.fillRect(x, y, 530, 154);
      context.strokeRect(x, y, 530, 154);
      context.fillStyle = '#6f6877';
      context.font = '500 25px Calibri, Arial, sans-serif';
      const label = this.metricLabel(metric.id);
      context.fillText(label.length > 36 ? `${label.slice(0, 35)}…` : label, x + 26, y + 45);
      context.fillStyle = '#32124f';
      context.font = '600 48px Calibri, Arial, sans-serif';
      context.fillText(this.cardValue(metric), x + 26, y + 112);
    });

    return canvas.toDataURL('image/png');
  }

  explorerHasRows(chart: CustomChart): boolean {
    return (this.explorerResult(chart)?.rows.length ?? 0) > 0;
  }

  /**
   * Dense category charts need the full report width. Small summaries intentionally
   * remain in the two-column grid so the workspace stays compact.
   */
  isWideChart(chart: CustomChart): boolean {
    if (chart.explorer) {
      return (this.explorerResult(chart)?.rows.length ?? 0) > 12;
    }
    if (chart.kind === 'card') return false;
    const metrics = this.chartMetrics(chart);
    return metrics.length > 3 || this.metricPeriods(metrics).length > 18;
  }

  explorerCaption(chart: CustomChart): string {
    if (!chart.explorer) return '';
    const dataset = this.exploreDatasetLabel(chart.explorer.dataset);
    const measure = this.exploreFieldLabel(chart.explorer.measure, chart.explorer.measure);
    const dimension = this.exploreFieldLabel(chart.explorer.dimension, chart.explorer.dimension);
    return `${dataset} · ${measure} ${this.languageService.t('workspace.explorer.by')} ${dimension}`;
  }

  /** Format a dimension key for display (month keys "2025-11" become "Nov 2025"). */
  private explorerKeyLabel(key: string): string {
    const monthMatch = /^(\d{4})-(\d{2})$/.exec(key);
    if (monthMatch) {
      return this.periodDisplay(`${key}-01T00:00:00Z`);
    }
    const statusKey: Record<string, string> = {
      'Not submitted': 'notSubmitted',
      'On time': 'onTime',
      Late: 'late',
      Missing: 'missing',
      Excused: 'excused',
    };
    if (statusKey[key]) {
      return this.languageService.t(`workspace.explorer.values.${statusKey[key]}`);
    }
    return key;
  }

  private wrapCategoryLabel(value: string, maximumLineLength = 34): string {
    const words = value.split(/\s+/).filter(Boolean);
    const lines: string[] = [];
    let current = '';
    for (const word of words) {
      if (!current) {
        current = word;
      } else if (`${current} ${word}`.length <= maximumLineLength) {
        current += ` ${word}`;
      } else {
        lines.push(current);
        current = word;
      }
    }
    if (current) lines.push(current);
    return lines.join('\n');
  }

  private explorerChartOptions(chart: CustomChart): EChartsCoreOption {
    const result = this.explorerResult(chart);
    const colors = ['#32124f', '#b38116', '#31706f', '#7b4aa4', '#a9485d', '#4464ad'];
    const rows = result?.rows ?? [];
    const labels = rows.map((row) => this.explorerKeyLabel(row.key));
    const values = rows.map((row) => row.value);
    const unit = result?.unit ?? '';
    const fmt = (value: string | number) => (unit === '%' ? `${value}%` : `${value}`);
    const denseCategories = rows.length > 12;

    if (chart.kind === 'pie') {
      return {
        color: colors,
        tooltip: { trigger: 'item', valueFormatter: fmt },
        legend: { bottom: 0, type: 'scroll', icon: 'circle', textStyle: { color: '#5f5368' } },
        toolbox: { right: 8, feature: { restore: {}, saveAsImage: {} } },
        series: [
          {
            name: chart.title,
            type: 'pie',
            radius: ['42%', '68%'],
            center: ['50%', '43%'],
            data: rows.map((row) => ({
              name: this.explorerKeyLabel(row.key), value: row.value, groupKey: row.key,
            })),
          },
        ],
      };
    }

    if (chart.kind === 'bar' && denseCategories) {
      const savedZoom = this.chartZoomRange(chart.id);
      const initialZoom = {
        start: 0,
        end: Math.min(100, (10 / rows.length) * 100),
        ...savedZoom,
      };
      return {
        color: colors,
        tooltip: { trigger: 'axis', axisPointer: { type: 'shadow' }, valueFormatter: fmt },
        grid: { left: 18, right: 76, bottom: 24, top: 48, containLabel: true },
        toolbox: {
          right: 28,
          feature: {
            restore: {},
            saveAsImage: {},
          },
        },
        xAxis: {
          type: 'value',
          axisLabel: { color: '#6f6877', formatter: fmt },
          splitLine: { lineStyle: { color: '#ebe6ef' } },
        },
        yAxis: {
          type: 'category',
          inverse: true,
          data: labels,
          axisLine: { lineStyle: { color: '#d8d0df' } },
          axisTick: { show: false },
          axisLabel: {
            color: '#5f5368',
            interval: 0,
            margin: 14,
            fontSize: 11,
            lineHeight: 15,
            formatter: (value: string) => this.wrapCategoryLabel(value),
          },
        },
        dataZoom: [
          {
            type: 'slider', yAxisIndex: 0, filterMode: 'filter', orient: 'vertical',
            ...initialZoom, width: 16, right: 8, top: 54, bottom: 24,
            borderColor: '#d8d0df', fillerColor: 'rgba(109,58,145,0.16)',
            handleStyle: { color: '#6d3a91' }, showDetail: false,
          },
        ],
        series: [
          {
            name: chart.title,
            type: 'bar',
            cursor: 'pointer',
            data: rows.map((row) => ({ value: row.value, groupKey: row.key })),
            barMaxWidth: 24,
            itemStyle: { color: '#32124f', borderRadius: [0, 4, 4, 0] },
            label: {
              show: true,
              position: 'right',
              color: '#5f5368',
              fontSize: 11,
              formatter: (params: { value?: string | number }) => fmt(params.value ?? ''),
            },
          },
        ],
      };
    }

    const cartesianKind = chart.kind === 'line' ? 'line' : 'bar';
    const savedZoom = this.chartZoomRange(chart.id);
    const initialZoom = denseCategories
      ? { start: 0, end: Math.min(100, (12 / rows.length) * 100), ...savedZoom }
      : savedZoom;
    const dataZoom = denseCategories
      ? [
          { type: 'inside' as const, filterMode: 'filter' as const, ...initialZoom },
          {
            type: 'slider' as const, filterMode: 'filter' as const, ...initialZoom,
            height: 18, bottom: 8, borderColor: '#d8d0df', fillerColor: 'rgba(109,58,145,0.16)',
            handleStyle: { color: '#6d3a91' }, showDetail: false,
          },
        ]
      : [{ type: 'inside' as const, ...savedZoom }];
    return {
      color: colors,
      tooltip: { trigger: 'axis', valueFormatter: fmt },
      grid: { left: 16, right: 24, bottom: denseCategories ? 82 : 52, top: 44, containLabel: true },
      toolbox: {
        right: 8,
        feature: { dataZoom: { yAxisIndex: 'none' }, magicType: { type: ['line', 'bar'] }, restore: {}, saveAsImage: {} },
      },
      xAxis: {
        type: 'category',
        data: labels,
        axisLine: { lineStyle: { color: '#d8d0df' } },
        axisLabel: {
          color: '#6f6877',
          interval: 0,
          rotate: labels.length > 6 ? 28 : 0,
          margin: 14,
          fontSize: 11,
          formatter: (value: string) => value.length > 20 ? `${value.slice(0, 19)}…` : value,
        },
      },
      yAxis: {
        type: 'value',
        axisLabel: { color: '#6f6877', formatter: fmt },
        splitLine: { lineStyle: { color: '#ebe6ef' } },
      },
      dataZoom,
      series: [
        {
          name: chart.title,
          type: cartesianKind,
          cursor: 'pointer',
          data: rows.map((row) => ({ value: row.value, groupKey: row.key })),
          // Monthly/category values are discrete observations. Spline smoothing can
          // overshoot sharply-changing counts and make the chart imply values that
          // are not present in the source data.
          smooth: false,
          connectNulls: true,
          showSymbol: cartesianKind === 'line' && rows.length <= 36,
          symbol: 'circle',
          symbolSize: 6,
          barMaxWidth: 34,
          itemStyle: { color: '#32124f' },
          lineStyle: cartesianKind === 'line' ? { width: 2 } : undefined,
        },
      ],
    };
  }

  chartOptions(chart: CustomChart): EChartsCoreOption {
    if (chart.explorer) {
      return this.explorerChartOptions(chart);
    }
    const metrics = this.chartMetrics(chart);
    const colors = ['#32124f', '#b38116', '#31706f', '#7b4aa4', '#a9485d', '#4464ad'];

    if (chart.kind === 'pie') {
      return {
        color: colors,
        tooltip: {
          trigger: 'item',
          valueFormatter: (value: string | number) => `${value}`,
        },
        legend: {
          bottom: 0,
          type: 'scroll',
          icon: 'circle',
          textStyle: { color: '#5f5368' },
        },
        toolbox: {
          right: 8,
          feature: {
            restore: {},
            saveAsImage: {},
          },
        },
        series: [
          {
            name: chart.title,
            type: 'pie',
            radius: ['42%', '68%'],
            center: ['50%', '43%'],
            avoidLabelOverlap: true,
            itemStyle: {
              borderColor: '#ffffff',
              borderRadius: 6,
              borderWidth: 3,
            },
            label: {
              color: '#5f5368',
              formatter: '{b}\n{d}%',
              lineHeight: 18,
            },
            labelLine: {
              length: 12,
              length2: 8,
            },
            emphasis: {
              scale: true,
              scaleSize: 8,
              itemStyle: {
                shadowBlur: 16,
                shadowColor: 'rgba(50, 18, 79, 0.24)',
              },
            },
            data: metrics.map((metric) => {
              return {
                name: this.metricLabel(metric.id),
                // Pie charts are filter-scope summaries rather than time-series
                // snapshots. Using each metric's latest point can silently compare
                // different months and send each slice to a different drill-down.
                value: Math.max(0, metric.value ?? 0),
              };
            }),
          },
        ],
      };
    }

    if (chart.kind === 'scatter') {
      const xMetric = metrics[0];
      const comparedMetrics = metrics.length > 1 ? metrics.slice(1) : metrics;

      return {
        color: colors,
        tooltip: {
          trigger: 'item',
          axisPointer: { type: 'cross' },
        },
        legend: {
          bottom: 0,
          type: 'scroll',
          icon: 'circle',
          textStyle: { color: '#5f5368' },
        },
        grid: {
          top: 42,
          right: 34,
          bottom: 72,
          left: 58,
          containLabel: true,
        },
        toolbox: {
          right: 8,
          feature: {
            dataZoom: {},
            restore: {},
            saveAsImage: {},
          },
        },
        xAxis: {
          type: 'value',
          name:
            metrics.length > 1 && xMetric
              ? this.metricLabel(xMetric.id)
              : this.languageService.t('workspace.chartAxis.period'),
          nameLocation: 'middle',
          nameGap: 32,
          axisLine: { lineStyle: { color: '#d8d0df' } },
          axisLabel: { color: '#6f6877' },
          splitLine: { lineStyle: { color: '#ebe6ef' } },
        },
        yAxis: {
          type: 'value',
          axisLabel: { color: '#6f6877' },
          splitLine: { lineStyle: { color: '#ebe6ef' } },
        },
        dataZoom: [{ type: 'inside', ...this.chartZoomRange(chart.id) }],
        series: comparedMetrics.map((metric) => ({
          name: this.metricLabel(metric.id),
          type: 'scatter',
          symbolSize: 12,
          cursor: 'pointer',
          data: metric.series
            .map((point, index) => ({
              name: this.periodDisplay(point.periodStart),
              periodStart: point.periodStart,
              value: [
                metrics.length > 1
                  ? (xMetric ? this.metricPoint(xMetric, point.periodStart)?.value ?? null : null)
                  : index + 1,
                point.value,
              ],
            }))
            .filter((point) => point.value[0] !== null),
          emphasis: {
            focus: 'series',
            scale: 1.5,
          },
        })),
      };
    }

    const cartesianKind = chart.kind === 'line' ? 'line' : 'bar';
    const periods = this.metricPeriods(metrics);
    const baseline = metrics[0]?.baselineValue ?? null;

    const referenceLines: object[] = [];
    if (chart.showBaseline && baseline !== null) {
      referenceLines.push({
        yAxis: baseline,
        name: this.languageService.t('workspace.baselineLine'),
        lineStyle: { color: '#7b4aa4', type: 'dashed', width: 1.5 },
        label: { formatter: `${this.languageService.t('workspace.baselineLine')}: ${baseline}`, color: '#7b4aa4' },
      });
    }
    const kpiTarget = chart.kpiTarget ?? metrics[0]?.kpiTarget ?? null;
    if (kpiTarget !== null) {
      referenceLines.push({
        yAxis: kpiTarget,
        name: this.languageService.t('workspace.kpiLine'),
        lineStyle: { color: '#c94f4f', type: 'dashed', width: 1.5 },
        label: { formatter: `${this.languageService.t('workspace.kpiLine')}: ${kpiTarget}`, color: '#c94f4f' },
      });
    }
    const usesDualUnitAxis = metrics.some((metric) => metric.unit === '%')
      && metrics.some((metric) => metric.unit !== '%');
    const valueAxis = (percentage: boolean, right = false) => ({
      type: 'value' as const,
      name: this.languageService.t(percentage ? 'workspace.chartAxis.percentage' : 'workspace.chartAxis.count'),
      nameTextStyle: { color: '#7c7284' },
      position: right ? 'right' as const : 'left' as const,
      axisLabel: {
        color: '#6f6877',
        formatter: percentage ? '{value}%' : '{value}',
      },
      splitLine: right ? { show: false } : { lineStyle: { color: '#ebe6ef' } },
    });

    return {
      color: colors,
      tooltip: {
        trigger: 'axis',
        axisPointer: { type: cartesianKind === 'bar' ? 'shadow' : 'cross' },
        formatter: (params: unknown) => this.metricAxisTooltip(params),
      },
      legend: {
        bottom: 0,
        icon: 'roundRect',
        textStyle: { color: '#5f5368' },
      },
      grid: {
        top: 36,
        right: 24,
        bottom: 72,
        left: 48,
        containLabel: true,
      },
      toolbox: {
        right: 8,
        feature: {
          dataZoom: { yAxisIndex: 'none' },
          magicType: { type: ['line', 'bar'] },
          restore: {},
          saveAsImage: {},
        },
      },
      xAxis: {
        type: 'category',
        boundaryGap: cartesianKind === 'bar',
        data: periods.map((period) => this.periodDisplay(period)),
        axisLine: { lineStyle: { color: '#d8d0df' } },
        axisLabel: { color: '#6f6877' },
      },
      yAxis: usesDualUnitAxis
        ? [valueAxis(true), valueAxis(false, true)]
        : valueAxis(metrics.every((metric) => metric.unit === '%')),
      dataZoom: [
        {
          type: 'inside',
          ...this.chartZoomRange(chart.id),
        },
      ],
      series: metrics.map((metric, index) => ({
        name: this.metricLabel(metric.id),
        type: cartesianKind,
        cursor: 'pointer',
        yAxisIndex: usesDualUnitAxis && metric.unit !== '%' ? 1 : 0,
        data: periods.map((periodStart) => ({
          value: this.metricPoint(metric, periodStart)?.value ?? null,
          periodStart,
        })),
        // Keep count/percentage observations faithful to each period. Full spline
        // smoothing creates artificial waves and overshoot when adjacent months
        // differ substantially (especially across long Canvas histories).
        smooth: false,
        connectNulls: true,
        // A sparse metric may share a long axis with a denser one. Base marker
        // visibility on this metric's observations so isolated values remain visible.
        showSymbol: cartesianKind === 'line' && metric.series.length <= 36,
        symbol: 'circle',
        symbolSize: 6,
        lineStyle: cartesianKind === 'line' ? { width: 2 } : undefined,
        barMaxWidth: 30,
        barMinHeight: cartesianKind === 'bar' ? 6 : undefined,
        emphasis: { focus: 'series' },
        markLine:
          index === 0 && referenceLines.length
            ? { symbol: 'none', data: referenceLines }
            : undefined,
      })),
    };
  }

  seriesColor(index: number): string {
    return ['#32124f', '#b38116', '#31706f', '#7b4aa4'][index % 4];
  }

  private metricAxisTooltip(params: unknown): string {
    const points = (Array.isArray(params) ? params : [params]) as {
      axisValueLabel?: string;
      name?: string;
      seriesName?: string;
      marker?: string;
      value?: unknown;
    }[];
    const visible = points.filter((point) => {
      const value = Array.isArray(point.value) ? point.value.at(-1) : point.value;
      return value !== null && value !== undefined && value !== '' && value !== '-';
    });
    if (!visible.length) return '';

    const heading = this.escapeTooltipText(
      points[0]?.axisValueLabel ?? points[0]?.name ?? '',
    );
    const rows = visible.map((point) => {
      const value = Array.isArray(point.value) ? point.value.at(-1) : point.value;
      return `${point.marker ?? ''}${this.escapeTooltipText(point.seriesName ?? '')}` +
        `<span style="float:right;margin-left:24px;font-weight:600">${this.escapeTooltipText(String(value))}</span>`;
    });
    return [heading, ...rows].filter(Boolean).join('<br/>');
  }

  private escapeTooltipText(value: string): string {
    return value
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#39;');
  }

  private periodDisplay(value: string): string {
    return new Intl.DateTimeFormat(this.languageService.language() === 'vi' ? 'vi-VN' : 'en-US', {
      month: 'short', year: 'numeric',
    }).format(new Date(value));
  }

  onChartPointClick(chart: CustomChart, event: { seriesName?: string; name?: string; data?: unknown }): void {
    if (chart.explorer) {
      const data = event?.data as { groupKey?: string } | undefined;
      const groupKey = data?.groupKey;
      if (groupKey) {
        this.openExplorerDrilldown(chart, groupKey);
      }
      return;
    }
    const metrics = this.chartMetrics(chart);
    if (!metrics.length) {
      return;
    }

    const metric = metrics.find((candidate) => this.metricLabel(candidate.id) === event?.name)
      ?? metrics.find((candidate) => this.metricLabel(candidate.id) === event?.seriesName)
      ?? metrics[0];
    const clickedData = event?.data as { periodStart?: string } | undefined;
    const periodStart = clickedData?.periodStart ?? null;
    const periodLabel = periodStart ? this.periodDisplay(periodStart) : null;

    this.openDrilldown(metric.id, periodStart, periodLabel);
  }

  onChartDataZoom(
    chartId: string,
    event: { start?: number; end?: number; batch?: { start?: number; end?: number }[] },
  ): void {
    const range = event.batch?.[0] ?? event;
    if (typeof range.start === 'number' && typeof range.end === 'number') {
      this.chartZoomRanges.set(chartId, { start: range.start, end: range.end });
    }
  }

  private chartZoomRange(chartId: string): { start?: number; end?: number } {
    return this.chartZoomRanges.get(chartId) ?? {};
  }

  openDrilldown(
    metricId: string,
    periodStart: string | null = null,
    periodLabel: string | null = null,
  ): void {
    this.drilldownMetricId.set(metricId);
    this.drilldownMetricLabel.set(this.metricLabel(metricId));
    this.drilldownPeriod.set(periodLabel);
    this.drilldownError.set(null);
    this.drilldownUnavailable.set(false);
    this.drilldownData.set(null);
    this.explorerDrilldown.set(false);
    this.explorerDrilldownData.set(null);
    this.explorerDrilldownSpec = null;
    this.drilldownPeriodStart = periodStart;
    this.mountDrilldown();

    const metric = this.metricVariables().find((candidate) => candidate.id === metricId);
    if (metric?.supportsDrilldown === false) {
      this.drilldownLoading.set(false);
      this.drilldownUnavailable.set(true);
      return;
    }

    this.loadDrilldownData(metricId, periodStart, 1);
  }

  closeDrilldown(): void {
    this.drilldownOpen.set(false);
    this.drilldownUnmountTimer = window.setTimeout(() => {
      this.drilldownMounted.set(false);
      this.drilldownUnmountTimer = null;
    }, 300);
  }

  studentInitials(name: string): string {
    return (
      name
        .trim()
        .split(/\s+/)
        .slice(-2)
        .map((part) => part[0]?.toUpperCase())
        .join('') || 'S'
    );
  }

  changeDrilldownPage(page: number): void {
    if (page < 1 || page > this.drilldownTotalPages()) return;
    if (this.explorerDrilldown() && this.explorerDrilldownSpec) {
      this.loadExplorerDrilldown(this.explorerDrilldownSpec, page);
      return;
    }
    const metricId = this.drilldownMetricId();
    if (metricId) this.loadDrilldownData(metricId, this.drilldownPeriodStart, page);
  }

  private openExplorerDrilldown(chart: CustomChart, groupKey: string): void {
    if (!chart.explorer) return;
    this.explorerDrilldown.set(true);
    this.explorerDrilldownData.set(null);
    this.drilldownData.set(null);
    this.drilldownUnavailable.set(false);
    this.drilldownError.set(null);
    this.drilldownMetricId.set(null);
    this.drilldownMetricLabel.set(chart.title);
    this.drilldownPeriod.set(this.explorerKeyLabel(groupKey));
    this.explorerDrilldownSpec = { ...chart.explorer, groupKey };
    this.mountDrilldown();
    this.loadExplorerDrilldown(this.explorerDrilldownSpec, 1);
  }

  private loadExplorerDrilldown(
    spec: { dataset: string; dimension: string; measure: string; groupKey: string },
    page: number,
  ): void {
    const filters = this.filters();
    const params = Object.fromEntries(Object.entries({
      ...spec,
      year: filters.year, term: filters.semester, campus: filters.campus,
      level: filters.level, grade: filters.grade, homeroom: filters.homeroom,
      studentId: filters.studentId, program: filters.program, subject: filters.subject,
      page: String(page), pageSize: String(this.drilldownPageSize),
    }).filter(([, value]) => !!value));
    this.drilldownLoading.set(true);
    const requestId = ++this.drilldownRequestId;
    this.reportBuilderService.loadExploreDrilldown(params).subscribe({
      next: (data) => {
        if (requestId !== this.drilldownRequestId || !this.explorerDrilldown()) return;
        this.explorerDrilldownData.set(data);
        this.drilldownLoading.set(false);
      },
      error: () => {
        if (requestId !== this.drilldownRequestId) return;
        this.drilldownLoading.set(false);
        this.drilldownError.set(this.languageService.t('workspace.drilldown.loadError'));
      },
    });
  }

  private loadDrilldownData(metricId: string, periodStart: string | null, page: number): void {
    const filters = this.filters();
    const params = Object.fromEntries(Object.entries({
      metricId,
      year: filters.year, term: filters.semester, cycle: filters.cycle, campus: filters.campus,
      level: filters.level, grade: filters.grade, homeroom: filters.homeroom,
      studentId: filters.studentId, program: filters.program, subject: filters.subject,
      periodStart: periodStart ?? '',
      page: String(page),
      pageSize: String(this.drilldownPageSize),
    }).filter(([, value]) => !!value));

    this.drilldownLoading.set(true);
    const requestId = ++this.drilldownRequestId;
    this.reportBuilderService.loadMetricDrilldown(params).subscribe({
      next: (data) => {
        // Ignore a late response if the user already opened a different metric.
        if (this.drilldownMetricId() !== metricId || requestId !== this.drilldownRequestId) {
          return;
        }
        this.drilldownData.set(data);
        this.drilldownLoading.set(false);
      },
      error: () => {
        if (requestId !== this.drilldownRequestId) return;
        this.drilldownLoading.set(false);
        this.drilldownError.set(this.languageService.t('workspace.drilldown.loadError'));
      },
    });
  }

  formatDrilldownValue(value: number): string {
    const unit = this.drilldownUnit();
    return unit === '%' ? `${value}%` : `${value}`;
  }

  formatRecordDate(value: string): string {
    return new Intl.DateTimeFormat(this.languageService.language() === 'vi' ? 'vi-VN' : 'en-US', {
      dateStyle: 'medium', timeStyle: 'short',
    }).format(new Date(value));
  }

  recordFields(fields: Record<string, string | null>): { key: string; value: string }[] {
    return Object.entries(fields ?? {})
      .filter((entry): entry is [string, string] => !!entry[1])
      .map(([key, value]) => ({ key, value }));
  }

  /** Card value: keep symbol units like "%" on the number, drop word units (the label already names them). */
  cardValue(metric: MetricVariable): string {
    if (metric.value === null || metric.value === undefined) {
      return '—';
    }
    return metric.unit === '%' ? `${metric.value}%` : `${metric.value}`;
  }

  toggleSnapshotMenu(): void {
    this.snapshotMenuOpen.update((open) => !open);
  }

  closeSnapshotMenu(): void {
    this.snapshotMenuOpen.set(false);
  }

  /**
   * Capture the dashboard with the current filters applied. Both menu actions route here:
   * the browser's print dialog is where the user chooses a printer or "Save as PDF".
   */
  takeSnapshot(): void {
    this.closeSnapshotMenu();
    // Close any open panels so they are not part of the snapshot.
    this.closeSheet();
    this.closeDrilldown();
    this.snapshotTimestamp.set(
      new Intl.DateTimeFormat(this.languageService.language() === 'vi' ? 'vi-VN' : 'en-US', {
        dateStyle: 'long', timeStyle: 'short',
      }).format(new Date()),
    );
    // Let the timestamp/header render before opening the print dialog.
    setTimeout(() => window.print(), 50);
  }

  private mountDrilldown(): void {
    if (this.drilldownUnmountTimer !== null) {
      window.clearTimeout(this.drilldownUnmountTimer);
      this.drilldownUnmountTimer = null;
    }

    this.drilldownMounted.set(true);
    this.drilldownOpen.set(true);
  }

  private openSheet(kind: SheetKind) {
    if (this.sheetUnmountTimer !== null) {
      window.clearTimeout(this.sheetUnmountTimer);
      this.sheetUnmountTimer = null;
    }

    this.sheetKind.set(kind);
    this.sheetMounted.set(true);
    this.sheetOpen.set(true);
  }

  private persistReport(report: CustomReport) {
    const nextReport = { ...report, updatedAt: new Date().toISOString() };
    this.customReports.update((reports) =>
      reports.map((candidate) => (candidate.id === report.id ? nextReport : candidate)),
    );
    this.reportBuilderService.saveReports(this.customReports());

    this.reportBuilderService.saveRemoteReport(nextReport).subscribe((savedReport) => {
      if (!savedReport) {
        return;
      }
      this.customReports.update((reports) =>
        reports.map((candidate) =>
          candidate.id === nextReport.id
            ? { ...candidate, serverId: savedReport.serverId }
            : candidate,
        ),
      );
      this.reportBuilderService.saveReports(this.customReports());
    });
  }

  private defaultAiReportDraft(): AiReportDraft {
    return {
      language: this.languageService.language() === 'vi' ? 'vi' : 'en',
      audience: 'leadership',
      tone: 'constructive',
      detailLevel: 'standard',
      additionalInstructions: '',
      includeRecommendations: true,
    };
  }

  private downloadFilename(contentDisposition: string | null, reportTitle: string): string {
    if (contentDisposition) {
      const encoded = /filename\*=UTF-8''([^;]+)/i.exec(contentDisposition)?.[1];
      if (encoded) {
        try {
          return decodeURIComponent(encoded);
        } catch {
          // Fall back to the regular filename or a locally generated safe name.
        }
      }

      const regular = /filename="?([^";]+)"?/i.exec(contentDisposition)?.[1]?.trim();
      if (regular) {
        return regular;
      }
    }

    const safeTitle = reportTitle
      .normalize('NFD')
      .replace(/[\u0300-\u036f]/g, '')
      .replace(/[^a-zA-Z0-9]+/g, '-')
      .replace(/^-+|-+$/g, '')
      .toLowerCase() || 'workspace';
    return `${safeTitle}-ai-report.docx`;
  }

  private emptyWorkspaceDraft(): WorkspaceDraft {
    return { title: '', description: '', sharedWith: [] };
  }

  private emptyChartDraft(): ChartDraft {
    return {
      id: null, title: '', kind: 'bar', metrics: [], showBaseline: false, kpiTarget: null,
      source: 'metric', dataset: '', dimension: '', measure: '',
    };
  }

  private owner(): string {
    return this.authService.getUsername() || this.authService.getFullName() || 'local-user';
  }
}
