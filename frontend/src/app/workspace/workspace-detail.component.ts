import { NgFor, NgIf } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { EChartsCoreOption } from 'echarts/core';
import { NgxEchartsDirective } from 'ngx-echarts';
import { AuthenticationService } from '../core/auth/auth.service';
import { LanguageService } from '../core/language/language.service';
import {
  ChartKind,
  CustomChart,
  CustomReport,
  MetricVariable,
  ReportBuilderService,
  SystemUser,
} from '../reports/report-builder.service';
import { StudentListItem, StudentsService } from '../students/students.service';

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
}

@Component({
  selector: 'app-workspace-detail',
  imports: [FormsModule, NgFor, NgIf, NgxEchartsDirective, RouterLink],
  template: `
    <section class="space-y-7">
      <div class="flex flex-wrap items-start justify-between gap-4">
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
        <div class="flex flex-wrap gap-2" *ngIf="selectedReport() && isSelectedOwner()">
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
        </div>
      </div>

      <section
        *ngIf="selectedReport() as report; else missingWorkspace"
        class="rounded-lg border border-[#ebe6ef] bg-white p-5 shadow-[0_12px_35px_rgba(50,18,79,0.08)]"
      >
        <div class="grid gap-5 lg:grid-cols-2">
          <article
            *ngFor="let chart of report.charts"
            class="rounded-lg border border-[#ebe6ef] bg-white p-5 shadow-[0_12px_35px_rgba(50,18,79,0.06)]"
          >
            <div class="flex flex-wrap items-start justify-between gap-3">
              <div>
                <h3 class="text-lg font-semibold text-[#32124f]">{{ chart.title }}</h3>
                <p class="mt-1 text-xs text-[#7c7284]">
                  {{ languageService.t('workspace.chartTypeLabel') }}:
                  {{ languageService.t('workspace.chartType.' + chart.kind) }}
                </p>
              </div>
              <button
                *ngIf="isSelectedOwner()"
                class="rounded-md border border-[#d8d0df] px-3 py-2 text-xs font-semibold text-[#32124f] cursor-pointer"
                type="button"
                (click)="startEditChart(chart)"
              >
                {{ languageService.t('workspace.configure') }}
              </button>
            </div>

            <div
              class="mt-5 min-h-72 rounded-md border border-dashed border-[#d8d0df] bg-[#faf9f6] p-4"
            >
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
                    <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ metric.value }}</p>
                    <p class="mt-2 text-sm font-medium text-[#8a6811]">{{ metric.trend }}</p>
                  </button>
                </div>

                <div
                  echarts
                  *ngIf="chart.kind !== 'card'"
                  class="h-72 w-full cursor-pointer"
                  [options]="chartOptions(chart)"
                  role="img"
                  [attr.aria-label]="chart.title"
                  (chartClick)="onChartPointClick(chart, $event)"
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
        class="fixed inset-0 z-40"
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
                    <option value="scatter">
                      {{ languageService.t('workspace.chartType.scatter') }}
                    </option>
                    <option value="card">
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
                <div>
                  <p class="text-sm font-medium text-[#34243d]">
                    {{ languageService.t('workspace.metricVariables') }}
                  </p>
                  <div class="mt-3 space-y-2">
                    <label
                      *ngFor="let metric of metricVariables"
                      class="flex cursor-pointer items-start justify-between gap-3 rounded-md border border-[#ebe6ef] bg-[#faf9f6] p-3"
                    >
                      <span>
                        <span class="block text-sm font-semibold text-[#32124f]">{{
                          metricLabel(metric.id)
                        }}</span>
                        <span class="mt-1 block text-xs text-[#7c7284]">{{
                          metricLineage(metric)
                        }}</span>
                      </span>
                      <input
                        class="mt-1"
                        type="checkbox"
                        [checked]="chartDraft.metrics.includes(metric.id)"
                        (change)="toggleChartMetric(metric.id)"
                      />
                    </label>
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
                    class="rounded-md bg-[#32124f] px-5 py-3 text-sm font-semibold text-white"
                    type="submit"
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
        *ngIf="drilldownMounted()"
        class="fixed inset-0 z-40"
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
                >{{ languageService.t('workspace.drilldown.subtitle') }}
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
              class="mb-4 rounded-md border border-[#efe7d3] bg-[#fff8e1] px-4 py-3 text-xs leading-5 text-[#7a5b12]"
            >
              {{ languageService.t('workspace.drilldown.dataNote') }}
            </p>

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

            <ul *ngIf="!drilldownLoading() && !drilldownError()" class="space-y-2">
              <li *ngFor="let student of drilldownStudents()">
                <a
                  class="flex items-center gap-3 rounded-lg border border-[#ebe6ef] p-3 transition hover:border-[#d3c6db] hover:bg-[#fdfcff]"
                  [routerLink]="['/students', student.id]"
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
                      {{ student.studentCode }} ·
                      {{ languageService.t('workspace.drilldown.grade') }}
                      {{ student.gradeLevel || '-' }} · {{ student.homeroom || '-' }}
                    </span>
                  </span>
                  <span class="flex shrink-0 items-center gap-2">
                    <span
                      class="rounded-full bg-[#f3c246]/25 px-2.5 py-1 text-xs font-bold text-[#7a5b12]"
                      [title]="languageService.t('workspace.drilldown.behaviours')"
                    >
                      {{ student.behaviourCount }}
                    </span>
                    <span
                      class="rounded-full bg-[#f1edf4] px-2.5 py-1 text-xs font-bold text-[#5b287d]"
                      [title]="languageService.t('workspace.drilldown.projects')"
                    >
                      {{ student.projectCount }}
                    </span>
                  </span>
                </a>
              </li>
              <li
                *ngIf="!drilldownStudents().length"
                class="rounded-md border border-dashed border-[#d8d0df] bg-[#faf9f6] p-8 text-center text-sm text-[#7c7284]"
              >
                {{ languageService.t('workspace.drilldown.empty') }}
              </li>
            </ul>
          </div>
        </aside>
      </div>
    </section>
  `,
  styles: `
    .sheet-backdrop {
      opacity: 0;
    }

    .sheet-backdrop-open {
      opacity: 1;
      animation: sheet-backdrop-in 300ms ease-out both;
    }

    .sheet-backdrop-closing {
      animation: sheet-backdrop-out 300ms ease-out both;
    }

    .sheet-panel {
      transform: translate3d(100%, 0, 0);
      backface-visibility: hidden;
      will-change: transform;
    }

    .sheet-panel-open {
      transform: translate3d(0, 0, 0);
      animation: sheet-slide-in 300ms cubic-bezier(0.22, 1, 0.36, 1) both;
    }

    .sheet-panel-closing {
      animation: sheet-slide-out 300ms ease-in both;
    }

    @keyframes sheet-slide-out {
      from {
        transform: translate3d(0, 0, 0);
      }
      to {
        transform: translate3d(100%, 0, 0);
      }
    }

    @keyframes sheet-slide-in {
      from {
        transform: translate3d(100%, 0, 0);
      }
      to {
        transform: translate3d(0, 0, 0);
      }
    }

    @keyframes sheet-backdrop-in {
      from {
        opacity: 0;
      }
      to {
        opacity: 1;
      }
    }

    @keyframes sheet-backdrop-out {
      from {
        opacity: 1;
      }
      to {
        opacity: 0;
      }
    }

    @media (prefers-reduced-motion: reduce) {
      .sheet-backdrop,
      .sheet-panel {
        animation: none;
      }
    }
  `,
})
export class WorkspaceDetailComponent implements OnInit {
  private readonly authService = inject(AuthenticationService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly reportBuilderService = inject(ReportBuilderService);
  private readonly studentsService = inject(StudentsService);
  readonly languageService = inject(LanguageService);

  readonly customReports = signal<CustomReport[]>([]);
  readonly systemUsers = signal<SystemUser[]>([]);
  readonly selectedReportId = signal<string | null>(null);
  readonly sheetMounted = signal(false);
  readonly sheetOpen = signal(false);
  readonly sheetKind = signal<SheetKind>('workspace');
  readonly workspaceSheetMode = signal<WorkspaceSheetMode>('edit');
  readonly shareDropdownOpen = signal(false);
  readonly metricVariables = this.reportBuilderService.metricVariables;

  readonly drilldownMounted = signal(false);
  readonly drilldownOpen = signal(false);
  readonly drilldownLoading = signal(false);
  readonly drilldownError = signal<string | null>(null);
  readonly drilldownMetricId = signal<string | null>(null);
  readonly drilldownMetricLabel = signal<string>('');
  readonly drilldownPeriod = signal<string | null>(null);
  private readonly drilldownStudentPool = signal<StudentListItem[]>([]);

  readonly drilldownStudents = computed(() => {
    const students = [...this.drilldownStudentPool()];
    const rankByProjects = this.isProjectMetric(this.drilldownMetricId());
    return students.sort((a, b) =>
      rankByProjects
        ? b.projectCount - a.projectCount || a.fullName.localeCompare(b.fullName)
        : b.behaviourCount - a.behaviourCount || a.fullName.localeCompare(b.fullName),
    );
  });

  private sheetUnmountTimer: number | null = null;
  private drilldownUnmountTimer: number | null = null;

  workspaceDraft: WorkspaceDraft = this.emptyWorkspaceDraft();
  chartDraft: ChartDraft = this.emptyChartDraft();

  readonly selectedReport = computed(() => {
    const selectedId = this.selectedReportId();
    return this.customReports().find((report) => report.id === selectedId) ?? null;
  });

  ngOnInit() {
    this.selectedReportId.set(this.route.snapshot.paramMap.get('workspaceId'));
    this.customReports.set(this.reportBuilderService.loadReports());

    this.reportBuilderService.loadRemoteReports().subscribe((remoteReports) => {
      if (!remoteReports.length) {
        return;
      }
      this.customReports.set(remoteReports);
      this.reportBuilderService.saveReports(remoteReports);
    });

    this.reportBuilderService.loadSystemUsers().subscribe((users) => this.systemUsers.set(users));
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
    this.chartDraft = {
      id: null,
      title: this.languageService.t('workspace.defaultNewChartTitle'),
      kind: 'bar',
      metrics: [],
    };
    this.openSheet('chart');
  }

  startEditChart(chart: CustomChart) {
    this.chartDraft = {
      id: chart.id,
      title: chart.title,
      kind: chart.kind,
      metrics: [...chart.metrics],
    };
    this.openSheet('chart');
  }

  saveChartDraft() {
    const report = this.selectedReport();
    if (!report || !this.isSelectedOwner() || !this.chartDraft.title.trim()) {
      return;
    }

    if (this.chartDraft.id) {
      report.charts = report.charts.map((chart) =>
        chart.id === this.chartDraft.id
          ? {
              id: chart.id,
              title: this.chartDraft.title.trim(),
              kind: this.chartDraft.kind,
              metrics: [...this.chartDraft.metrics],
            }
          : chart,
      );
    } else {
      report.charts = [
        ...report.charts,
        {
          id: crypto.randomUUID(),
          title: this.chartDraft.title.trim(),
          kind: this.chartDraft.kind,
          metrics: [...this.chartDraft.metrics],
        },
      ];
    }

    this.persistReport(report);
    this.closeSheet();
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

  metricLabel(metricId: string): string {
    return this.languageService.t(`workspace.metrics.${metricId}.label`);
  }

  metricLineage(metric: MetricVariable): string {
    const category = this.languageService.t(`workspace.categories.${metric.category}`);
    const source = this.languageService.t(`workspace.sources.${metric.source}`);
    return `${category} · ${source}`;
  }

  chartMetrics(chart: CustomChart): MetricVariable[] {
    return chart.metrics
      .map((metricId) => this.metricVariables.find((metric) => metric.id === metricId))
      .filter((metric): metric is MetricVariable => !!metric);
  }

  chartOptions(chart: CustomChart): EChartsCoreOption {
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
            data: metrics.map((metric) => ({
              name: this.metricLabel(metric.id),
              value: Math.max(0, metric.series.at(-1) ?? 0),
            })),
          },
        ],
      };
    }

    if (chart.kind === 'scatter') {
      const periods = ['Aug', 'Sep', 'Oct', 'Nov', 'Dec', 'Jan'];
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
        dataZoom: [{ type: 'inside' }],
        series: comparedMetrics.map((metric) => ({
          name: this.metricLabel(metric.id),
          type: 'scatter',
          symbolSize: 12,
          data: metric.series.map((value, index) => ({
            name: periods[index],
            value: [
              metrics.length > 1 ? (xMetric?.series[index] ?? index + 1) : index + 1,
              value,
            ],
          })),
          emphasis: {
            focus: 'series',
            scale: 1.5,
          },
        })),
      };
    }

    const cartesianKind = chart.kind === 'line' ? 'line' : 'bar';

    return {
      color: colors,
      tooltip: {
        trigger: 'axis',
        axisPointer: { type: cartesianKind === 'bar' ? 'shadow' : 'cross' },
        valueFormatter: (value: string | number) => `${value}`,
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
        data: ['Aug', 'Sep', 'Oct', 'Nov', 'Dec', 'Jan'],
        axisLine: { lineStyle: { color: '#d8d0df' } },
        axisLabel: { color: '#6f6877' },
      },
      yAxis: {
        type: 'value',
        axisLabel: { color: '#6f6877' },
        splitLine: { lineStyle: { color: '#ebe6ef' } },
      },
      dataZoom: [
        {
          type: 'inside',
        },
      ],
      series: metrics.map((metric) => ({
        name: this.metricLabel(metric.id),
        type: cartesianKind,
        data: metric.series,
        smooth: cartesianKind === 'line',
        symbolSize: 8,
        barMaxWidth: 30,
        emphasis: { focus: 'series' },
        areaStyle: cartesianKind === 'line' ? { opacity: 0.08 } : undefined,
      })),
    };
  }

  seriesColor(index: number): string {
    return ['#32124f', '#b38116', '#31706f', '#7b4aa4'][index % 4];
  }

  onChartPointClick(chart: CustomChart, event: { seriesName?: string; name?: string; data?: unknown }): void {
    const metrics = this.chartMetrics(chart);
    if (!metrics.length) {
      return;
    }

    const seriesName = event?.seriesName ?? event?.name;
    const metric = metrics.find((candidate) => this.metricLabel(candidate.id) === seriesName) ?? metrics[0];

    let period: string | null = null;
    if (chart.kind === 'bar' || chart.kind === 'line') {
      period = typeof event?.name === 'string' ? event.name : null;
    } else if (chart.kind === 'scatter') {
      const scatterPoint = event?.data as { name?: string } | undefined;
      period = scatterPoint?.name ?? null;
    }

    this.openDrilldown(metric.id, period);
  }

  openDrilldown(metricId: string, period: string | null): void {
    this.drilldownMetricId.set(metricId);
    this.drilldownMetricLabel.set(this.metricLabel(metricId));
    this.drilldownPeriod.set(period);
    this.drilldownError.set(null);
    this.mountDrilldown();
    this.loadDrilldownStudents();
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

  private isProjectMetric(metricId: string | null): boolean {
    if (!metricId) {
      return false;
    }
    const metric = this.metricVariables.find((candidate) => candidate.id === metricId);
    return !!metric && ['Learning', 'Engagement'].includes(metric.category);
  }

  private loadDrilldownStudents(): void {
    if (this.drilldownStudentPool().length) {
      return;
    }

    this.drilldownLoading.set(true);
    this.studentsService.list().subscribe({
      next: (students) => {
        this.drilldownStudentPool.set(students);
        this.drilldownLoading.set(false);
      },
      error: () => {
        this.drilldownLoading.set(false);
        this.drilldownError.set(this.languageService.t('workspace.drilldown.loadError'));
      },
    });
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

  private emptyWorkspaceDraft(): WorkspaceDraft {
    return { title: '', description: '', sharedWith: [] };
  }

  private emptyChartDraft(): ChartDraft {
    return { id: null, title: '', kind: 'bar', metrics: [] };
  }

  private owner(): string {
    return this.authService.getUsername() || this.authService.getFullName() || 'local-user';
  }
}
