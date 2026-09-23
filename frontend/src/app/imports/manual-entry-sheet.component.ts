import { Component, EventEmitter, HostListener, inject, Input, OnDestroy, OnInit, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { catchError, debounceTime, distinctUntilChanged, finalize, of, Subject, switchMap, takeUntil, tap } from 'rxjs';
import { LanguageService } from '../core/language/language.service';
import { ManualEntryOptions, ManualEntryService, ManualEntryStudent } from './manual-entry.service';
import { ImportModuleField, ImportModuleFieldsService } from './import-module-fields.service';

const localDateTimeNow = (): string => {
  const now = new Date();
  return new Date(now.getTime() - now.getTimezoneOffset() * 60_000).toISOString().slice(0, 16);
};

@Component({
  selector: 'app-manual-entry-sheet',
  imports: [FormsModule],
  styles: `
    :host { position: fixed; inset: 0; z-index: 70; }
    .sheet-backdrop { animation: fade-in 160ms ease-out; }
    .sheet-panel { animation: slide-in 220ms ease-out; }
    @keyframes fade-in { from { opacity: 0; } to { opacity: 1; } }
    @keyframes slide-in { from { transform: translateX(100%); } to { transform: translateX(0); } }
  `,
  template: `
    <div class="sheet-backdrop absolute inset-0 bg-[#1d1025]/35" (click)="close.emit()" aria-hidden="true"></div>
    <aside
      class="sheet-panel absolute inset-y-0 right-0 flex w-full max-w-xl flex-col bg-white shadow-[-18px_0_45px_rgba(50,18,79,0.18)]"
      role="dialog"
      aria-modal="true"
      [attr.aria-label]="languageService.t('imports.manual.title')"
    >
      <header class="flex items-start justify-between gap-4 border-b border-[#ebe6ef] px-6 py-5">
        <div>
          <p class="text-xs font-semibold uppercase tracking-[0.18em] text-[#8a6811]">
            {{ languageService.t('imports.manual.eyebrow') }}
          </p>
          <h2 class="mt-2 text-2xl font-semibold text-[#32124f]">
            {{ languageService.t(moduleTitleKey()) }}
          </h2>
          <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('imports.manual.hint') }}</p>
        </div>
        <button
          class="grid h-10 w-10 shrink-0 place-items-center rounded-full border border-[#ded6e4] text-xl text-[#5d5265] hover:bg-[#f7f3fa]"
          type="button"
          (click)="close.emit()"
          [attr.aria-label]="languageService.t('imports.manual.close')"
        >×</button>
      </header>

      <div class="flex-1 overflow-y-auto px-6 py-6">
        @if (loading()) {
          <p class="rounded-md bg-[#faf9f6] p-5 text-sm text-[#6f6877]">
            {{ languageService.t('imports.manual.loading') }}
          </p>
        } @else if (loadError()) {
          <p class="rounded-md bg-red-50 p-4 text-sm text-red-700">{{ loadError() }}</p>
        } @else {
          <form id="manual-entry-form" class="space-y-5" (ngSubmit)="submit()">
            <div class="relative block">
              <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.student') }}</span>
              <div class="relative mt-2">
                <input
                  class="w-full rounded-md border border-[#d8d0df] bg-white py-3 pl-10 pr-10 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10"
                  type="search"
                  autocomplete="off"
                  role="combobox"
                  aria-autocomplete="list"
                  aria-controls="manual-student-results"
                  [attr.aria-expanded]="studentSearchOpen()"
                  [attr.aria-activedescendant]="activeStudentIndex() >= 0 ? 'manual-student-option-' + activeStudentIndex() : null"
                  [attr.placeholder]="languageService.t('imports.manual.searchStudent')"
                  [ngModel]="studentSearchText()"
                  [ngModelOptions]="{ standalone: true }"
                  (ngModelChange)="onStudentSearch($event)"
                  (focus)="openStudentSearch()"
                  (blur)="closeStudentSearchSoon()"
                  (keydown)="onStudentSearchKeydown($event)"
                />
                <svg class="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-[#7c7284]" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
                  <circle cx="11" cy="11" r="7" /><path d="m20 20-3.5-3.5" />
                </svg>
                @if (studentId) {
                  <button class="absolute right-3 top-1/2 grid h-6 w-6 -translate-y-1/2 place-items-center rounded-full text-[#7c7284] hover:bg-[#f3eff6] hover:text-[#32124f]" type="button" (mousedown)="$event.preventDefault()" (click)="clearStudent()" [attr.aria-label]="languageService.t('imports.manual.clearStudent')">×</button>
                }
              </div>

              @if (studentSearchOpen()) {
                <div id="manual-student-results" class="absolute z-20 mt-1 max-h-72 w-full overflow-y-auto rounded-md border border-[#d8d0df] bg-white py-1 shadow-[0_12px_30px_rgba(50,18,79,0.16)]" role="listbox">
                  @if (studentSearchText().trim().length < 2) {
                    <p class="px-4 py-3 text-sm text-[#7c7284]">{{ languageService.t('imports.manual.searchStudentHint') }}</p>
                  } @else if (studentSearchLoading()) {
                    <p class="px-4 py-3 text-sm text-[#7c7284]">{{ languageService.t('imports.manual.searchingStudents') }}</p>
                  } @else if (studentSearchError()) {
                    <p class="px-4 py-3 text-sm text-red-700">{{ languageService.t('imports.manual.studentSearchError') }}</p>
                  } @else if (!studentSearchResults().length) {
                    <p class="px-4 py-3 text-sm text-[#7c7284]">{{ languageService.t('imports.manual.noStudentsFound') }}</p>
                  } @else {
                    @for (student of studentSearchResults(); track student.id; let index = $index) {
                      <button
                        [id]="'manual-student-option-' + index"
                        class="block w-full px-4 py-3 text-left transition hover:bg-[#f7f3fa]"
                        [class.bg-[#f7f3fa]]="activeStudentIndex() === index"
                        type="button"
                        role="option"
                        [attr.aria-selected]="student.id === studentId"
                        (mouseenter)="activeStudentIndex.set(index)"
                        (mousedown)="$event.preventDefault()"
                        (click)="selectStudent(student)"
                      >
                        <span class="block text-sm font-semibold text-[#32124f]">{{ student.fullName }}</span>
                        <span class="mt-0.5 block text-xs text-[#7c7284]">{{ student.sisUserId }} · {{ languageService.t('imports.manual.grade') }} {{ student.gradeLevel || '-' }} · {{ student.homeroom || '-' }}</span>
                      </button>
                    }
                  }
                </div>
              }
              <span class="mt-2 block text-xs text-[#7c7284]">{{ languageService.t('imports.manual.studentHint') }}</span>
            </div>

            @if (moduleId === 'student-behaviours') {
              <label class="block">
                <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.incidentDate') }}</span>
                <input
                  class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10"
                  type="date"
                  name="incidentDate"
                  required
                  [(ngModel)]="incidentDate"
                />
              </label>
              <label class="block">
                <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.behaviourType') }}</span>
                <select
                  class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10"
                  name="behaviourType"
                  required
                  [(ngModel)]="behaviourType"
                >
                  <option value="">{{ languageService.t('imports.manual.selectBehaviour') }}</option>
                  <optgroup [label]="languageService.t('imports.manual.positiveBehaviours')">
                    @for (type of options()?.positiveBehaviourTypes ?? []; track type) {
                      <option [value]="type">{{ type }}</option>
                    }
                  </optgroup>
                  <optgroup [label]="languageService.t('imports.manual.concerningBehaviours')">
                    @for (type of options()?.concerningBehaviourTypes ?? []; track type) {
                      <option [value]="type">{{ type }}</option>
                    }
                  </optgroup>
                </select>
              </label>
              <label class="block">
                <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.notes') }}</span>
                <textarea
                  class="mt-2 min-h-28 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10"
                  name="notes"
                  maxlength="1000"
                  [(ngModel)]="notes"
                  [placeholder]="languageService.t('imports.manual.optional')"
                ></textarea>
              </label>
            } @else if (moduleId === 'student-projects') {
              <label class="block">
                <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.projectName') }}</span>
                <input
                  class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10"
                  name="projectName"
                  maxlength="255"
                  required
                  [(ngModel)]="projectName"
                />
              </label>
              <div class="grid gap-5 sm:grid-cols-2">
                <label class="block">
                  <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.subject') }}</span>
                  <select
                    class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10"
                    name="subject"
                    required
                    [(ngModel)]="subject"
                  >
                    <option value="">{{ languageService.t('imports.manual.selectSubject') }}</option>
                    @for (value of options()?.projectSubjects ?? []; track value) {
                      <option [value]="value">{{ value }}</option>
                    }
                  </select>
                </label>
                <label class="block">
                  <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.status') }}</span>
                  <select
                    class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10"
                    name="status"
                    required
                    [(ngModel)]="status"
                  >
                    <option value="">{{ languageService.t('imports.manual.selectStatus') }}</option>
                    @for (value of options()?.projectStatuses ?? []; track value) {
                      <option [value]="value">{{ value }}</option>
                    }
                  </select>
                </label>
              </div>
              <label class="block">
                <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.teacherNotes') }}</span>
                <textarea
                  class="mt-2 min-h-28 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10"
                  name="teacherNotes"
                  maxlength="1000"
                  [(ngModel)]="teacherNotes"
                  [placeholder]="languageService.t('imports.manual.optional')"
                ></textarea>
              </label>
            } @else if (moduleId === 'student-bus-attendance') {
              <label class="block">
                <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.attendanceDate') }}</span>
                <input class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" type="date" name="attendanceDate" required [(ngModel)]="attendanceDate" />
              </label>
              <div class="grid gap-5 sm:grid-cols-2">
                <label class="block">
                  <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.tripDirection') }}</span>
                  <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" name="tripDirection" required [(ngModel)]="tripDirection">
                    <option value="">{{ languageService.t('imports.manual.selectTripDirection') }}</option>
                    @for (value of options()?.busTripDirections ?? []; track value) { <option [value]="value">{{ busOptionLabel('direction', value) }}</option> }
                  </select>
                </label>
                <label class="block">
                  <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.violationType') }}</span>
                  <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" name="violationType" required [(ngModel)]="violationType">
                    <option value="">{{ languageService.t('imports.manual.selectViolationType') }}</option>
                    @for (value of options()?.busViolationTypes ?? []; track value) { <option [value]="value">{{ busOptionLabel('violation', value) }}</option> }
                  </select>
                </label>
              </div>
              <div class="grid gap-5 sm:grid-cols-2">
                <label class="block">
                  <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.busCode') }}</span>
                  <input class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" name="busCode" maxlength="100" [(ngModel)]="busCode" [placeholder]="languageService.t('imports.manual.optional')" />
                </label>
                <label class="block">
                  <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.routeName') }}</span>
                  <input class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" name="routeName" maxlength="255" [(ngModel)]="routeName" [placeholder]="languageService.t('imports.manual.optional')" />
                </label>
              </div>
              <label class="block">
                <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.notes') }}</span>
                <textarea class="mt-2 min-h-28 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" name="busNotes" maxlength="1000" [(ngModel)]="notes" [placeholder]="languageService.t('imports.manual.optional')"></textarea>
              </label>
            } @else if (moduleId === 'student-mental-health') {
              <div class="grid gap-5 sm:grid-cols-2">
                <label class="block">
                  <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.recordDate') }}</span>
                  <input class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" type="date" name="recordDate" required [(ngModel)]="recordDate" />
                </label>
                <label class="block">
                  <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.academicYear') }}</span>
                  <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" name="academicYear" required [(ngModel)]="academicYear">
                    <option value="">{{ languageService.t('imports.manual.selectAcademicYear') }}</option>
                    @for (year of options()?.academicYears ?? []; track year) { <option [value]="year">{{ year }}</option> }
                  </select>
                </label>
              </div>
              <div class="grid gap-5 sm:grid-cols-2">
                <label class="block">
                  <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.concernType') }}</span>
                  <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" name="concernType" required [(ngModel)]="concernType">
                    <option value="">{{ languageService.t('imports.manual.selectConcernType') }}</option>
                    @for (value of options()?.mentalHealthConcernTypes ?? []; track value) { <option [value]="value">{{ mentalOptionLabel('concern', value) }}</option> }
                  </select>
                </label>
                <label class="block">
                  <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.riskLevel') }}</span>
                  <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" name="riskLevel" required [(ngModel)]="riskLevel">
                    <option value="">{{ languageService.t('imports.manual.selectRiskLevel') }}</option>
                    @for (value of options()?.mentalHealthRiskLevels ?? []; track value) { <option [value]="value">{{ mentalOptionLabel('risk', value) }}</option> }
                  </select>
                </label>
              </div>
              <label class="block">
                <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.notes') }}</span>
                <textarea class="mt-2 min-h-28 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" name="mentalNotes" maxlength="1000" [(ngModel)]="notes" [placeholder]="languageService.t('imports.manual.optional')"></textarea>
              </label>
            } @else {
              <label class="block">
                <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.visitDate') }}</span>
                <input class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" type="datetime-local" name="visitDate" required [(ngModel)]="visitDate" />
              </label>
              <label class="block">
                <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.classification') }}</span>
                <input class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" name="classification" maxlength="100" [(ngModel)]="classification" [placeholder]="languageService.t('imports.manual.optional')" />
              </label>
              <label class="block">
                <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.diagnosis') }}</span>
                <textarea class="mt-2 min-h-24 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" name="diagnosis" maxlength="1000" [(ngModel)]="diagnosis" [placeholder]="languageService.t('imports.manual.optional')"></textarea>
              </label>
              <label class="block">
                <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.symptoms') }}</span>
                <textarea class="mt-2 min-h-24 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" name="symptoms" maxlength="1000" required [(ngModel)]="symptoms"></textarea>
              </label>
              <label class="block">
                <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.treatment') }}</span>
                <textarea class="mt-2 min-h-24 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" name="treatment" maxlength="1000" [(ngModel)]="treatment" [placeholder]="languageService.t('imports.manual.optional')"></textarea>
              </label>
              <label class="block">
                <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.medication') }}</span>
                <input class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" name="medication" maxlength="500" [(ngModel)]="medication" [placeholder]="languageService.t('imports.manual.optional')" />
              </label>
              <label class="block">
                <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.notes') }}</span>
                <textarea class="mt-2 min-h-24 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" name="healthNotes" maxlength="1000" [(ngModel)]="notes" [placeholder]="languageService.t('imports.manual.optional')"></textarea>
              </label>
              <label class="flex items-center gap-3 rounded-md border border-[#e5dfe8] bg-[#faf9fb] px-4 py-3 text-sm font-semibold text-[#32124f]">
                <input class="h-4 w-4 accent-[#5d2d7b]" type="checkbox" name="notificationSent" [(ngModel)]="notificationSent" />
                {{ languageService.t('imports.manual.notificationSent') }}
              </label>
            }

            @if (customFields().length) {
              <div class="space-y-5 border-t border-[#ebe6ef] pt-5">
                <p class="text-xs font-semibold uppercase tracking-[0.12em] text-[#8a6811]">
                  {{ languageService.t('imports.designer.customSection') }}
                </p>
                @for (field of customFields(); track field.id) {
                  <label class="block">
                    <span class="text-sm font-semibold text-[#32124f]">
                      {{ field.label }}@if (field.required) { <span class="text-[#a9485d]">*</span> }
                    </span>
                    @switch (field.fieldType) {
                      @case ('textarea') {
                        <textarea class="mt-2 min-h-24 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" [name]="'cf_' + field.fieldKey" [(ngModel)]="customValues[field.fieldKey]"></textarea>
                      }
                      @case ('select') {
                        <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" [name]="'cf_' + field.fieldKey" [(ngModel)]="customValues[field.fieldKey]">
                          <option value="">{{ languageService.t('imports.manual.optional') }}</option>
                          @for (option of field.options; track option) { <option [value]="option">{{ option }}</option> }
                        </select>
                      }
                      @case ('checkbox') {
                        <span class="mt-2 flex items-center gap-2">
                          <input type="checkbox" [name]="'cf_' + field.fieldKey" [(ngModel)]="customChecks[field.fieldKey]" />
                          <span class="text-sm text-[#6f6877]">{{ languageService.t('imports.designer.checkboxYes') }}</span>
                        </span>
                      }
                      @case ('number') {
                        <input type="number" class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" [name]="'cf_' + field.fieldKey" [(ngModel)]="customValues[field.fieldKey]" />
                      }
                      @case ('date') {
                        <input type="date" class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" [name]="'cf_' + field.fieldKey" [(ngModel)]="customValues[field.fieldKey]" />
                      }
                      @default {
                        <input class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" [name]="'cf_' + field.fieldKey" [(ngModel)]="customValues[field.fieldKey]" />
                      }
                    }
                  </label>
                }
              </div>
            }

            @if (submitError()) {
              <p class="rounded-md bg-red-50 p-4 text-sm text-red-700">{{ submitError() }}</p>
            }
          </form>
        }
      </div>

      <footer class="flex items-center justify-end gap-3 border-t border-[#ebe6ef] bg-[#faf9f6] px-6 py-4">
        <button class="rounded-md border border-[#d8d0df] px-5 py-2.5 text-sm font-semibold text-[#32124f] hover:bg-white" type="button" (click)="close.emit()">
          {{ languageService.t('imports.manual.cancel') }}
        </button>
        <button
          class="rounded-md bg-[#32124f] px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-[#4b2070] disabled:cursor-not-allowed disabled:opacity-45"
          type="submit"
          form="manual-entry-form"
          [disabled]="loading() || saving() || !canSubmit()"
        >
          {{ saving() ? languageService.t('imports.manual.saving') : languageService.t('imports.manual.save') }}
        </button>
      </footer>
    </aside>
  `,
})
export class ManualEntrySheetComponent implements OnInit, OnDestroy {
  @Input({ required: true }) moduleId!: string;
  @Output() readonly close = new EventEmitter<void>();
  @Output() readonly saved = new EventEmitter<string>();

  private readonly manualEntryService = inject(ManualEntryService);
  private readonly fieldsService = inject(ImportModuleFieldsService);
  readonly languageService = inject(LanguageService);

  readonly options = signal<ManualEntryOptions | null>(null);
  readonly customFields = signal<ImportModuleField[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly submitError = signal<string | null>(null);
  readonly studentSearchText = signal('');
  readonly studentSearchResults = signal<ManualEntryStudent[]>([]);
  readonly studentSearchLoading = signal(false);
  readonly studentSearchError = signal(false);
  readonly studentSearchOpen = signal(false);
  readonly activeStudentIndex = signal(-1);

  private readonly studentSearchInput = new Subject<string>();
  private readonly destroyed = new Subject<void>();

  customValues: Record<string, string> = {};
  customChecks: Record<string, boolean> = {};

  studentId: number | null = null;
  selectedStudent: ManualEntryStudent | null = null;
  incidentDate = new Date().toISOString().slice(0, 10);
  behaviourType = '';
  notes = '';
  projectName = '';
  subject = '';
  status = '';
  teacherNotes = '';
  academicYear = '';
  recordDate = new Date().toISOString().slice(0, 10);
  concernType = '';
  riskLevel = '';
  attendanceDate = new Date().toISOString().slice(0, 10);
  tripDirection = '';
  violationType = '';
  busCode = '';
  routeName = '';
  visitDate = localDateTimeNow();
  symptoms = '';
  diagnosis = '';
  classification = '';
  treatment = '';
  medication = '';
  notificationSent = false;

  ngOnInit(): void {
    this.studentSearchInput.pipe(
      debounceTime(250),
      distinctUntilChanged(),
      tap((query) => {
        this.studentSearchError.set(false);
        this.studentSearchLoading.set(query.length >= 2);
      }),
      switchMap((query) => query.length < 2
        ? of([] as ManualEntryStudent[])
        : this.manualEntryService.searchStudents(query, true).pipe(
            catchError(() => {
              this.studentSearchError.set(true);
              return of([] as ManualEntryStudent[]);
            }),
          )),
      takeUntil(this.destroyed),
    ).subscribe((students) => {
      this.studentSearchResults.set(students);
      this.studentSearchLoading.set(false);
      this.activeStudentIndex.set(students.length ? 0 : -1);
    });

    this.manualEntryService.options(true).subscribe({
      next: (options) => {
        this.options.set(options);
        this.loading.set(false);
      },
      error: () => {
        this.loadError.set(this.languageService.t('imports.manual.loadError'));
        this.loading.set(false);
      },
    });
    this.fieldsService.list(this.moduleId, true).subscribe({
      next: (fields) => this.customFields.set(fields),
      error: () => {},
    });
  }

  ngOnDestroy(): void {
    this.destroyed.next();
    this.destroyed.complete();
  }

  onStudentSearch(value: string): void {
    this.studentSearchText.set(value);
    this.studentSearchOpen.set(true);
    if (!this.selectedStudent || value !== this.studentLabel(this.selectedStudent)) {
      this.studentId = null;
      this.selectedStudent = null;
    }
    const query = value.trim();
    this.studentSearchResults.set([]);
    this.studentSearchLoading.set(query.length >= 2);
    this.activeStudentIndex.set(-1);
    this.studentSearchInput.next(query);
  }

  openStudentSearch(): void {
    this.studentSearchOpen.set(true);
    const query = this.studentSearchText().trim();
    if (!this.selectedStudent && query.length >= 2 && !this.studentSearchResults().length) {
      this.studentSearchInput.next(query);
    }
  }

  closeStudentSearchSoon(): void {
    window.setTimeout(() => this.studentSearchOpen.set(false), 120);
  }

  selectStudent(student: ManualEntryStudent): void {
    this.studentId = student.id;
    this.selectedStudent = student;
    this.studentSearchText.set(this.studentLabel(student));
    this.studentSearchOpen.set(false);
    this.studentSearchError.set(false);
  }

  clearStudent(): void {
    this.studentId = null;
    this.selectedStudent = null;
    this.studentSearchText.set('');
    this.studentSearchResults.set([]);
    this.studentSearchOpen.set(true);
    this.activeStudentIndex.set(-1);
  }

  onStudentSearchKeydown(event: KeyboardEvent): void {
    const students = this.studentSearchResults();
    if (event.key === 'Escape') {
      this.studentSearchOpen.set(false);
      event.stopPropagation();
      return;
    }
    if (!this.studentSearchOpen() || !students.length) return;
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.activeStudentIndex.update((index) => Math.min(index + 1, students.length - 1));
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.activeStudentIndex.update((index) => Math.max(index - 1, 0));
    } else if (event.key === 'Enter') {
      const student = students[this.activeStudentIndex()];
      if (student) {
        event.preventDefault();
        this.selectStudent(student);
      }
    }
  }

  private studentLabel(student: ManualEntryStudent): string {
    return `${student.fullName} (${student.sisUserId})`;
  }

  /** Collects custom field values as strings for the request payload. */
  private buildCustomFields(): Record<string, string> {
    const result: Record<string, string> = {};
    for (const field of this.customFields()) {
      if (field.fieldType === 'checkbox') {
        result[field.fieldKey] = this.customChecks[field.fieldKey] ? 'true' : 'false';
      } else {
        const value = (this.customValues[field.fieldKey] ?? '').toString().trim();
        if (value) result[field.fieldKey] = value;
      }
    }
    return result;
  }

  private customFieldsValid(): boolean {
    return this.customFields().every((field) => {
      if (!field.required) return true;
      if (field.fieldType === 'checkbox') return this.customChecks[field.fieldKey] === true;
      return !!(this.customValues[field.fieldKey] ?? '').toString().trim();
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.close.emit();
  }

  moduleTitleKey(): string {
    if (this.moduleId === 'student-behaviours') return 'imports.modules.behaviours.title';
    if (this.moduleId === 'student-projects') return 'imports.modules.projects.title';
    if (this.moduleId === 'student-bus-attendance') return 'imports.modules.busAttendance.title';
    if (this.moduleId === 'student-health-visits') return 'imports.modules.healthVisits.title';
    return 'imports.modules.mentalHealth.title';
  }

  canSubmit(): boolean {
    if (!this.studentId || !this.customFieldsValid()) {
      return false;
    }
    if (this.moduleId === 'student-behaviours') return !!this.incidentDate && !!this.behaviourType;
    if (this.moduleId === 'student-projects') return !!this.projectName.trim() && !!this.subject && !!this.status;
    if (this.moduleId === 'student-bus-attendance') return !!this.attendanceDate && !!this.tripDirection && !!this.violationType;
    if (this.moduleId === 'student-health-visits') return !!this.visitDate && !!this.symptoms.trim();
    return !!this.recordDate && !!this.academicYear && !!this.concernType && !!this.riskLevel;
  }

  submit(): void {
    if (!this.studentId || !this.canSubmit()) {
      return;
    }

    this.saving.set(true);
    this.submitError.set(null);
    const customFields = this.buildCustomFields();
    const request = this.moduleId === 'student-behaviours'
      ? this.manualEntryService.createBehaviour(this.studentId, {
          incidentDate: this.incidentDate,
          behaviourType: this.behaviourType,
          notes: this.notes.trim() || null,
          customFields,
        })
      : this.moduleId === 'student-projects' ? this.manualEntryService.createProject(this.studentId, {
          projectName: this.projectName.trim(),
          subject: this.subject,
          status: this.status,
          teacherNotes: this.teacherNotes.trim() || null,
          customFields,
        }) : this.moduleId === 'student-bus-attendance' ? this.manualEntryService.createBusAttendance(this.studentId, {
          attendanceDate: this.attendanceDate,
          tripDirection: this.tripDirection,
          violationType: this.violationType,
          busCode: this.busCode.trim() || null,
          routeName: this.routeName.trim() || null,
          notes: this.notes.trim() || null,
          customFields,
        }) : this.moduleId === 'student-mental-health' ? this.manualEntryService.createMentalHealth(this.studentId, {
          recordDate: this.recordDate,
          academicYear: this.academicYear,
          concernType: this.concernType,
          riskLevel: this.riskLevel,
          notes: this.notes.trim() || null,
          customFields,
        }) : this.manualEntryService.createHealthVisit(this.studentId, {
          visitDate: this.visitDate,
          symptoms: this.symptoms.trim(),
          diagnosis: this.diagnosis.trim() || null,
          classification: this.classification.trim() || null,
          treatment: this.treatment.trim() || null,
          medication: this.medication.trim() || null,
          notes: this.notes.trim() || null,
          notificationSent: this.notificationSent,
          customFields,
        });

    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => this.saved.emit(this.moduleId),
      error: (error) => {
        this.submitError.set(error?.error?.message || this.languageService.t('imports.manual.saveError'));
      },
    });
  }

  mentalOptionLabel(kind: 'concern' | 'risk', value: string): string {
    const slug = value.toLowerCase().replace(/[\s/]+/g, '-');
    const key = `imports.mentalHealthOptions.${kind}.${slug}`;
    const translated = this.languageService.t(key);
    return translated === key ? value : translated;
  }

  busOptionLabel(kind: 'direction' | 'violation', value: string): string {
    const slug = value.toLowerCase().replace(/[\s/]+/g, '-');
    const key = `imports.busOptions.${kind}.${slug}`;
    const translated = this.languageService.t(key);
    return translated === key ? value : translated;
  }
}
