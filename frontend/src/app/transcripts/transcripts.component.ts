import { Component, effect, inject, signal, untracked } from '@angular/core';
import { Subscription } from 'rxjs';
import { RouterLink } from '@angular/router';
import { LanguageService } from '../core/language/language.service';
import { formatTranscriptScore, TranscriptScoreScale } from './grading-scale';
import { SchoolTranscript, SchoolTranscriptGrade, SchoolTranscriptYear, TranscriptClassScore, TranscriptSubject, TranscriptsService } from './transcripts.service';
import { TranscriptPreferencesService } from './transcript-preferences.service';

type TranscriptSemester = 'semester1' | 'semester2';
type TranscriptSemesterFilter = 'all' | TranscriptSemester;

@Component({
  selector: 'app-transcripts',
  imports: [RouterLink],
  template: `
    <section>
      <div class="flex flex-col gap-4 xl:flex-row xl:items-end xl:justify-between">
        <div>
          <p class="text-sm uppercase tracking-[0.2em] text-[#8a6811]">{{ t('transcript.summaryEyebrow') }}</p>
          <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ t('transcript.summaryTitle') }}</h2>
          <p class="mt-2 max-w-3xl text-[#6f6877]">{{ t('transcript.summaryDescription') }}</p>
        </div>
        <div class="inline-flex self-start rounded-lg border border-[#d9d0df] bg-white p-1" role="group" [attr.aria-label]="t('transcript.scoreScale')">
          @for (scale of scoreScales; track scale) {
            <button type="button" class="rounded-md px-3 py-2 text-xs font-semibold transition"
              [class.bg-[#32124f]]="scoreScale() === scale" [class.text-white]="scoreScale() === scale"
              [class.text-[#695d70]]="scoreScale() !== scale" (click)="scoreScale.set(scale)">
              {{ t('transcript.scales.' + scale) }}
            </button>
          }
        </div>
      </div>

      @if (loading()) {
        <div class="mt-8 rounded-xl border border-[#e4dde8] bg-white py-24 text-center text-[#766d7d]">{{ t('transcript.loadingSummary') }}</div>
      } @else if (error()) {
        <div class="mt-8 rounded-xl border border-[#efcaca] bg-[#fff8f7] px-6 py-16 text-center">
          <h3 class="text-lg font-semibold text-[#8b2727]">{{ t('transcript.errorTitle') }}</h3>
          <button type="button" class="mt-5 rounded-md bg-[#32124f] px-5 py-2.5 text-sm font-semibold text-white" (click)="load()">{{ t('transcript.retry') }}</button>
        </div>
      } @else if (transcript(); as record) {
        <div class="mt-8 overflow-hidden rounded-xl border border-[#e2dbe6] bg-white shadow-[0_12px_38px_rgba(50,18,79,0.06)]">
          <div class="grid grid-cols-2 border-b border-[#e8e1eb] xl:grid-cols-4">
            <div class="border-b border-r border-[#e8e1eb] p-5 xl:border-b-0">
              <p class="metric-label">{{ t('transcript.overallAverage') }}</p><p class="metric-value">{{ score(record.overallAverage) }}</p>
            </div>
            <div class="border-b border-[#e8e1eb] p-5 xl:border-b-0 xl:border-r">
              <p class="metric-label">{{ t('transcript.studentsCovered') }}</p><p class="metric-value">{{ record.totalStudents }}</p>
            </div>
            <div class="border-r border-[#e8e1eb] p-5">
              <p class="metric-label">{{ t('transcript.subjects') }}</p><p class="metric-value">{{ record.totalSubjects }}</p>
            </div>
            <div class="p-5">
              <p class="metric-label">{{ t('transcript.academicYears') }}</p><p class="metric-value">{{ record.totalAcademicYears }}</p>
            </div>
          </div>

          @if (record.years.length) {
            <div class="flex flex-wrap gap-4 border-b border-[#e8e1eb] bg-[#faf8fb] px-5 py-4">
              <label class="block min-w-64 text-xs font-bold uppercase tracking-[0.12em] text-[#766a7e]">
                {{ t('transcript.chooseYear') }}
                <select class="mt-2 block w-full rounded-lg border border-[#d8cede] bg-white px-4 py-3 text-sm font-semibold normal-case tracking-normal text-[#32124f]"
                  [value]="selectedYear()" (change)="changeYear($event)">
                  @for (year of orderedYears(record); track year.academicYear) { <option [value]="year.academicYear">{{ year.academicYear }}</option> }
                </select>
              </label>
              <label class="block min-w-56 text-xs font-bold uppercase tracking-[0.12em] text-[#766a7e]">
                {{ t('transcript.semesterFilter') }}
                <select class="mt-2 block w-full rounded-lg border border-[#d8cede] bg-white px-4 py-3 text-sm font-semibold normal-case tracking-normal text-[#32124f]"
                  [value]="selectedSemester()" (change)="changeSemester($event)">
                  <option value="all">{{ t('transcript.allSemesters') }}</option>
                  <option value="semester1">{{ t('transcript.semester1') }}</option>
                  <option value="semester2">{{ t('transcript.semester2') }}</option>
                </select>
              </label>
              <label class="block min-w-48 text-xs font-bold uppercase tracking-[0.12em] text-[#766a7e]">
                {{ t('transcript.gradeFilter') }}
                <select class="mt-2 block w-full rounded-lg border border-[#d8cede] bg-white px-4 py-3 text-sm font-semibold normal-case tracking-normal text-[#32124f]"
                  [value]="selectedGrade()" (change)="changeGrade($event)">
                  <option value="">{{ t('transcript.allGrades') }}</option>
                  @if (activeYear(record); as selectedYear) {
                    @for (grade of selectedYear.grades; track grade.gradeLevel) {
                      <option [value]="grade.gradeLevel">{{ t('transcript.grade') }} {{ grade.gradeLevel }}</option>
                    }
                  }
                </select>
              </label>
              <label class="block min-w-48 text-xs font-bold uppercase tracking-[0.12em] text-[#766a7e]">
                {{ t('transcript.classFilter') }}
                <select class="mt-2 block w-full rounded-lg border border-[#d8cede] bg-white px-4 py-3 text-sm font-semibold normal-case tracking-normal text-[#32124f]"
                  [value]="selectedClass()" (change)="changeClass($event)">
                  <option value="">{{ t('transcript.allClasses') }}</option>
                  @if (activeYear(record); as selectedYear) {
                    @for (className of classOptions(selectedYear); track className) {
                      <option [value]="className">{{ className }}</option>
                    }
                  }
                </select>
              </label>
            </div>
            @if (activeYear(record); as year) {
              <div class="p-5 lg:p-7">
                <div class="mb-5 flex items-center justify-between gap-4 rounded-lg border border-[#d8cede] bg-[#f1eaf4] px-5 py-3 text-[#32124f]">
                  <h3 class="text-lg font-bold">{{ t('transcript.schoolYear') }}: {{ year.academicYear }}</h3>
                  <div class="rounded-lg bg-[#f5eef8] px-4 py-3 text-right"><p class="metric-label">{{ t('transcript.yearAverage') }}</p><p class="mt-1 text-xl font-semibold text-[#32124f]">{{ score(year.averageScore) }}</p></div>
                </div>
                <p class="mb-5 text-sm text-[#756b7c]">{{ t('transcript.subjectAggregateNote') }}</p>
                <div class="space-y-5">
                  @for (grade of displayedGrades(year); track grade.gradeLevel) {
                    <article class="overflow-hidden rounded-xl border border-[#ded6e3]">
                      <header class="flex items-center justify-between gap-3 bg-[#32124f] px-4 py-3 text-white">
                        <h4 class="text-lg font-semibold">{{ t('transcript.grade') }} {{ grade.gradeLevel }}</h4>
                        <span class="text-sm font-bold">GPA {{ formatGpa(grade.gpa) }}</span>
                      </header>
                      @for (semester of visibleSemesters(); track semester) {
                        <section [attr.aria-label]="semesterLabel(semester)">
                          <h5 class="border-b border-[#d8cede] bg-[#eee5f2] px-4 py-2.5 text-sm font-bold text-[#4a3158]">{{ t('transcript.term') }}: {{ semesterLabel(semester) }}</h5>
                          <div class="overflow-x-auto">
                            <table class="w-full min-w-[620px] border-collapse text-sm">
                              <thead class="bg-[#51236f] text-white"><tr>
                                <th class="w-16 px-3 py-2.5 text-center">{{ t('transcript.subjectNumber') }}</th>
                                <th class="px-3 py-2.5 text-left">{{ t('transcript.subject') }}</th>
                                <th class="w-28 px-3 py-2.5 text-center">{{ t('transcript.semesterScore') }}</th>
                                <th class="w-28 px-3 py-2.5 text-center">{{ t('transcript.yearResult') }}</th>
                                <th class="w-24 px-3 py-2.5 text-center">{{ t('transcript.credit') }}</th>
                              </tr></thead>
                              <tbody>
                                @for (subject of subjectsForSemester(grade, semester); track subject.subjectCode; let index = $index) {
                                  <tr class="border-t border-[#e5e7eb] even:bg-[#fbfafc]">
                                    <td class="px-3 py-2.5 text-center text-[#5f5565]">{{ subjectNumber(grade, index) }}</td>
                                    <td class="px-3 py-2.5 font-semibold text-[#32124f]">{{ subject.subject }}</td>
                                    <td class="px-3 py-2.5 text-center font-semibold">{{ score(displaySemesterScore(subject, semester)) }}</td>
                                    <td class="bg-[#fcf8fd] px-3 py-2.5 text-center font-bold">{{ score(displayYearScore(subject)) }}</td>
                                    <td class="px-3 py-2.5 text-center">{{ credit(displayCredit(subject)) }}</td>
                                  </tr>
                                  @if (classScoresForSemester(subject, semester).length) {
                                    <tr class="border-t border-[#ece6ef] bg-[#faf8fb]">
                                      <td colspan="5" class="px-4 py-2.5">
                                        <details class="group">
                                          <summary class="cursor-pointer select-none text-xs font-semibold text-[#6d3a91] marker:text-[#8e69a6]">
                                            {{ classBreakdownLabel(classScoresForSemester(subject, semester).length) }}
                                          </summary>
                                          <div class="mt-3 overflow-hidden rounded-lg border border-[#ded6e3] bg-white">
                                            <table class="w-full min-w-[540px] border-collapse text-xs">
                                              <thead class="bg-[#f1eaf4] text-[#4a3158]"><tr>
                                                <th class="px-3 py-2 text-left">{{ t('transcript.class') }}</th>
                                                <th class="px-3 py-2 text-left">{{ t('transcript.course') }}</th>
                                                <th class="px-3 py-2 text-center">{{ t('transcript.semesterScore') }}</th>
                                                <th class="px-3 py-2 text-center">{{ t('transcript.yearResult') }}</th>
                                              </tr></thead>
                                              <tbody>
                                                @for (classScore of classScoresForSemester(subject, semester); track classScore.canvasCourseId) {
                                                  <tr class="border-t border-[#ece6ef]">
                                                    <td class="px-3 py-2 font-semibold text-[#32124f]">{{ classScore.className || '—' }}</td>
                                                    <td class="px-3 py-2 text-[#65596d]">{{ classScore.courseName }}</td>
                                                    <td class="px-3 py-2 text-center font-semibold">{{ score(classSemesterScore(classScore, semester)) }}</td>
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
                                } @empty {
                                  <tr><td colspan="5" class="px-5 py-8 text-center text-[#766d7d]">{{ t('transcript.noSemesterResults') }}</td></tr>
                                }
                              </tbody>
                            </table>
                          </div>
                        </section>
                      }
                      <footer class="flex flex-wrap items-center justify-between gap-3 border-t border-[#e8e1eb] px-4 py-3">
                        @if (grade.totalSubjectPages > 1) {
                          <div class="flex items-center gap-2 text-xs text-[#746a7b]">
                            <span>{{ subjectPageLabel(grade) }}</span>
                            <button type="button" class="rounded-md border border-[#d8cede] px-2.5 py-1.5 font-semibold text-[#32124f] disabled:opacity-40"
                              [disabled]="grade.subjectPage <= 1 || subjectPageLoading(year.academicYear, grade.gradeLevel)"
                              (click)="loadSubjectPage(year.academicYear, grade.gradeLevel, grade.subjectPage - 1)">
                              {{ t('pagination.previous') }}
                            </button>
                            <button type="button" class="rounded-md border border-[#d8cede] px-2.5 py-1.5 font-semibold text-[#32124f] disabled:opacity-40"
                              [disabled]="grade.subjectPage >= grade.totalSubjectPages || subjectPageLoading(year.academicYear, grade.gradeLevel)"
                              (click)="loadSubjectPage(year.academicYear, grade.gradeLevel, grade.subjectPage + 1)">
                              {{ t('pagination.next') }}
                            </button>
                          </div>
                        } @else {
                          <span></span>
                        }
                        <a [routerLink]="['/transcripts', year.academicYear, 'grades', grade.gradeLevel]" class="text-sm font-semibold text-[#6d3a91] hover:underline">{{ t('transcript.viewGradeDetails') }} →</a>
                      </footer>
                    </article>
                  }
                </div>
              </div>
            }
          } @else {
            <div class="px-6 py-12 lg:px-8">
              <p class="text-center text-[#766d7d]">{{ t('transcript.noCanvasDataOverall') }}</p>
            </div>
          }
        </div>
      }
    </section>
  `,
  styles: `
    .metric-label { font-size: .68rem; font-weight: 700; letter-spacing: .1em; text-transform: uppercase; color: #817589; }
    .metric-value { margin-top: .45rem; font-size: 1.75rem; font-weight: 600; color: #32124f; }
  `,
})
export class TranscriptsComponent {
  private readonly service = inject(TranscriptsService);
  private readonly preferences = inject(TranscriptPreferencesService);
  private overallRequest?: Subscription;
  private subjectPageRequest?: Subscription;
  readonly languageService = inject(LanguageService);
  readonly transcript = signal<SchoolTranscript | null>(null);
  readonly selectedYear = signal('');
  readonly selectedSemester = signal<TranscriptSemesterFilter>('all');
  readonly selectedGrade = signal('');
  readonly selectedClass = signal('');
  readonly loading = signal(true);
  readonly error = signal(false);
  readonly loadingSubjectPage = signal('');
  readonly scoreScale = signal<TranscriptScoreScale>('letter');
  readonly scoreScales: TranscriptScoreScale[] = ['percentage', 'gpa', 'letter'];

