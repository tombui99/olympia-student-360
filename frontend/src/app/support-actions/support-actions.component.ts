import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthenticationService } from '../core/auth/auth.service';
import { LanguageService } from '../core/language/language.service';
import { ToastService } from '../core/toast/toast.service';
import { SupportActionFormSheetComponent } from './support-action-form-sheet.component';
import {
  StudentSupportAction,
  StudentSupportActionsPage,
  SupportActionsService,
  SupportCategory,
} from './support-actions.service';

@Component({
  selector: 'app-support-actions',
  imports: [NgClass, FormsModule, RouterLink, SupportActionFormSheetComponent],
  template: `
    <section class="space-y-6">
      <header class="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p class="text-sm font-semibold uppercase tracking-[0.18em] text-[#8a6811]">{{ t('support.eyebrow') }}</p>
          <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ t('support.title') }}</h2>
          <p class="mt-2 max-w-3xl text-[#6f6877]">{{ t('support.description') }}</p>
        </div>
        <button type="button" class="inline-flex items-center gap-2 rounded-md bg-[#32124f] px-5 py-3 text-sm font-semibold text-white shadow-[0_10px_24px_rgba(50,18,79,0.18)] transition hover:bg-[#4b2070]" (click)="openCreate()">
          <span class="text-lg leading-none">＋</span>{{ t('support.addAction') }}
        </button>
      </header>

      <div class="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <article class="rounded-lg border border-[#e6deeb] bg-white p-5 shadow-[0_10px_30px_rgba(50,18,79,0.06)]">
          <p class="text-sm text-[#746b7b]">{{ t('support.summary.total') }}</p>
          <p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ pageData()?.totalCount ?? '—' }}</p>
        </article>
        <article class="rounded-lg border border-[#d9d2ee] bg-[#f8f6ff] p-5 shadow-[0_10px_30px_rgba(50,18,79,0.06)]">
          <p class="text-sm text-[#655b77]">{{ t('support.summary.active') }}</p>
          <p class="mt-2 text-3xl font-semibold text-[#55408c]">{{ pageData()?.activeCount ?? '—' }}</p>
        </article>
        <article class="rounded-lg border border-[#f0d8a5] bg-[#fffaf0] p-5 shadow-[0_10px_30px_rgba(50,18,79,0.06)]">
          <p class="text-sm text-[#795d1f]">{{ t('support.summary.followUp') }}</p>
          <p class="mt-2 text-3xl font-semibold text-[#9a6900]">{{ pageData()?.followUpDueCount ?? '—' }}</p>
        </article>
        <article class="rounded-lg border border-[#cce7d5] bg-[#f3fbf5] p-5 shadow-[0_10px_30px_rgba(50,18,79,0.06)]">
          <p class="text-sm text-[#3d6a4d]">{{ t('support.summary.achieved') }}</p>
          <p class="mt-2 text-3xl font-semibold text-[#257044]">{{ pageData()?.goalAchievedCount ?? '—' }}</p>
        </article>
      </div>

      <form class="rounded-lg border border-[#e6deeb] bg-white p-5 shadow-[0_10px_30px_rgba(50,18,79,0.06)]" (ngSubmit)="applyFilters()">
        <div class="grid gap-4 md:grid-cols-2 xl:grid-cols-[minmax(16rem,1.7fr)_1fr_1fr_1fr_auto] xl:items-end">
          <label class="block">
            <span class="text-xs font-semibold uppercase tracking-[0.1em] text-[#746b7b]">{{ t('support.filters.search') }}</span>
            <input class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-2.5 text-sm outline-none focus:border-[#6d3a91] focus:ring-2 focus:ring-[#6d3a91]/10" name="search" [(ngModel)]="search" [placeholder]="t('support.filters.searchPlaceholder')" />
          </label>
          <label class="block">
            <span class="text-xs font-semibold uppercase tracking-[0.1em] text-[#746b7b]">{{ t('support.filters.category') }}</span>
            <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-2.5 text-sm outline-none" name="category" [(ngModel)]="category">
              <option value="">{{ t('support.filters.allCategories') }}</option>
              @for (option of availableCategories(); track option) { <option [value]="option">{{ categoryLabel(option) }}</option> }
            </select>
          </label>
          <label class="block">
            <span class="text-xs font-semibold uppercase tracking-[0.1em] text-[#746b7b]">{{ t('support.filters.status') }}</span>
            <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-2.5 text-sm outline-none" name="status" [(ngModel)]="status">
              <option value="">{{ t('support.filters.allStatuses') }}</option>
              <option value="Planned">{{ statusLabel('Planned') }}</option>
              <option value="InProgress">{{ statusLabel('InProgress') }}</option>
              <option value="FollowUpRequired">{{ statusLabel('FollowUpRequired') }}</option>
              <option value="Completed">{{ statusLabel('Completed') }}</option>
            </select>
          </label>
          <label class="block">
            <span class="text-xs font-semibold uppercase tracking-[0.1em] text-[#746b7b]">{{ t('support.filters.year') }}</span>
            <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-2.5 text-sm outline-none" name="academicYear" [(ngModel)]="academicYear">
              <option value="">{{ t('support.filters.allYears') }}</option>
              @for (year of pageData()?.availableAcademicYears ?? []; track year) { <option [value]="year">{{ year.replace('-', '–') }}</option> }
            </select>
          </label>
          <button type="submit" class="rounded-md border border-[#32124f] bg-[#32124f] px-5 py-2.5 text-sm font-semibold text-white hover:bg-[#4b2070]">{{ t('support.filters.apply') }}</button>
        </div>
      </form>

      <section class="overflow-hidden rounded-lg border border-[#e6deeb] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.07)]">
        <div class="flex flex-wrap items-center justify-between gap-3 border-b border-[#ebe6ef] px-5 py-4">
          <div>
            <h3 class="text-lg font-semibold text-[#32124f]">{{ t('support.listTitle') }}</h3>
            <p class="mt-0.5 text-sm text-[#7c7284]">{{ totalLabel() }}</p>
          </div>
          <p class="text-xs text-[#8a8190]">{{ t('support.listHint') }}</p>
        </div>

        @if (loading()) {
          <div class="grid min-h-56 place-items-center"><span class="h-9 w-9 animate-spin rounded-full border-4 border-[#e6dfea] border-t-[#6d3a91]"></span></div>
        } @else if (loadError()) {
          <p class="m-5 rounded-md bg-red-50 p-5 text-center text-sm text-red-700">{{ t('support.loadError') }}</p>
        } @else {
          <div class="divide-y divide-[#eee8f1]">
            @for (action of pageData()?.items ?? []; track action.id) {
              <article class="group grid gap-4 p-5 transition hover:bg-[#fdfcff] lg:grid-cols-[12rem_minmax(0,1fr)_13rem]">
                <div>
                  <p class="text-xs font-semibold uppercase tracking-[0.1em] text-[#8a8190]">{{ formatDate(action.actionDate) }}</p>
                  <a class="mt-2 block font-semibold text-[#32124f] hover:underline" [routerLink]="['/students', action.studentId]" [queryParams]="{ section: 'support' }">{{ action.studentName }}</a>
                  <p class="mt-0.5 text-xs text-[#7c7284]">{{ action.sisUserId }} · {{ t('support.grade') }} {{ action.gradeLevel || '—' }} · {{ action.homeroom || '—' }}</p>
                  <span class="mt-3 inline-flex rounded-full px-2.5 py-1 text-xs font-semibold" [ngClass]="categoryClass(action.category)">{{ categoryLabel(action.category) }}</span>
                </div>
                <div class="min-w-0 border-l-2 pl-4" [ngClass]="categoryBorderClass(action.category)">
                  <div class="flex flex-wrap items-center gap-2">
                    <h4 class="text-base font-semibold text-[#32124f]">{{ action.title }}</h4>
                    @if (action.subject) { <span class="rounded-full bg-[#f1edf4] px-2 py-0.5 text-xs text-[#5b287d]">{{ action.subject }}</span> }
                  </div>
                  <p class="mt-2 text-xs font-semibold uppercase tracking-[0.1em] text-[#8a6811]">{{ t('support.goal') }}</p>
                  <p class="mt-1 text-sm leading-6 text-[#5f5666]">{{ action.goal }}</p>
                  <p class="mt-2 text-xs font-semibold uppercase tracking-[0.1em] text-[#7c7284]">{{ t('support.actionTaken') }}</p>
                  <p class="mt-1 line-clamp-2 text-sm leading-6 text-[#5f5666]">{{ action.actionTaken }}</p>
                  @if (action.result) {
                    <div class="mt-3 rounded-md bg-[#f7faf5] px-3 py-2.5">
                      <span class="text-xs font-semibold text-[#3d6a4d]">{{ t('support.result') }}</span>
                      <p class="mt-0.5 text-sm text-[#4e5f52]">{{ action.result }}</p>
                    </div>
                  }
                </div>
                <div class="flex flex-col items-start gap-2 lg:items-end">
                  <span class="rounded-full px-2.5 py-1 text-xs font-semibold" [ngClass]="statusClass(action.status)">{{ statusLabel(action.status) }}</span>
                  <span class="rounded-full px-2.5 py-1 text-xs font-semibold" [ngClass]="progressClass(action.progress)">{{ progressLabel(action.progress) }}</span>
                  @if (action.followUpDate) {
                    <p class="text-xs" [ngClass]="isOverdue(action) ? 'font-semibold text-amber-700' : 'text-[#7c7284]'">{{ t('support.followUp') }}: {{ formatDate(action.followUpDate) }}</p>
                  }
                  <p class="text-xs text-[#8a8190]">{{ action.updatedByName || action.createdByName }}</p>
                  <div class="mt-auto flex gap-2 pt-2">
                    <button type="button" class="rounded-md border border-[#d8d0df] px-3 py-1.5 text-xs font-semibold text-[#32124f] hover:bg-[#f7f3fa]" (click)="edit(action)">{{ t('common.edit') }}</button>
                    <button type="button" class="rounded-md border border-red-200 px-3 py-1.5 text-xs font-semibold text-red-700 hover:bg-red-50" (click)="remove(action)">{{ t('common.delete') }}</button>
                  </div>
                </div>
              </article>
            } @empty {
              <div class="p-12 text-center">
                <span class="mx-auto grid h-14 w-14 place-items-center rounded-full bg-[#f3eff6] text-2xl text-[#5b287d]">♡</span>
                <h4 class="mt-4 font-semibold text-[#32124f]">{{ t('support.emptyTitle') }}</h4>
                <p class="mt-1 text-sm text-[#7c7284]">{{ t('support.emptyHint') }}</p>
              </div>
            }
          </div>
        }

        @if ((pageData()?.totalPages ?? 0) > 1) {
          <footer class="flex items-center justify-between gap-3 border-t border-[#ebe6ef] bg-[#faf9f6] px-5 py-4">
            <p class="text-sm text-[#7c7284]">{{ paginationLabel() }}</p>
            <div class="flex gap-2">
              <button class="rounded-md border border-[#d8d0df] bg-white px-4 py-2 text-sm font-semibold text-[#32124f] disabled:opacity-40" type="button" [disabled]="(pageData()?.page ?? 1) <= 1" (click)="goToPage((pageData()?.page ?? 1) - 1)">{{ t('pagination.previous') }}</button>
              <button class="rounded-md border border-[#d8d0df] bg-white px-4 py-2 text-sm font-semibold text-[#32124f] disabled:opacity-40" type="button" [disabled]="(pageData()?.page ?? 1) >= (pageData()?.totalPages ?? 1)" (click)="goToPage((pageData()?.page ?? 1) + 1)">{{ t('pagination.next') }}</button>
            </div>
          </footer>
        }
      </section>
    </section>

    @if (formOpen()) {
      <app-support-action-form-sheet [action]="editingAction()" (close)="closeForm()" (saved)="onSaved()" />
    }
  `,
})
export class SupportActionsComponent implements OnInit {
  private readonly service = inject(SupportActionsService);
  private readonly auth = inject(AuthenticationService);
  private readonly language = inject(LanguageService);
  private readonly toast = inject(ToastService);

