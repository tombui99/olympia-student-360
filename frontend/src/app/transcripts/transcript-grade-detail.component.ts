import { Component, effect, inject, signal, untracked } from '@angular/core';
import { Subscription } from 'rxjs';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { LanguageService } from '../core/language/language.service';
import { formatTranscriptScore, TranscriptScoreScale } from './grading-scale';
import { SchoolTranscriptGradeDetail, TranscriptsService } from './transcripts.service';
import { TranscriptPreferencesService } from './transcript-preferences.service';

@Component({
  selector: 'app-transcript-grade-detail',
  imports: [RouterLink],
  template: `
    <section class="mx-auto max-w-[1500px]">
      <a routerLink="/transcripts" class="inline-flex items-center gap-2 text-sm font-semibold text-[#6d3a91] hover:text-[#32124f]">
        <span aria-hidden="true">←</span> {{ t('transcript.backToOverall') }}
      </a>

      <div class="mt-5 flex flex-col gap-4 lg:flex-row lg:items-end lg:justify-between">
        <div>
          <p class="text-sm uppercase tracking-[0.2em] text-[#8a6811]">{{ t('transcript.gradeDetailEyebrow') }}</p>
          <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">
            {{ t('transcript.grade') }} {{ gradeLevel }} · {{ academicYear }}
          </h2>
          <p class="mt-2 max-w-3xl text-[#6f6877]">{{ t('transcript.gradeDetailDescription') }}</p>
        </div>
        <div class="flex flex-col items-start gap-3 sm:items-end">
          <div class="inline-flex items-center gap-3 rounded-lg border border-[#d9d0df] bg-white px-4 py-3 text-sm text-[#65596d]">
            <span class="h-2.5 w-2.5 rounded-full bg-[#2f7778]"></span>{{ t('transcript.canvasOnly') }}
          </div>
          <div class="inline-flex rounded-lg border border-[#d9d0df] bg-white p-1" role="group" [attr.aria-label]="t('transcript.scoreScale')">
            @for (scale of scoreScales; track scale) {
              <button type="button" class="rounded-md px-3 py-2 text-xs font-semibold transition sm:text-sm"
                [class.bg-[#32124f]]="scoreScale() === scale" [class.text-white]="scoreScale() === scale"
                [class.text-[#695d70]]="scoreScale() !== scale" (click)="scoreScale.set(scale)">
                {{ t('transcript.scales.' + scale) }}
              </button>
            }
          </div>
        </div>
      </div>

      @if (loading() && !detail()) {
        <div class="mt-8 rounded-xl border border-[#e4dde8] bg-white py-24 text-center text-[#766d7d]">{{ t('transcript.loadingDetails') }}</div>
      } @else if (error()) {
        <div class="mt-8 rounded-xl border border-[#efcaca] bg-[#fff8f7] px-6 py-16 text-center">
          <h3 class="text-lg font-semibold text-[#8b2727]">{{ t('transcript.errorTitle') }}</h3>
          <button type="button" class="mt-5 rounded-md bg-[#32124f] px-5 py-2.5 text-sm font-semibold text-white" (click)="load(1)">{{ t('transcript.retry') }}</button>
        </div>
      } @else if (detail(); as record) {
        <div class="mt-8 overflow-hidden rounded-xl border border-[#e2dbe6] bg-white shadow-[0_12px_38px_rgba(50,18,79,0.06)]">
          <div class="grid grid-cols-2 border-b border-[#e8e1eb] lg:grid-cols-4">
            <div class="border-b border-r border-[#e8e1eb] p-5 lg:border-b-0 lg:p-6">
              <p class="text-xs font-bold uppercase tracking-[0.12em] text-[#817589]">{{ t('transcript.gradeAverage') }}</p>
              <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ score(record.averageScore) }}</p>
            </div>
            <div class="border-b border-[#e8e1eb] p-5 lg:border-b-0 lg:border-r lg:p-6">
              <p class="text-xs font-bold uppercase tracking-[0.12em] text-[#817589]">{{ t('transcript.studentsCovered') }}</p>
              <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ record.totalStudents }}</p>
            </div>
            <div class="border-r border-[#e8e1eb] p-5 lg:p-6">
              <p class="text-xs font-bold uppercase tracking-[0.12em] text-[#817589]">{{ t('transcript.subjects') }}</p>
              <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ record.totalSubjects }}</p>
            </div>
            <div class="p-5 lg:p-6">
              <p class="text-xs font-bold uppercase tracking-[0.12em] text-[#817589]">{{ t('transcript.sourceRecords') }}</p>
              <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ record.totalRecords }}</p>
            </div>
          </div>

          <div class="p-5 lg:p-8">
            <div class="mb-4 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
              <div>
                <h3 class="text-xl font-semibold text-[#32124f]">{{ t('transcript.subjectOverview') }}</h3>
                <p class="mt-1 text-sm text-[#756b7c]">{{ t('transcript.subjectAggregateNote') }}</p>
              </div>
              <label class="text-xs font-bold uppercase tracking-[0.1em] text-[#74677c]">
                {{ t('transcript.classFilter') }}
                <select class="mt-1 block min-w-48 rounded-md border border-[#d8cede] bg-white px-3 py-2.5 text-sm font-medium normal-case tracking-normal text-[#32124f]"
                  [value]="selectedClass()" (change)="changeClass($event)">
                  <option value="">{{ t('transcript.allClasses') }}</option>
                  @for (className of record.classOptions; track className) {
                    <option [value]="className">{{ className }}</option>
                  }
                </select>
              </label>
            </div>
            <article class="overflow-hidden rounded-xl border border-[#ded6e3]">
              <header class="flex items-center justify-between gap-3 bg-[#32124f] px-4 py-3 text-white">
                <h4 class="text-lg font-semibold">{{ t('transcript.grade') }} {{ record.gradeLevel }}</h4>
                <span class="text-sm font-bold">GPA {{ formatGpa(record.gpa) }}</span>
              </header>
              <div class="overflow-x-auto">
                <table class="w-full min-w-[480px] border-collapse text-sm">
                  <thead class="bg-[#eee5f2] text-[#4a3158]">
                    <tr>
                      <th class="px-4 py-2.5 text-left">{{ t('transcript.subject') }}</th>
                      <th class="px-3 py-2.5 text-center"><abbr class="no-underline" [title]="t('transcript.s1fHelp')">S1F</abbr></th>
                      <th class="px-3 py-2.5 text-center"><abbr class="no-underline" [title]="t('transcript.s2fHelp')">S2F</abbr></th>
                      <th class="bg-[#e9dfed] px-3 py-2.5 text-center"><abbr class="no-underline" [title]="t('transcript.yrHelp')">YR</abbr></th>
                      <th class="px-3 py-2.5 text-center">{{ t('transcript.credit') }}</th>
                    </tr>
                  </thead>
                  <tbody>
                    @for (subject of record.subjects; track subject.subjectCode) {
                      <tr class="border-t border-[#ece6ef]">
                        <td class="px-4 py-2.5 text-[#32124f]">
                          <span class="block font-semibold">{{ subject.subject }}</span>
                        </td>
                        <td class="px-3 py-2.5 text-center">{{ score(subject.semester1Final) }}</td>
                        <td class="px-3 py-2.5 text-center">{{ score(subject.semester2Final) }}</td>
                        <td class="bg-[#fcf8fd] px-3 py-2.5 text-center font-bold text-[#32124f]">{{ score(subject.yearAverage) }}</td>
                        <td class="px-3 py-2.5 text-center">{{ formatCredit(subject.credit) }}</td>
                      </tr>
                      @if (subject.classScores.length) {
                        <tr class="border-t border-[#ece6ef] bg-[#faf8fb]">
                          <td colspan="5" class="px-4 py-2.5">
                            <details>
                              <summary class="cursor-pointer select-none text-xs font-semibold text-[#6d3a91] marker:text-[#8e69a6]">
                                {{ classBreakdownLabel(subject.classScores.length) }}
                              </summary>
                              <div class="mt-3 overflow-x-auto rounded-lg border border-[#ded6e3] bg-white">
                                <table class="w-full min-w-[620px] border-collapse text-xs">
                                  <thead class="bg-[#f1eaf4] text-[#4a3158]"><tr>
                                    <th class="px-3 py-2 text-left">{{ t('transcript.class') }}</th>
                                    <th class="px-3 py-2 text-left">{{ t('transcript.course') }}</th>
                                    <th class="px-3 py-2 text-center">{{ t('transcript.semester1') }}</th>
                                    <th class="px-3 py-2 text-center">{{ t('transcript.semester2') }}</th>
                                    <th class="px-3 py-2 text-center">{{ t('transcript.yearResult') }}</th>
                                  </tr></thead>
                                  <tbody>
                                    @for (classScore of subject.classScores; track classScore.canvasCourseId) {
                                      <tr class="border-t border-[#ece6ef]">
                                        <td class="px-3 py-2 font-semibold text-[#32124f]">{{ classScore.className || '—' }}</td>
                                        <td class="px-3 py-2 text-[#65596d]">{{ classScore.courseName }}</td>
                                        <td class="px-3 py-2 text-center">{{ score(classScore.semester1Final) }}</td>
                                        <td class="px-3 py-2 text-center">{{ score(classScore.semester2Final) }}</td>
                                        <td class="px-3 py-2 text-center font-semibold">{{ score(classScore.yearAverage) }}</td>
                                      </tr>
                                    }
                                  </tbody>
                                </table>
                              </div>
                            </details>
                          </td>
                        </tr>
                      }
                    }
                  </tbody>
                </table>
              </div>
              @if (record.totalSubjectPages > 1) {
                <footer class="flex flex-wrap items-center justify-between gap-3 border-t border-[#e8e1eb] px-4 py-3 text-sm text-[#746a7b]">
                  <span>{{ subjectPageLabel(record) }}</span>
                  <div class="flex gap-2">
                    <button type="button" class="rounded-md border border-[#d8cede] px-4 py-2 font-semibold text-[#32124f] disabled:opacity-40"
                      [disabled]="record.subjectPage <= 1 || loading()" (click)="load(record.page, record.subjectPage - 1)">
                      {{ t('pagination.previous') }}
                    </button>
                    <button type="button" class="rounded-md border border-[#d8cede] px-4 py-2 font-semibold text-[#32124f] disabled:opacity-40"
                      [disabled]="record.subjectPage >= record.totalSubjectPages || loading()" (click)="load(record.page, record.subjectPage + 1)">
                      {{ t('pagination.next') }}
                    </button>
                  </div>
                </footer>
              }
            </article>
          </div>
        </div>

        <div class="mt-6 overflow-hidden rounded-xl border border-[#e2dbe6] bg-white">
          <div class="flex flex-col gap-4 border-b border-[#e8e1eb] px-5 py-5 lg:flex-row lg:items-end lg:justify-between lg:px-8">
            <div>
              <h3 class="text-xl font-semibold text-[#32124f]">{{ t('transcript.canvasRecords') }}</h3>
              <p class="mt-1 text-sm text-[#756b7c]">{{ t('transcript.canvasRecordsHelp') }}</p>
            </div>
            <div>
              <label class="text-xs font-bold uppercase tracking-[0.1em] text-[#74677c]">
                {{ t('transcript.subject') }}
                <select class="mt-1 block min-w-48 rounded-md border border-[#d8cede] bg-white px-3 py-2.5 text-sm font-medium normal-case tracking-normal text-[#32124f]" [value]="selectedSubject()" (change)="changeSubject($event)">
                  <option value="">{{ t('transcript.all') }}</option>
                  @for (subject of record.subjectOptions; track subject.subjectCode) {
                    <option [value]="subject.subjectCode">{{ subject.subject }}</option>
                  }
                </select>
              </label>
            </div>
          </div>

          <div class="overflow-x-auto">
            <table class="min-w-[1320px] w-full border-collapse text-sm">
              <thead class="bg-[#faf8fb] text-left text-[0.68rem] uppercase tracking-[0.1em] text-[#756a7c]">
                <tr>
                  <th class="px-5 py-3 lg:px-8">{{ t('transcript.student') }}</th>
                  <th class="px-4 py-3">{{ t('transcript.course') }}</th>
                  <th class="px-4 py-3">{{ t('transcript.enrollmentTerm') }}</th>
                  <th class="px-4 py-3">{{ t('transcript.scoreScope') }}</th>
                  <th class="px-4 py-3">{{ t('transcript.gradingPeriod') }}</th>
                  <th class="px-4 py-3 text-right">{{ t('transcript.score') }}</th>
                  <th class="px-4 py-3">{{ t('transcript.scoreSource') }}</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-[#ece6ef]" [class.opacity-50]="loading()">
                @for (item of record.records; track item.warehouseScoreFactId) {
                  <tr class="hover:bg-[#fcfafc]">
                    <td class="px-5 py-4 lg:px-8">
                      <a [routerLink]="['/transcripts/student', item.studentProfileId]" class="font-semibold text-[#32124f] hover:underline">{{ item.studentName }}</a>
                      <p class="mt-0.5 text-xs text-[#8a808f]">{{ item.sisUserId }} · {{ t('transcript.canvasId') }} {{ item.canvasUserId }}</p>
                    </td>
                    <td class="px-4 py-4 text-[#4b3d53]">
                      <span class="block font-medium">{{ item.courseName }}</span>
                      <span class="mt-0.5 block text-xs text-[#8a808f]">{{ item.courseCode || '—' }} · {{ t('transcript.canvasId') }} {{ item.canvasCourseId }}</span>
                    </td>
                    <td class="px-4 py-4 text-[#6f6475]">
                      <span class="block">{{ item.enrollmentTermName || '—' }}</span>
                      @if (item.enrollmentTermId) { <span class="mt-0.5 block text-xs text-[#8a808f]">{{ t('transcript.canvasId') }} {{ item.enrollmentTermId }}</span> }
                    </td>
                    <td class="px-4 py-4"><span class="rounded-full bg-[#edf5f4] px-2.5 py-1 text-xs font-semibold text-[#286768]">{{ t('transcript.scoreScopes.' + item.scoreScope) }}</span></td>
                    <td class="px-4 py-4 text-[#6f6475]">
                      <span class="block">{{ item.gradingPeriodTitle || '—' }}</span>
                      @if (item.gradingPeriodId) { <span class="mt-0.5 block text-xs text-[#8a808f]">{{ t('transcript.canvasId') }} {{ item.gradingPeriodId }}</span> }
                    </td>
                    <td class="px-4 py-4 text-right text-base font-bold text-[#32124f]">{{ score(item.score) }}</td>
                    <td class="px-4 py-4"><span class="rounded-full bg-[#f1eaf4] px-2.5 py-1 text-xs font-semibold text-[#694078]">{{ t('transcript.scoreSources.' + item.scoreSource) }}</span></td>
                  </tr>
                } @empty {
                  <tr><td colspan="7" class="px-6 py-16 text-center text-[#766d7d]">{{ t('transcript.noDetailRecords') }}</td></tr>
                }
              </tbody>
            </table>
          </div>

          <footer class="flex flex-wrap items-center justify-between gap-3 border-t border-[#e8e1eb] px-5 py-4 text-sm text-[#746a7b] lg:px-8">
            <span>{{ pageLabel(record) }}</span>
            <div class="flex gap-2">
              <button type="button" class="rounded-md border border-[#d8cede] px-4 py-2 font-semibold text-[#32124f] disabled:opacity-40" [disabled]="record.page <= 1 || loading()" (click)="load(record.page - 1)">{{ t('pagination.previous') }}</button>
              <button type="button" class="rounded-md border border-[#d8cede] px-4 py-2 font-semibold text-[#32124f] disabled:opacity-40" [disabled]="record.page >= record.totalPages || loading()" (click)="load(record.page + 1)">{{ t('pagination.next') }}</button>
            </div>
          </footer>
        </div>
      }
    </section>
  `,
})
export class TranscriptGradeDetailComponent {
  private readonly service = inject(TranscriptsService);
  private readonly preferences = inject(TranscriptPreferencesService);
  private request?: Subscription;
  private readonly route = inject(ActivatedRoute);
  readonly languageService = inject(LanguageService);
  readonly academicYear = this.route.snapshot.paramMap.get('academicYear') ?? '';
  readonly gradeLevel = this.route.snapshot.paramMap.get('gradeLevel') ?? '';
  readonly detail = signal<SchoolTranscriptGradeDetail | null>(null);
  readonly loading = signal(true);
  readonly error = signal(false);
  readonly selectedSubject = signal('');
  readonly selectedClass = signal('');
  readonly scoreScale = signal<TranscriptScoreScale>('letter');
  readonly scoreScales: TranscriptScoreScale[] = ['percentage', 'gpa', 'letter'];

