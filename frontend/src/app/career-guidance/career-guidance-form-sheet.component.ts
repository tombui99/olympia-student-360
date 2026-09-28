import { Component, EventEmitter, HostListener, inject, Input, OnDestroy, OnInit, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { catchError, debounceTime, distinctUntilChanged, of, Subject, switchMap, takeUntil, tap } from 'rxjs';
import { LanguageService } from '../core/language/language.service';
import { ManualEntryService, ManualEntryStudent } from '../imports/manual-entry.service';
import {
  CareerGuidanceRecord,
  CareerGuidanceRecordType,
  CareerGuidanceService,
  CareerGuidanceStatus,
  SaveCareerGuidanceRecord,
} from './career-guidance.service';

const today = (): string => {
  const date = new Date();
  return new Date(date.getTime() - date.getTimezoneOffset() * 60_000).toISOString().slice(0, 10);
};

@Component({
  selector: 'app-career-guidance-form-sheet',
  imports: [FormsModule],
  styles: `
    :host { position: fixed; inset: 0; z-index: 80; }
    .sheet-backdrop { animation: career-fade 160ms ease-out; }
    .sheet-panel { animation: career-slide 220ms ease-out; }
    @keyframes career-fade { from { opacity: 0; } to { opacity: 1; } }
    @keyframes career-slide { from { transform: translateX(100%); } to { transform: translateX(0); } }
  `,
  template: `
    <div class="sheet-backdrop absolute inset-0 bg-[#1d1025]/40 backdrop-blur-[1px]" (click)="close.emit()" aria-hidden="true"></div>
    <aside class="sheet-panel absolute inset-y-0 right-0 flex w-full max-w-3xl flex-col bg-white shadow-[-20px_0_55px_rgba(50,18,79,0.2)]" role="dialog" aria-modal="true">
      <header class="flex items-start justify-between gap-4 border-b border-[#ebe6ef] px-6 py-5">
        <div>
          <p class="text-xs font-semibold uppercase tracking-[0.18em] text-[#8a6811]">{{ t('career.form.eyebrow') }}</p>
          <h2 class="mt-1 text-2xl font-semibold text-[#32124f]">{{ t(record ? 'career.form.editTitle' : 'career.form.addTitle') }}</h2>
          <p class="mt-1 max-w-2xl text-sm text-[#6f6877]">{{ t('career.form.hint') }}</p>
        </div>
        <button type="button" class="grid h-10 w-10 shrink-0 place-items-center rounded-full border border-[#ded6e4] text-xl text-[#5d5265] hover:bg-[#f7f3fa]" (click)="close.emit()" [attr.aria-label]="t('common.close')">×</button>
      </header>

      <form id="career-guidance-form" class="min-h-0 flex-1 overflow-y-auto px-6 py-6" (ngSubmit)="submit()">
        <div class="space-y-6">
          <section class="space-y-5">
            <h3 class="border-b border-[#eee8f1] pb-2 text-xs font-bold uppercase tracking-[0.16em] text-[#8a6811]">{{ t('career.form.studentAndPeriod') }}</h3>
            <div class="relative">
              <span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.student') }} <span class="text-red-600">*</span></span>
              @if (fixedStudent || record) {
                <div class="mt-2 rounded-md border border-[#d8d0df] bg-[#faf8fb] px-4 py-3">
                  <p class="font-semibold text-[#32124f]">{{ selectedStudent()?.fullName }}</p>
                  <p class="mt-0.5 text-xs text-[#7c7284]">{{ selectedStudent()?.sisUserId }} · {{ t('career.grade') }} {{ selectedStudent()?.gradeLevel || '—' }} · {{ selectedStudent()?.homeroom || '—' }}</p>
                </div>
              } @else {
                <div class="relative mt-2">
                  <input class="w-full rounded-md border border-[#d8d0df] py-3 pl-10 pr-4 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" type="search" autocomplete="off" role="combobox" [placeholder]="t('career.form.studentSearch')" [ngModel]="studentSearchText()" [ngModelOptions]="{ standalone: true }" (ngModelChange)="searchStudents($event)" (focus)="studentSearchOpen.set(true)" (blur)="closeStudentSearchSoon()" />
                  <svg class="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-[#7c7284]" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="7"/><path d="m20 20-3.5-3.5"/></svg>
                </div>
                @if (studentSearchOpen()) {
                  <div class="absolute z-30 mt-1 max-h-64 w-full overflow-y-auto rounded-md border border-[#d8d0df] bg-white py-1 shadow-[0_14px_34px_rgba(50,18,79,0.18)]">
                    @if (studentSearchText().trim().length < 2) {
                      <p class="px-4 py-3 text-sm text-[#7c7284]">{{ t('career.form.studentSearchHint') }}</p>
                    } @else if (studentSearchLoading()) {
                      <p class="px-4 py-3 text-sm text-[#7c7284]">{{ t('career.form.searching') }}</p>
                    } @else {
                      @for (student of studentResults(); track student.id) {
                        <button type="button" class="block w-full px-4 py-3 text-left hover:bg-[#f7f3fa]" (mousedown)="$event.preventDefault()" (click)="selectStudent(student)">
                          <span class="block text-sm font-semibold text-[#32124f]">{{ student.fullName }}</span>
                          <span class="mt-0.5 block text-xs text-[#7c7284]">{{ student.sisUserId }} · {{ t('career.grade') }} {{ student.gradeLevel || '—' }} · {{ student.homeroom || '—' }}</span>
                        </button>
                      } @empty { <p class="px-4 py-3 text-sm text-[#7c7284]">{{ t('career.form.noStudents') }}</p> }
                    }
                  </div>
                }
              }
            </div>

            <div class="grid gap-5 sm:grid-cols-2">
              <label class="block">
                <span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.recordType') }} <span class="text-red-600">*</span></span>
                <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="recordType" [(ngModel)]="recordType">
                  @for (option of recordTypes; track option) { <option [value]="option">{{ typeLabel(option) }}</option> }
                </select>
              </label>
              <label class="block">
                <span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.recordDate') }} <span class="text-red-600">*</span></span>
                <input class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" type="date" name="recordDate" required [(ngModel)]="recordDate" />
              </label>
            </div>
            <div class="grid gap-5 sm:grid-cols-3">
              <label class="block">
                <span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.academicYear') }}</span>
                <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="academicYear" [(ngModel)]="academicYear">
                  <option value=""></option>
                  @for (year of academicYears(); track year) { <option [value]="year">{{ year.replace('-', '–') }}</option> }
                </select>
              </label>
              <label class="block">
                <span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.term') }}</span>
                <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="term" [(ngModel)]="term">
                  <option value=""></option><option value="hk1">{{ t('dashboard.period.term.hk1') }}</option><option value="hk2">{{ t('dashboard.period.term.hk2') }}</option>
                </select>
              </label>
              <label class="block">
                <span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.status') }} <span class="text-red-600">*</span></span>
                <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="status" [(ngModel)]="status">
                  @for (option of statuses; track option) { <option [value]="option">{{ statusLabel(option) }}</option> }
                </select>
              </label>
            </div>
            <label class="block">
              <span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.title') }} <span class="text-red-600">*</span></span>
              <input class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="title" maxlength="255" required [(ngModel)]="title" [placeholder]="t('career.form.titlePlaceholder')" />
            </label>
          </section>

          @if (recordType === 'UniversityApplication' || recordType === 'Preparation') {
          <section class="space-y-5">
            <h3 class="border-b border-[#eee8f1] pb-2 text-xs font-bold uppercase tracking-[0.16em] text-[#8a6811]">{{ t('career.form.direction') }}</h3>
            <label class="block"><span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.careerInterests') }}</span><textarea class="mt-2 min-h-20 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="careerInterests" maxlength="2000" [(ngModel)]="careerInterests"></textarea></label>
            <div class="grid gap-5 sm:grid-cols-2">
              <label class="block"><span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.targetCountries') }}</span><input class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="targetCountries" maxlength="1000" [(ngModel)]="targetCountries" /></label>
              <label class="block"><span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.targetMajors') }}</span><input class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="targetMajors" maxlength="1000" [(ngModel)]="targetMajors" /></label>
            </div>
            <label class="block"><span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.targetUniversities') }}</span><textarea class="mt-2 min-h-20 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="targetUniversities" maxlength="2000" [(ngModel)]="targetUniversities"></textarea></label>
          </section>

          <section class="space-y-5">
            <h3 class="border-b border-[#eee8f1] pb-2 text-xs font-bold uppercase tracking-[0.16em] text-[#8a6811]">{{ t('career.form.progress') }}</h3>
            <div class="grid gap-5 sm:grid-cols-2">
              @if (recordType === 'UniversityApplication') {
                <label class="block"><span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.applicationProgress') }}</span><input class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" type="number" min="0" max="100" name="applicationProgressPercent" [(ngModel)]="applicationProgressPercent" /></label>
              } @else {
                <label class="block"><span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.preparationProgress') }}</span><input class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" type="number" min="0" max="100" name="preparationProgressPercent" [(ngModel)]="preparationProgressPercent" /></label>
              }
              <label class="block"><span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.qualityRating') }}</span><input class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" type="number" min="1" max="5" name="qualityRating" [(ngModel)]="qualityRating" /></label>
            </div>
          </section>
          }

          @if (recordType === 'Internship') {
            <section class="space-y-5 rounded-lg border border-[#dce7e3] bg-[#f8fcfa] p-5">
              <h3 class="text-xs font-bold uppercase tracking-[0.16em] text-[#087466]">{{ t('career.form.internship') }}</h3>
              <div class="grid gap-5 sm:grid-cols-2">
                <label class="block"><span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.internshipCompany') }}</span><input class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="internshipCompany" [(ngModel)]="internshipCompany" /></label>
                <label class="block"><span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.internshipRole') }}</span><input class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="internshipRole" [(ngModel)]="internshipRole" /></label>
                <label class="block"><span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.startDate') }}</span><input class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" type="date" name="internshipStartDate" [(ngModel)]="internshipStartDate" /></label>
                <label class="block"><span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.endDate') }}</span><input class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" type="date" name="internshipEndDate" [(ngModel)]="internshipEndDate" /></label>
                <label class="block"><span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.internshipProgress') }}</span><input class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" type="number" min="0" max="100" name="internshipProgressPercent" [(ngModel)]="internshipProgressPercent" /></label>
                <label class="block"><span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.internshipQuality') }}</span><input class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" type="number" min="1" max="5" name="internshipQualityRating" [(ngModel)]="internshipQualityRating" /></label>
              </div>
              <label class="block"><span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.studentSelfAssessment') }}</span><textarea class="mt-2 min-h-20 w-full resize-y rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="studentSelfAssessment" [(ngModel)]="studentSelfAssessment"></textarea></label>
              <label class="block"><span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.hostAssessment') }}</span><textarea class="mt-2 min-h-20 w-full resize-y rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="hostAssessment" [(ngModel)]="hostAssessment"></textarea></label>
              <label class="block"><span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.finalProduct') }}</span><textarea class="mt-2 min-h-20 w-full resize-y rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="finalProduct" [(ngModel)]="finalProduct"></textarea></label>
            </section>
          }

          <section class="space-y-5">
            <h3 class="border-b border-[#eee8f1] pb-2 text-xs font-bold uppercase tracking-[0.16em] text-[#8a6811]">{{ t('career.form.report') }}</h3>
            @if (recordType === 'GeneralPlan' || recordType === 'Counseling') {
              <label class="block"><span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.activitiesSummary') }}</span><textarea class="mt-2 min-h-24 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="activitiesSummary" [(ngModel)]="activitiesSummary"></textarea></label>
            }
            <div class="grid gap-5 sm:grid-cols-2">
              <label class="block"><span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.issues') }}</span><textarea class="mt-2 min-h-24 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="issues" [(ngModel)]="issues"></textarea></label>
              <label class="block"><span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.solutions') }}</span><textarea class="mt-2 min-h-24 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="solutions" [(ngModel)]="solutions"></textarea></label>
              @if (recordType !== 'Internship') {
                <label class="block"><span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.studentSupport') }}</span><textarea class="mt-2 min-h-24 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="studentSupport" [(ngModel)]="studentSupport"></textarea></label>
                <label class="block"><span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.studentFeedback') }}</span><textarea class="mt-2 min-h-24 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="studentFeedback" [(ngModel)]="studentFeedback"></textarea></label>
              }
            </div>
            <label class="block"><span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.nextSteps') }}</span><textarea class="mt-2 min-h-24 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="nextSteps" [(ngModel)]="nextSteps"></textarea></label>
            @if (recordType === 'GeneralPlan' || recordType === 'Counseling') {
              <div class="grid gap-5 sm:grid-cols-2">
                <label class="block"><span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.recommendations') }}</span><textarea class="mt-2 min-h-20 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="recommendations" [(ngModel)]="recommendations"></textarea></label>
                <label class="block"><span class="text-sm font-semibold text-[#32124f]">{{ t('career.form.proposals') }}</span><textarea class="mt-2 min-h-20 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="proposals" [(ngModel)]="proposals"></textarea></label>
              </div>
            }
          </section>

          @if (saveError()) { <p class="rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">{{ saveError() }}</p> }
        </div>
      </form>

      <footer class="flex justify-end gap-3 border-t border-[#ebe6ef] bg-[#faf9f6] px-6 py-4">
        <button type="button" class="rounded-md border border-[#d8d0df] bg-white px-5 py-2.5 text-sm font-semibold text-[#32124f] hover:bg-[#f7f3fa]" (click)="close.emit()">{{ t('common.cancel') }}</button>
        <button type="submit" form="career-guidance-form" class="rounded-md bg-[#32124f] px-5 py-2.5 text-sm font-semibold text-white hover:bg-[#4b2070] disabled:cursor-not-allowed disabled:opacity-45" [disabled]="!canSave() || saving()">{{ t(saving() ? 'career.form.saving' : 'career.form.save') }}</button>
      </footer>
    </aside>
  `,
})
export class CareerGuidanceFormSheetComponent implements OnInit, OnDestroy {
  @Input() record: CareerGuidanceRecord | null = null;
  @Input() fixedStudent: ManualEntryStudent | null = null;
  @Output() readonly close = new EventEmitter<void>();
  @Output() readonly saved = new EventEmitter<CareerGuidanceRecord>();

  private readonly service = inject(CareerGuidanceService);
  private readonly manualEntry = inject(ManualEntryService);
  private readonly language = inject(LanguageService);
  private readonly destroy$ = new Subject<void>();
  private readonly search$ = new Subject<string>();

  readonly selectedStudent = signal<ManualEntryStudent | null>(null);
  readonly studentSearchText = signal('');
  readonly studentResults = signal<ManualEntryStudent[]>([]);
  readonly studentSearchOpen = signal(false);
  readonly studentSearchLoading = signal(false);
  readonly academicYears = signal<string[]>([]);
  readonly saving = signal(false);
  readonly saveError = signal('');
  readonly recordTypes: CareerGuidanceRecordType[] = ['GeneralPlan', 'UniversityApplication', 'Preparation', 'Internship', 'Counseling'];
  readonly statuses: CareerGuidanceStatus[] = ['Planned', 'InProgress', 'NeedsAttention', 'Completed'];

  recordType: CareerGuidanceRecordType = 'GeneralPlan'; recordDate = today(); academicYear = ''; term = ''; title = ''; status: CareerGuidanceStatus = 'InProgress';
  targetCountries = ''; targetUniversities = ''; targetMajors = ''; careerInterests = '';
  applicationProgressPercent: number | null = null; preparationProgressPercent: number | null = null; qualityRating: number | null = null;
  activitiesSummary = ''; issues = ''; solutions = ''; studentSupport = ''; studentFeedback = ''; studentSelfAssessment = ''; nextSteps = ''; recommendations = ''; proposals = '';
  internshipCompany = ''; internshipRole = ''; internshipStartDate = ''; internshipEndDate = ''; internshipProgressPercent: number | null = null; internshipQualityRating: number | null = null; hostAssessment = ''; finalProduct = '';

  ngOnInit(): void {
    this.search$.pipe(debounceTime(250), distinctUntilChanged(), tap(() => this.studentSearchLoading.set(true)), switchMap((query) => query.trim().length < 2 ? of([]) : this.manualEntry.searchStudents(query, true).pipe(catchError(() => of([])))), takeUntil(this.destroy$)).subscribe((students) => { this.studentResults.set(students); this.studentSearchLoading.set(false); });
    this.manualEntry.options(true).subscribe((options) => this.academicYears.set(options.academicYears));
    this.hydrate();
  }
  ngOnDestroy(): void { this.destroy$.next(); this.destroy$.complete(); }
  @HostListener('document:keydown.escape') onEscape(): void { this.close.emit(); }
  searchStudents(value: string): void { this.studentSearchText.set(value); this.selectedStudent.set(null); this.studentSearchOpen.set(true); this.search$.next(value); }
  selectStudent(student: ManualEntryStudent): void { this.selectedStudent.set(student); this.studentSearchText.set(student.fullName); this.studentSearchOpen.set(false); }
  closeStudentSearchSoon(): void { window.setTimeout(() => this.studentSearchOpen.set(false), 150); }
  canSave(): boolean { return !!this.selectedStudent() && !!this.recordDate && !!this.title.trim(); }

  submit(): void {
    const student = this.selectedStudent();
    if (!student || !this.canSave() || this.saving()) return;
    const text = (value: string) => value.trim() || null;
    const isApplication = this.recordType === 'UniversityApplication';
    const isPreparation = this.recordType === 'Preparation';
    const isDirectionRecord = isApplication || isPreparation;
    const isInternship = this.recordType === 'Internship';
    const isPlanOrCounseling = this.recordType === 'GeneralPlan' || this.recordType === 'Counseling';
    const model: SaveCareerGuidanceRecord = {
      studentId: student.id, recordType: this.recordType, recordDate: this.recordDate, academicYear: this.academicYear || null, term: this.term || null, title: this.title.trim(), status: this.status,
      targetCountries: isDirectionRecord ? text(this.targetCountries) : null, targetUniversities: isDirectionRecord ? text(this.targetUniversities) : null, targetMajors: isDirectionRecord ? text(this.targetMajors) : null, careerInterests: isDirectionRecord ? text(this.careerInterests) : null,
      applicationProgressPercent: isApplication ? this.applicationProgressPercent : null, preparationProgressPercent: isPreparation ? this.preparationProgressPercent : null, qualityRating: isDirectionRecord ? this.qualityRating : null,
      activitiesSummary: isPlanOrCounseling ? text(this.activitiesSummary) : null, issues: text(this.issues), solutions: text(this.solutions), studentSupport: isInternship ? null : text(this.studentSupport), studentFeedback: isInternship ? null : text(this.studentFeedback), studentSelfAssessment: isInternship ? text(this.studentSelfAssessment) : null, nextSteps: text(this.nextSteps), recommendations: isPlanOrCounseling ? text(this.recommendations) : null, proposals: isPlanOrCounseling ? text(this.proposals) : null,
      internshipCompany: isInternship ? text(this.internshipCompany) : null, internshipRole: isInternship ? text(this.internshipRole) : null, internshipStartDate: isInternship ? this.internshipStartDate || null : null, internshipEndDate: isInternship ? this.internshipEndDate || null : null, internshipProgressPercent: isInternship ? this.internshipProgressPercent : null, internshipQualityRating: isInternship ? this.internshipQualityRating : null, hostAssessment: isInternship ? text(this.hostAssessment) : null, finalProduct: isInternship ? text(this.finalProduct) : null,
    };
    this.saving.set(true); this.saveError.set('');
    const request = this.record ? this.service.update(this.record.id, model) : this.service.create(model);
    request.subscribe({ next: (saved) => { this.saving.set(false); this.saved.emit(saved); }, error: (error) => { this.saving.set(false); this.saveError.set(error?.error?.message || error?.error?.title || this.t('career.form.saveError')); } });
  }

  typeLabel(value: string): string { return this.t(`career.type.${value}`); }
  statusLabel(value: string): string { return this.t(`career.status.${value}`); }
  t(key: string): string { return this.language.t(key); }

  private hydrate(): void {
    const value = this.record;
    if (value) {
      this.selectedStudent.set({ id: value.studentId, fullName: value.studentName, sisUserId: value.sisUserId, gradeLevel: value.gradeLevel, homeroom: value.homeroom });
      this.recordType = value.recordType; this.recordDate = value.recordDate.slice(0, 10); this.academicYear = value.academicYear; this.term = value.term ?? ''; this.title = value.title; this.status = value.status;
      this.targetCountries = value.targetCountries ?? ''; this.targetUniversities = value.targetUniversities ?? ''; this.targetMajors = value.targetMajors ?? ''; this.careerInterests = value.careerInterests ?? '';
      this.applicationProgressPercent = value.applicationProgressPercent ?? null; this.preparationProgressPercent = value.preparationProgressPercent ?? null; this.qualityRating = value.qualityRating ?? null;
      this.activitiesSummary = value.activitiesSummary ?? ''; this.issues = value.issues ?? ''; this.solutions = value.solutions ?? ''; this.studentSupport = value.studentSupport ?? ''; this.studentFeedback = value.studentFeedback ?? ''; this.studentSelfAssessment = value.studentSelfAssessment ?? ''; this.nextSteps = value.nextSteps ?? ''; this.recommendations = value.recommendations ?? ''; this.proposals = value.proposals ?? '';
      this.internshipCompany = value.internshipCompany ?? ''; this.internshipRole = value.internshipRole ?? ''; this.internshipStartDate = value.internshipStartDate?.slice(0, 10) ?? ''; this.internshipEndDate = value.internshipEndDate?.slice(0, 10) ?? ''; this.internshipProgressPercent = value.internshipProgressPercent ?? null; this.internshipQualityRating = value.internshipQualityRating ?? null; this.hostAssessment = value.hostAssessment ?? ''; this.finalProduct = value.finalProduct ?? '';
    } else if (this.fixedStudent) {
      this.selectedStudent.set(this.fixedStudent);
    }
  }
}
