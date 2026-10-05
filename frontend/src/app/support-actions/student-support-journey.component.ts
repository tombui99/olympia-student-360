import { Component, computed, inject, Input, OnChanges, OnInit, signal, SimpleChanges } from '@angular/core';
import { NgClass } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LanguageService } from '../core/language/language.service';
import { ToastService } from '../core/toast/toast.service';
import { ManualEntryStudent } from '../imports/manual-entry.service';
import { SupportActionFormSheetComponent } from './support-action-form-sheet.component';
import {
  StudentSupportAction,
  StudentSupportJourneyPage,
  SupportActionsService,
} from './support-actions.service';

@Component({
  selector: 'app-student-support-journey',
  imports: [NgClass, RouterLink, SupportActionFormSheetComponent],
  template: `
    <section class="space-y-4">
      <div class="overflow-hidden rounded-lg border border-[#dfd5e5] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
        <div class="flex flex-wrap items-start justify-between gap-3 bg-gradient-to-r from-[#f4eef8] via-white to-[#fff8e5] px-5 py-4">
          <div>
            <p class="text-[10px] font-semibold uppercase tracking-[0.16em] text-[#8a6811]">{{ t('support.journey.eyebrow') }}</p>
            <h3 class="mt-0.5 text-lg font-semibold text-[#32124f]">{{ t('support.journey.title') }}</h3>
            <p class="mt-0.5 max-w-2xl text-xs leading-5 text-[#6f6877]">{{ t('support.journey.description') }}</p>
          </div>
          <button type="button" class="inline-flex items-center gap-1.5 rounded-md bg-[#32124f] px-3.5 py-2 text-sm font-semibold text-white hover:bg-[#4b2070]" (click)="openCreate()"><span aria-hidden="true">＋</span>{{ t('support.addAction') }}</button>
        </div>

        <div class="grid border-t border-[#e9e2ed] sm:grid-cols-3">
          <div class="border-b border-[#e9e2ed] px-4 py-3 sm:border-b-0 sm:border-r">
            <p class="text-xs font-semibold uppercase tracking-[0.1em] text-blue-700">{{ t('support.category.Learning') }}</p>
            <p class="mt-0.5 text-xl font-semibold text-[#32124f]">{{ categoryCount('Learning') }}</p>
          </div>
          <div class="border-b border-[#e9e2ed] px-4 py-3 sm:border-b-0 sm:border-r">
            <p class="text-xs font-semibold uppercase tracking-[0.1em] text-amber-700">{{ t('support.category.Behaviour') }}</p>
            <p class="mt-0.5 text-xl font-semibold text-[#32124f]">{{ categoryCount('Behaviour') }}</p>
          </div>
          <div class="px-4 py-3">
            <p class="text-xs font-semibold uppercase tracking-[0.1em] text-teal-700">{{ t('support.category.MentalHealth') }}</p>
            <p class="mt-0.5 text-xl font-semibold text-[#32124f]">{{ categoryCount('MentalHealth') }}</p>
          </div>
        </div>
      </div>

      @if (loading()) {
        <div class="grid min-h-52 place-items-center rounded-lg border border-[#ebe6ef] bg-white"><span class="h-9 w-9 animate-spin rounded-full border-4 border-[#e6dfea] border-t-[#6d3a91]"></span></div>
      } @else if (loadError()) {
        <p class="rounded-lg border border-red-200 bg-red-50 p-6 text-center text-sm text-red-700">{{ t('support.loadError') }}</p>
      } @else if (!actions().length) {
        <div class="rounded-lg border border-dashed border-[#d6cadd] bg-white p-10 text-center">
          <span class="mx-auto grid h-14 w-14 place-items-center rounded-full bg-[#f3eff6] text-2xl text-[#5b287d]">♡</span>
          <h4 class="mt-4 font-semibold text-[#32124f]">{{ t('support.journey.emptyTitle') }}</h4>
          <p class="mx-auto mt-1 max-w-xl text-sm text-[#7c7284]">{{ t('support.journey.emptyHint') }}</p>
          <button class="mt-5 rounded-md border border-[#6d3a91] px-4 py-2 text-sm font-semibold text-[#5b287d] hover:bg-[#f7f1fb]" type="button" (click)="openCreate()">{{ t('support.addFirstAction') }}</button>
        </div>
      } @else {
        <div class="rounded-lg border border-[#ebe6ef] bg-white px-4 py-4 shadow-[0_12px_35px_rgba(50,18,79,0.07)] sm:px-5">
          <div class="mb-4 flex flex-wrap items-center justify-between gap-3">
            <div>
              <h4 class="font-semibold text-[#32124f]">{{ t('support.journey.timelineTitle') }}</h4>
              <p class="mt-0.5 text-xs text-[#7c7284]">{{ t('support.journey.timelineHint') }}</p>
            </div>
            <a class="text-xs font-semibold text-[#5b287d] hover:underline" routerLink="/support-journey">{{ t('support.journey.viewAll') }} →</a>
          </div>

          <div class="relative ml-2 border-l-2 border-[#e2d9e7] pl-7">
            @for (action of actions(); track action.id; let last = $last) {
              <article class="relative" [class.pb-4]="!last">
                <span class="absolute -left-[2.29rem] top-1 h-4 w-4 rounded-full border-[3px] border-white shadow-sm" [ngClass]="dotClass(action.category)"></span>
                <div class="rounded-lg border border-[#e8e1ec] p-4 transition hover:border-[#cfc0d8] hover:shadow-[0_8px_24px_rgba(50,18,79,0.07)]">
                  <div class="flex flex-wrap items-start justify-between gap-2">
                    <div class="min-w-0">
                      <div class="flex flex-wrap items-center gap-1.5">
                        <span class="rounded-full px-3 py-1 text-sm font-semibold" [ngClass]="categoryClass(action.category)">{{ t('support.category.' + action.category) }}</span>
                        <span class="text-[11px] text-[#8a8190]">{{ formatDate(action.actionDate) }} · {{ action.academicYear.replace('-', '–') }}</span>
                        @if (action.subject) { <span class="rounded-full bg-[#f1edf4] px-2 py-0.5 text-[11px] text-[#5b287d]">{{ action.subject }}</span> }
                      </div>
                      <h5 class="mt-1.5 text-base font-semibold text-[#32124f]">{{ action.title }}</h5>
                    </div>
                    <div class="flex flex-wrap gap-1.5">
                      <span class="rounded-full px-3 py-1 text-sm font-semibold" [ngClass]="statusClass(action.status)">{{ t('support.status.' + action.status) }}</span>
                      <span class="rounded-full px-3 py-1 text-sm font-semibold" [ngClass]="progressClass(action.progress)">{{ t('support.progress.' + action.progress) }}</span>
                    </div>
                  </div>

                  <div class="mt-3 grid gap-3 lg:grid-cols-2">
                    <div>
                      <p class="text-[10px] font-semibold uppercase tracking-[0.1em] text-[#8a6811]">{{ t('support.goal') }}</p>
                      <p class="mt-0.5 text-[13px] leading-5 text-[#5f5666]">{{ action.goal }}</p>
                    </div>
                    <div>
                      <p class="text-[10px] font-semibold uppercase tracking-[0.1em] text-[#7c7284]">{{ t('support.actionTaken') }}</p>
                      <p class="mt-0.5 text-[13px] leading-5 text-[#5f5666]">{{ action.actionTaken }}</p>
                    </div>
                  </div>

                  @if (action.result || action.nextStep) {
                    <div class="mt-3 grid gap-3 rounded-md border border-[#e0eadc] bg-[#f7fbf5] p-3 lg:grid-cols-2">
                      <div>
                        <p class="text-[10px] font-semibold uppercase tracking-[0.1em] text-[#39714b]">{{ t('support.result') }}</p>
                        <p class="mt-0.5 text-[13px] leading-5 text-[#4e5f52]">{{ action.result || t('support.notRecorded') }}</p>
                      </div>
                      <div>
                        <p class="text-[10px] font-semibold uppercase tracking-[0.1em] text-[#39714b]">{{ t('support.nextStep') }}</p>
                        <p class="mt-0.5 text-[13px] leading-5 text-[#4e5f52]">{{ action.nextStep || t('support.notRecorded') }}</p>
                      </div>
                    </div>
                  }

                  <div class="mt-3 flex flex-wrap items-center gap-x-3 gap-y-1.5 border-t border-[#eee8f1] pt-2.5 text-[11px] text-[#7c7284]">
                    <span>{{ t('support.recordedBy') }} <strong class="font-semibold text-[#5f5666]">{{ action.updatedByName || action.createdByName }}</strong></span>
                    @if (action.followUpDate) { <span [ngClass]="isOverdue(action) ? 'font-semibold text-amber-700' : ''">{{ t('support.followUp') }}: {{ formatDate(action.followUpDate) }}</span> }
                    @if (action.shareWithFamily) { <span class="inline-flex items-center gap-1.5 rounded-full bg-emerald-50 px-2.5 py-1 text-xs font-semibold text-emerald-700"><span aria-hidden="true">●</span>{{ t('support.familyVisible') }}</span> }
                    <span class="ml-auto flex gap-2">
                      <button type="button" class="font-semibold text-[#5b287d] hover:underline" (click)="edit(action)">{{ t('common.edit') }}</button>
                      <button type="button" class="font-semibold text-red-700 hover:underline" (click)="remove(action)">{{ t('common.delete') }}</button>
                    </span>
                  </div>
                </div>
              </article>
            }
          </div>

          @if ((pageData()?.totalPages ?? 0) > 1) {
            <footer class="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-[#ebe6ef] pt-4">
              <p class="text-xs text-[#7c7284]">{{ paginationLabel() }}</p>
              <div class="flex gap-2">
                <button class="rounded-md border border-[#d8d0df] bg-white px-3 py-2 text-xs font-semibold text-[#32124f] disabled:cursor-not-allowed disabled:opacity-40" type="button" [disabled]="(pageData()?.page ?? 1) <= 1 || loading()" (click)="goToPage((pageData()?.page ?? 1) - 1)">{{ t('pagination.previous') }}</button>
                <button class="rounded-md border border-[#d8d0df] bg-white px-3 py-2 text-xs font-semibold text-[#32124f] disabled:cursor-not-allowed disabled:opacity-40" type="button" [disabled]="(pageData()?.page ?? 1) >= (pageData()?.totalPages ?? 1) || loading()" (click)="goToPage((pageData()?.page ?? 1) + 1)">{{ t('pagination.next') }}</button>
              </div>
            </footer>
          }
        </div>
      }
    </section>

    @if (formOpen()) {
      <app-support-action-form-sheet [action]="editingAction()" [fixedStudent]="fixedStudent()" (close)="closeForm()" (saved)="onSaved()" />
    }
  `,
})
export class StudentSupportJourneyComponent implements OnInit, OnChanges {
  @Input({ required: true }) studentId = 0;
  @Input() studentName = '';
  @Input() sisUserId = '';
  @Input() gradeLevel: string | null = null;
  @Input() homeroom: string | null = null;
  @Input() academicYear = '';
  @Input() term = 'all';

