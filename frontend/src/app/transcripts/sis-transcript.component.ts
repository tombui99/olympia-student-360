import { HttpClient, HttpContext } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, combineLatest, distinctUntilChanged, map, Observable, of, startWith, switchMap } from 'rxjs';
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

interface Directory { students: Student[]; page: number; totalPages: number }
interface Transcript { student: Student; marks: Mark[] }
type View = { kind: 'directory'; data: Directory } | { kind: 'transcript'; data: Transcript }
  | { kind: 'loading' | 'error' | 'notFound' };
interface Period { semester: string; stage: string; label: string | null; marks: Mark[] }
interface Year { key: string; name: string; grade: string; periods: Period[] }

const semesterOrder = ['BEGIN', 'SEMESTER_1', 'SEMESTER_2', 'FULL_YEAR'];
const stageOrder = ['MIDDLE', 'FINAL', 'FULL'];
const order = (values: string[], value: string) => values.includes(value) ? values.indexOf(value) : values.length;

@Component({
  selector: 'app-sis-transcript',
  imports: [FormsModule, RouterLink],
  template: `
    @let current = view();
    <section class="overflow-hidden rounded-xl border border-[#ebe2f1] bg-white shadow-sm">
      <header class="border-b border-[#ebe2f1] bg-[#faf7fc] p-5 sm:p-7">
        <p class="text-sm uppercase tracking-[0.2em] text-[#8a6811]">{{ t('sis.source') }}</p>
        <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ t('sis.title') }}</h2>
        <p class="mt-2 max-w-3xl text-[#6f6877]">{{ t('sis.subtitle') }}</p>
        @if (studentId()) {
          <a routerLink="/transcripts/sis" class="mt-4 inline-block text-sm font-semibold text-[#6d3a91] hover:underline">← {{ t('sis.back') }}</a>
        } @else {
          <form class="mt-5 flex max-w-lg flex-wrap gap-2 text-sm font-semibold" (ngSubmit)="search()">
            <input type="search" name="search" [(ngModel)]="searchText" maxlength="100"
              [attr.aria-label]="t('sis.searchHint')" [placeholder]="t('sis.searchHint')"
              class="min-w-48 flex-1 rounded-md border border-[#d9cfe2] bg-white px-3 py-2 text-[#32124f]" />
            <button type="submit" class="rounded-md bg-[#32124f] px-4 py-2 text-white hover:bg-[#512874]">{{ t('sis.search') }}</button>
          </form>
        }
      </header>

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
          <p role="status" class="p-6 text-sm text-[#65596d]">{{ t('sis.notFound') }}</p>
        }
        @case ('directory') {
          @if (current.data.students.length) {
            <ul class="divide-y divide-[#ebe2f1]">
              @for (student of current.data.students; track student.id) {
                <li>
                  <a [routerLink]="['/transcripts/sis', student.id]"
                    class="flex items-center justify-between gap-3 px-5 py-4 hover:bg-[#faf7fc] sm:px-7">
                    <div>
                      <p class="text-sm font-semibold text-[#32124f]">{{ student.fullName }}</p>
                      <p class="mt-0.5 text-xs text-[#817589]">
                        {{ student.studentCode }} @if (student.homeroom) { · {{ student.homeroom }} }
                      </p>
                    </div>
                    <span class="text-sm font-semibold text-[#6d3a91]">{{ t('sis.open') }} →</span>
                  </a>
                </li>
              }
            </ul>
            @if (current.data.totalPages > 1) {
              <nav class="flex flex-wrap items-center justify-end gap-3 border-t border-[#ebe2f1] px-5 py-4 text-sm" [attr.aria-label]="t('sis.pages')">
                <button class="rounded-md border border-[#d9cfe2] px-3 py-1.5 disabled:opacity-40"
                  [disabled]="current.data.page === 1" (click)="setPage(current.data.page - 1)">{{ t('sis.previous') }}</button>
                <span>{{ current.data.page }} / {{ current.data.totalPages }}</span>
                <button class="rounded-md border border-[#d9cfe2] px-3 py-1.5 disabled:opacity-40"
                  [disabled]="current.data.page === current.data.totalPages" (click)="setPage(current.data.page + 1)">{{ t('sis.next') }}</button>
              </nav>
            }
          } @else {
            <p role="status" class="p-6 text-sm text-[#65596d]">{{ t('sis.emptyDirectory') }}</p>
          }
        }
        @case ('transcript') {
          <div class="p-5 sm:p-7">
            <div class="flex flex-wrap items-end justify-between gap-4">
              <div>
                <h3 class="text-lg font-semibold text-[#32124f]">{{ current.data.student.fullName }}</h3>
                <p class="mt-1 text-xs text-[#817589]">{{ current.data.student.studentCode }}</p>
              </div>
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
    </section>
  `,
})
export class SisTranscriptComponent {
  private readonly http = inject(HttpClient);
  private readonly language = inject(LanguageService);
  private readonly base = inject(BASE_PATH) + '/api/transcripts/sis';
  private readonly ids = inject(ActivatedRoute).paramMap.pipe(
    map(params => params.get('studentId')), distinctUntilChanged(),
  );
  readonly studentId = toSignal(this.ids);
  private readonly request = signal({ search: '', page: 1 });
  private readonly attempt = signal(0);
  readonly selectedYear = signal<string | null>(null);
  searchText = '';

  readonly view = toSignal(combineLatest([this.ids, toObservable(this.request), toObservable(this.attempt)]).pipe(
    switchMap(([id, request]) => {
      const context = new HttpContext().set(SKIP_GLOBAL_API_LOADING, true);
      const response: Observable<View> = id
        ? this.http.get<Transcript>(`${this.base}/students/${encodeURIComponent(id)}`, { context })
            .pipe(map(data => ({ kind: 'transcript', data })))
        : this.http.get<Directory>(`${this.base}/students`, { context, params: request })
            .pipe(map(data => ({ kind: 'directory', data })));
      return response.pipe(
        startWith<View>({ kind: 'loading' }),
        catchError(error => of<View>({ kind: error.status === 404 ? 'notFound' : 'error' })),
      );
    }),
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

  search(): void { this.request.set({ search: this.searchText.trim(), page: 1 }); }
  setPage(page: number): void { this.request.update(request => ({ ...request, page })); }
  reload(): void { this.attempt.update(value => value + 1); }
}
