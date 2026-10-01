import { Component, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { catchError, debounceTime, distinctUntilChanged, of, Subject, switchMap } from 'rxjs';
import { LanguageService } from '../core/language/language.service';
import { formatTranscriptScore, TranscriptScoreScale } from './grading-scale';
import { SchoolTranscript, SchoolTranscriptGrade, TranscriptStudent, TranscriptSubject, TranscriptsService } from './transcripts.service';

type TranscriptType = 'bilingual' | 'international' | 'ib';
type TranscriptSemester = 'semester1' | 'semester2';
type TranscriptSemesterFilter = 'all' | TranscriptSemester;

@Component({
  selector: 'app-student-transcript',
  imports: [RouterLink],
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
                        <span class="mt-0.5 block text-xs text-[#817589]">{{ student.sisUserId }} · {{ student.homeroom || (t('transcript.grade') + ' ' + (student.gradeLevel || '—')) }}</span>
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
              <label class="text-xs font-bold uppercase tracking-[0.1em] text-[#74677c]">
                {{ t('transcript.semesterFilter') }}
                <select class="mt-1 block min-w-44 rounded-lg border border-[#d8cede] bg-white px-4 py-3 text-sm font-semibold normal-case tracking-normal text-[#32124f]"
                  [value]="selectedSemester()" (change)="changeSemester($event)">
                  <option value="all">{{ t('transcript.allSemesters') }}</option>
                  <option value="semester1">{{ t('transcript.semester1') }}</option>
                  <option value="semester2">{{ t('transcript.semester2') }}</option>
                </select>
              </label>
            }
            @if (activeTranscriptType() === 'international') {
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
          }
          <details class="export-menu relative">
            <summary
              class="inline-flex cursor-pointer list-none items-center gap-2 rounded-lg bg-[#32124f] px-5 py-3 text-sm font-semibold text-white shadow-sm transition hover:bg-[#51236f]"
              [class.pointer-events-none]="!transcript() || !transcript()?.years?.length"
              [class.opacity-50]="!transcript() || !transcript()?.years?.length">
              <span aria-hidden="true">↧</span> {{ t('transcript.exportPdf') }} <span aria-hidden="true">⌄</span>
            </summary>
            <div class="absolute right-0 z-30 mt-2 w-72 overflow-hidden rounded-lg border border-[#d8cede] bg-white p-1.5 shadow-[0_14px_35px_rgba(50,18,79,0.16)]">
              @for (type of transcriptTypes; track type.id) {
                <button type="button" class="block w-full rounded-md px-3 py-2.5 text-left hover:bg-[#f4eef7]" (click)="exportPdf(type.id)">
                  <span class="block text-sm font-semibold text-[#32124f]">{{ t('transcript.types.' + type.id + '.name') }}</span>
                  <span class="mt-0.5 block text-xs text-[#817589]">{{ t('transcript.types.' + type.id + '.exportHint') }}</span>
                </button>
              }
            </div>
          </details>
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
        <a routerLink="/transcripts/student" class="no-print mt-6 inline-flex items-center gap-2 text-sm font-semibold text-[#6d3a91] hover:text-[#32124f]">
          <span aria-hidden="true">←</span> {{ t('transcript.backToStudentDirectory') }}
        </a>
        <section class="no-print mt-5" aria-labelledby="transcript-type-title">
          <div class="flex flex-wrap items-end justify-between gap-3">
            <div>
              <p class="text-xs font-bold uppercase tracking-[0.14em] text-[#8a6811]">{{ t('transcript.typeEyebrow') }}</p>
              <h3 id="transcript-type-title" class="mt-1 text-lg font-semibold text-[#32124f]">{{ t('transcript.typeTitle') }}</h3>
            </div>
            <p class="max-w-xl text-sm text-[#756b7c]">{{ t('transcript.typeHelp') }}</p>
          </div>
          <div class="mt-3 grid gap-3 lg:grid-cols-3">
            @for (type of transcriptTypes; track type.id) {
              <button type="button"
                class="rounded-xl border p-4 text-left transition"
                [class.border-[#6d3a91]]="activeTranscriptType() === type.id"
                [class.bg-[#f7f1f9]]="activeTranscriptType() === type.id"
                [class.shadow-[0_6px_18px_rgba(50,18,79,0.08)]]="activeTranscriptType() === type.id"
                [class.border-[#ded6e3]]="activeTranscriptType() !== type.id"
                [class.bg-white]="activeTranscriptType() !== type.id"
                (click)="selectTranscriptType(type.id)">
                <span class="flex items-center justify-between gap-3">
                  <span class="font-semibold text-[#32124f]">{{ t('transcript.types.' + type.id + '.name') }}</span>
                  @if (activeTranscriptType() === type.id) { <span class="rounded-full bg-[#6d3a91] px-2 py-0.5 text-[0.65rem] font-bold uppercase tracking-wide text-white">{{ t('transcript.selected') }}</span> }
                </span>
                <span class="mt-1.5 block text-sm leading-5 text-[#756b7c]">{{ t('transcript.types.' + type.id + '.description') }}</span>
                <span class="mt-3 block text-xs font-semibold text-[#8a6811]">{{ t('transcript.types.' + type.id + '.scale') }}</span>
              </button>
            }
          </div>
          @if (hasScienceMappingIssue(record)) {
            <div class="mt-3 flex gap-3 rounded-lg border border-[#e5c97a] bg-[#fffaf0] px-4 py-3 text-sm text-[#6b5318]">
              <span aria-hidden="true">⚠</span>
              <p><strong>{{ t('transcript.mappingReviewTitle') }}</strong> {{ t('transcript.mappingReviewScience') }}</p>
            </div>
          }
        </section>
        <article class="print-region transcript-sheet mt-8 overflow-hidden rounded-xl border border-[#ded6e3] bg-white shadow-[0_12px_38px_rgba(50,18,79,0.07)]">
          <header class="transcript-header border-b border-[#d9cfdf] px-6 py-6 lg:px-9">
            <div class="letterhead-content flex items-center gap-5">
              <img src="/assets/olympia_logo.png" alt="The Olympia Schools" class="h-20 w-20 object-contain" />
              <div class="min-w-0 flex-1">
                <h1 class="text-2xl font-bold text-[#64266f] lg:text-3xl">The Olympia Schools</h1>
                <p class="mt-1 text-sm font-semibold uppercase tracking-[0.18em] text-[#756b7c]">{{ t('transcript.types.' + activeTranscriptType() + '.documentTitle') }}</p>
              </div>
              <div class="hidden text-right text-xs text-[#756b7c] sm:block">
                <p>{{ t('transcript.types.' + activeTranscriptType() + '.name') }}</p>
                <p class="mt-1">{{ generatedDate() }}</p>
              </div>
            </div>

            <div class="mt-5 grid gap-x-8 gap-y-3 border-t border-[#e8e1eb] pt-4 text-sm sm:grid-cols-2 lg:grid-cols-3">
              <div><span class="meta-label">{{ t('transcript.student') }}</span><strong>{{ record.studentName || '—' }}</strong></div>
              <div><span class="meta-label">{{ t('transcript.sisUserId') }}</span><strong>{{ record.sisUserId || '—' }}</strong></div>
              <div><span class="meta-label">{{ t('transcript.dateOfBirth') }}</span><strong>{{ dateOfBirth(record.dateOfBirth) }}</strong></div>
              <div><span class="meta-label">{{ t('transcript.gender') }}</span><strong>{{ record.gender || '—' }}</strong></div>
              <div><span class="meta-label">{{ t('transcript.currentGrade') }}</span><strong>{{ record.gradeLevel || '—' }}</strong></div>
              <div><span class="meta-label">{{ t('transcript.homeroom') }}</span><strong>{{ record.homeroom || '—' }}</strong></div>
            </div>
          </header>

          @if (activeTranscriptType() === 'ib') {
            <section class="transcript-note mx-6 mt-5 border border-[#cfc4d5] bg-[#faf8fb] p-4 text-xs leading-5 text-[#4e4056] lg:mx-9">
              <h2 class="font-bold text-[#24162b]">{{ t('transcript.ibProgrammeTitle') }}</h2>
              <p class="mt-1">{{ t('transcript.ibProgrammeNote') }}</p>
              <p class="mt-2 font-semibold">{{ t('transcript.ibScaleNote') }}</p>
            </section>
          } @else if (activeTranscriptType() === 'bilingual') {
            <section class="transcript-note mx-6 mt-5 flex flex-wrap items-center justify-between gap-2 border-b-2 border-[#32124f] pb-3 text-sm lg:mx-9">
              <strong>{{ t('transcript.bilingualAuthority') }}</strong>
              <span class="text-[#756b7c]">{{ t('transcript.bilingualPurpose') }}</span>
            </section>
          }

          @if (record.years.length) {
            <div class="transcript-years space-y-7 p-6 lg:p-9">
              @for (year of displayedYears(record); track year.academicYear) {
                <section class="year-block overflow-hidden rounded-md border border-[#cfc4d5] break-inside-avoid">
                  <header class="flex items-center justify-between gap-3 bg-[#f1eaf4] px-4 py-3 text-[#32124f]">
                    <h2 class="text-base font-bold">{{ t('transcript.schoolYear') }}: {{ year.academicYear }}</h2>
                    <span class="text-sm font-semibold">{{ t('transcript.yearAverage') }}: {{ typeScore(year.averageScore) }}</span>
                  </header>
                  @for (grade of year.grades; track grade.gradeLevel) {
                    <div class="flex items-center justify-between gap-3 border-t border-[#d8c4bd] bg-[#f6eef8] px-4 py-2.5">
                      <h3 class="font-bold text-[#32124f]">{{ gradeLabel(grade.gradeLevel) }}</h3>
                      @if (activeTranscriptType() === 'international') {
                        <p class="text-sm font-bold text-[#32124f]">GPA <span class="ml-2">{{ formatGpa(grade.gpa) }}</span></p>
                      } @else if (activeTranscriptType() === 'ib') {
                        <p class="text-xs font-bold uppercase tracking-wide text-[#32124f]">{{ isIbGrade(grade.gradeLevel) ? t('transcript.ibSevenPoint') : t('transcript.letterGrade') }}</p>
                      } @else {
                        <p class="text-xs font-bold uppercase tracking-wide text-[#32124f]">{{ t('transcript.tenPointScale') }}</p>
                      }
                    </div>
                    @for (semester of visibleSemesters(); track semester) {
                      <section class="semester-block" [attr.aria-label]="semesterLabel(semester)">
                        <h4 class="border-b border-[#d8cede] bg-[#eee5f2] px-4 py-2 text-sm font-bold text-[#4a3158]">{{ t('transcript.term') }}: {{ semesterLabel(semester) }}</h4>
                        <div class="overflow-x-auto">
                          <table class="w-full min-w-[660px] table-fixed border-collapse text-[0.78rem]">
                            <thead class="bg-[#51236f] text-white">
                              <tr>
                                <th class="w-14 px-2 py-2 text-center">{{ t('transcript.subjectNumber') }}</th>
                                <th class="px-3 py-2 text-left">{{ activeTranscriptType() === 'bilingual' ? t('transcript.bilingualSubject') : t('transcript.subject') }}</th>
                                <th class="w-24 px-2 py-2 text-center">{{ t('transcript.semesterScore') }}</th>
                                <th class="w-24 px-2 py-2 text-center">{{ t('transcript.yearResult') }}</th>
                                <th class="w-24 px-2 py-2 text-center">{{ activeTranscriptType() === 'international' || (activeTranscriptType() === 'ib' && !isIbGrade(grade.gradeLevel)) ? t('transcript.credit') : (activeTranscriptType() === 'ib' ? t('transcript.ibLevel') : t('transcript.result')) }}</th>
                              </tr>
                            </thead>
                            <tbody>
                              @for (subject of subjectsForSemester(grade, semester); track subject.subjectCode; let index = $index) {
                                <tr class="border-t border-[#e7e0ea] even:bg-[#fbfafc]">
                                  <td class="px-2 py-2 text-center">{{ subjectNumber(grade, index) }}</td>
                                  <td class="px-3 py-2 text-[#35293b]">
                                    <span class="block font-medium">{{ subject.subject }}</span>
                                    @if (subject.courseNames.length) {
                                      <span class="mt-0.5 block text-[0.62rem] leading-tight text-[#817589]">{{ subject.courseNames.join(' · ') }}</span>
                                    }
                                  </td>
                                  <td class="px-2 py-2 text-center">{{ typeScore(semesterScore(subject, semester), subject.subjectCode, grade.gradeLevel) }}</td>
                                  <td class="bg-[#fbf8fc] px-2 py-2 text-center font-semibold">{{ typeScore(subject.yearAverage, subject.subjectCode, grade.gradeLevel) }}</td>
                                  <td class="px-2 py-2 text-center">{{ trailingValue(subject.credit, subject.yearAverage, subject.subjectCode, grade.gradeLevel) }}</td>
                                </tr>
                              } @empty {
                                <tr><td colspan="5" class="px-5 py-8 text-center text-[#766d7d]">{{ t('transcript.noSemesterResults') }}</td></tr>
                              }
                            </tbody>
                          </table>
                        </div>
                      </section>
                    }
                    @if (grade.totalSubjectPages > 1) {
                      <footer class="no-print flex flex-wrap items-center justify-between gap-3 border-t border-[#e8e1eb] px-4 py-3 text-xs text-[#746a7b]">
                        <span>{{ subjectPageLabel(grade) }}</span>
                        <div class="flex gap-2">
                          <button type="button" class="rounded-md border border-[#d8cede] px-3 py-1.5 font-semibold text-[#32124f] disabled:opacity-40"
                            [disabled]="grade.subjectPage <= 1 || subjectPageLoading(year.academicYear, grade.gradeLevel)"
                            (click)="loadSubjectPage(year.academicYear, grade.gradeLevel, grade.subjectPage - 1)">{{ t('pagination.previous') }}</button>
                          <button type="button" class="rounded-md border border-[#d8cede] px-3 py-1.5 font-semibold text-[#32124f] disabled:opacity-40"
                            [disabled]="grade.subjectPage >= grade.totalSubjectPages || subjectPageLoading(year.academicYear, grade.gradeLevel)"
                            (click)="loadSubjectPage(year.academicYear, grade.gradeLevel, grade.subjectPage + 1)">{{ t('pagination.next') }}</button>
                        </div>
                      </footer>
                    }
                  }
                </section>
              }
            </div>

            <footer class="formal-footer mx-6 mb-6 border-t border-[#bfb2c7] pt-4 lg:mx-9 lg:mb-8">
              <div class="text-sm">
                <p class="font-bold text-[#32124f]">{{ activeTranscriptType() === 'ib' ? t('transcript.ibSummary') : t('transcript.academicStanding') }}</p>
                @if (activeTranscriptType() === 'international') {
                  <p class="mt-2">{{ t('transcript.cumulativeGpa') }} <strong class="ml-3">{{ formatGpa(record.overallGpa) }}</strong></p>
                } @else if (activeTranscriptType() === 'bilingual') {
                  <p class="mt-2">{{ t('transcript.overallResult') }} <strong class="ml-3">{{ tenPoint(record.overallAverage) }}</strong></p>
                } @else {
                  <p class="mt-2 text-[#6f6877]">{{ t('transcript.ibSummaryNote') }}</p>
                }
              </div>
              @if (activeTranscriptType() === 'international') {
                <div class="grading-scale mt-5 break-inside-avoid">
                  <p class="mb-2 text-sm font-bold text-[#32124f]">{{ t('transcript.gradingScale') }}</p>
                  <div class="overflow-hidden rounded border border-[#bfb2c7]">
                    <table class="w-full table-fixed border-collapse text-[0.62rem]">
                      <tbody>
                        <tr>
                          <th class="w-24 border-r border-[#d8cfdd] bg-[#f4eff6] px-2 py-1.5 text-left">{{ t('transcript.tenPointScale') }}</th>
                          @for (band of gradingScale; track band.range) { <td class="border-r border-[#e3dce7] px-1 py-1.5 text-center last:border-r-0">{{ band.range }}</td> }
                        </tr>
                        <tr class="border-t border-[#d8cfdd]">
                          <th class="border-r border-[#d8cfdd] bg-[#f4eff6] px-2 py-1.5 text-left">{{ t('transcript.letterGrade') }}</th>
                          @for (band of gradingScale; track band.range) { <td class="border-r border-[#e3dce7] px-1 py-1.5 text-center font-semibold last:border-r-0">{{ band.letter }}</td> }
                        </tr>
                        <tr class="border-t border-[#d8cfdd]">
                          <th class="border-r border-[#d8cfdd] bg-[#f4eff6] px-2 py-1.5 text-left">{{ t('transcript.gpaScale') }}</th>
                          @for (band of gradingScale; track band.range) { <td class="border-r border-[#e3dce7] px-1 py-1.5 text-center last:border-r-0">{{ band.gpa }}</td> }
                        </tr>
                      </tbody>
                    </table>
                  </div>
                </div>
              } @else if (activeTranscriptType() === 'ib') {
                <div class="grading-scale mt-5 grid grid-cols-7 overflow-hidden rounded border border-[#bfb2c7] text-center text-[0.68rem]">
                  @for (point of ibScale; track point.value) {
                    <div class="border-r border-[#d8cfdd] px-1 py-2 last:border-r-0"><strong class="block text-sm text-[#32124f]">{{ point.value }}</strong>{{ t(point.label) }}</div>
                  }
                </div>
              } @else {
                <p class="mt-5 text-xs leading-5 text-[#6f6877]">{{ t('transcript.bilingualScaleNote') }}</p>
              }
              <div class="mt-8 grid grid-cols-2 gap-12 text-xs">
                <div class="border-t border-[#8e8295] pt-2">{{ t('transcript.schoolOfficer') }}</div>
                <div class="border-t border-[#8e8295] pt-2">{{ t('transcript.signatureDate') }}</div>
              </div>
              <div class="letterhead-content mt-6 flex items-end justify-between gap-4 border-t border-[#e8e1eb] pt-3 text-[0.68rem] text-[#756b7c]">
                <p>The Olympia Schools · Trung Van, Nam Tu Liem, Hanoi, Vietnam</p>
                <p class="font-semibold">{{ t('transcript.types.' + activeTranscriptType() + '.documentTitle') }}</p>
              </div>
            </footer>
          } @else {
            <div class="px-8 py-16 text-center text-[#766d7d]">
              <p>{{ t('transcript.noStudentData') }}</p>
            </div>
          }
        </article>
        @if (activeTranscriptType() === 'international') {
        <section class="no-print mt-6 rounded-xl border border-[#ddd3e2] bg-[#fbf8fc] p-5 lg:p-7" aria-labelledby="transcript-calculation-title">
          <div class="max-w-3xl">
            <p class="text-xs font-bold uppercase tracking-[0.14em] text-[#8a6811]">{{ t('transcript.calculationEyebrow') }}</p>
            <h3 id="transcript-calculation-title" class="mt-1 text-xl font-semibold text-[#32124f]">{{ t('transcript.calculationTitle') }}</h3>
            <p class="mt-2 text-sm text-[#6f6877]">{{ t('transcript.calculationDescription') }}</p>
          </div>
          <div class="mt-5 grid gap-4 md:grid-cols-2">
            <article class="rounded-lg border border-[#e2d9e6] bg-white p-4">
              <h4 class="font-semibold text-[#32124f]">{{ t('transcript.yearResultCalculation') }}</h4>
              <p class="mt-2 rounded bg-[#f4eef7] px-3 py-2 font-mono text-sm font-semibold text-[#4f226b]">YR = ROUND((S1F + 2 × S2F) ÷ 3, 1)</p>
              <p class="mt-2 text-sm leading-6 text-[#706477]">{{ t('transcript.yearResultCalculationHelp') }}</p>
            </article>
            <article class="rounded-lg border border-[#e2d9e6] bg-white p-4">
              <h4 class="font-semibold text-[#32124f]">{{ t('transcript.creditCalculation') }}</h4>
              <p class="mt-2 rounded bg-[#f4eef7] px-3 py-2 font-mono text-sm font-semibold text-[#4f226b]">{{ t('transcript.creditFormula') }}</p>
              <p class="mt-2 text-sm leading-6 text-[#706477]">{{ t('transcript.creditCalculationHelp') }}</p>
            </article>
            <article class="rounded-lg border border-[#e2d9e6] bg-white p-4">
              <h4 class="font-semibold text-[#32124f]">{{ t('transcript.annualGpaCalculation') }}</h4>
              <p class="mt-2 rounded bg-[#f4eef7] px-3 py-2 font-mono text-sm font-semibold text-[#4f226b]">GPA = ROUND(Σ(GPA(YR) × Credit) ÷ Σ(Credit), 2)</p>
              <p class="mt-2 text-sm leading-6 text-[#706477]">{{ t('transcript.annualGpaCalculationHelp') }}</p>
            </article>
            <article class="rounded-lg border border-[#e2d9e6] bg-white p-4">
              <h4 class="font-semibold text-[#32124f]">{{ t('transcript.academicGpaCalculation') }}</h4>
              <p class="mt-2 rounded bg-[#f4eef7] px-3 py-2 font-mono text-sm font-semibold text-[#4f226b]">{{ academicGpaBreakdown(record) }}</p>
              <p class="mt-2 text-sm leading-6 text-[#706477]">{{ t('transcript.academicGpaCalculationHelp') }}</p>
            </article>
          </div>
        </section>
        }
      } @else {
        <section class="no-print mt-8 overflow-hidden rounded-xl border border-[#ded6e3] bg-white shadow-[0_12px_38px_rgba(50,18,79,0.06)]">
          <header class="flex flex-wrap items-end justify-between gap-4 border-b border-[#e8e1eb] px-5 py-5 lg:px-7">
            <div>
              <h3 class="text-lg font-semibold text-[#32124f]">{{ t('transcript.studentDirectory') }}</h3>
              <p class="mt-1 text-sm text-[#756b7c]">{{ t('transcript.studentDirectoryHelp') }}</p>
            </div>
            <p class="text-sm font-semibold text-[#6d3a91]">{{ directoryTotal() }} {{ t('transcript.studentsLabel') }}</p>
          </header>
          <div class="overflow-x-auto">
            <table class="w-full min-w-[680px] border-collapse text-sm">
              <thead class="bg-[#faf8fb] text-left text-xs uppercase tracking-[0.1em] text-[#756a7c]">
                <tr>
                  <th class="px-5 py-3 lg:px-7">{{ t('transcript.student') }}</th>
                  <th class="px-4 py-3">{{ t('transcript.currentGrade') }}</th>
                  <th class="px-4 py-3">{{ t('transcript.homeroom') }}</th>
                  <th class="px-5 py-3 text-right lg:px-7"><span class="sr-only">{{ t('transcript.openTranscript') }}</span></th>
                </tr>
              </thead>
              <tbody class="divide-y divide-[#ece6ef]">
                @for (student of directoryStudents(); track student.id) {
                  <tr class="transition hover:bg-[#fcfafc]">
                    <td class="px-5 py-4 lg:px-7">
                      <a [routerLink]="['/transcripts/student', student.id]" class="flex items-center gap-3">
                        <span class="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-[#f1eaf4] text-xs font-bold text-[#5b287d]">{{ initials(student.fullName) }}</span>
                        <span>
                          <span class="block font-semibold text-[#32124f]">{{ student.fullName }}</span>
                          <span class="mt-0.5 block text-xs text-[#817589]">{{ student.sisUserId }}</span>
                        </span>
                      </a>
                    </td>
                    <td class="px-4 py-4 text-[#65596d]">{{ student.gradeLevel || '—' }}</td>
                    <td class="px-4 py-4 text-[#65596d]">{{ student.homeroom || '—' }}</td>
                    <td class="px-5 py-4 text-right lg:px-7">
                      <a [routerLink]="['/transcripts/student', student.id]" class="font-semibold text-[#6d3a91] hover:underline">{{ t('transcript.openTranscript') }} →</a>
                    </td>
                  </tr>
                } @empty {
                  <tr><td colspan="4" class="px-6 py-16 text-center text-[#766d7d]">{{ directoryLoading() ? t('transcript.loadingStudents') : t('transcript.noStudentMatches') }}</td></tr>
                }
              </tbody>
            </table>
          </div>
          @if (!directoryLoading() && directoryTotal() > 0) {
            <footer class="flex flex-wrap items-center justify-between gap-3 border-t border-[#e8e1eb] px-5 py-4 text-sm text-[#746a7b] lg:px-7">
              <span>{{ directoryPageLabel() }}</span>
              <div class="flex gap-2">
                <button type="button" class="rounded-md border border-[#d8cede] px-4 py-2 font-semibold text-[#32124f] disabled:opacity-40" [disabled]="directoryPage() <= 1" (click)="loadDirectory(directoryPage() - 1)">{{ t('pagination.previous') }}</button>
                <button type="button" class="rounded-md border border-[#d8cede] px-4 py-2 font-semibold text-[#32124f] disabled:opacity-40" [disabled]="directoryPage() >= directoryPages()" (click)="loadDirectory(directoryPage() + 1)">{{ t('pagination.next') }}</button>
              </div>
            </footer>
          }
        </section>
      }
    </section>
  `,
  styles: `
    .meta-label { display: block; margin-bottom: .2rem; color: #817589; font-size: .65rem; font-weight: 700; letter-spacing: .09em; text-transform: uppercase; }
    @media print {
      :host { display: block; }
      .transcript-sheet { width: 100%; border: 0 !important; border-radius: 0 !important; box-shadow: none !important; }
      .letterhead-content { display: none !important; }
      .transcript-header { padding: 0 0 4mm !important; }
      .transcript-years { padding: 6mm 0 !important; }
      .year-block { break-inside: avoid; page-break-inside: avoid; }
      .year-block table { font-size: 7.4pt !important; }
      .year-block th { padding-top: 1.5mm !important; padding-bottom: 1.5mm !important; }
      .year-block td { padding-top: 1mm !important; padding-bottom: 1mm !important; }
      .formal-footer { margin: 2mm 0 0 !important; }
      .grading-scale { break-inside: avoid; page-break-inside: avoid; }
      .grading-scale table { font-size: 5.8pt !important; }
    }
  `,
})
export class StudentTranscriptComponent {
  private readonly service = inject(TranscriptsService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly searchTerms = new Subject<string>();
  readonly languageService = inject(LanguageService);
  readonly transcript = signal<SchoolTranscript | null>(null);
  readonly students = signal<TranscriptStudent[]>([]);
  readonly selectedStudentId = signal<number | null>(null);
  readonly selectedYears = signal<string[]>([]);
  readonly selectedSemester = signal<TranscriptSemesterFilter>('all');
  readonly activeTranscriptType = signal<TranscriptType>('international');
  readonly transcriptTypes: { id: TranscriptType }[] = [
    { id: 'bilingual' },
    { id: 'international' },
    { id: 'ib' },
  ];
  readonly scoreScale = signal<TranscriptScoreScale>('letter');
  readonly scoreScales: TranscriptScoreScale[] = ['percentage', 'gpa', 'letter'];
  readonly searchTerm = signal('');
  readonly searchOpen = signal(false);
  readonly searching = signal(false);
  readonly activeResult = signal(-1);
  readonly loading = signal(false);
  readonly error = signal(false);
  readonly loadingSubjectPage = signal('');
  readonly directoryStudents = signal<TranscriptStudent[]>([]);
  readonly directoryLoading = signal(false);
  readonly directoryPage = signal(1);
  readonly directoryPages = signal(0);
  readonly directoryTotal = signal(0);
  readonly gradingScale = [
    { range: '9.5–10.0', letter: 'A+', gpa: '4.2' },
    { range: '9.0–9.4', letter: 'A', gpa: '4.0' },
    { range: '8.0–8.9', letter: 'A-', gpa: '3.7' },
    { range: '7.5–7.9', letter: 'B+', gpa: '3.3' },
    { range: '7.0–7.4', letter: 'B', gpa: '3.0' },
    { range: '6.5–6.9', letter: 'B-', gpa: '2.7' },
    { range: '6.0–6.4', letter: 'C+', gpa: '2.3' },
    { range: '5.5–5.9', letter: 'C', gpa: '2.0' },
    { range: '5.0–5.4', letter: 'C-', gpa: '1.7' },
    { range: '4.5–4.9', letter: 'D+', gpa: '1.3' },
    { range: '4.0–4.4', letter: 'D', gpa: '1.0' },
    { range: '<4.0', letter: 'D-', gpa: '0.7' },
  ];
  readonly ibScale = [
    { value: 7, label: 'transcript.ibBands.exceptional' },
    { value: 6, label: 'transcript.ibBands.excellent' },
    { value: 5, label: 'transcript.ibBands.veryGood' },
    { value: 4, label: 'transcript.ibBands.good' },
    { value: 3, label: 'transcript.ibBands.mediocre' },
    { value: 2, label: 'transcript.ibBands.poor' },
    { value: 1, label: 'transcript.ibBands.veryPoor' },
  ];

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
        if (!this.selectedStudentId()) this.loadDirectory(1);
      },
    });

    this.route.paramMap.subscribe((params) => {
      const studentId = Number(params.get('studentId'));
      if (Number.isInteger(studentId) && studentId > 0) {
        this.selectedStudentId.set(studentId);
        this.loadTranscript(studentId);
      } else {
        this.selectedStudentId.set(null);
        this.transcript.set(null);
        this.searchTerm.set('');
        this.error.set(false);
        this.loadDirectory(1);
      }
    });
  }

  private loadTranscript(studentId: number): void {
    this.loading.set(true);
    this.error.set(false);
    this.service.getOverall(studentId, 1, 10).subscribe({
      next: (record) => {
        this.transcript.set(record);
        this.searchTerm.set(`${record.studentName || ''} · ${record.sisUserId || ''}`);
        this.selectedYears.set(record.years.slice(-1).map((year) => year.academicYear));
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
    this.searchOpen.set(false);
    this.students.set([]);
    void this.router.navigate(['/transcripts/student', student.id]);
  }

  loadDirectory(page: number): void {
    this.directoryLoading.set(true);
    this.service.getStudentDirectory(page, 20, this.searchTerm()).subscribe({
      next: (response) => {
        this.directoryStudents.set(response.items);
        this.directoryPage.set(response.page);
        this.directoryPages.set(response.totalPages);
        this.directoryTotal.set(response.totalCount);
        this.directoryLoading.set(false);
      },
      error: () => {
        this.directoryStudents.set([]);
        this.directoryLoading.set(false);
      },
    });
  }

  directoryPageLabel(): string {
    return this.t('transcript.directoryPageSummary')
      .replace('{page}', String(this.directoryPage()))
      .replace('{pages}', String(this.directoryPages()))
      .replace('{total}', String(this.directoryTotal()));
  }

  initials(name: string): string {
    return name.trim().split(/\s+/).slice(-2).map((part) => part[0]?.toUpperCase()).join('') || 'S';
  }

  toggleYear(academicYear: string): void {
    this.selectedYears.update((years) => {
      if (years.includes(academicYear) && years.length === 1) return years;
      return years.includes(academicYear)
        ? years.filter((year) => year !== academicYear)
        : [...years, academicYear];
    });
  }

  changeSemester(event: Event): void {
    this.selectedSemester.set((event.target as HTMLSelectElement).value as TranscriptSemesterFilter);
  }

  visibleSemesters(): TranscriptSemester[] {
    const selected = this.selectedSemester();
    return selected === 'all' ? ['semester1', 'semester2'] : [selected];
  }

  semesterLabel(semester: TranscriptSemester): string {
    return this.t(`transcript.${semester}`);
  }

  semesterScore(subject: TranscriptSubject, semester: TranscriptSemester): number | null | undefined {
    return semester === 'semester1' ? subject.semester1Final : subject.semester2Final;
  }

  subjectsForSemester(grade: SchoolTranscriptGrade, semester: TranscriptSemester): TranscriptSubject[] {
    return grade.subjects.filter((subject) => this.semesterScore(subject, semester) != null);
  }

  subjectNumber(grade: SchoolTranscriptGrade, index: number): number {
    return (grade.subjectPage - 1) * grade.subjectPageSize + index + 1;
  }

  loadSubjectPage(academicYear: string, gradeLevel: string, page: number): void {
    const studentId = this.selectedStudentId();
    if (!studentId) return;
    const key = this.subjectPageKey(academicYear, gradeLevel);
    this.loadingSubjectPage.set(key);
    this.service.getOverall(studentId, page, 10).subscribe({
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

  subjectPageLabel(grade: SchoolTranscriptGrade): string {
    return this.t('pagination.pageOf')
      .replace('{page}', String(grade.subjectPage))
      .replace('{pages}', String(grade.totalSubjectPages))
      .replace('{total}', String(grade.totalSubjects));
  }

  private subjectPageKey(academicYear: string, gradeLevel: string): string {
    return `${academicYear}\u0000${gradeLevel}`;
  }

  displayedYears(record: SchoolTranscript) {
    const selected = new Set(this.selectedYears());
    return record.years.filter((year) => selected.has(year.academicYear));
  }

  reload(): void {
    const studentId = this.selectedStudentId();
    if (studentId) this.loadTranscript(studentId);
  }

  selectTranscriptType(type: TranscriptType): void {
    this.activeTranscriptType.set(type);
    if (type === 'bilingual') this.scoreScale.set('percentage');
    if (type === 'international' && this.scoreScale() === 'percentage') this.scoreScale.set('letter');
  }

  exportPdf(type: TranscriptType = this.activeTranscriptType()): void {
    this.selectTranscriptType(type);
    const studentId = this.selectedStudentId();
    const paginatedRecord = this.transcript();
    if (!studentId || !paginatedRecord) return;
    this.loading.set(true);
    this.service.getOverall(studentId).subscribe({
      next: (completeRecord) => {
        this.loading.set(false);
        this.transcript.set(completeRecord);
        this.openPrintDialog(type, paginatedRecord);
      },
      error: () => {
        this.loading.set(false);
        this.openPrintDialog(type);
      },
    });
  }

  private openPrintDialog(type: TranscriptType, restoreRecord?: SchoolTranscript): void {
    const currentTitle = document.title;
    const record = this.transcript();
    document.title = `${record?.sisUserId || 'student'}-${type}-transcript`;
    // Give Angular one frame to render the requested template before opening the print dialog.
    requestAnimationFrame(() => requestAnimationFrame(() => {
      window.print();
      setTimeout(() => {
        document.title = currentTitle;
        if (restoreRecord) this.transcript.set(restoreRecord);
      }, 250);
    }));
  }

  score(value?: number | null): string {
    return formatTranscriptScore(value, this.scoreScale());
  }

  typeScore(value?: number | null, subjectCode = '', gradeLevel = ''): string {
    if (value == null || !Number.isFinite(value)) return '—';
    if (this.activeTranscriptType() === 'bilingual') {
      if (this.isPassFailSubject(subjectCode)) return value >= 50 ? this.t('transcript.passed') : this.t('transcript.notPassed');
      return this.tenPoint(value);
    }
    if (this.activeTranscriptType() === 'ib') {
      return this.isIbGrade(gradeLevel) ? String(this.ibPoint(value)) : formatTranscriptScore(value, 'letter');
    }
    return this.score(value);
  }

  tenPoint(value?: number | null): string {
    if (value == null || !Number.isFinite(value)) return '—';
    const converted = value > 10 ? value / 10 : value;
    return converted.toFixed(1);
  }

  trailingValue(credit: number, score?: number | null, subjectCode = '', gradeLevel = ''): string {
    if (this.activeTranscriptType() === 'international') return this.formatCredit(credit);
    if (this.activeTranscriptType() === 'ib') return this.isIbGrade(gradeLevel) ? '—' : this.formatCredit(credit);
    if (score == null) return '—';
    return this.isPassFailSubject(subjectCode)
      ? (score >= 50 ? this.t('transcript.passed') : this.t('transcript.notPassed'))
      : this.t('transcript.graded');
  }

  isIbGrade(gradeLevel: string): boolean {
    const grade = Number(gradeLevel);
    return Number.isFinite(grade) && grade >= 11;
  }

  hasScienceMappingIssue(record: SchoolTranscript): boolean {
    return record.years.some((year) => year.grades.some((grade) => grade.subjects.some((subject) =>
      this.normalizeSubjectCode(subject.subjectCode) === 'science')));
  }

  private ibPoint(value: number): number {
    if (value >= 80) return 7;
    if (value >= 70) return 6;
    if (value >= 60) return 5;
    if (value >= 50) return 4;
    if (value >= 40) return 3;
    if (value >= 30) return 2;
    return 1;
  }

  private isPassFailSubject(subjectCode: string): boolean {
    const normalized = this.normalizeSubjectCode(subjectCode);
    return ['music', 'physical education', 'fine arts', 'service learning', 'cas'].includes(normalized);
  }

  private normalizeSubjectCode(subjectCode: string): string {
    return subjectCode.trim().toLowerCase().replace(/[_-]+/g, ' ').replace(/\s+/g, ' ');
  }

  formatGpa(value?: number | null): string {
    return value == null ? '—' : value.toFixed(2);
  }

  formatCredit(value: number): string {
    return Number.isInteger(value) ? value.toFixed(0) : value.toFixed(1);
  }

  academicGpaBreakdown(record: SchoolTranscript): string {
    const annualGpas = record.years
      .flatMap((year) => year.grades)
      .filter((grade) => {
        const gradeLevel = Number(grade.gradeLevel);
        return Number.isInteger(gradeLevel) && gradeLevel >= 9 && gradeLevel <= 12 && grade.gpa != null;
      })
      .map((grade) => grade.gpa!.toFixed(2));
    if (!annualGpas.length) return this.t('transcript.notAvailable');
    return `ROUND((${annualGpas.join(' + ')}) ÷ ${annualGpas.length}, 2) = ${this.formatGpa(record.overallGpa)}`;
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