  constructor() {
    effect((onCleanup) => {
      const excluded = this.preferences.excludedSubjectCodes();
      untracked(() => {
        if (excluded.includes(this.selectedSubject())) this.selectedSubject.set('');
        this.load(1, 1);
      });
      onCleanup(() => this.request?.unsubscribe());
    });
  }

  load(page: number, subjectPage = this.detail()?.subjectPage ?? 1): void {
    this.request?.unsubscribe();
    this.loading.set(true);
    this.error.set(false);
    this.request = this.service.getGradeDetails(this.academicYear, this.gradeLevel, page, 25,
      this.selectedSubject(), '', subjectPage, 10, this.selectedClass()).subscribe({
      next: (record) => {
        this.detail.set(record);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(true);
        this.loading.set(false);
      },
    });
  }

  changeSubject(event: Event): void {
    this.selectedSubject.set((event.target as HTMLSelectElement).value);
    this.load(1);
  }

  changeClass(event: Event): void {
    this.selectedClass.set((event.target as HTMLSelectElement).value);
    this.selectedSubject.set('');
    this.load(1, 1);
  }

  classBreakdownLabel(count: number): string {
    return this.t('transcript.classBreakdown').replace('{count}', String(count));
  }

  pageLabel(record: SchoolTranscriptGradeDetail): string {
    return this.t('transcript.pageSummary')
      .replace('{page}', String(record.page))
      .replace('{pages}', String(record.totalPages))
      .replace('{total}', String(record.matchingRecords));
  }

  subjectPageLabel(record: SchoolTranscriptGradeDetail): string {
    return this.t('pagination.pageOf')
      .replace('{page}', String(record.subjectPage))
      .replace('{pages}', String(record.totalSubjectPages))
      .replace('{total}', String(record.totalSubjects));
  }

  score(value?: number | null): string {
    return value == null ? this.t('transcript.notAvailable') : formatTranscriptScore(value, this.scoreScale());
  }
  formatGpa(value?: number | null): string { return value == null ? '—' : value.toFixed(2); }
  formatCredit(value: number): string { return Number.isInteger(value) ? value.toFixed(0) : value.toFixed(1); }
  date(value: string): string { return new Intl.DateTimeFormat(this.languageService.language() === 'vi' ? 'vi-VN' : 'en-GB', { dateStyle: 'medium' }).format(new Date(value)); }
  t(key: string): string { return this.languageService.t(key); }
}