  constructor() {
    effect((onCleanup) => {
      this.preferences.excludedSubjectCodes();
      untracked(() => this.load());
      onCleanup(() => {
        this.overallRequest?.unsubscribe();
        this.subjectPageRequest?.unsubscribe();
      });
    });
  }
  load(): void {
    this.overallRequest?.unsubscribe();
    this.subjectPageRequest?.unsubscribe();
    this.loadingSubjectPage.set('');
    this.loading.set(true);
    this.error.set(false);
    this.overallRequest = this.service.getOverall(undefined, 1).subscribe({
      next: (record) => {
        this.transcript.set(record);
        if (!record.years.some((year) => year.academicYear === this.selectedYear()))
          this.selectedYear.set(this.latestYear(record)?.academicYear ?? '');
        this.loading.set(false);
      },
      error: () => { this.loading.set(false); this.error.set(true); },
    });
  }
  activeYear(record: SchoolTranscript): SchoolTranscriptYear | undefined {
    return record.years.find((year) => year.academicYear === this.selectedYear()) ?? this.latestYear(record);
  }
  orderedYears(record: SchoolTranscript): SchoolTranscriptYear[] {
    return [...record.years].sort((left, right) => this.startYear(right.academicYear) - this.startYear(left.academicYear));
  }
  private latestYear(record: SchoolTranscript): SchoolTranscriptYear | undefined {
    return this.orderedYears(record)[0];
  }
  private startYear(value: string): number {
    return Number(value.match(/\d{4}/)?.[0] ?? 0);
  }
  changeYear(event: Event): void {
    this.selectedYear.set((event.target as HTMLSelectElement).value);
    this.selectedGrade.set('');
    this.selectedClass.set('');
  }
  changeSemester(event: Event): void {
    this.selectedSemester.set((event.target as HTMLSelectElement).value as TranscriptSemesterFilter);
  }
  changeGrade(event: Event): void {
    this.selectedGrade.set((event.target as HTMLSelectElement).value);
    this.selectedClass.set('');
  }
  changeClass(event: Event): void {
    this.selectedClass.set((event.target as HTMLSelectElement).value);
  }
  displayedGrades(year: SchoolTranscriptYear): SchoolTranscriptGrade[] {
    const selectedGrade = this.selectedGrade();
    const selectedClass = this.selectedClass();
    return year.grades.filter((grade) =>
      (!selectedGrade || grade.gradeLevel === selectedGrade) &&
      (!selectedClass || grade.classOptions.includes(selectedClass)));
  }
  classOptions(year: SchoolTranscriptYear): string[] {
    const grades = this.selectedGrade()
      ? year.grades.filter((grade) => grade.gradeLevel === this.selectedGrade())
      : year.grades;
    return [...new Set(grades.flatMap((grade) => grade.classOptions))]
      .sort((left, right) => left.localeCompare(right, undefined, { numeric: true }));
  }
  visibleSemesters(): TranscriptSemester[] {
    const selected = this.selectedSemester();
    return selected === 'all' ? ['semester1', 'semester2'] : [selected];
  }
  semesterLabel(semester: TranscriptSemester): string {
    return this.t(`transcript.${semester}`);
  }
  classSemesterScore(classScore: TranscriptClassScore, semester: TranscriptSemester): number | null | undefined {
    return semester === 'semester1' ? classScore.semester1Final : classScore.semester2Final;
  }
  semesterScore(subject: TranscriptSubject, semester: TranscriptSemester): number | null | undefined {
    return semester === 'semester1' ? subject.semester1Final : subject.semester2Final;
  }
  subjectsForSemester(grade: SchoolTranscriptGrade, semester: TranscriptSemester): TranscriptSubject[] {
    return grade.subjects.filter((subject) =>
      this.displaySemesterScore(subject, semester) != null ||
      this.classScoresForSemester(subject, semester).length > 0);
  }
  classScoresForSemester(subject: TranscriptSubject, semester: TranscriptSemester): TranscriptClassScore[] {
    const selected = this.selectedClass();
    return subject.classScores.filter((classScore) => {
      if (selected && classScore.className !== selected) return false;
      const hasAnyResult = classScore.semester1Final != null || classScore.semester2Final != null ||
        classScore.yearAverage != null;
      return this.classSemesterScore(classScore, semester) != null || !hasAnyResult;
    });
  }
  displaySemesterScore(subject: TranscriptSubject, semester: TranscriptSemester): number | null | undefined {
    if (!this.selectedClass()) return this.semesterScore(subject, semester);
    return this.weightedClassScore(this.classScoresForSemester(subject, semester), (item) =>
      this.classSemesterScore(item, semester));
  }
  displayYearScore(subject: TranscriptSubject): number | null | undefined {
    if (!this.selectedClass()) return subject.yearAverage;
    return this.weightedClassScore(subject.classScores.filter((item) =>
      item.className === this.selectedClass()), (item) => item.yearAverage);
  }
  displayCredit(subject: TranscriptSubject): number {
    if (!this.selectedClass()) return subject.credit;
    const matches = subject.classScores.filter((item) => item.className === this.selectedClass());
    return matches.length ? Math.max(...matches.map((item) => item.credit)) : 0;
  }
  private weightedClassScore(
    items: TranscriptClassScore[],
    select: (item: TranscriptClassScore) => number | null | undefined,
  ): number | null {
    const available = items.map((item) => ({ value: select(item), weight: item.recordCount }))
      .filter((item): item is { value: number; weight: number } => item.value != null);
    const weight = available.reduce((total, item) => total + item.weight, 0);
    return weight ? available.reduce((total, item) => total + item.value * item.weight, 0) / weight : null;
  }
  classBreakdownLabel(count: number): string {
    return this.t('transcript.classBreakdown').replace('{count}', String(count));
  }
  subjectNumber(grade: SchoolTranscriptGrade, index: number): number {
    return (grade.subjectPage - 1) * grade.subjectPageSize + index + 1;
  }
  loadSubjectPage(academicYear: string, gradeLevel: string, page: number): void {
    this.subjectPageRequest?.unsubscribe();
    const key = this.subjectPageKey(academicYear, gradeLevel);
    this.loadingSubjectPage.set(key);
    this.subjectPageRequest = this.service.getOverall(undefined, page).subscribe({
      next: (response) => {
        const updatedGrade = response.years
          .find((year) => year.academicYear === academicYear)?.grades
          .find((grade) => grade.gradeLevel === gradeLevel);
        if (updatedGrade) {
          this.transcript.update((record) => record ? {
            ...record,
            years: record.years.map((year) => year.academicYear !== academicYear ? year : {
              ...year,
              grades: year.grades.map((grade) => grade.gradeLevel === gradeLevel ? updatedGrade : grade),
            }),
          } : record);
        }
        this.loadingSubjectPage.set('');
      },
      error: () => this.loadingSubjectPage.set(''),
    });
  }
  subjectPageLoading(academicYear: string, gradeLevel: string): boolean {
    return this.loadingSubjectPage() === this.subjectPageKey(academicYear, gradeLevel);
  }
  subjectPageLabel(grade: SchoolTranscriptYear['grades'][number]): string {
    return this.t('pagination.pageOf')
      .replace('{page}', String(grade.subjectPage))
      .replace('{pages}', String(grade.totalSubjectPages))
      .replace('{total}', String(grade.totalSubjects));
  }
  private subjectPageKey(academicYear: string, gradeLevel: string): string {
    return `${academicYear}\u0000${gradeLevel}`;
  }
  score(value?: number | null): string {
    return value == null ? this.t('transcript.notAvailable') : formatTranscriptScore(value, this.scoreScale());
  }
  formatGpa(value?: number | null): string { return value == null ? '—' : value.toFixed(2); }
  credit(value: number): string { return Number.isInteger(value) ? value.toFixed(0) : value.toFixed(1); }
  t(key: string): string { return this.languageService.t(key); }
}
