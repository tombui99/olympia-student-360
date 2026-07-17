import { NgFor, NgIf } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
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

type WorkspaceSection = 'mine' | 'shared';
type WorkspaceSheetMode = 'create' | 'edit';
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

interface MetricGroup {
  category: string;
  metrics: MetricVariable[];
}

@Component({
  selector: 'app-workspace',
  imports: [FormsModule, NgFor, NgIf, RouterLink],
  template: `
    <section class="space-y-7">
      <div class="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">{{ languageService.t('workspace.eyebrow') }}</p>
          <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ languageService.t('workspace.title') }}</h2>
          <p class="mt-2 max-w-3xl text-[#6f6877]">{{ languageService.t('workspace.description') }}</p>
        </div>
        <button
          *ngIf="activeSection() === 'mine'"
          class="rounded-md bg-[#32124f] px-4 py-3 text-sm font-semibold text-white transition hover:bg-[#4a1b73]"
          type="button"
          (click)="startCreateWorkspace()"
        >
          {{ languageService.t('workspace.newWorkspace') }}
        </button>
      </div>

      <div class="grid gap-5 xl:grid-cols-[240px_minmax(0,1fr)]">
        <aside class="rounded-lg border border-[#ebe6ef] bg-white p-3 shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <button
            class="flex w-full items-center justify-between rounded-md px-4 py-3 text-left text-sm font-semibold transition"
            type="button"
            [class.bg-[#32124f]]="activeSection() === 'mine'"
            [class.text-white]="activeSection() === 'mine'"
            [class.text-[#32124f]]="activeSection() !== 'mine'"
            (click)="switchSection('mine')"
          >
            <span>{{ languageService.t('workspace.myWorkspaces') }}</span>
            <span class="rounded bg-white/20 px-2 py-0.5 text-xs">{{ myReports().length }}</span>
          </button>
          <button
            class="mt-2 flex w-full items-center justify-between rounded-md px-4 py-3 text-left text-sm font-semibold transition"
            type="button"
            [class.bg-[#32124f]]="activeSection() === 'shared'"
            [class.text-white]="activeSection() === 'shared'"
            [class.text-[#32124f]]="activeSection() !== 'shared'"
            (click)="switchSection('shared')"
          >
            <span>{{ languageService.t('workspace.sharedWithMe') }}</span>
            <span class="rounded bg-white/20 px-2 py-0.5 text-xs">{{ sharedReports().length }}</span>
          </button>
        </aside>

        <main class="min-w-0 space-y-5">
          <section class="rounded-lg border border-[#ebe6ef] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
            <div class="border-b border-[#ebe6ef] p-5">
              <div class="flex flex-wrap items-start justify-between gap-4">
                <div>
                  <h3 class="text-lg font-semibold text-[#32124f]">
                    {{
                      activeSection() === 'mine'
                        ? languageService.t('workspace.myWorkspaces')
                        : languageService.t('workspace.sharedWithMe')
                    }}
                  </h3>
                  <p class="mt-1 text-sm text-[#6f6877]">
                    {{
                      activeSection() === 'mine'
                        ? languageService.t('workspace.myWorkspacesHint')
                        : languageService.t('workspace.sharedWithMeHint')
                    }}
                  </p>
                </div>
              </div>
            </div>

            <div *ngIf="sectionReports().length; else emptySection" class="grid gap-3 p-5 lg:grid-cols-2">
              <article
                *ngFor="let report of sectionReports()"
                class="rounded-md border border-[#ebe6ef] p-4 transition hover:border-[#f3c246]"
              >
                <a [routerLink]="['/workspace', report.id]" class="block text-left">
                  <div class="flex items-start justify-between gap-3">
                    <div class="min-w-0">
                      <p class="truncate text-base font-semibold text-[#32124f]">{{ report.title }}</p>
                      <p class="mt-1 line-clamp-2 text-sm text-[#6f6877]">{{ report.description }}</p>
                    </div>
                    <span class="rounded bg-[#f3c246]/25 px-3 py-1 text-xs font-semibold text-[#7a5b12]">
                      {{ report.charts.length }} {{ languageService.t('workspace.charts') }}
                    </span>
                  </div>
                  <p class="mt-4 text-xs text-[#7c7284]">
                    {{ languageService.t('workspace.owner') }}: {{ report.owner }}
                  </p>
                </a>
                <div *ngIf="isOwner(report)" class="mt-4 flex justify-end gap-2 border-t border-[#ebe6ef] pt-3">
                  <button
                    class="rounded-md border border-[#d8d0df] px-3 py-2 text-xs font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
                    type="button"
                    (click)="startEditWorkspace(report)"
                  >
                    {{ languageService.t('workspace.manageWorkspace') }}
                  </button>
                  <button
                    class="rounded-md border border-red-200 px-3 py-2 text-xs font-semibold text-red-700 transition hover:bg-red-50"
                    type="button"
                    (click)="confirmDeleteReport(report)"
                  >
                    {{ languageService.t('workspace.delete') }}
                  </button>
                </div>
              </article>
            </div>

            <ng-template #emptySection>
              <div class="grid min-h-52 place-items-center p-8 text-center">
                <div>
                  <p class="text-lg font-semibold text-[#32124f]">
                    {{
                      activeSection() === 'mine'
                        ? languageService.t('workspace.emptyMineTitle')
                        : languageService.t('workspace.emptySharedTitle')
                    }}
                  </p>
                  <p class="mt-2 max-w-md text-sm text-[#6f6877]">
                    {{
                      activeSection() === 'mine'
                        ? languageService.t('workspace.emptyMineBody')
                        : languageService.t('workspace.emptySharedBody')
                    }}
                  </p>
                </div>
              </div>
            </ng-template>
          </section>
        </main>
      </div>

      <div *ngIf="sheetMounted()" class="fixed inset-0 z-40" [class.pointer-events-none]="!sheetOpen()">
        <button
          class="absolute inset-0 bg-[#24182f]/35 transition-opacity duration-300 ease-out"
          [class.opacity-100]="sheetOpen()"
          [class.opacity-0]="!sheetOpen()"
          type="button"
          [attr.aria-label]="languageService.t('workspace.closeSheet')"
          (click)="closeSheet()"
        ></button>

        <aside
          class="absolute right-0 top-0 flex h-full w-full max-w-xl flex-col bg-white shadow-[0_24px_80px_rgba(36,24,47,0.28)] transition-transform duration-300 ease-out will-change-transform"
          [class.translate-x-0]="sheetOpen()"
          [class.translate-x-full]="!sheetOpen()"
          role="dialog"
          aria-modal="true"
        >
          <ng-container *ngIf="sheetKind() === 'workspace'; else chartSheet">
            <div class="flex items-start justify-between gap-4 border-b border-[#ebe6ef] px-6 py-5">
              <div>
                <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">{{ languageService.t('workspace.eyebrow') }}</p>
                <h3 class="mt-2 text-xl font-semibold text-[#32124f]">
                  {{
                    workspaceSheetMode() === 'create'
                      ? languageService.t('workspace.createWorkspace')
                      : languageService.t('workspace.manageWorkspace')
                  }}
                </h3>
                <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('workspace.workspaceSheetHint') }}</p>
              </div>
              <button
                class="grid h-9 w-9 place-items-center rounded-full border border-[#d8d0df] text-lg font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
                type="button"
                [attr.aria-label]="languageService.t('workspace.closeSheet')"
                (click)="closeSheet()"
              >
                x
              </button>
            </div>

            <form class="flex min-h-0 flex-1 flex-col" (ngSubmit)="saveWorkspaceDraft()">
              <div class="min-h-0 flex-1 space-y-5 overflow-y-auto px-6 py-6">
                <label class="block">
                  <span class="text-sm font-medium text-[#34243d]">{{ languageService.t('workspace.workspaceTitle') }}</span>
                  <input
                    class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-4 py-3 outline-none transition focus:border-[#32124f] focus:ring-4 focus:ring-[#32124f]/10"
                    name="workspaceTitle"
                    [(ngModel)]="workspaceDraft.title"
                    required
                  />
                </label>

                <label class="block">
                  <span class="text-sm font-medium text-[#34243d]">{{ languageService.t('workspace.workspaceDescription') }}</span>
                  <textarea
                    class="mt-2 min-h-28 w-full rounded-md border border-[#d8d0df] bg-white px-4 py-3 outline-none transition focus:border-[#32124f] focus:ring-4 focus:ring-[#32124f]/10"
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
                          <span class="block font-semibold text-[#32124f]">{{ user.fullName }}</span>
                          <span class="block text-xs text-[#7c7284]">{{ user.username }}{{ user.email ? ' · ' + user.email : '' }}</span>
                        </span>
                      </label>
                      <p *ngIf="!systemUsers().length" class="px-3 py-2 text-sm text-[#6f6877]">
                        {{ languageService.t('workspace.noUsersAvailable') }}
                      </p>
                    </div>
                  </div>
                </div>
              </div>

              <div class="flex flex-wrap items-center justify-between gap-3 border-t border-[#ebe6ef] bg-[#faf9f6] px-6 py-4">
                <div class="flex gap-2">
                  <button
                    *ngIf="workspaceSheetMode() === 'edit'"
                    class="rounded-md border border-[#d8d0df] px-4 py-3 text-sm font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
                    type="button"
                    (click)="duplicateSelected()"
                  >
                    {{ languageService.t('workspace.duplicate') }}
                  </button>
                </div>
                <div class="flex gap-2">
                  <button
                    class="rounded-md border border-[#d8d0df] px-5 py-3 text-sm font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
                    type="button"
                    (click)="closeSheet()"
                  >
                    {{ languageService.t('workspace.cancel') }}
                  </button>
                  <button class="rounded-md bg-[#32124f] px-5 py-3 text-sm font-semibold text-white transition hover:bg-[#4b2070]" type="submit">
                    {{ languageService.t('workspace.save') }}
                  </button>
                </div>
              </div>
            </form>
          </ng-container>

          <ng-template #chartSheet>
            <div class="flex items-start justify-between gap-4 border-b border-[#ebe6ef] px-6 py-5">
              <div>
                <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">{{ languageService.t('workspace.chart') }}</p>
                <h3 class="mt-2 text-xl font-semibold text-[#32124f]">
                  {{ chartDraft.id ? languageService.t('workspace.configureChart') : languageService.t('workspace.addChart') }}
                </h3>
                <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('workspace.chartSheetHint') }}</p>
              </div>
              <button
                class="grid h-9 w-9 place-items-center rounded-full border border-[#d8d0df] text-lg font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
                type="button"
                [attr.aria-label]="languageService.t('workspace.closeSheet')"
                (click)="closeSheet()"
              >
                x
              </button>
            </div>

            <form class="flex min-h-0 flex-1 flex-col" (ngSubmit)="saveChartDraft()">
              <div class="min-h-0 flex-1 space-y-5 overflow-y-auto px-6 py-6">
                <label class="block">
                  <span class="text-sm font-medium text-[#34243d]">{{ languageService.t('workspace.chartTitle') }}</span>
                  <input
                    class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-4 py-3 outline-none transition focus:border-[#32124f] focus:ring-4 focus:ring-[#32124f]/10"
                    name="chartTitle"
                    [(ngModel)]="chartDraft.title"
                    required
                  />
                </label>

                <label class="block">
                  <span class="text-sm font-medium text-[#34243d]">{{ languageService.t('workspace.chartTypeLabel') }}</span>
                  <select
                    class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-4 py-3 outline-none transition focus:border-[#32124f] focus:ring-4 focus:ring-[#32124f]/10"
                    name="chartKind"
                    [(ngModel)]="chartDraft.kind"
                  >
                    <option value="bar">{{ languageService.t('workspace.chartType.bar') }}</option>
                    <option value="line">{{ languageService.t('workspace.chartType.line') }}</option>
                    <option value="pie">{{ languageService.t('workspace.chartType.pie') }}</option>
                    <option value="scatter">{{ languageService.t('workspace.chartType.scatter') }}</option>
                    <option value="card">{{ languageService.t('workspace.chartType.card') }}</option>
                  </select>
                  <p *ngIf="chartDraft.kind === 'pie'" class="mt-2 text-xs leading-5 text-[#7c7284]">
                    {{ languageService.t('workspace.chartTypeHint.pie') }}
                  </p>
                  <p *ngIf="chartDraft.kind === 'scatter'" class="mt-2 text-xs leading-5 text-[#7c7284]">
                    {{ languageService.t('workspace.chartTypeHint.scatter') }}
                  </p>
                </label>

                <div>
                  <p class="text-sm font-medium text-[#34243d]">{{ languageService.t('workspace.metricVariables') }}</p>
                  <div class="mt-3 space-y-3">
                    <details *ngFor="let group of metricGroups(); trackBy: trackMetricGroup" class="overflow-hidden rounded-md border border-[#e1dbe6] bg-white">
                      <summary class="flex cursor-pointer list-none items-center justify-between gap-3 bg-[#f4f0f6] px-4 py-3 text-sm font-semibold text-[#32124f]">
                        <span>{{ categoryLabel(group.category) }}</span>
                        <span class="rounded-full bg-white px-2.5 py-0.5 text-xs text-[#6f6877]">
                          {{ selectedMetricCount(group) }}/{{ group.metrics.length }}
                        </span>
                      </summary>
                      <div class="space-y-2 border-t border-[#e1dbe6] p-3">
                        <label
                          *ngFor="let metric of group.metrics; trackBy: trackMetric"
                          class="flex items-start justify-between gap-3 rounded-md border border-[#ebe6ef] bg-[#faf9f6] p-3 transition hover:border-[#f3c246]"
                          [class.cursor-pointer]="!isMetricDisabled(metric)"
                          [class.opacity-40]="isMetricDisabled(metric)"
                        >
                          <span>
                            <span class="block text-sm font-semibold text-[#32124f]">{{ metricLabel(metric.id) }}</span>
                            <span class="mt-1 block text-xs text-[#7c7284]">{{ sourceLabel(metric.source) }}</span>
                          </span>
                          <input
                            class="mt-1"
                            type="checkbox"
                            [checked]="chartDraft.metrics.includes(metric.id)"
                            [disabled]="isMetricDisabled(metric)"
                            (change)="toggleChartMetric(metric.id)"
                          />
                        </label>
                      </div>
                    </details>
                  </div>
                </div>
              </div>

              <div class="flex flex-wrap items-center justify-between gap-3 border-t border-[#ebe6ef] bg-[#faf9f6] px-6 py-4">
                <button
                  *ngIf="chartDraft.id"
                  class="rounded-md border border-red-200 px-4 py-3 text-sm font-semibold text-red-700 transition hover:bg-red-50"
                  type="button"
                  (click)="deleteChartDraft()"
                >
                  {{ languageService.t('workspace.delete') }}
                </button>
                <div class="ml-auto flex gap-2">
                  <button
                    class="rounded-md border border-[#d8d0df] px-5 py-3 text-sm font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
                    type="button"
                    (click)="closeSheet()"
                  >
                    {{ languageService.t('workspace.cancel') }}
                  </button>
                  <button class="rounded-md bg-[#32124f] px-5 py-3 text-sm font-semibold text-white transition hover:bg-[#4b2070]" type="submit">
                    {{ languageService.t('workspace.save') }}
                  </button>
                </div>
              </div>
            </form>
          </ng-template>
        </aside>
      </div>

      <div *ngIf="deleteConfirmMounted()" class="fixed inset-0 z-50" [class.pointer-events-none]="!deleteConfirmOpen()">
        <button
          class="absolute inset-0 bg-[#24182f]/35 transition-opacity duration-200 ease-out"
          [class.opacity-100]="deleteConfirmOpen()"
          [class.opacity-0]="!deleteConfirmOpen()"
          type="button"
          [attr.aria-label]="languageService.t('workspace.cancel')"
          (click)="closeDeleteConfirm()"
        ></button>
        <section
          class="absolute left-1/2 top-1/2 w-[calc(100%-2rem)] max-w-md -translate-x-1/2 -translate-y-1/2 rounded-lg bg-white p-6 shadow-[0_24px_80px_rgba(36,24,47,0.28)] transition duration-200 ease-out"
          [class.scale-100]="deleteConfirmOpen()"
          [class.opacity-100]="deleteConfirmOpen()"
          [class.scale-95]="!deleteConfirmOpen()"
          [class.opacity-0]="!deleteConfirmOpen()"
          role="alertdialog"
          aria-modal="true"
        >
          <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">{{ languageService.t('workspace.delete') }}</p>
          <h3 class="mt-2 text-xl font-semibold text-[#32124f]">{{ languageService.t('workspace.deleteConfirmTitle') }}</h3>
          <p class="mt-3 text-sm leading-6 text-[#6f6877]">
            {{ languageService.t('workspace.deleteConfirmBody') }}
          </p>
          <p class="mt-4 rounded-md bg-[#faf9f6] px-4 py-3 text-sm font-semibold text-[#32124f]">
            {{ pendingDeleteReport()?.title }}
          </p>
          <div class="mt-6 flex justify-end gap-3">
            <button
              class="rounded-md border border-[#d8d0df] px-4 py-3 text-sm font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
              type="button"
              (click)="closeDeleteConfirm()"
            >
              {{ languageService.t('workspace.cancel') }}
            </button>
            <button
              class="rounded-md bg-red-700 px-4 py-3 text-sm font-semibold text-white transition hover:bg-red-800"
              type="button"
              (click)="deletePendingReport()"
            >
              {{ languageService.t('workspace.delete') }}
            </button>
          </div>
        </section>
      </div>
    </section>
  `,
})
export class WorkspaceComponent implements OnInit {
  private readonly authService = inject(AuthenticationService);
  private readonly reportBuilderService = inject(ReportBuilderService);
  readonly languageService = inject(LanguageService);