  readonly pageData = signal<StudentSupportActionsPage | null>(null);
  readonly loading = signal(true);
  readonly loadError = signal(false);
  readonly formOpen = signal(false);
  readonly editingAction = signal<StudentSupportAction | null>(null);
  readonly totalLabel = computed(() => this.t('support.totalResults').replace('{count}', String(this.pageData()?.totalCount ?? 0)));

  search = '';
  category = '';
  status = '';
  academicYear = '';
  private page = 1;

  ngOnInit(): void { this.load(); }

  availableCategories(): SupportCategory[] {
    const result: SupportCategory[] = [];
    if (this.auth.canAccess('academic')) result.push('Learning', 'Behaviour');
    if (this.auth.canAccess('health')) result.push('MentalHealth');
    return result;
  }

  applyFilters(): void { this.page = 1; this.load(); }
  goToPage(page: number): void { this.page = page; this.load(); }
  openCreate(): void { this.editingAction.set(null); this.formOpen.set(true); }
  edit(action: StudentSupportAction): void { this.editingAction.set(action); this.formOpen.set(true); }
  closeForm(): void { this.formOpen.set(false); this.editingAction.set(null); }
  onSaved(): void { this.closeForm(); this.toast.success(this.t('support.saved')); this.load(); }

  remove(action: StudentSupportAction): void {
    if (!window.confirm(this.t('support.deleteConfirm').replace('{title}', action.title))) return;
    this.service.delete(action.id).subscribe({
      next: () => { this.toast.success(this.t('support.deleted')); this.load(); },
      error: () => this.toast.error(this.t('support.deleteError')),
    });
  }

