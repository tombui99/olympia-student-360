import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LanguageService } from '../core/language/language.service';
import { formatTranscriptScore, TranscriptScoreScale } from './grading-scale';
import { SchoolTranscript, SchoolTranscriptYear, TranscriptsService } from './transcripts.service';

@Component({
  selector: 'app-transcripts',
  imports: [RouterLink],
  template: `
    <section class="mx-auto max-w-[1500px]">
      <div class="flex flex-col gap-4 xl:flex-row xl:items-end xl:justify-between">
        <div>
          <p class="text-sm uppercase tracking-[0.2em] text-[#8a6811]">{{ t('transcript.eyebrow') }}</p>
          <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ t('transcript.title') }}</h2>
          <p class="mt-2 max-w-3xl text-[#6f6877]">{{ t('transcript.overallDescription') }}</p>
        </div>
        <div class="flex flex-col items-start gap-3 sm:items-end">
          <div class="inline-flex items-center gap-3 rounded-lg border border-[#d9d0df] bg-white px-4 py-3 text-sm text-[#65596d]">
            <span class="h-2.5 w-2.5 rounded-full bg-[#2f7778]"></span>
            {{ t('transcript.canvasOnly') }}
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

      @if (loading()) {
        <div class="mt-8 rounded-xl border border-[#e4dde8] bg-white py-24 text-center text-[#766d7d]">{{ t('transcript.loadingOverall') }}</div>
      } @else if (error()) {
        <div class="mt-8 rounded-xl border border-[#efcaca] bg-[#fff8f7] px-6 py-16 text-center">
          <h3 class="text-lg font-semibold text-[#8b2727]">{{ t('transcript.errorTitle') }}</h3>
          <p class="mx-auto mt-2 max-w-xl text-sm text-[#765c5c]">{{ t('transcript.errorBody') }}</p>
          <button type="button" class="mt-5 rounded-md bg-[#32124f] px-5 py-2.5 text-sm font-semibold text-white" (click)="load()">{{ t('transcript.retry') }}</button>
        </div>
      } @else if (transcript(); as record) {
        <div class="mt-8 overflow-hidden rounded-xl border border-[#e2dbe6] bg-white shadow-[0_12px_38px_rgba(50,18,79,0.06)]">
          <div class="grid grid-cols-2 border-b border-[#e8e1eb] md:grid-cols-4">
            <div class="border-b border-r border-[#e8e1eb] p-5 md:border-b-0 lg:p-6">
              <p class="text-xs font-bold uppercase tracking-[0.12em] text-[#817589]">{{ t('transcript.overallAverage') }}</p>
              <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ score(record.overallAverage) }}</p>
            </div>
            <div class="border-b border-[#e8e1eb] p-5 md:border-b-0 md:border-r lg:p-6">
              <p class="text-xs font-bold uppercase tracking-[0.12em] text-[#817589]">{{ t('transcript.studentsCovered') }}</p>
              <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ record.totalStudents }}</p>
            </div>
            <div class="border-r border-[#e8e1eb] p-5 lg:p-6">
              <p class="text-xs font-bold uppercase tracking-[0.12em] text-[#817589]">{{ t('transcript.subjects') }}</p>
              <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ record.totalSubjects }}</p>
            </div>
            <div class="p-5 lg:p-6">
              <p class="text-xs font-bold uppercase tracking-[0.12em] text-[#817589]">{{ t('transcript.academicYears') }}</p>
              <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ record.totalAcademicYears }}</p>
            </div>
          </div>

          @if (record.years.length) {
            <div class="border-b border-[#e8e1eb] bg-[#faf8fb] px-5 py-4 lg:px-8">
              <p class="mb-3 text-xs font-bold uppercase tracking-[0.14em] text-[#766a7e]">{{ t('transcript.chooseYear') }}</p>
              <div class="flex gap-2 overflow-x-auto">
                @for (year of record.years; track year.academicYear) {
                  <button type="button" class="shrink-0 rounded-full border px-4 py-2 text-sm font-semibold transition"
                    [class.border-[#32124f]]="selectedYear() === year.academicYear"
                    [class.bg-[#32124f]]="selectedYear() === year.academicYear"
                    [class.text-white]="selectedYear() === year.academicYear"
                    [class.border-[#ddd5e2]]="selectedYear() !== year.academicYear"
                    [class.bg-white]="selectedYear() !== year.academicYear"
                    [class.text-[#5f5268]]="selectedYear() !== year.academicYear"
                    (click)="selectedYear.set(year.academicYear)">
                    {{ year.academicYear }}
                  </button>
                }
              </div>
            </div>

            @if (activeYear(record); as year) {
              <div class="p-5 lg:p-8">
                <div class="mb-6 flex flex-wrap items-end justify-between gap-4">
                  <div>
                    <p class="text-xs font-bold uppercase tracking-[0.16em] text-[#8a6811]">{{ t('transcript.schoolYear') }}</p>
                    <h3 class="mt-1 text-2xl font-semibold text-[#32124f]">{{ year.academicYear }}</h3>
                    <p class="mt-1 text-sm text-[#756b7c]">{{ year.grades.length }} {{ t('transcript.gradesAvailable') }}</p>
                  </div>
                  <div class="rounded-lg bg-[#f5eef8] px-5 py-3 text-right">
                    <p class="text-xs font-bold uppercase tracking-[0.12em] text-[#725a80]">{{ t('transcript.yearAverage') }}</p>
                    <p class="mt-1 text-2xl font-semibold text-[#32124f]">{{ score(year.averageScore) }}</p>
                  </div>
                </div>

                <div class="grid items-start gap-6 xl:grid-cols-2">
                  @for (grade of year.grades; track grade.gradeLevel) {
                    <article class="overflow-hidden rounded-xl border border-[#ded6e3] bg-white">
                      <header class="flex items-center justify-between gap-4 bg-[#32124f] px-5 py-4 text-white">
                        <div>
                          <p class="text-xs uppercase tracking-[0.14em] text-[#d6c5e1]">{{ year.academicYear }}</p>
                          <h4 class="mt-1 text-xl font-semibold">{{ t('transcript.grade') }} {{ grade.gradeLevel }}</h4>
                        </div>
                        <div class="rounded-md bg-white/12 px-3 py-2 text-right">
                          <p class="text-[0.65rem] uppercase tracking-[0.12em] text-[#d6c5e1]">{{ t('transcript.gradeAverage') }}</p>
                          <p class="mt-0.5 text-lg font-bold">{{ score(grade.averageScore) }}</p>
                        </div>
                      </header>
                      <div class="overflow-x-auto">
                        <table class="min-w-full border-collapse text-sm">
                          <thead class="bg-[#eee5f2] text-left text-[0.68rem] uppercase tracking-[0.09em] text-[#4a3158]">
                            <tr>
                              <th class="px-4 py-3">{{ t('transcript.subject') }}</th>
                              @for (term of grade.terms; track term) { <th class="px-3 py-3 text-center">{{ term }}</th> }
                              <th class="bg-[#e4d5eb] px-3 py-3 text-center">{{ t('transcript.average') }}</th>
                            </tr>
                          </thead>
                          <tbody class="divide-y divide-[#ece6ef]">
                            @for (subject of grade.subjects; track subject.subject) {
                              <tr class="hover:bg-[#fcfafc]">
                                <td class="px-4 py-3 font-semibold text-[#32124f]">{{ subject.subject }}</td>
                                @for (term of grade.terms; track term) {
                                  <td class="px-3 py-3 text-center" [class.text-[#aaa1b0]]="subject.termScores[term] == null">{{ score(subject.termScores[term]) }}</td>
                                }
                                <td class="bg-[#fcf8fd] px-3 py-3 text-center font-bold text-[#32124f]">{{ score(subject.yearAverage) }}</td>
                              </tr>
                            }
                          </tbody>
                        </table>
                      </div>
                      <footer class="flex items-center justify-between gap-4 border-t border-[#e8e1eb] bg-[#fcfafc] px-5 py-4">
                        <p class="text-xs text-[#7a7080]">{{ grade.subjects.length }} {{ t('transcript.subjects') }}</p>
                        <a class="inline-flex items-center gap-2 rounded-md border border-[#6b477f] px-4 py-2 text-sm font-semibold text-[#32124f] transition hover:bg-[#32124f] hover:text-white"
                          [routerLink]="['/transcripts', year.academicYear, 'grades', grade.gradeLevel]">
                          {{ t('transcript.viewGradeDetails') }} <span aria-hidden="true">→</span>
                        </a>
                      </footer>
                    </article>
                  } @empty {
                    <p class="col-span-full py-16 text-center text-sm text-[#766d7d]">{{ t('transcript.noGrades') }}</p>
                  }
                </div>
                <p class="mt-6 text-xs text-[#827789]">{{ t('transcript.overallCanvasNote') }}</p>
              </div>
            }
          } @else {
            <div class="px-8 py-20 text-center text-[#766d7d]">{{ t('transcript.noCanvasDataOverall') }}</div>
          }
        </div>
      }
    </section>
  `,
})
export class TranscriptsComponent {
  private readonly service = inject(TranscriptsService);
  readonly languageService = inject(LanguageService);
  readonly transcript = signal<SchoolTranscript | null>(null);
  readonly selectedYear = signal('');
  readonly loading = signal(true);
  readonly error = signal(false);
  readonly scoreScale = signal<TranscriptScoreScale>('percentage');
  readonly scoreScales: TranscriptScoreScale[] = ['percentage', 'gpa', 'letter'];

  constructor() { this.load(); }

  load(): void {
    this.loading.set(true);
    this.error.set(false);
    this.service.getOverall().subscribe({
      next: (record) => {
        this.transcript.set(record);
        this.selectedYear.set(record.years[0]?.academicYear ?? '');
        this.loading.set(false);
      },
      error: () => {
        this.transcript.set(null);
        this.loading.set(false);
        this.error.set(true);
      },
    });
  }

  activeYear(record: SchoolTranscript): SchoolTranscriptYear | undefined {
    return record.years.find((year) => year.academicYear === this.selectedYear()) ?? record.years[0];
  }

  score(value?: number | null): string { return formatTranscriptScore(value, this.scoreScale()); }
  t(key: string): string { return this.languageService.t(key); }
}