  readonly activeSection = signal<WorkspaceSection>('mine');
  readonly customReports = signal<CustomReport[]>([]);
  readonly systemUsers = signal<SystemUser[]>([]);
  readonly selectedReportId = signal<string | null>(null);
  readonly sheetMounted = signal(false);
  readonly sheetOpen = signal(false);
  readonly sheetKind = signal<SheetKind>('workspace');
  readonly workspaceSheetMode = signal<WorkspaceSheetMode>('create');
  readonly shareDropdownOpen = signal(false);
  readonly deleteConfirmMounted = signal(false);
  readonly deleteConfirmOpen = signal(false);
  readonly pendingDeleteReport = signal<CustomReport | null>(null);
  readonly metricVariables = this.reportBuilderService.metricVariables;

  workspaceDraft: WorkspaceDraft = this.emptyWorkspaceDraft();
  chartDraft: ChartDraft = this.emptyChartDraft();

  readonly myReports = computed(() => this.customReports().filter((report) => report.owner === this.owner()));
  readonly sharedReports = computed(() => this.customReports().filter((report) => report.owner !== this.owner()));
  readonly sectionReports = computed(() => (this.activeSection() === 'mine' ? this.myReports() : this.sharedReports()));

  readonly selectedReport = computed(() => {
    const selectedId = this.selectedReportId();
    return this.customReports().find((report) => report.id === selectedId) ?? null;
  });

