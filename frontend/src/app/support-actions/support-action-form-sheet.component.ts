import { Component, EventEmitter, HostListener, inject, Input, OnDestroy, OnInit, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { catchError, debounceTime, distinctUntilChanged, of, Subject, switchMap, takeUntil, tap } from 'rxjs';
import { AuthenticationService } from '../core/auth/auth.service';
import { LanguageService } from '../core/language/language.service';
import { ManualEntryService, ManualEntryStudent } from '../imports/manual-entry.service';
import {
  SaveStudentSupportAction,
  StudentSupportAction,
  SupportActionsService,
  SupportCategory,
  SupportProgress,
  SupportStatus,
} from './support-actions.service';

const today = (): string => {
  const value = new Date();
  return new Date(value.getTime() - value.getTimezoneOffset() * 60_000).toISOString().slice(0, 10);
};

@Component({
  selector: 'app-support-action-form-sheet',
  imports: [FormsModule],
  styles: `
    :host { position: fixed; inset: 0; z-index: 80; }
    .sheet-backdrop { animation: support-fade 160ms ease-out; }
    .sheet-panel { animation: support-slide 220ms ease-out; }
    @keyframes support-fade { from { opacity: 0; } to { opacity: 1; } }
    @keyframes support-slide { from { transform: translateX(100%); } to { transform: translateX(0); } }
  `,
  template: `
    <div class="sheet-backdrop absolute inset-0 bg-[#1d1025]/40 backdrop-blur-[1px]" (click)="close.emit()" aria-hidden="true"></div>
    <aside class="sheet-panel absolute inset-y-0 right-0 flex w-full max-w-2xl flex-col bg-white shadow-[-20px_0_55px_rgba(50,18,79,0.2)]" role="dialog" aria-modal="true">
      <header class="flex items-start justify-between gap-4 border-b border-[#ebe6ef] px-6 py-5">
        <div>
          <p class="text-xs font-semibold uppercase tracking-[0.18em] text-[#8a6811]">{{ t('support.form.eyebrow') }}</p>
          <h2 class="mt-1 text-2xl font-semibold text-[#32124f]">{{ t(action ? 'support.form.editTitle' : 'support.form.addTitle') }}</h2>
          <p class="mt-1 max-w-xl text-sm text-[#6f6877]">{{ t('support.form.hint') }}</p>
        </div>
        <button type="button" class="grid h-10 w-10 shrink-0 place-items-center rounded-full border border-[#ded6e4] text-xl text-[#5d5265] hover:bg-[#f7f3fa]" (click)="close.emit()" [attr.aria-label]="t('common.close')">×</button>
      </header>

      <form id="support-action-form" class="min-h-0 flex-1 overflow-y-auto px-6 py-6" (ngSubmit)="submit()">
        <div class="space-y-5">
          <div class="relative">
            <span class="text-sm font-semibold text-[#32124f]">{{ t('support.form.student') }} *</span>
            @if (fixedStudent || action) {
              <div class="mt-2 rounded-md border border-[#d8d0df] bg-[#faf8fb] px-4 py-3">
                <p class="font-semibold text-[#32124f]">{{ selectedStudent()?.fullName }}</p>
                <p class="mt-0.5 text-xs text-[#7c7284]">{{ selectedStudent()?.sisUserId }} · {{ t('support.grade') }} {{ selectedStudent()?.gradeLevel || '—' }} · {{ selectedStudent()?.homeroom || '—' }}</p>
              </div>
            } @else {
              <div class="relative mt-2">
                <input class="w-full rounded-md border border-[#d8d0df] py-3 pl-10 pr-4 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" type="search" autocomplete="off" role="combobox" [placeholder]="t('support.form.studentSearch')" [ngModel]="studentSearchText()" [ngModelOptions]="{ standalone: true }" (ngModelChange)="searchStudents($event)" (focus)="studentSearchOpen.set(true)" (blur)="closeStudentSearchSoon()" />
                <svg class="pointer-events-none absolute left-3.5 top-1/2 h-4 w-4 -translate-y-1/2 text-[#7c7284]" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="7"/><path d="m20 20-3.5-3.5"/></svg>
              </div>
              @if (studentSearchOpen()) {
                <div class="absolute z-30 mt-1 max-h-64 w-full overflow-y-auto rounded-md border border-[#d8d0df] bg-white py-1 shadow-[0_14px_34px_rgba(50,18,79,0.18)]">
                  @if (studentSearchText().trim().length < 2) {
                    <p class="px-4 py-3 text-sm text-[#7c7284]">{{ t('support.form.studentSearchHint') }}</p>
                  } @else if (studentSearchLoading()) {
                    <p class="px-4 py-3 text-sm text-[#7c7284]">{{ t('support.form.searching') }}</p>
                  } @else {
                    @for (student of studentResults(); track student.id) {
                      <button type="button" class="block w-full px-4 py-3 text-left hover:bg-[#f7f3fa]" (mousedown)="$event.preventDefault()" (click)="selectStudent(student)">
                        <span class="block text-sm font-semibold text-[#32124f]">{{ student.fullName }}</span>
                        <span class="mt-0.5 block text-xs text-[#7c7284]">{{ student.sisUserId }} · {{ t('support.grade') }} {{ student.gradeLevel || '—' }} · {{ student.homeroom || '—' }}</span>
                      </button>
                    } @empty {
                      <p class="px-4 py-3 text-sm text-[#7c7284]">{{ t('support.form.noStudents') }}</p>
                    }
                  }
                </div>
              }
            }
          </div>

          <div class="grid gap-5 sm:grid-cols-2">
            <label class="block">
              <span class="text-sm font-semibold text-[#32124f]">{{ t('support.form.category') }} *</span>
              <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="category" required [(ngModel)]="category">
                @for (option of availableCategories(); track option) { <option [value]="option">{{ categoryLabel(option) }}</option> }
              </select>
            </label>
            <label class="block">
              <span class="text-sm font-semibold text-[#32124f]">{{ t('support.form.actionDate') }} *</span>
              <input class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" type="date" name="actionDate" required [(ngModel)]="actionDate" />
            </label>
          </div>

          <label class="block">
            <span class="text-sm font-semibold text-[#32124f]">{{ t('support.form.title') }} *</span>
            <input class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="title" maxlength="255" required [(ngModel)]="title" [placeholder]="t('support.form.titlePlaceholder')" />
          </label>

          @if (category === 'Learning') {
            <label class="block">
              <span class="text-sm font-semibold text-[#32124f]">{{ t('support.form.subject') }}</span>
              <input class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="subject" maxlength="100" [(ngModel)]="subject" [placeholder]="t('support.form.optional')" />
            </label>
          }

          <label class="block">
            <span class="text-sm font-semibold text-[#32124f]">{{ t('support.form.goal') }} *</span>
            <textarea class="mt-2 min-h-24 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="goal" maxlength="2000" required [(ngModel)]="goal" [placeholder]="t('support.form.goalPlaceholder')"></textarea>
          </label>
          <label class="block">
            <span class="text-sm font-semibold text-[#32124f]">{{ t('support.form.actionTaken') }} *</span>
            <textarea class="mt-2 min-h-28 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="actionTaken" maxlength="4000" required [(ngModel)]="actionTaken" [placeholder]="t('support.form.actionPlaceholder')"></textarea>
          </label>

          <div class="grid gap-5 sm:grid-cols-2">
            <label class="block">
              <span class="text-sm font-semibold text-[#32124f]">{{ t('support.form.status') }} *</span>
              <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="status" [(ngModel)]="status">
                @for (option of statuses; track option) { <option [value]="option">{{ statusLabel(option) }}</option> }
              </select>
            </label>
            <label class="block">
              <span class="text-sm font-semibold text-[#32124f]">{{ t('support.form.progress') }} *</span>
              <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="progress" [(ngModel)]="progress">
                @for (option of progressValues; track option) { <option [value]="option">{{ progressLabel(option) }}</option> }
              </select>
            </label>
          </div>

          <label class="block">
            <span class="text-sm font-semibold text-[#32124f]">{{ t('support.form.result') }}</span>
            <textarea class="mt-2 min-h-24 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="result" maxlength="4000" [(ngModel)]="result" [placeholder]="t('support.form.resultPlaceholder')"></textarea>
          </label>

          <div class="grid gap-5 sm:grid-cols-2">
            <label class="block">
              <span class="text-sm font-semibold text-[#32124f]">{{ t('support.form.followUpDate') }}</span>
              <input class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" type="date" name="followUpDate" [min]="actionDate" [(ngModel)]="followUpDate" />
            </label>
            <div class="grid grid-cols-2 gap-3">
              <label class="block">
                <span class="text-sm font-semibold text-[#32124f]">{{ t('support.form.academicYear') }}</span>
                <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="academicYear" [(ngModel)]="academicYear">
                  <option value="">{{ t('support.form.auto') }}</option>
                  @for (year of academicYears(); track year) { <option [value]="year">{{ year.replace('-', '–') }}</option> }
                </select>
              </label>
              <label class="block">
                <span class="text-sm font-semibold text-[#32124f]">{{ t('support.form.term') }}</span>
                <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="term" [(ngModel)]="term">
                  <option value="">{{ t('support.form.allYear') }}</option>
                  <option value="hk1">{{ t('dashboard.period.term.hk1') }}</option>
                  <option value="hk2">{{ t('dashboard.period.term.hk2') }}</option>
                </select>
              </label>
            </div>
          </div>

          <label class="block">
            <span class="text-sm font-semibold text-[#32124f]">{{ t('support.form.nextStep') }}</span>
            <textarea class="mt-2 min-h-20 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" name="nextStep" maxlength="2000" [(ngModel)]="nextStep" [placeholder]="t('support.form.nextStepPlaceholder')"></textarea>
          </label>

          <label class="flex items-start gap-3 rounded-md border border-[#e4d9e9] bg-[#faf7fc] p-4">
            <input class="mt-0.5 h-4 w-4 accent-[#5b287d]" type="checkbox" name="shareWithFamily" [(ngModel)]="shareWithFamily" />
            <span>
              <span class="block text-sm font-semibold text-[#32124f]">{{ t('support.form.shareWithFamily') }}</span>
              <span class="mt-0.5 block text-xs leading-5 text-[#6f6877]">{{ t('support.form.shareHint') }}</span>
            </span>
          </label>

          @if (saveError()) { <p class="rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">{{ saveError() }}</p> }
        </div>
      </form>

      <footer class="flex justify-end gap-3 border-t border-[#ebe6ef] bg-[#faf9f6] px-6 py-4">
        <button type="button" class="rounded-md border border-[#d8d0df] bg-white px-5 py-2.5 text-sm font-semibold text-[#32124f] hover:bg-[#f7f3fa]" (click)="close.emit()">{{ t('common.cancel') }}</button>
        <button type="submit" form="support-action-form" class="rounded-md bg-[#32124f] px-5 py-2.5 text-sm font-semibold text-white hover:bg-[#4b2070] disabled:cursor-not-allowed disabled:opacity-45" [disabled]="!canSave() || saving()">{{ t(saving() ? 'support.form.saving' : 'support.form.save') }}</button>
      </footer>
    </aside>
  `,
})
export class SupportActionFormSheetComponent implements OnInit, OnDestroy {
  @Input() action: StudentSupportAction | null = null;
  @Input() fixedStudent: ManualEntryStudent | null = null;
  @Output() readonly close = new EventEmitter<void>();
  @Output() readonly saved = new EventEmitter<StudentSupportAction>();

  private readonly service = inject(SupportActionsService);
  private readonly manualEntry = inject(ManualEntryService);
  private readonly auth = inject(AuthenticationService);
  readonly language = inject(LanguageService);
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

  readonly statuses: SupportStatus[] = ['Planned', 'InProgress', 'FollowUpRequired', 'Completed'];
  readonly progressValues: SupportProgress[] = ['NotYetAssessed', 'NoChange', 'SomeProgress', 'SignificantProgress', 'GoalAchieved', 'NeedsMoreSupport'];

  actionDate = today();
  followUpDate = '';
  academicYear = '';
  term = '';
  category: SupportCategory = 'Learning';
  title = '';
  goal = '';
  actionTaken = '';
  result = '';
  nextStep = '';
  status: SupportStatus = 'InProgress';
  progress: SupportProgress = 'NotYetAssessed';
  subject = '';
  shareWithFamily = true;

  ngOnInit(): void {
    this.search$.pipe(
      debounceTime(250), distinctUntilChanged(),
      tap(() => this.studentSearchLoading.set(true)),
      switchMap((query) => query.trim().length < 2 ? of([]) : this.manualEntry.searchStudents(query, true).pipe(catchError(() => of([])))),
      takeUntil(this.destroy$),
    ).subscribe((students) => {
      this.studentResults.set(students);
      this.studentSearchLoading.set(false);
    });
    this.manualEntry.options(true).subscribe((options) => this.academicYears.set(options.academicYears));
    this.hydrate();
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  @HostListener('document:keydown.escape') onEscape(): void { this.close.emit(); }

  availableCategories(): SupportCategory[] {
    const categories: SupportCategory[] = [];
    if (this.auth.canAccess('academic')) categories.push('Learning', 'Behaviour');
    if (this.auth.canAccess('health')) categories.push('MentalHealth');
    return categories;
  }

  searchStudents(value: string): void {
    this.studentSearchText.set(value);
    this.selectedStudent.set(null);
    this.studentSearchOpen.set(true);
    this.search$.next(value);
  }

  selectStudent(student: ManualEntryStudent): void {
    this.selectedStudent.set(student);
    this.studentSearchText.set(student.fullName);
    this.studentSearchOpen.set(false);
  }

  closeStudentSearchSoon(): void { window.setTimeout(() => this.studentSearchOpen.set(false), 150); }

  canSave(): boolean {
    return !!this.selectedStudent() && !!this.actionDate && !!this.title.trim() && !!this.goal.trim() && !!this.actionTaken.trim();
  }

  submit(): void {
    const student = this.selectedStudent();
    if (!student || !this.canSave() || this.saving()) return;
    const model: SaveStudentSupportAction = {
      studentId: student.id,
      actionDate: this.actionDate,
      followUpDate: this.followUpDate || null,
      academicYear: this.academicYear || null,
      term: this.term || null,
      category: this.category,
      title: this.title.trim(),
      goal: this.goal.trim(),
      actionTaken: this.actionTaken.trim(),
      result: this.result.trim() || null,
      nextStep: this.nextStep.trim() || null,
      status: this.status,
      progress: this.progress,
      subject: this.category === 'Learning' ? this.subject.trim() || null : null,
      shareWithFamily: this.shareWithFamily,
    };
    this.saving.set(true);
    this.saveError.set('');
    const request = this.action ? this.service.update(this.action.id, model) : this.service.create(model);
    request.subscribe({
      next: (saved) => { this.saving.set(false); this.saved.emit(saved); },
      error: (error) => {
        this.saving.set(false);
        this.saveError.set(error?.error?.message || error?.error?.title || this.t('support.form.saveError'));
      },
    });
  }

  categoryLabel(value: string): string { return this.t(`support.category.${value}`); }
  statusLabel(value: string): string { return this.t(`support.status.${value}`); }
  progressLabel(value: string): string { return this.t(`support.progress.${value}`); }
  t(key: string): string { return this.language.t(key); }

  private hydrate(): void {
    if (this.action) {
      this.selectedStudent.set({
        id: this.action.studentId,
        fullName: this.action.studentName,
        sisUserId: this.action.sisUserId,
        gradeLevel: this.action.gradeLevel,
        homeroom: this.action.homeroom,
      });
      this.actionDate = this.action.actionDate.slice(0, 10);
      this.followUpDate = this.action.followUpDate?.slice(0, 10) ?? '';
      this.academicYear = this.action.academicYear;
      this.term = this.action.term ?? '';
      this.category = this.action.category;
      this.title = this.action.title;
      this.goal = this.action.goal;
      this.actionTaken = this.action.actionTaken;
      this.result = this.action.result ?? '';
      this.nextStep = this.action.nextStep ?? '';
      this.status = this.action.status;
      this.progress = this.action.progress;
      this.subject = this.action.subject ?? '';
      this.shareWithFamily = this.action.shareWithFamily;
    } else if (this.fixedStudent) {
      this.selectedStudent.set(this.fixedStudent);
    }
    const options = this.availableCategories();
    if (!options.includes(this.category)) this.category = options[0] ?? 'Learning';
  }
}
