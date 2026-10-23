import { Component, computed, inject, Input, OnChanges, OnDestroy, OnInit, signal, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { LanguageService } from '../core/language/language.service';
import { ToastService } from '../core/toast/toast.service';
import { ReportExcelExportService, ReportExportColumn } from './report-excel-export.service';
import { ReportExportButtonComponent } from './report-export-button.component';
import { SearchableMultiSelectComponent } from './searchable-multi-select.component';
import { AssessmentCategory } from './leadership-reports.service';
import {
  EnglishProgramme, ScoreReportOptions, ScoreReportQuery, ScoreReportResult, ScoreReportRow,
  ScoreReportsService, ScoreReportViewKey,
} from './score-reports.service';

export type ScoreReportMenuKey =
  | 'school-subject-grade-average'
  | 'school-score-histogram'
  | 'school-current-course-score'
  | 'school-top-students'
  | 'school-bottom-students'
  | 'subject-band-by-subject'
  | 'subject-band-by-grade'
  | 'subject-band-by-class'
  | 'subject-assessment-average'
  | 'subject-class-results';

const reportViews: Record<ScoreReportMenuKey, { view: ScoreReportViewKey; title: string }> = {
  'school-subject-grade-average': { view: 'subject-grade-average', title: 'subjectGradeAverage' },
  'school-score-histogram': { view: 'score-histogram', title: 'histogram' },
  'school-current-course-score': { view: 'current-course-score', title: 'currentCourseScore' },
  'school-top-students': { view: 'top-students', title: 'topStudents' },
  'school-bottom-students': { view: 'bottom-students', title: 'bottomStudents' },
  'subject-band-by-subject': { view: 'subject-letter-distribution', title: 'subjectDistribution' },
  'subject-band-by-grade': { view: 'grade-letter-distribution', title: 'gradeDistribution' },
  'subject-band-by-class': { view: 'class-letter-distribution', title: 'classDistribution' },
  'subject-assessment-average': { view: 'assessment-average', title: 'assessmentAverage' },
  'subject-class-results': { view: 'class-subject-distribution', title: 'classSubjectResults' },
};

interface MatrixSubject {
  key: string;
  label: string;
  rows: ScoreReportRow[];
}

@Component({
  selector: 'app-score-report',
  imports: [FormsModule, RouterLink, SearchableMultiSelectComponent, ReportExportButtonComponent],
  template: `
    <section class="overflow-visible rounded-xl border border-[#e7e0eb] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.07)]" [attr.aria-busy]="loading() || optionsLoading()">
      <header class="flex flex-wrap items-start justify-between gap-4 border-b border-[#eee8f1] px-5 py-5 sm:px-6">
        <div>
          <p class="text-xs font-bold uppercase tracking-[0.14em] text-[#8a6811]">{{ periodLabel }}</p>
          <h3 class="mt-1 text-xl font-semibold text-[#32124f]">{{ t(reportBase() + '.title') }}</h3>
          <p class="mt-1 max-w-3xl text-sm leading-6 text-[#6f6877]">{{ t(reportBase() + '.description') }}</p>
        </div>
        <div class="flex flex-col items-start gap-2 sm:items-end">
          <app-report-export-button [exporting]="exporting()" [disabled]="loading() || !data()?.rows?.length || !data()?.aggregateAvailable" (exportRequested)="exportExcel()" />
          <span class="rounded-full bg-[#f2ecf5] px-3 py-1 text-xs font-semibold text-[#5d2d7b]">{{ sourceLabel() }}</span>
        </div>
      </header>

      <form class="no-print border-b border-[#eee8f1] bg-[#fdfbfe] px-5 py-5 sm:px-6" (ngSubmit)="run(true)">
        <div class="mb-4 flex flex-wrap items-baseline justify-between gap-2">
          <h4 class="text-sm font-semibold text-[#32124f]">{{ t('reports.leadership.filters') }}</h4>
          @if (data() && filtersPending()) { <span class="rounded-full bg-amber-100 px-2.5 py-1 text-xs font-semibold text-amber-900">{{ t('reports.score.pendingFilters') }}</span> }
          @if (isCourseScore()) { <span class="text-xs text-[#817589]">{{ t('reports.score.courseFiltersNote') }}</span> }
        </div>
        @if (optionsError()) {
          <div class="mb-4 rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900" role="alert">
            {{ t('reports.score.optionsUnavailable') }}
            <button class="ml-2 font-semibold underline" type="button" (click)="loadOptions()">{{ t('reports.retry') }}</button>
          </div>
        }
        <div class="grid min-w-0 items-start gap-x-4 gap-y-5 md:grid-cols-2 xl:grid-cols-3 [&>*]:min-w-0">
          <app-searchable-multi-select
            [label]="t('reports.leadership.subaccounts')"
            [searchPlaceholder]="t('reports.leadership.searchSubaccounts')"
            [emptySelectionLabel]="t('reports.leadership.allSubaccounts')"
            [multiple]="!singleSubjectReport()"
            [options]="subaccountChoices()" [selected]="selectedSubaccountOptionIds()"
            (selectedChange)="selectSubaccounts($event)" />
          <label class="grid min-w-0 gap-1 text-sm font-semibold text-[#32124f]">
            {{ t('reports.score.schoolLevel') }}
            <select class="h-11 w-full min-w-0 max-w-full rounded-md border border-[#d8d0df] bg-white px-3 font-normal" name="scoreSchoolLevel" [(ngModel)]="query.schoolLevel" (ngModelChange)="onSchoolLevelChange()">
              <option value="">{{ t('reports.score.allSchoolLevels') }}</option>
            @for (level of schoolLevelChoices(); track level) { <option [value]="level">{{ schoolLevelLabel(level) }}</option> }
            </select>
          </label>
          <label class="grid min-w-0 gap-1 text-sm font-semibold text-[#32124f]">
            {{ t('reports.leadership.grade') }}
            <select class="h-11 w-full min-w-0 max-w-full rounded-md border border-[#d8d0df] bg-white px-3 font-normal" name="scoreGrade" [(ngModel)]="query.grade" (ngModelChange)="onGradeChange()">
              <option value="">{{ t(viewKey() === 'class-letter-distribution' ? 'reports.score.chooseGrade' : 'reports.leadership.allGrades') }}</option>
              @for (grade of gradeChoices(); track grade) { <option [value]="grade">{{ grade }}</option> }
            </select>
          </label>
          <label class="grid min-w-0 gap-1 text-sm font-semibold text-[#32124f]">
            {{ t('reports.leadership.homeroom') }}
            <select class="h-11 w-full min-w-0 max-w-full rounded-md border border-[#d8d0df] bg-white px-3 font-normal" name="scoreHomeroom" [(ngModel)]="query.homeroom" (ngModelChange)="onHomeroomChange()">
              <option value="">{{ t('reports.leadership.allHomerooms') }}</option>
              @for (homeroom of homeroomChoices(); track homeroom) { <option [value]="homeroom">{{ homeroom }}</option> }
            </select>
          </label>
          @if (showSubjectFilter()) {
            <app-searchable-multi-select
              [label]="t('reports.score.subject')"
              [searchPlaceholder]="t('reports.score.searchSubjects')"
              [emptySelectionLabel]="t('reports.score.allSubjects')"
              [multiple]="false"
              [options]="subjectChoices()" [selected]="query.subjectCode ? [query.subjectCode] : []"
              (selectedChange)="selectSubject($event)" />
          }
          @if (showSectionFilter()) {
            <label class="grid min-w-0 gap-1 text-sm font-semibold text-[#32124f]">
              {{ t('reports.score.section') }}
              <select class="h-11 w-full min-w-0 max-w-full rounded-md border border-[#d8d0df] bg-white px-3 font-normal" name="scoreSectionName" [(ngModel)]="query.canvasSectionName">
                <option value="">{{ t(viewKey() === 'class-subject-distribution' ? 'reports.score.chooseSection' : 'reports.score.allSections') }}</option>
                @for (sectionName of sectionChoices(); track sectionName) { <option [value]="sectionName">{{ sectionName }}</option> }
              </select>
            </label>
          }
          @if (!isCourseScore()) {
            <label class="grid min-w-0 gap-1 text-sm font-semibold text-[#32124f]">
              {{ t('reports.leadership.assessmentCategory') }}
              <select class="h-11 w-full min-w-0 max-w-full rounded-md border border-[#d8d0df] bg-white px-3 font-normal" name="scoreCategory" [(ngModel)]="query.assessmentCategory">
                @for (category of categoryChoices(); track category) { <option [value]="category">{{ t('reports.leadership.category.' + category) }}</option> }
              </select>
            </label>
          }
          @if (isRanking()) {
            <label class="grid min-w-0 gap-1 text-sm font-semibold text-[#32124f]">
              {{ t('reports.score.rankingLimit') }}
              <select class="h-11 w-full min-w-0 max-w-full rounded-md border border-[#d8d0df] bg-white px-3 font-normal" name="scoreRankingLimit" [(ngModel)]="query.rankingLimit">
                @for (limit of rankingLimitChoices; track limit) { <option [ngValue]="limit">{{ limit }}</option> }
              </select>
            </label>
          }
          <app-searchable-multi-select
            [label]="t('reports.score.englishProgrammes')"
            [searchPlaceholder]="t('reports.score.searchEnglishProgrammes')"
            [emptySelectionLabel]="t('reports.score.allEnglishProgrammes')"
            [hint]="t('reports.score.englishProgrammesHint')"
            [options]="englishProgrammeChoices" [selected]="query.englishProgrammes"
            (selectedChange)="selectEnglishProgrammes($event)" />
        </div>
        @if (!requiredFiltersSelected()) {
          <p class="mt-4 rounded-md border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900">{{ missingSelectionText() }}</p>
        }
        <div class="mt-5 flex flex-wrap items-center gap-2">
          <button type="submit" class="rounded-md bg-[#32124f] px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-[#4a1b73] disabled:opacity-50" [disabled]="loading() || !academicYear || !requiredFiltersSelected()">{{ t('reports.leadership.run') }}</button>
          <button type="button" class="rounded-md border border-[#d8d0df] bg-white px-4 py-2.5 text-sm font-semibold text-[#32124f] transition hover:bg-[#f5f0f7]" (click)="resetFilters()">{{ t('reports.leadership.reset') }}</button>
        </div>
      </form>

      @if (loading() && !data()) {
        <div class="grid min-h-72 place-items-center" role="status"><div class="flex items-center gap-3 text-sm font-semibold text-[#625969]"><span class="h-7 w-7 animate-spin rounded-full border-2 border-[#e6dfea] border-t-[#6d3a91]"></span>{{ t('reports.loading') }}</div></div>
      } @else if (data(); as result) {
        @if (loading()) { <p class="border-b border-[#eee8f1] bg-[#faf8fb] px-5 py-3 text-sm text-[#625969]" role="status">{{ t('reports.loading') }}</p> }
        @if (!result.aggregateAvailable) {
          <div class="m-5 rounded-lg border border-amber-200 bg-amber-50 p-5 text-sm leading-6 text-amber-900">{{ noticeText(result.notice) }}</div>
        } @else if (isSelectionNotice(result.notice)) {
          @if (requiredFiltersSelected()) {
            <div class="m-5 rounded-lg border border-amber-200 bg-amber-50 p-5 text-sm leading-6 text-amber-900">{{ noticeText(result.notice) }}</div>
          }
        } @else {
          <div class="grid gap-px border-b border-[#eee8f1] bg-[#eee8f1] sm:grid-cols-3">
            <div class="bg-white px-5 py-5"><p class="text-[10px] font-bold uppercase tracking-[0.12em] text-[#817589]">{{ t(primaryValueKey()) }}</p><p class="mt-1 text-2xl font-semibold text-[#32124f]">{{ isBandReport() ? formatPercent(result.total.cdPercent) : formatScore(result.total.averageScore, result.unit) }}</p></div>
            <div class="bg-white px-5 py-5"><p class="text-[10px] font-bold uppercase tracking-[0.12em] text-[#817589]">{{ t(isRanking() ? 'reports.leadership.students' : 'reports.score.scoreCount') }}</p><p class="mt-1 text-2xl font-semibold text-[#32124f]">{{ formatNumber(result.total.scoreCount) }}</p></div>
            <div class="bg-white px-5 py-5"><p class="text-[10px] font-bold uppercase tracking-[0.12em] text-[#817589]">{{ t('reports.leadership.students') }}</p><p class="mt-1 text-2xl font-semibold text-[#32124f]">{{ formatNumber(result.total.studentCount) }}</p></div>
          </div>
          <p class="border-b border-[#eee8f1] bg-[#faf8fb] px-5 py-3 text-xs leading-5 text-[#756b7c] sm:px-6">{{ t(methodologyKey()) }}</p>
          @if (result.rows.length > 0 && result.notice) {
            <p class="border-b border-[#f0e3c6] bg-[#fffaf0] px-5 py-3 text-xs leading-5 text-[#795c1d] sm:px-6">{{ noticeText(result.notice) }}</p>
          }
          @if (!isCourseScore() && result.scoreSource === 'canvas-submission-score-percent') {
            <p class="border-b border-[#f0e3c6] bg-[#fffaf0] px-5 py-3 text-xs leading-5 text-[#795c1d] sm:px-6">{{ t('reports.score.referenceDifference') }}</p>
          }
          @if (isCourseScore()) {
            <p class="border-b border-[#f0e3c6] bg-[#fffaf0] px-5 py-3 text-xs leading-5 text-[#795c1d] sm:px-6">{{ t('reports.score.currentScoreNotice') }}</p>
          }
          @if (isBandReport()) {
            <div class="flex flex-wrap items-center gap-x-4 gap-y-1 border-b border-[#eee8f1] bg-[#fffaf0] px-5 py-3 text-xs text-[#654e20] sm:px-6">
              <strong>{{ t('reports.score.bandLegend') }}:</strong>
              <span>{{ t('reports.score.bandAPlus') }}</span><span>{{ t('reports.score.bandA') }}</span>
              <span>{{ t('reports.score.bandB') }}</span><span>{{ t('reports.score.bandC') }}</span>
              <span>{{ t('reports.score.bandD') }}</span>
            </div>
          }
          @if (result.rows.length === 0) {
            <div class="grid min-h-48 place-items-center px-6 py-10 text-center text-sm text-[#817589]">{{ noticeText(result.notice) }}</div>
          } @else if (isMatrix()) {
            <div class="overflow-x-auto p-5 sm:p-6">
              <table class="w-full min-w-[780px] border-collapse text-sm">
                <caption class="sr-only">{{ t(reportBase() + '.title') }}</caption>
                <thead><tr class="border-b border-[#e7e0eb] bg-[#f5f0f7] text-[#5d486b]"><th scope="col" class="sticky left-0 bg-[#f5f0f7] px-4 py-3 text-left">{{ t('reports.score.subject') }}</th>@for (grade of matrixGrades(); track grade) { <th scope="col" class="px-3 py-3 text-right">{{ grade }}</th> }<th scope="col" class="px-4 py-3 text-right">{{ t('reports.score.overall') }}</th></tr></thead>
                <tbody class="divide-y divide-[#eee8f1]">
                  @for (subject of matrixSubjects(); track subject.key) {
                    <tr class="hover:bg-[#fdfbfe]"><th scope="row" class="sticky left-0 bg-white px-4 py-3 text-left font-semibold text-[#32124f]">{{ subject.label }}</th>@for (grade of matrixGrades(); track grade) { <td class="px-3 py-3 text-right" [title]="matrixCount(subject.rows, grade) + ' ' + t('reports.score.scoreCount')">{{ formatScore(matrixScore(subject.rows, grade), 'score10') }}</td> }<td class="px-4 py-3 text-right font-semibold text-[#32124f]">{{ formatScore(weightedAverage(subject.rows), 'score10') }}</td></tr>
                  }
                </tbody>
                <tfoot><tr class="border-t-2 border-[#d3c5dc] bg-[#f5f0f7] font-semibold text-[#32124f]"><th scope="row" class="sticky left-0 bg-[#f5f0f7] px-4 py-3 text-left">{{ t('reports.score.overall') }}</th>@for (grade of matrixGrades(); track grade) { <td class="px-3 py-3 text-right">{{ formatScore(gradeAverage(result.rows, grade), 'score10') }}</td> }<td class="px-4 py-3 text-right">{{ formatScore(result.total.averageScore, 'score10') }}</td></tr></tfoot>
              </table>
            </div>
          } @else if (isBandReport()) {
            <div class="overflow-x-auto p-5 sm:p-6">
              <table class="w-full min-w-[740px] border-collapse text-sm">
                <caption class="sr-only">{{ t(reportBase() + '.title') }}</caption>
                <thead><tr class="border-b border-[#e7e0eb] bg-[#f5f0f7] text-[#5d486b]"><th scope="col" class="px-4 py-3 text-left">{{ rowHeading() }}</th><th scope="col" class="px-3 py-3 text-right">A+</th><th scope="col" class="px-3 py-3 text-right">A</th><th scope="col" class="px-3 py-3 text-right">B</th><th scope="col" class="px-3 py-3 text-right">C</th><th scope="col" class="px-3 py-3 text-right">D</th><th scope="col" class="px-3 py-3 text-right">{{ t('reports.score.cdRate') }}</th><th scope="col" class="px-4 py-3 text-right">{{ t('reports.score.scoreCount') }}</th></tr></thead>
                <tbody class="divide-y divide-[#eee8f1]">@for (row of result.rows; track row.key) { <tr class="hover:bg-[#fdfbfe]"><th scope="row" class="px-4 py-3 text-left font-semibold text-[#32124f]">{{ row.label }}</th><td class="px-3 py-3 text-right">{{ formatNumber(row.aPlus) }}</td><td class="px-3 py-3 text-right">{{ formatNumber(row.a) }}</td><td class="px-3 py-3 text-right">{{ formatNumber(row.b) }}</td><td class="px-3 py-3 text-right">{{ formatNumber(row.c) }}</td><td class="px-3 py-3 text-right">{{ formatNumber(row.d) }}</td><td class="px-3 py-3 text-right font-semibold" [class.text-red-700]="(row.cdPercent ?? 0) >= 30">{{ formatPercent(row.cdPercent) }}</td><td class="px-4 py-3 text-right">{{ formatNumber(row.scoreCount) }}</td></tr> }</tbody>
                <tfoot><tr class="border-t-2 border-[#d3c5dc] bg-[#f5f0f7] font-semibold text-[#32124f]"><th scope="row" class="px-4 py-3 text-left">{{ t('reports.score.overall') }}</th><td class="px-3 py-3 text-right">{{ formatNumber(result.total.aPlus) }}</td><td class="px-3 py-3 text-right">{{ formatNumber(result.total.a) }}</td><td class="px-3 py-3 text-right">{{ formatNumber(result.total.b) }}</td><td class="px-3 py-3 text-right">{{ formatNumber(result.total.c) }}</td><td class="px-3 py-3 text-right">{{ formatNumber(result.total.d) }}</td><td class="px-3 py-3 text-right">{{ formatPercent(result.total.cdPercent) }}</td><td class="px-4 py-3 text-right">{{ formatNumber(result.total.scoreCount) }}</td></tr></tfoot>
              </table>
            </div>
          } @else if (isHistogram()) {
            <div class="p-5 sm:p-6"><div class="space-y-2" role="img" [attr.aria-label]="t(reportBase() + '.title')">
              @for (row of result.rows; track row.key) { <div class="grid grid-cols-[72px_minmax(0,1fr)_64px] items-center gap-3 text-sm"><span class="text-[#625969]">{{ binLabel(row) }}</span><div class="h-7 overflow-hidden rounded bg-[#f2ecf5]"><div class="h-full min-w-[2px] rounded bg-[#6d3a91]" [style.width.%]="barWidth(row.scoreCount, result.rows)"></div></div><span class="text-right font-semibold text-[#32124f]">{{ formatNumber(row.scoreCount) }}</span></div> }
            </div><p class="mt-4 text-right text-sm font-semibold text-[#32124f]">{{ t('reports.score.overall') }}: {{ formatNumber(result.total.scoreCount) }}</p></div>
          } @else {
            <div class="overflow-x-auto p-5 sm:p-6"><table class="w-full min-w-[520px] border-collapse text-sm">
              <caption class="sr-only">{{ t(reportBase() + '.title') }}</caption>
              <thead><tr class="border-b border-[#e7e0eb] bg-[#f5f0f7] text-[#5d486b]">
                <th scope="col" class="px-4 py-3 text-left">{{ rowHeading() }}</th>
                @if (isRanking()) {
                  <th scope="col" class="px-3 py-3 text-left">{{ t('reports.leadership.grade') }}</th>
                  <th scope="col" class="px-3 py-3 text-left">{{ t('reports.leadership.homeroom') }}</th>
                }
                <th scope="col" class="px-4 py-3 text-right">{{ t(primaryValueKey()) }}</th>
                @if (!isRanking()) { <th scope="col" class="px-4 py-3 text-right">{{ t('reports.score.scoreCount') }}</th> }
              </tr></thead>
              <tbody class="divide-y divide-[#eee8f1]">
                @for (row of result.rows; track row.key; let index = $index) {
                  <tr class="hover:bg-[#fdfbfe]">
                    <th scope="row" class="px-4 py-3 text-left font-semibold text-[#32124f]">
                      @if (isRanking()) { <span class="mr-2 inline-grid h-6 w-6 place-items-center rounded bg-[#f2ecf5] text-xs">{{ index + 1 }}</span> }
                      @if (isRanking() && row.studentId) { <a class="hover:text-[#6d3a91] hover:underline" [routerLink]="['/students', row.studentId]">{{ row.label }}</a> }
                      @else { {{ row.label }} }
                      @if (isRanking() && row.sisUserId) { <span class="ml-2 text-xs font-normal text-[#817589]">{{ row.sisUserId }}</span> }
                    </th>
                    @if (isRanking()) {
                      <td class="px-3 py-3">{{ row.grade || '—' }}</td>
                      <td class="px-3 py-3">{{ row.homeroom || '—' }}</td>
                    }
                    <td class="px-4 py-3 text-right font-semibold text-[#32124f]">{{ formatScore(row.averageScore, result.unit) }}</td>
                    @if (!isRanking()) { <td class="px-4 py-3 text-right">{{ formatNumber(row.scoreCount) }}</td> }
                  </tr>
                }
              </tbody>
              <tfoot><tr class="border-t-2 border-[#d3c5dc] bg-[#f5f0f7] font-semibold text-[#32124f]">
                <th scope="row" class="px-4 py-3 text-left">{{ isRanking() ? selectedStudentsLabel(result.rows.length) : t('reports.score.overall') }}</th>
                @if (isRanking()) { <td colspan="2"></td> }
                <td class="px-4 py-3 text-right">{{ formatScore(result.total.averageScore, result.unit) }}</td>
                @if (!isRanking()) { <td class="px-4 py-3 text-right">{{ formatNumber(result.total.scoreCount) }}</td> }
              </tr></tfoot>
            </table></div>
          }
        }
      }
      @if (error()) { <div class="m-5 rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-800" role="alert">{{ error() }} <button class="ml-2 font-semibold underline" type="button" (click)="run(true)">{{ t('reports.retry') }}</button></div> }
    </section>
  `,
})
export class ScoreReportComponent implements OnInit, OnChanges, OnDestroy {
  @Input({ required: true }) reportKey: ScoreReportMenuKey = 'school-subject-grade-average';
  @Input({ required: true }) academicYear = '';
  @Input({ required: true }) term = 'all';
  @Input({ required: true }) periodLabel = '';

  private readonly reports = inject(ScoreReportsService);
  private readonly language = inject(LanguageService);
  private readonly excel = inject(ReportExcelExportService);
  private readonly toast = inject(ToastService);
  readonly exporting = signal(false);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  readonly options = signal<ScoreReportOptions | null>(null);
  readonly data = signal<ScoreReportResult | null>(null);
  readonly loading = signal(false);
  readonly optionsLoading = signal(true);
  readonly optionsError = signal(false);
  readonly error = signal<string | null>(null);
  readonly rankingLimitChoices = [10, 25, 50, 100] as const;
  readonly englishProgrammeChoices = [{ value: 'ESL', label: 'ESL' }, { value: 'ELA', label: 'ELA' }];
  selectEnglishProgrammes(values: Array<string | number>): void {
    this.query.englishProgrammes = this.englishProgrammeChoices
      .filter(option => values.includes(option.value)).map(option => option.value as EnglishProgramme);
  }
  private normalizedEnglishProgrammes(values?: readonly string[]): EnglishProgramme[] {
    return !values?.length ? ['ESL', 'ELA'] : this.englishProgrammeChoices
      .filter(option => values.includes(option.value)).map(option => option.value as EnglishProgramme);
  }
  englishProgrammeNotice(values = this.appliedQuery?.englishProgrammes ?? this.query.englishProgrammes): string {
    const programmes = this.normalizedEnglishProgrammes(values);
    return `${programmes.join(' + ')}. ${this.t('reports.score.englishProgrammeScope')}`
      + (programmes.length === 2 ? ` ${this.t('reports.score.englishBothNotice')}` : '');
  }
  categoryChoices(): AssessmentCategory[] {
    return (this.options()?.assessmentCategories ?? [])
      .filter((category): category is AssessmentCategory =>
        category === 'all' || category === 'HS1' || category === 'HS2' ||
        category === 'HS3' || category === 'GPA');
  }
  subaccountChoices(): Array<{ value: number; label: string; detail?: string }> {
    const constrained = !!(this.query.schoolLevel || this.query.grade || this.query.homeroom);
    const grades = this.gradesForCurrentCohortSelection();
    return (this.options()?.subaccounts ?? [])
      .filter(account => !constrained || account.gradeLevels.some(grade => grades.has(grade)))
      .map(account => ({ value: account.id, label: account.name }));
  }
  selectedSubaccountOptionIds(): number[] {
    return this.selectedSubaccountOptions(this.query.subaccountIds).map(account => account.id);
  }
  readonly subjectChoices = computed(() => (this.options()?.subjects ?? []).map(subject => ({
    value: subject.code, label: subject.name,
  })));
  readonly matrixGrades = computed(() => [...new Set((this.data()?.rows ?? []).map(row => row.grade).filter((grade): grade is string => !!grade))]
    .sort((a, b) => a.localeCompare(b, undefined, { numeric: true })));
  readonly matrixSubjects = computed<MatrixSubject[]>(() => {
    const grouped = new Map<string, MatrixSubject>();
    for (const row of this.data()?.rows ?? []) {
      const key = row.subjectCode || row.subjectName || row.label;
      const group = grouped.get(key) ?? { key, label: row.subjectName || row.label, rows: [] };
      group.rows.push(row);
      grouped.set(key, group);
    }
    return [...grouped.values()].sort((a, b) => a.label.localeCompare(b.label));
  });
  query: ScoreReportQuery = this.defaultQuery();
  private initialized = false;
  private requestVersion = 0;
  private runSubscription?: Subscription;
  private optionsSubscription?: Subscription;
  private appliedRequestKey: string | null = null;
  private appliedQuery: ScoreReportQuery | null = null;

  ngOnInit(): void {
    this.initialized = true;
    this.query = this.queryFromUrl();
    this.run();
    this.loadOptions();
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (!this.initialized || (!changes['academicYear'] && !changes['term'] && !changes['reportKey'])) return;
    if (changes['reportKey']) this.query = this.queryFromUrl();
    this.run();
  }

  ngOnDestroy(): void {
    this.requestVersion++;
    this.runSubscription?.unsubscribe();
    this.optionsSubscription?.unsubscribe();
  }

  t(key: string): string { return this.language.t(key); }
  reportBase(): string { return `reports.score.${reportViews[this.reportKey].title}`; }
  viewKey(): ScoreReportViewKey { return reportViews[this.reportKey].view; }
  isCourseScore(): boolean { return this.viewKey() === 'current-course-score'; }
  sourceLabel(): string { return this.t(this.isCourseScore() ? 'reports.score.source.current' : 'reports.score.source.test'); }
  schoolLevelLabel(level: string): string { return this.t(`access.schoolLevel.${level}.title`); }
  isMatrix(): boolean { return this.viewKey() === 'subject-grade-average'; }
  isBandReport(): boolean { return ['subject-letter-distribution', 'grade-letter-distribution', 'class-letter-distribution', 'class-subject-distribution'].includes(this.viewKey()); }
  isHistogram(): boolean { return this.viewKey() === 'score-histogram'; }
  isRanking(): boolean { return this.viewKey() === 'top-students' || this.viewKey() === 'bottom-students'; }
  showSubjectFilter(): boolean { return ['score-histogram', 'top-students', 'bottom-students'].includes(this.viewKey()); }
  showSectionFilter(): boolean { return this.viewKey() === 'class-subject-distribution'; }
  singleSubjectReport(): boolean { return ['grade-letter-distribution', 'class-letter-distribution'].includes(this.viewKey()); }
  hasSubjectScope(): boolean { return this.query.subaccountIds.length > 0; }
  requiredFiltersSelected(): boolean {
    if (this.viewKey() === 'grade-letter-distribution') return this.hasSubjectScope();
    if (this.viewKey() === 'class-letter-distribution') return this.hasSubjectScope() && !!this.query.grade;
    if (this.viewKey() === 'class-subject-distribution') return !!this.query.canvasSectionName || this.query.canvasSectionId != null;
    return true;
  }
  missingSelectionText(): string {
    if (this.viewKey() === 'class-letter-distribution') return this.t('reports.score.notice.selectSubjectAndGrade');
    if (this.viewKey() === 'class-subject-distribution') return this.t('reports.score.notice.selectSection');
    return this.t('reports.score.notice.selectSubject');
  }
  isSelectionNotice(notice: string | null): boolean {
    return notice === 'select_subject' || notice === 'select_subject_and_grade' || notice === 'select_section';
  }
  rowHeading(): string {
    switch (this.viewKey()) {
      case 'subject-letter-distribution': case 'assessment-average': case 'current-course-score': case 'class-subject-distribution': return this.t('reports.score.subject');
      case 'grade-letter-distribution': return this.t('reports.leadership.grade');
      case 'class-letter-distribution': return this.t('reports.leadership.homeroom');
      case 'top-students': case 'bottom-students': return this.t('reports.leadership.students');
      default: return this.t('reports.score.range');
    }
  }
  primaryValueKey(): string { return this.isBandReport() ? 'reports.score.cdRate' : this.isCourseScore() ? 'reports.score.currentCourseScore.value' : 'reports.score.averageScore10'; }
  methodologyKey(): string {
    if (this.isCourseScore()) return 'reports.score.method.current';
    if (this.isBandReport()) return 'reports.score.method.bandsTest';
    if (this.isHistogram()) return 'reports.score.method.histogram';
    if (this.isRanking()) return 'reports.score.method.ranking';
    return 'reports.score.method.assignment';
  }
  noticeText(notice: string | null): string {
    const notices: Record<string, string> = {
      aggregate_unavailable: 'reports.leadership.unavailable',
      no_matching_data: 'reports.leadership.noRows',
      select_subject: 'reports.score.notice.selectSubject',
      select_subject_and_grade: 'reports.score.notice.selectSubjectAndGrade',
      select_section: 'reports.score.notice.selectSection',
      term_current_score_unavailable: 'reports.score.notice.termCurrentUnavailable',
    };
    return this.t(notices[notice ?? ''] ?? 'reports.leadership.noRows');
  }
  formatNumber(value: number | null | undefined): string { return value == null || !Number.isFinite(value) ? '—' : new Intl.NumberFormat(this.language.language() === 'vi' ? 'vi-VN' : 'en-US').format(value); }
  formatScore(value: number | null | undefined, unit: 'score10' | 'percent'): string {
    if (value == null || !Number.isFinite(value)) return '—';
    return `${new Intl.NumberFormat(this.language.language() === 'vi' ? 'vi-VN' : 'en-US', { minimumFractionDigits: 1, maximumFractionDigits: 2 }).format(value)}${unit === 'percent' ? '%' : ''}`;
  }
  formatPercent(value: number | null | undefined): string { return this.formatScore(value, 'percent'); }
  selectedStudentsLabel(count: number): string {
    return this.t('reports.score.selectedStudents').replace('{count}', this.formatNumber(count));
  }
  binLabel(row: ScoreReportRow): string { return row.label; }
  barWidth(count: number, rows: ScoreReportRow[]): number { const maximum = Math.max(...rows.map(row => row.scoreCount), 1); return Math.max(0, Math.min(100, count / maximum * 100)); }
  matrixScore(rows: ScoreReportRow[], grade: string): number | null { return this.weightedAverage(rows.filter(row => row.grade === grade)); }
  matrixCount(rows: ScoreReportRow[], grade: string): number { return rows.filter(row => row.grade === grade).reduce((sum, row) => sum + row.scoreCount, 0); }
  gradeAverage(rows: ScoreReportRow[], grade: string): number | null { return this.weightedAverage(rows.filter(row => row.grade === grade)); }
  weightedAverage(rows: ScoreReportRow[]): number | null {
    const scored = rows.filter(row => row.averageScore != null && row.scoreCount > 0);
    const count = scored.reduce((sum, row) => sum + row.scoreCount, 0);
    return count ? scored.reduce((sum, row) => sum + (row.scoreSum ?? row.averageScore! * row.scoreCount), 0) / count : null;
  }
  homeroomChoices(): string[] {
    const rows = this.cohortRows();
    if (rows.length === 0) return this.options()?.homerooms ?? [];
    const subaccountGrades = this.selectedSubaccountGrades();
    return this.sortedUnique(rows.filter(row =>
      (!this.query.schoolLevel || this.schoolLevelForGrade(row.grade) === this.query.schoolLevel) &&
      (!this.query.grade || row.grade === this.query.grade) &&
      (!subaccountGrades.size || subaccountGrades.has(row.grade))).map(row => row.homeroom));
  }
  schoolLevelChoices(): string[] {
    const cohorts = this.cohortRows();
    if (cohorts.length === 0) return this.options()?.schoolLevels ?? [];
    const subaccountGrades = this.selectedSubaccountGrades();
    const rows = cohorts.filter(row =>
      (!this.query.grade || row.grade === this.query.grade) &&
      (!this.query.homeroom || row.homeroom === this.query.homeroom) &&
      (!subaccountGrades.size || subaccountGrades.has(row.grade)));
    const available = new Set(rows.map(row => this.schoolLevelForGrade(row.grade)).filter(Boolean));
    return (this.options()?.schoolLevels ?? []).filter(level => available.has(level));
  }
  gradeChoices(): string[] {
    const rows = this.cohortRows();
    if (rows.length === 0) return this.options()?.grades ?? [];
    const subaccountGrades = this.selectedSubaccountGrades();
    const values = rows.filter(row =>
      (!this.query.schoolLevel || this.schoolLevelForGrade(row.grade) === this.query.schoolLevel) &&
      (!this.query.homeroom || row.homeroom === this.query.homeroom) &&
      (!subaccountGrades.size || subaccountGrades.has(row.grade))).map(row => row.grade);
    return this.sortedUnique(values);
  }
  sectionChoices(): string[] {
    const selected = new Set(this.query.subaccountIds);
    return (this.options()?.sections ?? [])
      .filter(section => !selected.size || section.subaccountIds.some(id => selected.has(id)))
      .map(section => section.name);
  }
  selectSubaccounts(values: Array<string | number>): void {
    const selected = values.map(Number).filter(Number.isSafeInteger);
    this.query.subaccountIds = this.singleSubjectReport() ? selected.slice(-1) : selected;
    if (this.query.homeroom && !this.homeroomChoices().includes(this.query.homeroom)) this.query.homeroom = '';
    if (this.query.grade && !this.gradeChoices().includes(this.query.grade)) this.query.grade = '';
    if (this.query.schoolLevel && !this.schoolLevelChoices().includes(this.query.schoolLevel)) this.query.schoolLevel = '';
    this.clearInvalidSection();
  }
  selectSubject(values: Array<string | number>): void {
    this.query.subjectCode = values.length ? String(values[values.length - 1]) : '';
    this.onSubjectChange();
  }
  onSchoolLevelChange(): void {
    this.clearInvalidSubaccounts();
    if (this.query.homeroom && !this.homeroomChoices().includes(this.query.homeroom)) this.query.homeroom = '';
    if (this.query.grade && !this.gradeChoices().includes(this.query.grade)) this.query.grade = '';
  }
  onGradeChange(): void {
    this.clearInvalidSubaccounts();
    if (this.query.homeroom && !this.homeroomChoices().includes(this.query.homeroom)) this.query.homeroom = '';
    if (this.query.schoolLevel && !this.schoolLevelChoices().includes(this.query.schoolLevel)) this.query.schoolLevel = '';
  }
  onHomeroomChange(): void {
    this.clearInvalidSubaccounts();
    if (this.query.grade && !this.gradeChoices().includes(this.query.grade)) this.query.grade = '';
    if (this.query.schoolLevel && !this.schoolLevelChoices().includes(this.query.schoolLevel)) this.query.schoolLevel = '';
  }
  onSubjectChange(): void { this.query.canvasCourseId = null; this.query.canvasSectionId = null; }
  resetFilters(): void { this.query = this.defaultQuery(); this.run(true); }
  filtersPending(): boolean { return this.appliedRequestKey !== JSON.stringify(this.requestFromQuery()); }

  loadOptions(): void {
    this.optionsSubscription?.unsubscribe();
    this.optionsLoading.set(true);
    this.optionsError.set(false);
    this.optionsSubscription = this.reports.options().subscribe({
      next: options => {
        this.options.set(options);
        this.optionsLoading.set(false);
        if (this.normalizeCohortSelection()) this.run(true);
      },
      error: () => { this.optionsLoading.set(false); this.optionsError.set(true); },
    });
  }

  run(force = false): void {
    if (!this.academicYear) { this.data.set(null); this.loading.set(false); return; }
    const request = this.requestFromQuery();
    const key = JSON.stringify(request);
    if (!force && key === this.appliedRequestKey && this.data()) return;
    this.runSubscription?.unsubscribe();
    const version = ++this.requestVersion;
    this.loading.set(true);
    this.error.set(null);
    this.syncQueryToUrl();
    this.runSubscription = this.reports.run(request).subscribe({
      next: result => { if (version !== this.requestVersion) return; this.data.set(result); this.appliedRequestKey = key; this.appliedQuery = request; this.loading.set(false); },
      error: () => { if (version !== this.requestVersion) return; this.error.set(this.t('reports.loadError')); this.loading.set(false); },
    });
  }

  async exportExcel(): Promise<void> {
    const result = this.data();
    const applied = this.appliedQuery;
    if (!result?.aggregateAvailable || !result.rows.length || !applied || this.exporting()) return;
    this.exporting.set(true);
    try {
      const title = this.t(this.reportBase() + '.title');
      const filters: Array<[string, string]> = [
        [this.t('reports.score.englishScope'), this.englishProgrammeNotice(applied.englishProgrammes)],
        [this.t('reports.score.schoolLevel'), applied.schoolLevel ? this.schoolLevelLabel(applied.schoolLevel) : this.t('reports.score.allSchoolLevels')],
        [this.t('reports.leadership.grade'), applied.grade || this.t('reports.leadership.allGrades')],
        [this.t('reports.leadership.homeroom'), applied.homeroom || this.t('reports.leadership.allHomerooms')],
        ...(this.showSubjectFilter() ? [
          [this.t('reports.score.subject'), this.options()?.subjects.find(subject => subject.code === applied.subjectCode)?.name || this.t('reports.score.allSubjects')] as [string, string],
        ] : []),
        ...(this.showSectionFilter() ? [
          [this.t('reports.score.section'), applied.canvasSectionName || this.t('reports.score.allSections')] as [string, string],
        ] : []),
        ...(!this.isCourseScore() ? [
          [this.t('reports.leadership.assessmentCategory'), this.t('reports.leadership.category.' + applied.assessmentCategory)] as [string, string],
        ] : []),
        ...(this.isRanking() ? [
          [this.t('reports.score.rankingLimit'), String(applied.rankingLimit)] as [string, string],
        ] : []),
        [this.t('reports.leadership.subaccounts'), this.selectedSubaccountOptions(applied.subaccountIds).map(account => account.name).join(', ') || this.t('reports.leadership.allSubaccounts')],
      ];
      const labelColumn = { header: this.rowHeading(), value: (row: ScoreReportRow) => row.label };
      const columns: ReportExportColumn<ScoreReportRow>[] = this.isBandReport() ? [
        labelColumn,
        { header: 'A+', value: (row: ScoreReportRow) => row.aPlus },
        { header: 'A', value: (row: ScoreReportRow) => row.a },
        { header: 'B', value: (row: ScoreReportRow) => row.b },
        { header: 'C', value: (row: ScoreReportRow) => row.c },
        { header: 'D', value: (row: ScoreReportRow) => row.d },
        { header: this.t('reports.score.cdRate'), value: (row: ScoreReportRow) => row.cdPercent, format: 'percentage' },
        { header: this.t('reports.score.scoreCount'), value: (row: ScoreReportRow) => row.scoreCount },
      ] : this.isMatrix() ? [
        { header: this.t('reports.score.subject'), value: (row: ScoreReportRow) => row.subjectName || row.label },
        { header: this.t('reports.leadership.grade'), value: (row: ScoreReportRow) => row.grade },
        { header: this.t('reports.score.averageScore10'), value: (row: ScoreReportRow) => row.averageScore, format: result.unit === 'percent' ? 'percentage' : 'decimal' },
        { header: this.t('reports.score.scoreCount'), value: (row: ScoreReportRow) => row.scoreCount },
      ] : this.isHistogram() ? [
        { header: this.t('reports.score.range'), value: (row: ScoreReportRow) => this.binLabel(row) },
        { header: this.t('reports.score.scoreCount'), value: (row: ScoreReportRow) => row.scoreCount },
      ] : this.isRanking() ? [
        labelColumn,
        { header: this.t('reports.leadership.grade'), value: (row: ScoreReportRow) => row.grade },
        { header: this.t('reports.leadership.homeroom'), value: (row: ScoreReportRow) => row.homeroom },
        { header: this.t(primaryValueKeyForExport(result.unit)), value: (row: ScoreReportRow) => row.averageScore, format: result.unit === 'percent' ? 'percentage' : 'decimal' },
      ] : [
        labelColumn,
        { header: this.t(primaryValueKeyForExport(result.unit)), value: (row: ScoreReportRow) => row.averageScore, format: result.unit === 'percent' ? 'percentage' : 'decimal' },
        { header: this.t('reports.score.scoreCount'), value: (row: ScoreReportRow) => row.scoreCount },
      ];
      await this.excel.download({ fileName: `${title}-${this.academicYear}-${this.term}`, title, period: this.periodLabel, filters, columns, rows: result.rows });
    } catch {
      this.toast.error(this.t('reports.exportError'));
    } finally {
      this.exporting.set(false);
    }
  }

  private defaultQuery(): ScoreReportQuery {
    return { viewKey: this.viewKey(), academicYear: this.academicYear, term: this.term,
      grade: '', homeroom: '', schoolLevel: '', canvasSectionId: null, canvasSectionName: '', subjectCode: '', canvasCourseId: null,
      subaccountIds: [], assessmentCategory: 'all', englishProgrammes: ['ESL', 'ELA'], rankingLimit: 10 };
  }

  private requestFromQuery(): ScoreReportQuery {
    const request = { ...this.query, viewKey: this.viewKey(), academicYear: this.academicYear, term: this.term,
      subaccountIds: [...new Set(this.query.subaccountIds)].sort((a, b) => a - b),
      englishProgrammes: this.normalizedEnglishProgrammes(this.query.englishProgrammes),
      assessmentCategory: this.isCourseScore() ? 'all' : this.query.assessmentCategory };
    if (!this.showSubjectFilter()) {
      request.subjectCode = '';
      request.canvasCourseId = null;
    }
    if (!this.showSectionFilter()) {
      request.canvasSectionId = null;
      request.canvasSectionName = '';
    }
    return request;
  }

  private queryFromUrl(): ScoreReportQuery {
    const query = this.defaultQuery();
    const params = this.route.snapshot.queryParamMap;
    if (params.get('scoreReport') !== this.reportKey) return query;
    query.englishProgrammes = this.normalizedEnglishProgrammes(params.getAll('scoreEnglish')
      .filter(value => value === 'ESL' || value === 'ELA'));
    query.grade = this.shortValue(params.get('scoreGrade'), 50);
    query.homeroom = this.shortValue(params.get('scoreHomeroom'), 100);
    const schoolLevel = params.get('scoreLevel');
    if (schoolLevel && ['Primary', 'LowerSecondary', 'UpperSecondary'].includes(schoolLevel)) query.schoolLevel = schoolLevel;
    if (this.showSubjectFilter()) {
      query.subjectCode = this.shortValue(params.get('scoreSubject'), 100);
      query.canvasCourseId = this.positiveId(params.get('scoreCourse'));
    }
    if (this.showSectionFilter()) {
      query.canvasSectionId = this.positiveId(params.get('scoreSection'));
      query.canvasSectionName = this.shortValue(params.get('scoreSectionName'), 150);
    }
    query.subaccountIds = [...new Set(params.getAll('scoreAccount').map(Number).filter(value => Number.isSafeInteger(value) && value > 0))]
      .slice(0, this.singleSubjectReport() ? 1 : 50);
    const category = params.get('scoreCategory');
    if (!this.isCourseScore() && this.categoryChoices().includes(category as AssessmentCategory))
      query.assessmentCategory = category as AssessmentCategory;
    const rankingLimit = Number(params.get('scoreLimit'));
    if (this.isRanking() && this.rankingLimitChoices.some(value => value === rankingLimit))
      query.rankingLimit = rankingLimit;
    return query;
  }
  private shortValue(value: string | null, maximum: number): string { const trimmed = value?.trim() ?? ''; return trimmed.length <= maximum ? trimmed : ''; }
  private positiveId(value: string | null): number | null { const id = Number(value); return Number.isSafeInteger(id) && id > 0 ? id : null; }
  private cohortRows() { return this.options()?.cohorts ?? []; }
  private selectedSubaccountGrades(): Set<string> {
    return new Set(this.selectedSubaccountOptions(this.query.subaccountIds)
      .flatMap(account => account.gradeLevels));
  }
  private gradesForCurrentCohortSelection(): Set<string> {
    const rows = this.cohortRows();
    const values = rows.length
      ? rows.filter(row =>
          (!this.query.schoolLevel || this.schoolLevelForGrade(row.grade) === this.query.schoolLevel) &&
          (!this.query.grade || row.grade === this.query.grade) &&
          (!this.query.homeroom || row.homeroom === this.query.homeroom)).map(row => row.grade)
      : (this.options()?.grades ?? []).filter(grade =>
          (!this.query.schoolLevel || this.schoolLevelForGrade(grade) === this.query.schoolLevel) &&
          (!this.query.grade || grade === this.query.grade));
    return new Set(values);
  }
  private clearInvalidSubaccounts(): void {
    const available = new Set(this.subaccountChoices().map(option => Number(option.value)));
    this.query.subaccountIds = this.selectedSubaccountOptions(this.query.subaccountIds)
      .filter(account => available.has(account.id)).map(account => account.id);
  }
  private selectedSubaccountOptions(ids: number[]) {
    const selected = new Set(ids);
    return (this.options()?.subaccounts ?? []).filter(account =>
      selected.has(account.id) || account.accountIds.some(id => selected.has(id)));
  }
  private clearInvalidSection(): void {
    if (this.query.canvasSectionName &&
        !this.sectionChoices().includes(this.query.canvasSectionName))
      this.query.canvasSectionName = '';
  }
  private schoolLevelForGrade(grade: string): string | null {
    const value = Number(grade.match(/\d+/)?.[0]);
    if (value >= 1 && value <= 4) return 'Primary';
    if (value >= 5 && value <= 9) return 'LowerSecondary';
    if (value >= 10 && value <= 12) return 'UpperSecondary';
    return null;
  }
  private sortedUnique(values: string[]): string[] {
    return [...new Set(values)].sort((a, b) => a.localeCompare(b, undefined, { numeric: true }));
  }
  private normalizeCohortSelection(): boolean {
    const before = `${this.query.schoolLevel}|${this.query.grade}|${this.query.homeroom}|${this.query.subaccountIds.join(',')}|${this.query.canvasSectionName}`;
    this.clearInvalidSubaccounts();
    this.clearInvalidSection();
    if (this.query.homeroom && !this.homeroomChoices().includes(this.query.homeroom)) this.query.homeroom = '';
    if (this.query.grade && !this.gradeChoices().includes(this.query.grade)) this.query.grade = '';
    if (this.query.schoolLevel && !this.schoolLevelChoices().includes(this.query.schoolLevel)) this.query.schoolLevel = '';
    return before !== `${this.query.schoolLevel}|${this.query.grade}|${this.query.homeroom}|${this.query.subaccountIds.join(',')}|${this.query.canvasSectionName}`;
  }
  private syncQueryToUrl(): void {
    const request = this.requestFromQuery();
    void this.router.navigate([], { relativeTo: this.route, queryParams: {
      scoreReport: this.reportKey,
      scoreGrade: request.grade || null,
      scoreHomeroom: request.homeroom || null,
      scoreLevel: request.schoolLevel || null,
      scoreSubject: request.subjectCode || null,
      scoreCourse: request.canvasCourseId || null,
      scoreSection: request.canvasSectionId || null,
      scoreSectionName: request.canvasSectionName || null,
      scoreAccount: request.subaccountIds.length ? request.subaccountIds : null,
      scoreCategory: !this.isCourseScore() && request.assessmentCategory !== 'all' ? request.assessmentCategory : null,
      scoreEnglish: request.englishProgrammes.length === 2 ? null : request.englishProgrammes,
      scoreLimit: this.isRanking() && request.rankingLimit !== 10 ? request.rankingLimit : null,
      scoreBandSource: null,
    }, queryParamsHandling: 'merge', replaceUrl: true });
  }
}

function primaryValueKeyForExport(unit: 'score10' | 'percent'): string {
  return unit === 'score10' ? 'reports.score.averageScore10' : 'reports.score.currentCourseScore.value';
}