  ngOnInit() {
    this.reportBuilderService.loadMetricVariables({}).subscribe();
    const reports = this.reportBuilderService.loadReports();
    this.customReports.set(reports);
    this.selectFirstInSection();

    this.reportBuilderService.loadRemoteReports().subscribe((remoteReports) => {
      if (!remoteReports.length) {
        return;
      }

      this.customReports.set(remoteReports);
      this.reportBuilderService.saveReports(remoteReports);
      this.selectFirstInSection();
    });

    this.reportBuilderService.loadSystemUsers().subscribe((users) => this.systemUsers.set(users));
  }

  switchSection(section: WorkspaceSection) {
    this.activeSection.set(section);
    this.selectFirstInSection();
  }

  selectReport(reportId: string) {
    this.selectedReportId.set(reportId);
  }

  startCreateWorkspace() {
    this.workspaceSheetMode.set('create');
    this.workspaceDraft = {
      title: '',
      description: '',

      sharedWith: [],
    };
    this.openSheet('workspace');
  }

  startEditWorkspace(report: CustomReport) {
    this.selectedReportId.set(report.id);
    this.workspaceSheetMode.set('edit');
    this.workspaceDraft = {
      title: report.title,
      description: report.description,

      sharedWith: report.sharedWith.filter((username) => this.systemUsers().some((user) => user.username === username)),
    };
    this.openSheet('workspace');
  }

