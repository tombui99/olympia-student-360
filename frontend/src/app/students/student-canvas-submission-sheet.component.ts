import { Component, EventEmitter, HostListener, inject, Input, OnInit, Output, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { LanguageService } from '../core/language/language.service';
import {
  CanvasSubmissionCategory,
  StudentCanvasSubmission,
  StudentsService,
} from './students.service';

export type CanvasSubmissionSheetKind = 'assignmentActivity' | 'needsAttention';

@Component({
  selector: 'app-student-canvas-submission-sheet',
  styles: `
    :host { position: fixed; inset: 0; z-index: 70; }
    .sheet-backdrop { animation: fade-in 160ms ease-out; }
    .sheet-panel { animation: slide-in 220ms ease-out; }
    @keyframes fade-in { from { opacity: 0; } to { opacity: 1; } }
    @keyframes slide-in { from { transform: translateX(100%); } to { transform: translateX(0); } }
  `,
  template: `
    <div class="sheet-backdrop absolute inset-0 bg-[#1d1025]/35" aria-hidden="true" (click)="close.emit()"></div>
    <aside
      class="sheet-panel absolute inset-y-0 right-0 flex w-full max-w-2xl flex-col bg-white shadow-[-18px_0_45px_rgba(50,18,79,0.18)]"
      role="dialog"
      aria-modal="true"
      [attr.aria-label]="sheetTitle()"
    >
      <header class="flex items-start justify-between gap-4 border-b border-[#ebe6ef] px-6 py-5">
        <div class="min-w-0">
          <p class="text-xs font-semibold uppercase tracking-[0.18em] text-[#8a6811]">
            {{ languageService.t('studentProfile.canvas.sheet.eyebrow') }}
          </p>
          <h2 class="mt-2 text-2xl font-semibold text-[#32124f]">{{ sheetTitle() }}</h2>
          <p class="mt-1 truncate text-sm text-[#6f6877]">
            {{ studentName }} · {{ academicYear || '—' }}
          </p>
        </div>
        <button
          class="grid h-10 w-10 shrink-0 place-items-center rounded-full border border-[#ded6e4] text-xl text-[#5d5265] transition hover:bg-[#f7f3fa]"
          type="button"
          (click)="close.emit()"
          [attr.aria-label]="languageService.t('studentProfile.canvas.sheet.close')"
        >×</button>
      </header>

      <div class="border-b border-[#ebe6ef] px-6 py-4">
        <div class="grid grid-cols-2 gap-2 rounded-lg bg-[#f5f1f7] p-1" role="tablist">
          @for (category of categories(); track category) {
            <button
              class="rounded-md px-4 py-2.5 text-sm font-semibold transition"
              [class.bg-white]="activeCategory() === category"
              [class.text-[#32124f]]="activeCategory() === category"
              [class.shadow-sm]="activeCategory() === category"
              [class.text-[#746b7b]]="activeCategory() !== category"
              type="button"
              role="tab"
              [attr.aria-selected]="activeCategory() === category"
              (click)="selectCategory(category)"
            >
              {{ categoryLabel(category) }}
              <span class="ml-1 rounded-full bg-[#eee7f2] px-2 py-0.5 text-xs">{{ categoryCount(category) }}</span>
            </button>
          }
        </div>
      </div>

      <div class="flex-1 overflow-y-auto px-6 py-5">
        @if (loading()) {
          <p class="rounded-md bg-[#faf9f6] p-5 text-sm text-[#6f6877]">
            {{ languageService.t('studentProfile.canvas.sheet.loading') }}
          </p>
        } @else if (error()) {
          <div class="rounded-md border border-red-200 bg-red-50 p-5 text-sm text-red-700">
            <p>{{ languageService.t('studentProfile.canvas.sheet.loadError') }}</p>
            <button class="mt-3 rounded-md border border-red-300 px-3 py-2 font-semibold" type="button" (click)="load(page())">
              {{ languageService.t('studentProfile.canvas.sheet.retry') }}
            </button>
          </div>
        } @else if (!items().length) {
          <p class="rounded-md border border-dashed border-[#d8d0df] bg-[#faf9f6] p-8 text-center text-sm text-[#7c7284]">
            {{ languageService.t('studentProfile.canvas.sheet.empty') }}
          </p>
        } @else {
          <div class="space-y-3">
            @for (submission of items(); track submission.canvasSubmissionId) {
              <article class="rounded-lg border border-[#e5dee9] bg-white p-4 shadow-[0_2px_8px_rgba(50,18,79,0.05)]">
                <div class="flex items-start justify-between gap-4">
                  <div class="min-w-0">
                    <h3 class="font-semibold text-[#32124f]">{{ submission.assignmentTitle }}</h3>
                    <p class="mt-1 text-sm text-[#6f6877]">{{ submission.courseName || '—' }}</p>
                    <p class="mt-0.5 text-xs text-[#8a808f]">
                      {{ languageService.t('studentProfile.canvas.sheet.assignmentId') }} {{ submission.canvasAssignmentId }} ·
                      {{ languageService.t('studentProfile.canvas.sheet.courseId') }} {{ submission.canvasCourseId }}
                    </p>
                  </div>
                  <div class="shrink-0 text-right">
                    <p class="font-semibold text-[#32124f]">
                      {{ submission.score ?? '—' }}<span class="text-xs font-medium text-[#7c7284]">/{{ submission.pointsPossible ?? '—' }}</span>
                    </p>
                    @if (submission.grade) {
                      <p class="mt-0.5 text-xs text-[#7c7284]">{{ submission.grade }}</p>
                    }
                  </div>
                </div>

                <div class="mt-3 flex flex-wrap gap-2">
                  <span class="rounded-full bg-[#f1edf4] px-2.5 py-1 text-xs font-semibold text-[#5b287d]">{{ workflowLabel(submission.workflowState) }}</span>
                  @if (submission.missing && !submission.excused) {
                    <span class="rounded-full bg-[#a9485d]/15 px-2.5 py-1 text-xs font-semibold text-[#a9485d]">{{ languageService.t('studentProfile.canvas.badge.missing') }}</span>
                  }
                  @if (submission.late) {
                    <span class="rounded-full bg-[#f3c246]/25 px-2.5 py-1 text-xs font-semibold text-[#8a6811]">{{ languageService.t('studentProfile.canvas.badge.late') }}</span>
                  }
                  @if (submission.excused) {
                    <span class="rounded-full bg-[#f1edf4] px-2.5 py-1 text-xs font-semibold text-[#5b287d]">{{ languageService.t('studentProfile.canvas.badge.excused') }}</span>
                  }
                </div>

                <dl class="mt-4 grid gap-3 border-t border-[#eee8f1] pt-3 text-xs sm:grid-cols-3">
                  <div>
                    <dt class="uppercase tracking-[0.1em] text-[#8a808f]">{{ languageService.t('studentProfile.canvas.sheet.due') }}</dt>
                    <dd class="mt-1 font-medium text-[#5d5265]">{{ formatDate(submission.dueAt) }}</dd>
                  </div>
                  <div>
                    <dt class="uppercase tracking-[0.1em] text-[#8a808f]">{{ languageService.t('studentProfile.canvas.sheet.submitted') }}</dt>
                    <dd class="mt-1 font-medium text-[#5d5265]">{{ formatDate(submission.submittedAt) }}</dd>
                  </div>
                  <div>
                    <dt class="uppercase tracking-[0.1em] text-[#8a808f]">{{ languageService.t('studentProfile.canvas.sheet.graded') }}</dt>
                    <dd class="mt-1 font-medium text-[#5d5265]">{{ formatDate(submission.gradedAt) }}</dd>
                  </div>
                </dl>
              </article>
            }
          </div>
        }
      </div>

      <footer class="flex flex-wrap items-center justify-between gap-3 border-t border-[#ebe6ef] px-6 py-4 text-xs text-[#7c7284]">
        <span>{{ paginationSummary() }}</span>
        <div class="flex gap-2">
          <button
            class="rounded-md border border-[#d8d0df] px-3 py-2 font-semibold text-[#32124f] disabled:cursor-not-allowed disabled:opacity-45"
            type="button"
            [disabled]="loading() || page() <= 1"
            (click)="load(page() - 1)"
          >{{ languageService.t('pagination.previous') }}</button>
          <button
            class="rounded-md border border-[#d8d0df] px-3 py-2 font-semibold text-[#32124f] disabled:cursor-not-allowed disabled:opacity-45"
            type="button"
            [disabled]="loading() || page() >= totalPages()"
            (click)="load(page() + 1)"
          >{{ languageService.t('pagination.next') }}</button>
        </div>
      </footer>
    </aside>
  `,
})
export class StudentCanvasSubmissionSheetComponent implements OnInit {
  private readonly studentsService = inject(StudentsService);
  readonly languageService = inject(LanguageService);

  @Input({ required: true }) studentId!: number;
  @Input() studentName = '';
  @Input() academicYear?: string | null;
  @Input({ required: true }) kind!: CanvasSubmissionSheetKind;
  @Input() gradedCount = 0;
  @Input() submittedCount = 0;
  @Input() missingCount = 0;
  @Input() lateCount = 0;
  @Output() close = new EventEmitter<void>();

  readonly activeCategory = signal<CanvasSubmissionCategory>('graded');
  readonly items = signal<StudentCanvasSubmission[]>([]);
  readonly page = signal(1);
  readonly pageSize = 20;
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly loading = signal(true);
  readonly error = signal(false);
  private requestId = 0;

  ngOnInit(): void {
    this.activeCategory.set(this.kind === 'assignmentActivity' ? 'graded' : 'missing');
    this.load(1);
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.close.emit();
  }

  categories(): CanvasSubmissionCategory[] {
    return this.kind === 'assignmentActivity' ? ['graded', 'submitted'] : ['missing', 'late'];
  }

  selectCategory(category: CanvasSubmissionCategory): void {
    if (category === this.activeCategory()) return;
    this.activeCategory.set(category);
    this.load(1);
  }

  load(page: number): void {
    const category = this.activeCategory();
    const requestId = ++this.requestId;
    this.loading.set(true);
    this.error.set(false);
    this.studentsService.canvasSubmissions(
      this.studentId,
      category,
      page,
      this.pageSize,
      this.academicYear,
    ).pipe(finalize(() => {
      if (requestId === this.requestId) this.loading.set(false);
    })).subscribe({
      next: (result) => {
        if (requestId !== this.requestId || category !== this.activeCategory()) return;
        this.items.set(result.items);
        this.page.set(result.page);
        this.totalCount.set(result.totalCount);
        this.totalPages.set(result.totalPages);
      },
      error: () => {
        if (requestId === this.requestId) this.error.set(true);
      },
    });
  }

  sheetTitle(): string {
    return this.languageService.t(
      this.kind === 'assignmentActivity'
        ? 'studentProfile.canvas.sheet.assignmentActivityTitle'
        : 'studentProfile.canvas.sheet.needsAttentionTitle',
    );
  }

  categoryLabel(category: CanvasSubmissionCategory): string {
    return this.languageService.t(`studentProfile.canvas.sheet.category.${category}`);
  }

  categoryCount(category: CanvasSubmissionCategory): number {
    switch (category) {
      case 'graded': return this.gradedCount;
      case 'submitted': return this.submittedCount;
      case 'missing': return this.missingCount;
      case 'late': return this.lateCount;
    }
  }

  workflowLabel(value: string): string {
    const key = `studentProfile.canvas.sheet.workflow.${value || 'unknown'}`;
    const translated = this.languageService.t(key);
    return translated === key ? (value || '—') : translated;
  }

  formatDate(value?: string | null): string {
    if (!value) return '—';
    return new Intl.DateTimeFormat(this.languageService.language() === 'vi' ? 'vi-VN' : 'en-US', {
      dateStyle: 'medium',
      timeStyle: 'short',
    }).format(new Date(value));
  }

  paginationSummary(): string {
    return this.languageService.t('pagination.pageOf')
      .replace('{page}', String(this.page()))
      .replace('{pages}', String(this.totalPages()))
      .replace('{total}', String(this.totalCount()));
  }
}