  categoryLabel(value: string): string { return this.t(`support.category.${value}`); }
  statusLabel(value: string): string { return this.t(`support.status.${value}`); }
  progressLabel(value: string): string { return this.t(`support.progress.${value}`); }
  categoryClass(value: string): string { return value === 'Learning' ? 'bg-blue-50 text-blue-700' : value === 'Behaviour' ? 'bg-amber-50 text-amber-700' : 'bg-teal-50 text-teal-700'; }
  categoryBorderClass(value: string): string { return value === 'Learning' ? 'border-blue-300' : value === 'Behaviour' ? 'border-amber-300' : 'border-teal-300'; }
  statusClass(value: string): string { return value === 'Completed' ? 'bg-emerald-50 text-emerald-700' : value === 'FollowUpRequired' ? 'bg-amber-50 text-amber-700' : value === 'Planned' ? 'bg-slate-100 text-slate-600' : 'bg-violet-50 text-violet-700'; }
  progressClass(value: string): string { return value === 'GoalAchieved' ? 'bg-emerald-100 text-emerald-800' : value === 'NeedsMoreSupport' || value === 'NoChange' ? 'bg-red-50 text-red-700' : value === 'SignificantProgress' || value === 'SomeProgress' ? 'bg-blue-50 text-blue-700' : 'bg-slate-100 text-slate-600'; }
  formatDate(value: string): string { return new Intl.DateTimeFormat(this.language.language() === 'vi' ? 'vi-VN' : 'en-GB', { day: '2-digit', month: 'short', year: 'numeric' }).format(new Date(value)); }
  isOverdue(action: StudentSupportAction): boolean { return !!action.followUpDate && action.status !== 'Completed' && new Date(action.followUpDate).getTime() < Date.now(); }
  paginationLabel(): string { return this.t('pagination.pageOf').replace('{page}', String(this.pageData()?.page ?? 1)).replace('{pages}', String(this.pageData()?.totalPages ?? 1)).replace('{total}', String(this.pageData()?.totalCount ?? 0)); }
  t(key: string): string { return this.language.t(key); }

  private load(): void {
    this.loading.set(true);
    this.loadError.set(false);
    this.service.list({ page: this.page, pageSize: 20, search: this.search, category: this.category, status: this.status, academicYear: this.academicYear }).subscribe({
      next: (data) => { this.pageData.set(data); this.page = data.page; this.loading.set(false); },
      error: () => { this.loading.set(false); this.loadError.set(true); },
    });
  }
}