  private readonly service = inject(SupportActionsService);
  private readonly language = inject(LanguageService);
  private readonly toast = inject(ToastService);
  readonly pageData = signal<StudentSupportJourneyPage | null>(null);
  readonly actions = computed(() => this.pageData()?.items ?? []);
  readonly loading = signal(true);
  readonly loadError = signal(false);
  readonly formOpen = signal(false);
  readonly editingAction = signal<StudentSupportAction | null>(null);
  readonly fixedStudent = computed<ManualEntryStudent>(() => ({ id: this.studentId, fullName: this.studentName, sisUserId: this.sisUserId, gradeLevel: this.gradeLevel, homeroom: this.homeroom }));
  private page = 1;
  private readonly pageSize = 5;

  ngOnInit(): void { this.load(); }
  ngOnChanges(changes: SimpleChanges): void {
    const studentChanged = changes['studentId'] && !changes['studentId'].firstChange;
    const yearChanged = changes['academicYear'] && !changes['academicYear'].firstChange;
    const termChanged = changes['term'] && !changes['term'].firstChange;
    if (studentChanged || yearChanged || termChanged) {
      this.page = 1;
      this.load();
    }
  }
  categoryCount(category: string): number {
    const data = this.pageData();
    if (!data) return 0;
    if (category === 'Learning') return data.learningCount;
    if (category === 'Behaviour') return data.behaviourCount;
    return data.mentalHealthCount;
  }
  goToPage(page: number): void {
    const totalPages = this.pageData()?.totalPages ?? 1;
    if (page < 1 || page > totalPages || page === this.page) return;
    this.page = page;
    this.load();
  }
  openCreate(): void { this.editingAction.set(null); this.formOpen.set(true); }
  edit(action: StudentSupportAction): void { this.editingAction.set(action); this.formOpen.set(true); }
  closeForm(): void { this.formOpen.set(false); this.editingAction.set(null); }
  onSaved(): void { this.closeForm(); this.toast.success(this.t('support.saved')); this.page = 1; this.load(); }
  remove(action: StudentSupportAction): void {
    if (!window.confirm(this.t('support.deleteConfirm').replace('{title}', action.title))) return;
    this.service.delete(action.id).subscribe({ next: () => { this.toast.success(this.t('support.deleted')); this.load(); }, error: () => this.toast.error(this.t('support.deleteError')) });
  }
  categoryClass(value: string): string { return value === 'Learning' ? 'bg-blue-50 text-blue-700' : value === 'Behaviour' ? 'bg-amber-50 text-amber-700' : 'bg-teal-50 text-teal-700'; }
  dotClass(value: string): string { return value === 'Learning' ? 'bg-blue-500' : value === 'Behaviour' ? 'bg-amber-500' : 'bg-teal-500'; }
  statusClass(value: string): string { return value === 'Completed' ? 'bg-emerald-50 text-emerald-700' : value === 'FollowUpRequired' ? 'bg-amber-50 text-amber-700' : value === 'Planned' ? 'bg-slate-100 text-slate-600' : 'bg-violet-50 text-violet-700'; }
  progressClass(value: string): string { return value === 'GoalAchieved' ? 'bg-emerald-100 text-emerald-800' : value === 'NeedsMoreSupport' || value === 'NoChange' ? 'bg-red-50 text-red-700' : value === 'SignificantProgress' || value === 'SomeProgress' ? 'bg-blue-50 text-blue-700' : 'bg-slate-100 text-slate-600'; }
  isOverdue(action: StudentSupportAction): boolean { return !!action.followUpDate && action.status !== 'Completed' && new Date(action.followUpDate).getTime() < Date.now(); }
  formatDate(value: string): string { return new Intl.DateTimeFormat(this.language.language() === 'vi' ? 'vi-VN' : 'en-GB', { day: '2-digit', month: 'short', year: 'numeric' }).format(new Date(value)); }
  paginationLabel(): string { return this.t('pagination.pageOf').replace('{page}', String(this.pageData()?.page ?? 1)).replace('{pages}', String(this.pageData()?.totalPages ?? 1)).replace('{total}', String(this.pageData()?.totalCount ?? 0)); }
  t(key: string): string { return this.language.t(key); }

  private load(): void {
    if (!this.studentId) return;
    this.loading.set(true);
    this.loadError.set(false);
    this.service.forStudent(this.studentId, this.academicYear, this.term, this.page, this.pageSize).subscribe({
      next: (data) => {
        this.pageData.set(data);
        this.page = data.page;
        this.loading.set(false);
      },
      error: () => { this.loading.set(false); this.loadError.set(true); },
    });
  }
}