  saveWorkspaceDraft() {
    if (!this.workspaceDraft.title.trim()) {
      return;
    }

    if (this.workspaceSheetMode() === 'create') {
      const report = this.reportBuilderService.createReport(this.owner());
      report.title = this.workspaceDraft.title.trim();
      report.description = this.workspaceDraft.description.trim();

      report.sharedWith = [...this.workspaceDraft.sharedWith];
      this.customReports.set([report, ...this.customReports()]);
      this.activeSection.set('mine');
      this.selectedReportId.set(report.id);
      this.persistReport(report);
      this.closeSheet();
      return;
    }

    const report = this.selectedReport();
    if (!report || !this.isSelectedOwner()) {
      return;
    }

    report.title = this.workspaceDraft.title.trim();
    report.description = this.workspaceDraft.description.trim();

    report.sharedWith = [...this.workspaceDraft.sharedWith];
    this.persistReport(report);
    this.closeSheet();
  }

  startCreateChart() {
    if (!this.isSelectedOwner()) {
      return;
    }

    this.chartDraft = {
      id: null,
      title: '',
      kind: 'bar',
      metrics: [],
    };
    this.openSheet('chart');
  }

  startEditChart(chart: CustomChart) {
    if (!this.isSelectedOwner()) {
      return;
    }

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
  }

