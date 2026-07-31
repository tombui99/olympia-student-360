import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LanguageService } from '../core/language/language.service';
import { formatTranscriptScore, TranscriptScoreScale } from './grading-scale';
import { SchoolTranscript, SchoolTranscriptYear, TranscriptsService } from './transcripts.service';

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
            <div class="border-b border-[#e8e1eb] bg-[#faf8fb] px-5 py-4">
              <label class="block max-w-xs text-xs font-bold uppercase tracking-[0.12em] text-[#766a7e]">
                {{ t('transcript.chooseYear') }}
                <select class="mt-2 block w-full rounded-lg border border-[#d8cede] bg-white px-4 py-3 text-sm font-semibold normal-case tracking-normal text-[#32124f]"
                  [value]="selectedYear()" (change)="changeYear($event)">
                  @for (year of orderedYears(record); track year.academicYear) { <option [value]="year.academicYear">{{ year.academicYear }}</option> }
                </select>
              </label>
            </div>
            @if (activeYear(record); as year) {
              <div class="p-5 lg:p-7">
                <div class="mb-5 flex items-end justify-between gap-4">
                  <div><p class="text-xs font-bold uppercase tracking-[0.14em] text-[#8a6811]">{{ t('transcript.schoolYear') }}</p><h3 class="mt-1 text-2xl font-semibold text-[#32124f]">{{ year.academicYear }}</h3></div>
                  <div class="rounded-lg bg-[#f5eef8] px-4 py-3 text-right"><p class="metric-label">{{ t('transcript.yearAverage') }}</p><p class="mt-1 text-xl font-semibold text-[#32124f]">{{ score(year.averageScore) }}</p></div>
                </div>
                <div class="grid items-start gap-5 xl:grid-cols-2">
                  @for (grade of year.grades; track grade.gradeLevel) {
                    <article class="overflow-hidden rounded-xl border border-[#ded6e3]">
                      <header class="flex items-center justify-between gap-3 bg-[#32124f] px-4 py-3 text-white">
                        <h4 class="text-lg font-semibold">{{ t('transcript.grade') }} {{ grade.gradeLevel }}</h4>
                        <span class="text-sm font-bold">GPA {{ formatGpa(grade.gpa) }}</span>
                      </header>
                      <div class="overflow-x-auto">
                        <table class="w-full min-w-[480px] border-collapse text-xs">
                          <thead class="bg-[#eee5f2] text-[#4a3158]"><tr>
                            <th class="px-3 py-2 text-left">{{ t('transcript.subject') }}</th>
                            <th class="px-2 py-2 text-center">S1F</th><th class="px-2 py-2 text-center">S2F</th>
                            <th class="px-2 py-2 text-center">YR</th><th class="px-2 py-2 text-center">{{ t('transcript.credit') }}</th>
                          </tr></thead>
                          <tbody>
                            @for (subject of grade.subjects; track subject.subject) {
                              <tr class="border-t border-[#ece6ef]">
                                <td class="px-3 py-2 font-semibold text-[#32124f]">{{ subject.subject }}</td>
                                <td class="px-2 py-2 text-center">{{ score(subject.semester1Final) }}</td>
                                <td class="px-2 py-2 text-center">{{ score(subject.semester2Final) }}</td>
                                <td class="bg-[#fcf8fd] px-2 py-2 text-center font-bold">{{ score(subject.yearAverage) }}</td>
                                <td class="px-2 py-2 text-center">{{ credit(subject.credit) }}</td>
                              </tr>
                            }
                          </tbody>
                        </table>
                      </div>
                      <footer class="flex items-center justify-end border-t border-[#e8e1eb] px-4 py-3">
                        <a [routerLink]="['/transcripts', year.academicYear, 'grades', grade.gradeLevel]" class="text-sm font-semibold text-[#6d3a91] hover:underline">{{ t('transcript.viewGradeDetails') }} →</a>
                      </footer>
                    </article>
                  }
                </div>
              </div>
            }
          } @else {
            <div class="px-8 py-20 text-center text-[#766d7d]">{{ t('transcript.noCanvasDataOverall') }}</div>
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
  readonly languageService = inject(LanguageService);
  readonly transcript = signal<SchoolTranscript | null>(null);
  readonly selectedYear = signal('');
  readonly loading = signal(true);
  readonly error = signal(false);
  readonly scoreScale = signal<TranscriptScoreScale>('letter');
  readonly scoreScales: TranscriptScoreScale[] = ['percentage', 'gpa', 'letter'];

  constructor() { this.load(); }
  load(): void {
    this.loading.set(true);
    this.error.set(false);
    this.service.getOverall().subscribe({
      next: (record) => {
        this.transcript.set(record);
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
  changeYear(event: Event): void { this.selectedYear.set((event.target as HTMLSelectElement).value); }
  score(value?: number | null): string { return formatTranscriptScore(value, this.scoreScale()); }
  formatGpa(value?: number | null): string { return value == null ? '—' : value.toFixed(2); }
  credit(value: number): string { return Number.isInteger(value) ? value.toFixed(0) : value.toFixed(1); }
  t(key: string): string { return this.languageService.t(key); }
}
