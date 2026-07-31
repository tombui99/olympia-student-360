import { Component, inject, signal } from '@angular/core';
import { catchError, debounceTime, distinctUntilChanged, of, Subject, switchMap } from 'rxjs';
import { LanguageService } from '../core/language/language.service';
import { formatTranscriptScore, TranscriptScoreScale } from './grading-scale';
import { SchoolTranscript, TranscriptStudent, TranscriptsService } from './transcripts.service';

@Component({
  selector: 'app-student-transcript',
  template: `
    <section class="mx-auto max-w-[1500px]">
      <div class="no-print flex flex-col gap-5 xl:flex-row xl:items-end xl:justify-between">
        <div>
          <p class="text-sm uppercase tracking-[0.2em] text-[#8a6811]">{{ t('transcript.eyebrow') }}</p>
          <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ t('transcript.title') }}</h2>
          <p class="mt-2 max-w-3xl text-[#6f6877]">{{ t('transcript.overallDescription') }}</p>
        </div>
        <div class="flex flex-wrap items-end gap-3">
          <div class="relative min-w-72">
            <label for="transcript-student-search" class="text-xs font-bold uppercase tracking-[0.1em] text-[#74677c]">
            {{ t('transcript.chooseStudent') }}
            </label>
            <div class="relative mt-1">
              <span class="pointer-events-none absolute inset-y-0 left-3.5 flex items-center text-xl font-bold leading-none text-[#6d3a91]" aria-hidden="true">⌕</span>
              <input
                id="transcript-student-search"
                type="search"
                autocomplete="off"
                class="block w-full rounded-lg border border-[#d8cede] bg-white py-3 pl-11 pr-4 text-sm font-semibold text-[#32124f] outline-none transition focus:border-[#6d3a91] focus:ring-2 focus:ring-[#6d3a91]/15"
                [placeholder]="t('transcript.searchStudentPlaceholder')"
                [value]="searchTerm()"
                [attr.aria-expanded]="searchOpen()"
                aria-controls="transcript-student-results"
                (input)="searchStudents($event)"
                (focus)="openSearch()"
                (blur)="closeSearch()"
                (keydown)="handleSearchKeydown($event)" />
            </div>
            @if (searchOpen()) {
              <div id="transcript-student-results" role="listbox"
                class="absolute z-30 mt-2 max-h-72 w-full overflow-y-auto rounded-lg border border-[#d8cede] bg-white p-1 shadow-[0_14px_35px_rgba(50,18,79,0.16)]">
                @if (searching()) {
                  <p class="px-4 py-4 text-sm text-[#766d7d]">{{ t('transcript.searchingStudents') }}</p>
                } @else {
                  @for (student of students(); track student.id; let index = $index) {
                    <button type="button" role="option"
                      class="flex w-full items-center justify-between gap-3 rounded-md px-3 py-2.5 text-left hover:bg-[#f4eef7]"
                      [class.bg-[#f4eef7]]="activeResult() === index"
                      [attr.aria-selected]="selectedStudentId() === student.id"
                      (mousedown)="$event.preventDefault()"
                      (click)="selectStudent(student)">
                      <span class="min-w-0">
                        <span class="block truncate text-sm font-semibold text-[#32124f]">{{ student.fullName }}</span>
                        <span class="mt-0.5 block text-xs text-[#817589]">{{ student.studentCode }} · {{ student.homeroom || (t('transcript.grade') + ' ' + (student.gradeLevel || '—')) }}</span>
                      </span>
                      <span class="shrink-0 text-xs font-semibold text-[#8a6811]">{{ t('transcript.select') }}</span>
                    </button>
                  } @empty {
                    <p class="px-4 py-4 text-sm text-[#766d7d]">
                      {{ searchTerm().trim().length < 2 ? t('transcript.searchStudentHint') : t('transcript.noStudentMatches') }}
                    </p>
                  }
                }
              </div>
            }
          </div>
          @if (transcript(); as current) {
            @if (current.years.length) {
              <details class="relative">
                <summary class="list-none cursor-pointer rounded-lg border border-[#d8cede] bg-white px-4 py-3 text-sm font-semibold text-[#32124f]">
                  {{ t('transcript.academicYearFilter') }} · {{ selectedYears().length }}
                  <span class="ml-2 text-[#8a6811]" aria-hidden="true">⌄</span>
                </summary>
                <div class="absolute right-0 z-20 mt-2 min-w-56 rounded-lg border border-[#d8cede] bg-white p-2 shadow-[0_14px_35px_rgba(50,18,79,0.16)]">
                  <p class="px-2 pb-2 text-xs text-[#817589]">{{ t('transcript.academicYearFilterHelp') }}</p>
                  @for (year of current.years; track year.academicYear) {
                    <label class="flex cursor-pointer items-center gap-3 rounded-md px-2 py-2 text-sm font-semibold text-[#4e4056] hover:bg-[#f4eef7]">
                      <input type="checkbox" class="h-4 w-4 accent-[#6d3a91]"
                        [checked]="selectedYears().includes(year.academicYear)"
                        (change)="toggleYear(year.academicYear)" />
                      {{ year.academicYear }}
                    </label>
                  }
                </div>
              </details>
            }
            <div class="inline-flex rounded-lg border border-[#d8cede] bg-white p-1" role="group" [attr.aria-label]="t('transcript.scoreScale')">
              @for (scale of scoreScales; track scale) {
                <button type="button"
                  class="rounded-md px-3 py-2 text-xs font-semibold transition sm:text-sm"
                  [class.bg-[#32124f]]="scoreScale() === scale"
                  [class.text-white]="scoreScale() === scale"
                  [class.text-[#695d70]]="scoreScale() !== scale"
                  [class.hover:bg-[#f3edf6]]="scoreScale() !== scale"
                  (click)="scoreScale.set(scale)">
                  {{ t('transcript.scales.' + scale) }}
                </button>
              }
            </div>
          }
          <button
            type="button"
            class="inline-flex items-center gap-2 rounded-lg bg-[#32124f] px-5 py-3 text-sm font-semibold text-white shadow-sm transition hover:bg-[#51236f] disabled:cursor-not-allowed disabled:opacity-50"
            [disabled]="!transcript() || !transcript()?.years?.length"
            (click)="exportPdf()">
            <span aria-hidden="true">↧</span> {{ t('transcript.exportPdf') }}
          </button>
        </div>
      </div>

      @if (loading()) {
        <div class="no-print mt-8 rounded-xl border border-[#e4dde8] bg-white py-24 text-center text-[#766d7d]">{{ t('transcript.loadingOverall') }}</div>
      } @else if (error()) {
        <div class="no-print mt-8 rounded-xl border border-[#efcaca] bg-[#fff8f7] px-6 py-16 text-center">
          <h3 class="text-lg font-semibold text-[#8b2727]">{{ t('transcript.errorTitle') }}</h3>
          <p class="mx-auto mt-2 max-w-xl text-sm text-[#765c5c]">{{ t('transcript.errorBody') }}</p>
          <button type="button" class="mt-5 rounded-md bg-[#32124f] px-5 py-2.5 text-sm font-semibold text-white" (click)="reload()">{{ t('transcript.retry') }}</button>
        </div>
      } @else if (transcript(); as record) {
        <article class="print-region transcript-sheet mt-8 overflow-hidden rounded-xl border border-[#ded6e3] bg-white shadow-[0_12px_38px_rgba(50,18,79,0.07)]">
          <header class="transcript-header border-b border-[#d9cfdf] px-6 py-6 lg:px-9">
            <div class="flex items-center gap-5">
              <img src="/assets/olympia_logo.png" alt="The Olympia Schools" class="h-20 w-20 object-contain" />
              <div class="min-w-0 flex-1">
                <h1 class="text-2xl font-bold text-[#64266f] lg:text-3xl">The Olympia Schools</h1>
                <p class="mt-1 text-sm font-semibold uppercase tracking-[0.24em] text-[#756b7c]">{{ t('transcript.documentTitle') }}</p>
              </div>
              <div class="hidden text-right text-xs text-[#756b7c] sm:block">
                <p>{{ t('transcript.officialRecord') }}</p>
                <p class="mt-1">{{ generatedDate() }}</p>
              </div>
            </div>

            <div class="mt-5 grid gap-x-8 gap-y-3 border-t border-[#e8e1eb] pt-4 text-sm sm:grid-cols-2 lg:grid-cols-3">
              <div><span class="meta-label">{{ t('transcript.student') }}</span><strong>{{ record.studentName || '—' }}</strong></div>
              <div><span class="meta-label">{{ t('transcript.studentCode') }}</span><strong>{{ record.studentCode || '—' }}</strong></div>
              <div><span class="meta-label">{{ t('transcript.dateOfBirth') }}</span><strong>{{ dateOfBirth(record.dateOfBirth) }}</strong></div>
              <div><span class="meta-label">{{ t('transcript.gender') }}</span><strong>{{ record.gender || '—' }}</strong></div>
              <div><span class="meta-label">{{ t('transcript.currentGrade') }}</span><strong>{{ record.gradeLevel || '—' }}</strong></div>
              <div><span class="meta-label">{{ t('transcript.homeroom') }}</span><strong>{{ record.homeroom || '—' }}</strong></div>
            </div>
          </header>

          @if (record.years.length) {
            <div class="grade-grid grid items-start gap-7 p-6 lg:grid-cols-2 lg:p-9">
              @for (year of displayedYears(record); track year.academicYear) {
                @for (grade of year.grades; track grade.gradeLevel) {
                  <section class="grade-block break-inside-avoid">
                    <div class="mb-2 flex items-end justify-between gap-3">
                      <h2 class="text-base font-bold text-[#24162b]">
                        {{ gradeLabel(grade.gradeLevel) }} {{ year.academicYear }}
                      </h2>
                      <p class="text-sm font-bold text-[#32124f]">
                        GPA <span class="ml-2">{{ formatGpa(grade.gpa) }}</span>
                      </p>
                    </div>
                    <div class="overflow-hidden rounded-md border border-[#cfc4d5]">
                      <table class="w-full table-fixed border-collapse text-[0.78rem]">
                        <thead class="bg-[#eee7f1] text-[#382541]">
                          <tr>
                            <th class="w-[52%] px-3 py-2 text-left">{{ t('transcript.subject') }}</th>
                            <th class="w-[12%] px-1 py-2 text-center">S1F</th>
                            <th class="w-[12%] px-1 py-2 text-center">S2F</th>
                            <th class="w-[12%] px-1 py-2 text-center">YR</th>
                            <th class="w-[12%] px-1 py-2 text-center">{{ t('transcript.credit') }}</th>
                          </tr>
                        </thead>
                        <tbody>
                          @for (subject of grade.subjects; track subject.subject) {
                            <tr class="border-t border-[#e7e0ea]">
                              <td class="px-3 py-1.5 font-medium text-[#35293b]">{{ subject.subject }}</td>
                              <td class="px-1 py-1.5 text-center">{{ score(subject.semester1Final) }}</td>
                              <td class="px-1 py-1.5 text-center">{{ score(subject.semester2Final) }}</td>
                              <td class="bg-[#fbf8fc] px-1 py-1.5 text-center font-semibold">{{ score(subject.yearAverage) }}</td>
                              <td class="px-1 py-1.5 text-center">{{ formatCredit(subject.credit) }}</td>
                            </tr>
                          }
                        </tbody>
                      </table>
                    </div>
                  </section>
                }
              }
            </div>

            <footer class="formal-footer mx-6 mb-6 border-t border-[#bfb2c7] pt-4 lg:mx-9 lg:mb-8">
              <div class="grid gap-6 text-sm sm:grid-cols-2">
                <div>
                  <p class="font-bold text-[#32124f]">{{ t('transcript.academicStanding') }}</p>
                  <p class="mt-2">{{ t('transcript.cumulativeGpa') }} <strong class="ml-3">{{ formatGpa(record.overallGpa) }}</strong></p>
                </div>
                <div>
                  <p class="font-bold text-[#32124f]">{{ t('transcript.gradingScale') }}</p>
                  <p class="mt-2 text-xs leading-5 text-[#63586a]">A+ 4.2 · A 4.0 · A- 3.7 · B+ 3.3 · B 3.0 · B- 2.7 · C+ 2.3 · C 2.0 · C- 1.7 · D+ 1.3 · D 1.0 · D- 0.7</p>
                </div>
              </div>
              <div class="mt-8 grid grid-cols-2 gap-12 text-xs">
                <div class="border-t border-[#8e8295] pt-2">{{ t('transcript.schoolOfficer') }}</div>
                <div class="border-t border-[#8e8295] pt-2">{{ t('transcript.signatureDate') }}</div>
              </div>
              <div class="mt-6 flex items-end justify-between gap-4 border-t border-[#e8e1eb] pt-3 text-[0.68rem] text-[#756b7c]">
                <p>The Olympia Schools · Trung Van, Nam Tu Liem, Hanoi, Vietnam</p>
                <p class="font-semibold">Official Transcript</p>
              </div>
            </footer>
          } @else {
            <div class="px-8 py-20 text-center text-[#766d7d]">{{ t('transcript.noStudentData') }}</div>
          }
        </article>
      } @else {
        <div class="no-print mt-8 rounded-xl border border-dashed border-[#d7cddd] bg-white px-6 py-20 text-center">
          <div class="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-[#f2eaf5] text-2xl text-[#6d3a91]" aria-hidden="true">⌕</div>
          <h3 class="mt-4 text-lg font-semibold text-[#32124f]">{{ t('transcript.emptyStudentTitle') }}</h3>
          <p class="mx-auto mt-2 max-w-lg text-sm text-[#766d7d]">{{ t('transcript.emptyStudentBody') }}</p>
        </div>
      }
    </section>
  `,
  styles: `
    .meta-label { display: block; margin-bottom: .2rem; color: #817589; font-size: .65rem; font-weight: 700; letter-spacing: .09em; text-transform: uppercase; }
    @media print {
      :host { display: block; }
      .transcript-sheet { width: 100%; border: 0 !important; border-radius: 0 !important; box-shadow: none !important; }
      .transcript-header { padding: 0 0 4mm !important; }
      .grade-grid { grid-template-columns: repeat(2, minmax(0, 1fr)) !important; gap: 7mm !important; padding: 6mm 0 !important; }
      .grade-block { break-inside: avoid; page-break-inside: avoid; }
      .grade-block table { font-size: 7.4pt !important; }
      .grade-block th { padding-top: 1.5mm !important; padding-bottom: 1.5mm !important; }
      .grade-block td { padding-top: 1mm !important; padding-bottom: 1mm !important; }
      .formal-footer { margin: 2mm 0 0 !important; }
    }
  `,
})
export class StudentTranscriptComponent {
  private readonly service = inject(TranscriptsService);
  private readonly searchTerms = new Subject<string>();
  readonly languageService = inject(LanguageService);
  readonly transcript = signal<SchoolTranscript | null>(null);
  readonly students = signal<TranscriptStudent[]>([]);
  readonly selectedStudentId = signal<number | null>(null);
  readonly selectedYears = signal<string[]>([]);
  readonly scoreScale = signal<TranscriptScoreScale>('letter');
  readonly scoreScales: TranscriptScoreScale[] = ['percentage', 'gpa', 'letter'];
  readonly searchTerm = signal('');
  readonly searchOpen = signal(false);
  readonly searching = signal(false);
  readonly activeResult = signal(-1);
  readonly loading = signal(false);
  readonly error = signal(false);

