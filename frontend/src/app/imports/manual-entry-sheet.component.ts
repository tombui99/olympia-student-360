import { Component, EventEmitter, HostListener, inject, Input, OnInit, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { LanguageService } from '../core/language/language.service';
import { ManualEntryOptions, ManualEntryService } from './manual-entry.service';

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
            <label class="block">
              <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.student') }}</span>
              <select
                class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10"
                name="studentId"
                required
                [(ngModel)]="studentId"
              >
                <option [ngValue]="null">{{ languageService.t('imports.manual.selectStudent') }}</option>
                @for (student of options()?.students ?? []; track student.id) {
                  <option [ngValue]="student.id">
                    {{ student.studentCode }} — {{ student.fullName }} · {{ languageService.t('imports.manual.grade') }} {{ student.gradeLevel || '-' }} · {{ student.homeroom || '-' }}
                  </option>
                }
              </select>
              <span class="mt-2 block text-xs text-[#7c7284]">{{ languageService.t('imports.manual.studentHint') }}</span>
            </label>

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
                  @for (type of options()?.behaviourTypes ?? []; track type) {
                    <option [value]="type">{{ type }}</option>
                  }
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
            } @else {
              <div class="grid gap-5 sm:grid-cols-2">
                <label class="block">
                  <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.incidentDate') }}</span>
                  <input class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" type="date" name="healthIncidentDate" required [(ngModel)]="incidentDate" />
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
                  <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.healthIncidentType') }}</span>
                  <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" name="healthIncidentType" required [(ngModel)]="healthIncidentType">
                    <option value="">{{ languageService.t('imports.manual.selectHealthIncidentType') }}</option>
                    @for (value of options()?.healthIncidentTypes ?? []; track value) { <option [value]="value">{{ healthOptionLabel('type', value) }}</option> }
                  </select>
                </label>
                <label class="block">
                  <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.severityLevel') }}</span>
                  <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" name="severityLevel" required [(ngModel)]="severityLevel">
                    <option [ngValue]="null">{{ languageService.t('imports.manual.selectSeverity') }}</option>
                    @for (value of options()?.healthSeverityLevels ?? []; track value) { <option [ngValue]="value">{{ languageService.t('imports.manual.severity.' + value) }}</option> }
                  </select>
                </label>
              </div>
              <div class="grid gap-5 sm:grid-cols-2">
                <label class="block">
                  <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.bodyArea') }}</span>
                  <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" name="bodyArea" [(ngModel)]="bodyArea">
                    <option value="">{{ languageService.t('imports.manual.optional') }}</option>
                    @for (value of options()?.healthBodyAreas ?? []; track value) { <option [value]="value">{{ healthOptionLabel('bodyArea', value) }}</option> }
                  </select>
                </label>
                <label class="block">
                  <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.healthLocation') }}</span>
                  <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" name="healthLocation" [(ngModel)]="healthLocation">
                    <option value="">{{ languageService.t('imports.manual.optional') }}</option>
                    @for (value of options()?.healthLocations ?? []; track value) { <option [value]="value">{{ healthOptionLabel('location', value) }}</option> }
                  </select>
                </label>
              </div>
              <label class="block">
                <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.cause') }}</span>
                <textarea class="mt-2 min-h-24 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" name="cause" maxlength="1000" required [(ngModel)]="cause"></textarea>
              </label>
              <label class="block">
                <span class="text-sm font-semibold text-[#32124f]">{{ languageService.t('imports.manual.actionTaken') }}</span>
                <textarea class="mt-2 min-h-24 w-full resize-y rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10" name="actionTaken" maxlength="1000" [(ngModel)]="actionTaken" [placeholder]="languageService.t('imports.manual.optional')"></textarea>
              </label>
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
export class ManualEntrySheetComponent implements OnInit {
  @Input({ required: true }) moduleId!: string;
  @Output() readonly close = new EventEmitter<void>();
  @Output() readonly saved = new EventEmitter<string>();

  private readonly manualEntryService = inject(ManualEntryService);
  readonly languageService = inject(LanguageService);

  readonly options = signal<ManualEntryOptions | null>(null);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly loadError = signal<string | null>(null);
  readonly submitError = signal<string | null>(null);

  studentId: number | null = null;
  incidentDate = new Date().toISOString().slice(0, 10);
  behaviourType = '';
  notes = '';
  projectName = '';
  subject = '';
  status = '';
  teacherNotes = '';
  academicYear = '';
  healthIncidentType = '';
  severityLevel: number | null = null;
  bodyArea = '';
  healthLocation = '';
  cause = '';
  actionTaken = '';

  ngOnInit(): void {
    this.manualEntryService.options().subscribe({
      next: (options) => {
        this.options.set(options);
        this.loading.set(false);
      },
      error: () => {
        this.loadError.set(this.languageService.t('imports.manual.loadError'));
        this.loading.set(false);
      },
    });
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.close.emit();
  }

  moduleTitleKey(): string {
    if (this.moduleId === 'student-behaviours') return 'imports.modules.behaviours.title';
    if (this.moduleId === 'student-projects') return 'imports.modules.projects.title';
    return 'imports.modules.health.title';
  }

  canSubmit(): boolean {
    if (!this.studentId) {
      return false;
    }
    if (this.moduleId === 'student-behaviours') return !!this.incidentDate && !!this.behaviourType;
    if (this.moduleId === 'student-projects') return !!this.projectName.trim() && !!this.subject && !!this.status;
    return !!this.incidentDate && !!this.academicYear && !!this.healthIncidentType &&
      this.severityLevel !== null && !!this.cause.trim();
  }

  submit(): void {
    if (!this.studentId || !this.canSubmit()) {
      return;
    }

    this.saving.set(true);
    this.submitError.set(null);
    const request = this.moduleId === 'student-behaviours'
      ? this.manualEntryService.createBehaviour(this.studentId, {
          incidentDate: this.incidentDate,
          behaviourType: this.behaviourType,
          notes: this.notes.trim() || null,
        })
      : this.moduleId === 'student-projects' ? this.manualEntryService.createProject(this.studentId, {
          projectName: this.projectName.trim(),
          subject: this.subject,
          status: this.status,
          teacherNotes: this.teacherNotes.trim() || null,
        }) : this.manualEntryService.createHealthIncident(this.studentId, {
          incidentDate: this.incidentDate,
          academicYear: this.academicYear,
          incidentType: this.healthIncidentType,
          severityLevel: this.severityLevel!,
          bodyArea: this.bodyArea || null,
          location: this.healthLocation || null,
          cause: this.cause.trim(),
          actionTaken: this.actionTaken.trim() || null,
        });

    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => this.saved.emit(this.moduleId),
      error: (error) => {
        this.submitError.set(error?.error?.message || this.languageService.t('imports.manual.saveError'));
      },
    });
  }

  healthOptionLabel(kind: 'type' | 'bodyArea' | 'location', value: string): string {
    const slug = value.toLowerCase().replace(/\s+/g, '-');
    const key = `imports.healthOptions.${kind}.${slug}`;
    const translated = this.languageService.t(key);
    return translated === key ? value : translated;
  }
}