  deleteSelected() {
    const report = this.selectedReport();
    if (!report || !this.isSelectedOwner()) {
      return;
    }

    this.confirmDeleteReport(report);
  }

  confirmDeleteReport(report: CustomReport) {
    if (report.owner !== this.owner()) {
      return;
    }

    this.pendingDeleteReport.set(report);
    this.deleteConfirmMounted.set(true);
    window.setTimeout(() => this.deleteConfirmOpen.set(true));
  }

  deletePendingReport() {
    const report = this.pendingDeleteReport();
    if (!report || report.owner !== this.owner()) {
      return;
    }

    this.customReports.set(this.customReports().filter((candidate) => candidate.id !== report.id));
    this.reportBuilderService.saveReports(this.customReports());
    this.reportBuilderService.deleteRemoteReport(report).subscribe();
    if (this.sheetMounted()) {
      this.closeSheet();
    }
    this.closeDeleteConfirm();
    this.selectFirstInSection();
  }

  closeDeleteConfirm() {
    this.deleteConfirmOpen.set(false);
    window.setTimeout(() => {
      this.deleteConfirmMounted.set(false);
      this.pendingDeleteReport.set(null);
    }, 200);
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

  metricGroups(): MetricGroup[] {
    const order = ['AdmissionsFinance', 'Safety', 'Compliance', 'Achievement'];
    return order
      .map((category) => ({
        category,
        metrics: this.metricVariables().filter((metric) => metric.category === category),
      }))
      .filter((group) => group.metrics.length > 0);
  }

  selectedMetricCount(group: MetricGroup): number {
    return group.metrics.filter((metric) => this.chartDraft.metrics.includes(metric.id)).length;
  }

  trackMetricGroup(_index: number, group: MetricGroup): string {
    return group.category;
  }

  trackMetric(_index: number, metric: MetricVariable): string {
    return metric.id;
  }

  isMetricDisabled(metric: MetricVariable): boolean {
    if (this.chartDraft.metrics.includes(metric.id)) return false;
    const selected = this.metricVariables().find((candidate) => this.chartDraft.metrics.includes(candidate.id));
    return !!selected && selected.category !== metric.category;
  }

  selectedShareLabel(): string {
    if (!this.workspaceDraft.sharedWith.length) {
      return this.languageService.t('workspace.selectUsers');
    }

    return this.workspaceDraft.sharedWith
      .map((username) => this.systemUsers().find((user) => user.username === username)?.fullName ?? username)
      .join(', ');
  }

  isSelectedOwner(): boolean {
    return this.selectedReport()?.owner === this.owner();
  }

  isOwner(report: CustomReport): boolean {
    return report.owner === this.owner();
  }

  closeSheet() {
    this.sheetOpen.set(false);
    this.shareDropdownOpen.set(false);
    window.setTimeout(() => this.sheetMounted.set(false), 300);
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

  private openSheet(kind: SheetKind) {
    this.sheetKind.set(kind);
    this.sheetMounted.set(true);
    window.setTimeout(() => this.sheetOpen.set(true));
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
          candidate.id === nextReport.id ? { ...candidate, serverId: savedReport.serverId } : candidate,
        ),
      );
      this.reportBuilderService.saveReports(this.customReports());
    });
  }

  private selectFirstInSection() {
    const reports = this.sectionReports();
    this.selectedReportId.set(reports[0]?.id ?? null);
  }

  private emptyWorkspaceDraft(): WorkspaceDraft {
    return {
      title: '',
      description: '',

      sharedWith: [],
    };
  }

  private emptyChartDraft(): ChartDraft {
    return {
      id: null,
      title: '',
      kind: 'bar',
      metrics: [],
    };
  }

  private owner(): string {
    return this.authService.getUsername() || this.authService.getFullName() || 'local-user';
  }
}