  constructor() {
    this.searchTerms.pipe(
      debounceTime(250),
      distinctUntilChanged(),
      switchMap((term) => term.trim().length >= 2
        ? this.service.getStudents(term.trim()).pipe(catchError(() => of([])))
        : of([])),
    ).subscribe({
      next: (students) => {
        this.students.set(students);
        this.searching.set(false);
        this.activeResult.set(students.length ? 0 : -1);
      },
    });
  }

  private loadTranscript(studentId: number): void {
    this.loading.set(true);
    this.error.set(false);
    this.service.getOverall(studentId).subscribe({
      next: (record) => {
        this.transcript.set(record);
        this.selectedYears.set(record.years.slice(-2).map((year) => year.academicYear));
        this.loading.set(false);
      },
      error: () => {
        this.transcript.set(null);
        this.loading.set(false);
        this.error.set(true);
      },
    });
  }

  searchStudents(event: Event): void {
    const term = (event.target as HTMLInputElement).value;
    this.searchTerm.set(term);
    this.searchOpen.set(true);
    this.searching.set(term.trim().length >= 2);
    this.searchTerms.next(term);
  }

  openSearch(): void {
    this.searchOpen.set(true);
  }

  closeSearch(): void {
    setTimeout(() => this.searchOpen.set(false), 120);
  }

  handleSearchKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      this.searchOpen.set(false);
      return;
    }
    if (!this.students().length) return;
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.activeResult.update((index) => Math.min(index + 1, this.students().length - 1));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.activeResult.update((index) => Math.max(index - 1, 0));
    } else if (event.key === 'Enter') {
      event.preventDefault();
      const student = this.students()[Math.max(0, this.activeResult())];
      if (student) this.selectStudent(student);
    }
  }

  selectStudent(student: TranscriptStudent): void {
    this.selectedStudentId.set(student.id);
    this.searchTerm.set(`${student.fullName} · ${student.studentCode}`);
    this.searchOpen.set(false);
    this.students.set([]);
    this.loadTranscript(student.id);
  }

  toggleYear(academicYear: string): void {
    this.selectedYears.update((years) => {
      if (years.includes(academicYear) && years.length === 1) return years;
      return years.includes(academicYear)
        ? years.filter((year) => year !== academicYear)
        : [...years, academicYear];
    });
  }

  displayedYears(record: SchoolTranscript) {
    const selected = new Set(this.selectedYears());
    return record.years.filter((year) => selected.has(year.academicYear));
  }

  reload(): void {
    const studentId = this.selectedStudentId();
    if (studentId) this.loadTranscript(studentId);
  }

  exportPdf(): void {
    const currentTitle = document.title;
    const record = this.transcript();
    document.title = `${record?.studentCode || 'student'}-official-transcript`;
    window.print();
    setTimeout(() => (document.title = currentTitle), 250);
  }

  score(value?: number | null): string {
    return formatTranscriptScore(value, this.scoreScale());
  }

  formatGpa(value?: number | null): string {
    return value == null ? '—' : value.toFixed(2);
  }

  formatCredit(value: number): string {
    return Number.isInteger(value) ? value.toFixed(0) : value.toFixed(1);
  }

  ordinal(gradeLevel: string): string {
    const grade = Number(gradeLevel);
    if (!Number.isFinite(grade)) return gradeLevel;
    const remainder = grade % 100;
    const suffix = remainder >= 11 && remainder <= 13 ? 'th' :
      grade % 10 === 1 ? 'st' : grade % 10 === 2 ? 'nd' : grade % 10 === 3 ? 'rd' : 'th';
    return `${grade}${suffix}`;
  }

  gradeLabel(gradeLevel: string): string {
    return this.languageService.language() === 'vi'
      ? `${this.t('transcript.gradeWord')} ${gradeLevel}`
      : `${this.ordinal(gradeLevel)} ${this.t('transcript.gradeWord')}`;
  }

  generatedDate(): string {
    return new Intl.DateTimeFormat(this.languageService.language() === 'vi' ? 'vi-VN' : 'en-GB', {
      dateStyle: 'long',
    }).format(new Date());
  }

  dateOfBirth(value?: string | null): string {
    if (!value) return '—';
    return new Intl.DateTimeFormat(this.languageService.language() === 'vi' ? 'vi-VN' : 'en-GB', {
      dateStyle: 'long',
    }).format(new Date(value));
  }

  t(key: string): string {
    return this.languageService.t(key);
  }
}
