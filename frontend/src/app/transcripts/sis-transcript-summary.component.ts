import { DecimalPipe } from '@angular/common';
import { HttpClient, HttpContext } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { toObservable, toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { catchError, map, of, startWith, switchMap } from 'rxjs';
import { BASE_PATH } from '../api/variables';
import { LanguageService } from '../core/language/language.service';
import { SKIP_GLOBAL_API_LOADING } from '../core/loading/api-loading.interceptor';

interface SchoolYear {
  id: string;
  name: string;
  grades: string[];
}

interface GradebookItem {
  item: string;
  semester: string;
  stage: string;
  periodName: string | null;
  students: number;
  average: number | null;
  maxScore: number | null;
  ratings: { letter: string; count: number }[];
}

interface Summary {
  students: number;
  subjects: { id: string; name: string; items: GradebookItem[] }[];
}

type LoadState<T> = { state: 'ready'; data: T } | { state: 'idle' | 'loading' | 'error' };

const semesters = ['BEGIN', 'SEMESTER_1', 'SEMESTER_2', 'FULL_YEAR'];
const stages = ['MIDDLE', 'FINAL', 'FULL'];

@Component({
  selector: 'app-sis-transcript-summary',
  imports: [DecimalPipe, FormsModule, RouterLink],
  template: `
    <section>
      <header>
        <h2 class="text-3xl font-semibold text-[#32124f]">{{ t('sis.summaryTitle') }}</h2>
        <p class="mt-2 max-w-3xl text-[#6f6877]">{{ t('sis.summaryDescription') }}</p>
      </header>

      @let available = catalog();
      <form class="mt-6 flex flex-wrap items-end gap-4 rounded-xl border border-[#ded6e3] bg-white p-5 text-sm font-semibold" (ngSubmit)="load()">
        @if (available.state === 'error') {
          <div role="alert" class="text-sm text-[#8b2727]">
            {{ t('sis.error') }}
            <button type="button" class="ml-2 font-semibold underline" (click)="retryCatalog()">{{ t('sis.retry') }}</button>
          </div>
        } @else if (available.state === 'ready' && !available.data.length) {
          <p role="status" class="text-sm text-[#65596d]">{{ t('sis.summaryEmpty') }}</p>
        } @else {
          <label class="block min-w-48">
            <span class="text-xs font-bold uppercase tracking-[0.1em] text-[#74677c]">{{ t('transcript.schoolYear') }}</span>
            <select class="mt-2 block w-full rounded-lg border border-[#d8cede] bg-white px-4 py-3 text-sm font-semibold text-[#32124f]"
              [ngModel]="yearId()" name="schoolYear" (ngModelChange)="changeYear($event)" [disabled]="available.state !== 'ready'">
              <option value="">{{ t('transcript.chooseYear') }}</option>
              @if (available.state === 'ready') {
                @for (year of available.data; track year.id) { <option [value]="year.id">{{ year.name }}</option> }
              }
            </select>
          </label>
          <label class="block min-w-48">
            <span class="text-xs font-bold uppercase tracking-[0.1em] text-[#74677c]">{{ t('transcript.grade') }}</span>
            <select class="mt-2 block w-full rounded-lg border border-[#d8cede] bg-white px-4 py-3 text-sm font-semibold text-[#32124f]"
              [ngModel]="grade()" name="grade" (ngModelChange)="changeGrade($event)" [disabled]="!yearId()">
              <option value="">{{ t('transcript.chooseGrade') }}</option>
              @for (level of grades(); track level) { <option [value]="level">{{ level }}</option> }
            </select>
          </label>
          <button type="submit" class="rounded-lg bg-[#32124f] px-5 py-3 text-sm font-semibold text-white disabled:opacity-40"
            [disabled]="!yearId() || !grade() || summary().state === 'loading'">{{ t('transcript.loadSummary') }}</button>
        }
        <a routerLink="/transcripts/sis" class="py-3 text-sm font-semibold text-[#6d3a91] hover:underline">{{ t('transcript.chooseStudent') }}</a>
      </form>

      @let result = summary();
      @if (result.state === 'loading' || available.state === 'loading') {
        <p role="status" class="mt-5 text-sm text-[#65596d]">{{ t('transcript.loadingSummary') }}</p>
      } @else if (result.state === 'error') {
        <div role="alert" class="mt-5 rounded-lg border border-[#efcaca] bg-[#fff8f7] p-4 text-sm text-[#8b2727]">
          {{ t('sis.error') }}
          <button type="button" class="ml-2 font-semibold underline" (click)="load()">{{ t('sis.retry') }}</button>
        </div>
      } @else if (result.state === 'ready') {
        @if (result.data.subjects.length) {
          <dl class="mt-6 flex flex-wrap gap-x-8 gap-y-2 rounded-lg border border-[#ded6e3] bg-white px-5 py-4 text-sm text-[#65596d]">
            <div class="flex items-baseline gap-2">
              <dt class="text-[0.68rem] font-bold uppercase tracking-[0.1em] text-[#817589]">{{ t('transcript.studentsCovered') }}</dt>
              <dd class="text-[1.75rem] font-semibold text-[#32124f]">{{ result.data.students | number }}</dd>
            </div>
            <div class="flex items-baseline gap-2">
              <dt class="text-[0.68rem] font-bold uppercase tracking-[0.1em] text-[#817589]">{{ t('transcript.subjects') }}</dt>
              <dd class="text-[1.75rem] font-semibold text-[#32124f]">{{ result.data.subjects.length | number }}</dd>
            </div>
          </dl>

          @for (subject of result.data.subjects; track subject.id) {
            <section class="mt-5 overflow-hidden rounded-lg border border-[#ded6e3] bg-white">
              <h3 class="border-b border-[#ded6e3] bg-[#f6f0f9] px-4 py-3 text-lg font-semibold text-[#32124f]">{{ subject.name }}</h3>
              <div class="overflow-x-auto">
                <table class="w-full min-w-[40rem] text-left text-sm">
                  <thead class="border-b border-[#ebe2f1] text-[#65596d]">
                    <tr>
                      <th scope="col" class="px-4 py-3">{{ t('transcript.term') }}</th>
                      <th scope="col" class="px-4 py-3">{{ t('sis.item') }}</th>
                      <th scope="col" class="px-4 py-3 text-right">{{ t('sis.summaryStudents') }}</th>
                      <th scope="col" class="px-4 py-3">{{ t('sis.summaryAverage') }}</th>
                      <th scope="col" class="px-4 py-3">{{ t('sis.summaryRatings') }}</th>
                    </tr>
                  </thead>
                  <tbody class="divide-y divide-[#ebe2f1]">
                    @for (item of subject.items; track $index) {
                      <tr class="even:bg-[#fbfafc]">
                        <td class="px-4 py-3 text-[#65596d]">{{ period(item) }}</td>
                        <th scope="row" class="px-4 py-3 font-semibold text-[#32124f]">{{ item.item }}</th>
                        <td class="px-4 py-3 text-right tabular-nums text-[#65596d]">{{ item.students | number }}</td>
                        <td class="whitespace-nowrap px-4 py-3 font-semibold tabular-nums text-[#32124f]">
                          @if (item.average !== null) {
                            {{ item.average | number:'1.0-2' }}@if (item.maxScore !== null) { / {{ item.maxScore | number:'1.0-2' }} }
                          } @else { — }
                        </td>
                        <td class="px-4 py-3 text-[#65596d]">
                          @for (rating of item.ratings; track rating.letter) {
                            <span class="me-3 inline-block"><strong class="text-[#32124f]">{{ rating.letter }}</strong> ({{ rating.count | number }})</span>
                          } @empty { — }
                        </td>
                      </tr>
                    }
                  </tbody>
                </table>
              </div>
            </section>
          }
        } @else {
          <p role="status" class="mt-5 rounded-lg border border-[#ded6e3] bg-white p-5 text-sm text-[#65596d]">{{ t('sis.summaryEmpty') }}</p>
        }
      }
    </section>
  `,
})
export class SisTranscriptSummaryComponent {
  private readonly http = inject(HttpClient);
  private readonly language = inject(LanguageService);
  private readonly url = inject(BASE_PATH) + '/api/transcripts/sis/summary';
  private readonly catalogAttempt = signal(0);
  private readonly selection = signal<{ schoolYearId: string; gradeLevel: string } | null>(null);
  readonly yearId = signal('');
  readonly grade = signal('');

  readonly catalog = toSignal(toObservable(this.catalogAttempt).pipe(
    switchMap(() => this.http.get<SchoolYear[]>(this.url + '/catalog', { context: this.context() }).pipe(
      map(data => ({ state: 'ready', data }) as const),
      startWith<LoadState<SchoolYear[]>>({ state: 'loading' }),
      catchError(() => of<LoadState<SchoolYear[]>>({ state: 'error' })),
    )),
  ), { initialValue: { state: 'loading' } as LoadState<SchoolYear[]> });

  readonly grades = computed(() => {
    const catalog = this.catalog();
    return catalog.state === 'ready' ? catalog.data.find(year => year.id === this.yearId())?.grades ?? [] : [];
  });

  readonly summary = toSignal(toObservable(this.selection).pipe(
    switchMap(params => params ? this.http.get<Summary>(this.url, { params, context: this.context() }).pipe(
      map(data => ({ state: 'ready', data: this.sortSummary(data) }) as const),
      startWith<LoadState<Summary>>({ state: 'loading' }),
      catchError(() => of<LoadState<Summary>>({ state: 'error' })),
    ) : of<LoadState<Summary>>({ state: 'idle' })),
  ), { initialValue: { state: 'idle' } as LoadState<Summary> });

  t(key: string): string { return this.language.t(key); }

  changeYear(id: string): void {
    this.yearId.set(id);
    this.changeGrade('');
  }

  changeGrade(grade: string): void {
    this.grade.set(grade);
    this.selection.set(null);
  }

  load(): void {
    if (this.yearId() && this.grades().includes(this.grade())) {
      this.selection.set({ schoolYearId: this.yearId(), gradeLevel: this.grade() });
    }
  }

  retryCatalog(): void { this.catalogAttempt.update(value => value + 1); }

  period(item: GradebookItem): string {
    if (item.periodName) return item.periodName;
    const label = (value: string) => {
      const key = 'sis.period.' + value;
      const text = this.t(key);
      return text === key ? value : text;
    };
    const semester = label(item.semester);
    return item.semester === 'BEGIN' || item.semester === 'FULL_YEAR' ? semester : semester + ' · ' + label(item.stage);
  }

  private context(): HttpContext { return new HttpContext().set(SKIP_GLOBAL_API_LOADING, true); }

  private sortSummary(summary: Summary): Summary {
    const locale = this.language.language();
    const rank = (options: string[], value: string) => options.includes(value) ? options.indexOf(value) : options.length;
    summary.subjects.sort((a, b) => a.name.localeCompare(b.name, locale));
    for (const subject of summary.subjects) {
      subject.items.sort((a, b) => rank(semesters, a.semester) - rank(semesters, b.semester) ||
        rank(stages, a.stage) - rank(stages, b.stage) || a.item.localeCompare(b.item, locale) ||
        (a.maxScore ?? 0) - (b.maxScore ?? 0));
    }
    return summary;
  }
}
