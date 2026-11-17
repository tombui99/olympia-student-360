import { HttpClient, HttpContext } from '@angular/common/http';
import { Component, computed, inject, input, linkedSignal, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { catchError, combineLatest, map, of, startWith, switchMap } from 'rxjs';
import { BASE_PATH } from '../api/variables';
import { LanguageService } from '../core/language/language.service';
import { SKIP_GLOBAL_API_LOADING } from '../core/loading/api-loading.interceptor';

interface Student {
  id: number;
  fullName: string;
  studentCode: string;
  homeroom: string | null;
}

interface Mark {
  schoolYearId: string;
  schoolYear: string;
  gradeLevel: string;
  subjectId: string;
  subject: string;
  gradeItemId: string;
  gradeItem: string;
  semester: string;
  stage: string;
  periodName: string | null;
  homeroom: string | null;
  score: number | null;
  letter: string | null;
  maxScore: number | null;
  comment: string | null;
}

interface Transcript { student: Student; marks: Mark[] }
type View = { kind: 'transcript'; data: Transcript } | { kind: 'loading' | 'error' | 'notFound' };

interface Period { semester: string; stage: string; label: string | null; marks: Mark[] }
interface Year { key: string; name: string; grade: string; periods: Period[] }

const semesterOrder = ['BEGIN', 'SEMESTER_1', 'SEMESTER_2', 'FULL_YEAR'];
const stageOrder = ['MIDDLE', 'FINAL', 'FULL'];
const order = (values: string[], value: string) => values.includes(value) ? values.indexOf(value) : values.length;

@Component({
  selector: 'app-sis-student-marks',
  imports: [FormsModule],
  template: `
    @let current = view();
    @switch (current.kind) {
      @case ('loading') {
        <p role="status" class="p-6 text-sm text-[#65596d]">{{ t('sis.loading') }}</p>
      }
      @case ('error') {
        <div role="alert" class="flex items-center gap-3 p-6 text-sm text-[#65596d]">
          {{ t('sis.error') }}
          <button class="font-semibold text-[#6d3a91] hover:underline" (click)="reload()">{{ t('sis.retry') }}</button>
        </div>
      }
      @case ('notFound') {
        <p role="status" class="p-6 text-sm text-[#65596d]">{{ t(showStudent() ? 'sis.notFound' : 'sis.emptyMarks') }}</p>
      }
      @case ('transcript') {
        <div class="p-5 sm:p-7">
          <div class="flex flex-wrap items-end justify-between gap-4">
            @if (showStudent()) {
              <div>
                <h3 class="text-lg font-semibold text-[#32124f]">{{ current.data.student.fullName }}</h3>
                <p class="mt-1 text-xs text-[#817589]">{{ current.data.student.studentCode }}</p>
              </div>
            }
            @if (activeYear(); as year) {
              <label class="flex flex-col gap-1 text-sm font-semibold text-[#32124f]">
                <span class="text-xs font-bold uppercase tracking-[0.1em] text-[#74677c]">{{ t('sis.year') }}</span>
                <select class="rounded-md border border-[#d9cfe2] bg-white px-3 py-2"
                  [ngModel]="year.key" (ngModelChange)="selectedYear.set($event)">
                  @for (option of years(); track option.key) {
                    <option [value]="option.key">{{ option.name }} · {{ t('transcript.gradeWord') }} {{ option.grade }}</option>
                  }
                </select>
              </label>
            }
          </div>
          @if (activeYear(); as year) {
            @for (period of year.periods; track period.semester + ':' + period.stage) {
              <section class="mt-6 overflow-hidden rounded-lg border border-[#ebe2f1]">
                <h4 class="bg-[#f6f0f9] px-4 py-3 text-sm font-bold text-[#32124f]">{{ period.label || periodName(period) }}</h4>
                <div class="overflow-x-auto">
                  <table class="w-full min-w-[38rem] text-left text-[0.78rem]">
                    <thead class="border-b border-[#ebe2f1] text-[#65596d]">
                      <tr>
                        <th scope="col" class="px-4 py-3">{{ t('sis.subject') }}</th>
                        <th scope="col" class="px-4 py-3">{{ t('sis.item') }}</th>
                        <th scope="col" class="px-4 py-3">{{ t('sis.result') }}</th>
                        <th scope="col" class="px-4 py-3">{{ t('sis.comment') }}</th>
                      </tr>
                    </thead>
                    <tbody class="divide-y divide-[#ebe2f1]">
                      @for (mark of period.marks; track $index) {
                        <tr>
                          <th scope="row" class="px-4 py-3 font-semibold text-[#32124f]">{{ mark.subject }}</th>
                          <td class="px-4 py-3 text-[#65596d]">
                            {{ mark.gradeItem }}
                            @if (mark.homeroom) { <span class="block text-xs">{{ mark.homeroom }}</span> }
                          </td>
                          <td class="whitespace-nowrap px-4 py-3 font-semibold tabular-nums text-[#32124f]">
                            @if (mark.score !== null) {
                              {{ mark.score }}@if (mark.maxScore !== null) { / {{ mark.maxScore }} }
                            } @else { {{ mark.letter ?? '—' }} }
                          </td>
                          <td class="max-w-md whitespace-pre-wrap break-words px-4 py-3 text-[#65596d]">{{ mark.comment || '—' }}</td>
                        </tr>
                      }
                    </tbody>
                  </table>
                </div>
              </section>
            }
          } @else {
            <p role="status" class="mt-6 rounded-md bg-[#faf7fc] p-4 text-sm text-[#65596d]">{{ t('sis.emptyMarks') }}</p>
          }
        </div>
      }
    }
  `,
})
export class SisStudentMarksComponent {
  private readonly http = inject(HttpClient);
  private readonly language = inject(LanguageService);
  private readonly base = inject(BASE_PATH) + '/api/transcripts/sis/students';
  private readonly attempt = signal(0);
  readonly studentId = input.required<string | number>();
  readonly showStudent = input(false);
  readonly selectedYear = linkedSignal({ source: this.studentId, computation: (): string | null => null });

  readonly view = toSignal(combineLatest([toObservable(this.studentId), toObservable(this.attempt)]).pipe(
    switchMap(([id]) => this.http.get<Transcript>(`${this.base}/${encodeURIComponent(id)}`, {
      context: new HttpContext().set(SKIP_GLOBAL_API_LOADING, true),
    }).pipe(
      map(data => ({ kind: 'transcript', data }) as const),
      startWith<View>({ kind: 'loading' }),
      catchError(error => of<View>({ kind: error.status === 404 ? 'notFound' : 'error' })),
    )),
  ), { initialValue: { kind: 'loading' } as View });

  readonly years = computed(() => {
    const view = this.view();
    if (view.kind !== 'transcript') return [];
    const years = new Map<string, Year>();
    for (const mark of view.data.marks) {
      const key = mark.schoolYearId + ':' + mark.gradeLevel;
      let year = years.get(key);
      if (!year) {
        year = { key, name: mark.schoolYear, grade: mark.gradeLevel, periods: [] };
        years.set(key, year);
      }
      let period = year.periods.find(p => p.semester === mark.semester && p.stage === mark.stage);
      if (!period) {
        period = { semester: mark.semester, stage: mark.stage, label: mark.periodName, marks: [] };
        year.periods.push(period);
      }
      period.label ??= mark.periodName;
      period.marks.push(mark);
    }
    for (const year of years.values()) {
      year.periods.sort((a, b) => order(semesterOrder, a.semester) - order(semesterOrder, b.semester)
        || order(stageOrder, a.stage) - order(stageOrder, b.stage));
      for (const period of year.periods) {
        period.marks.sort((a, b) => a.subject.localeCompare(b.subject, this.language.language())
          || a.subjectId.localeCompare(b.subjectId) || a.gradeItem.localeCompare(b.gradeItem, this.language.language())
          || a.gradeItemId.localeCompare(b.gradeItemId));
      }
    }
    return [...years.values()].sort((a, b) => Number(b.grade) - Number(a.grade) || b.name.localeCompare(a.name));
  });

  readonly activeYear = computed(() =>
    this.years().find(year => year.key === this.selectedYear()) ?? this.years()[0]);

  t(key: string): string { return this.language.t(key); }

  periodName(period: Period): string {
    const label = (value: string) => {
      const key = 'sis.period.' + value;
      const text = this.t(key);
      return text === key ? value : text;
    };
    return period.semester === 'BEGIN' || period.semester === 'FULL_YEAR'
      ? label(period.semester) : `${label(period.semester)} · ${label(period.stage)}`;
  }

  reload(): void { this.attempt.update(value => value + 1); }
}
