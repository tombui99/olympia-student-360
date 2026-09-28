import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { NgClass } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LanguageService } from '../core/language/language.service';
import { ToastService } from '../core/toast/toast.service';
import { CareerGuidanceFormSheetComponent } from './career-guidance-form-sheet.component';
import {
  CareerGuidancePage,
  CareerGuidanceRecord,
  CareerGuidanceRecordType,
  CareerGuidanceService,
} from './career-guidance.service';

@Component({
  selector: 'app-career-guidance',
  imports: [NgClass, FormsModule, RouterLink, CareerGuidanceFormSheetComponent],
  template: `
    <section class="space-y-6">
      <header class="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p class="text-sm font-semibold uppercase tracking-[0.18em] text-[#8a6811]">{{ t('career.eyebrow') }}</p>
          <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ t('career.title') }}</h2>
          <p class="mt-2 max-w-3xl text-[#6f6877]">{{ t('career.description') }}</p>
        </div>
        <button type="button" class="inline-flex items-center gap-2 rounded-md bg-[#32124f] px-5 py-3 text-sm font-semibold text-white shadow-[0_10px_24px_rgba(50,18,79,0.18)] transition hover:bg-[#4b2070]" (click)="openCreate()">
          <span class="text-lg leading-none">＋</span>{{ t('career.addRecord') }}
        </button>
      </header>

      <div class="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <article class="rounded-lg border border-[#e6deeb] bg-white p-5 shadow-[0_10px_30px_rgba(50,18,79,0.06)]"><p class="text-sm text-[#746b7b]">{{ t('career.summary.total') }}</p><p class="mt-2 text-3xl font-semibold text-[#32124f]">{{ pageData()?.totalCount ?? '—' }}</p></article>
        <article class="rounded-lg border border-[#d9d2ee] bg-[#f8f6ff] p-5 shadow-[0_10px_30px_rgba(50,18,79,0.06)]"><p class="text-sm text-[#655b77]">{{ t('career.summary.inProgress') }}</p><p class="mt-2 text-3xl font-semibold text-[#55408c]">{{ pageData()?.inProgressCount ?? '—' }}</p></article>
        <article class="rounded-lg border border-[#f0d8a5] bg-[#fffaf0] p-5 shadow-[0_10px_30px_rgba(50,18,79,0.06)]"><p class="text-sm text-[#795d1f]">{{ t('career.summary.needsAttention') }}</p><p class="mt-2 text-3xl font-semibold text-[#9a6900]">{{ pageData()?.needsAttentionCount ?? '—' }}</p></article>
        <article class="rounded-lg border border-[#cce7d5] bg-[#f3fbf5] p-5 shadow-[0_10px_30px_rgba(50,18,79,0.06)]"><p class="text-sm text-[#3d6a4d]">{{ t('career.summary.completed') }}</p><p class="mt-2 text-3xl font-semibold text-[#257044]">{{ pageData()?.completedCount ?? '—' }}</p></article>
      </div>

      <form class="rounded-lg border border-[#e6deeb] bg-white p-5 shadow-[0_10px_30px_rgba(50,18,79,0.06)]" (ngSubmit)="applyFilters()">
        <div class="grid gap-4 md:grid-cols-2 xl:grid-cols-[minmax(15rem,1.7fr)_1fr_1fr_1fr_1fr_auto] xl:items-end">
          <label class="block"><span class="text-xs font-semibold uppercase tracking-[0.1em] text-[#746b7b]">{{ t('career.filters.search') }}</span><input class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-2.5 text-sm outline-none focus:border-[#6d3a91] focus:ring-2 focus:ring-[#6d3a91]/10" name="search" [(ngModel)]="search" [placeholder]="t('career.filters.searchPlaceholder')" /></label>
          <label class="block"><span class="text-xs font-semibold uppercase tracking-[0.1em] text-[#746b7b]">{{ t('career.filters.type') }}</span><select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-2.5 text-sm outline-none" name="recordType" [(ngModel)]="recordType"><option value="">{{ t('career.filters.allTypes') }}</option>@for (option of recordTypes; track option) { <option [value]="option">{{ typeLabel(option) }}</option> }</select></label>
          <label class="block"><span class="text-xs font-semibold uppercase tracking-[0.1em] text-[#746b7b]">{{ t('career.filters.status') }}</span><select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-2.5 text-sm outline-none" name="status" [(ngModel)]="status"><option value="">{{ t('career.filters.allStatuses') }}</option><option value="Planned">{{ statusLabel('Planned') }}</option><option value="InProgress">{{ statusLabel('InProgress') }}</option><option value="NeedsAttention">{{ statusLabel('NeedsAttention') }}</option><option value="Completed">{{ statusLabel('Completed') }}</option></select></label>
          <label class="block"><span class="text-xs font-semibold uppercase tracking-[0.1em] text-[#746b7b]">{{ t('career.filters.year') }}</span><select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-2.5 text-sm outline-none" name="academicYear" [(ngModel)]="academicYear"><option value="">{{ t('career.filters.allYears') }}</option>@for (year of pageData()?.availableAcademicYears ?? []; track year) { <option [value]="year">{{ year.replace('-', '–') }}</option> }</select></label>
          <label class="block"><span class="text-xs font-semibold uppercase tracking-[0.1em] text-[#746b7b]">{{ t('career.filters.term') }}</span><select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-2.5 text-sm outline-none" name="term" [(ngModel)]="term"><option value="">{{ t('career.filters.allTerms') }}</option><option value="hk1">{{ t('dashboard.period.term.hk1') }}</option><option value="hk2">{{ t('dashboard.period.term.hk2') }}</option></select></label>
          <button type="submit" class="rounded-md border border-[#32124f] bg-[#32124f] px-5 py-2.5 text-sm font-semibold text-white hover:bg-[#4b2070]">{{ t('career.filters.apply') }}</button>
        </div>
      </form>

      <section class="overflow-hidden rounded-lg border border-[#e6deeb] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.07)]">
        <div class="flex flex-wrap items-center justify-between gap-3 border-b border-[#ebe6ef] px-5 py-4"><div><h3 class="text-lg font-semibold text-[#32124f]">{{ t('career.listTitle') }}</h3><p class="mt-0.5 text-sm text-[#7c7284]">{{ totalLabel() }}</p></div><p class="text-xs text-[#8a8190]">{{ t('career.listHint') }}</p></div>
        @if (loading()) {
          <div class="grid min-h-56 place-items-center"><span class="h-9 w-9 animate-spin rounded-full border-4 border-[#e6dfea] border-t-[#6d3a91]"></span></div>
        } @else if (loadError()) {
          <p class="m-5 rounded-md bg-red-50 p-5 text-center text-sm text-red-700">{{ t('career.loadError') }}</p>
        } @else {
          <div class="grid gap-4 p-4 lg:grid-cols-2">
            @for (record of pageData()?.items ?? []; track record.id) {
              <article class="flex min-w-0 flex-col rounded-lg border border-[#e7e0eb] bg-white p-5 transition hover:border-[#cdbdd8] hover:shadow-[0_10px_30px_rgba(50,18,79,0.08)]">
                <div class="flex flex-wrap items-start justify-between gap-3">
                  <div class="min-w-0">
                    <div class="flex flex-wrap items-center gap-2"><span class="rounded-full px-3 py-1 text-xs font-semibold" [ngClass]="typeClass(record.recordType)">{{ typeLabel(record.recordType) }}</span><span class="text-xs text-[#817687]">{{ formatDate(record.recordDate) }} · {{ record.academicYear.replace('-', '–') || '—' }}</span></div>
                    <h4 class="mt-3 text-lg font-semibold leading-snug text-[#32124f]">{{ record.title }}</h4>
                    <a class="mt-1 inline-block text-sm font-semibold text-[#5b287d] hover:underline" [routerLink]="['/students', record.studentId]" [queryParams]="{ section: 'career' }">{{ record.studentName }}</a>
                    <p class="mt-0.5 text-xs text-[#7c7284]">{{ record.sisUserId }} · {{ t('career.grade') }} {{ record.gradeLevel || '—' }} · {{ record.homeroom || '—' }}</p>
                  </div>
                  <span class="shrink-0 rounded-full px-3 py-1.5 text-sm font-semibold" [ngClass]="statusClass(record.status)">{{ statusLabel(record.status) }}</span>
                </div>

                @if (record.targetMajors || record.targetUniversities || record.careerInterests) {
                  <div class="mt-4 grid gap-3 rounded-md bg-[#faf8fb] p-3 sm:grid-cols-2">
                    @if (record.careerInterests || record.targetMajors) { <div><p class="text-[0.68rem] font-bold uppercase tracking-[0.12em] text-[#8a6811]">{{ t('career.direction') }}</p><p class="mt-1 line-clamp-2 text-sm text-[#554b5d]">{{ record.careerInterests || record.targetMajors }}</p></div> }
                    @if (record.targetUniversities) { <div><p class="text-[0.68rem] font-bold uppercase tracking-[0.12em] text-[#8a6811]">{{ t('career.universities') }}</p><p class="mt-1 line-clamp-2 text-sm text-[#554b5d]">{{ record.targetUniversities }}</p></div> }
                  </div>
                }
                @if (record.recordType === 'Internship' && (record.internshipCompany || record.internshipRole)) {
                  <div class="mt-4 rounded-md border border-[#dce7e3] bg-[#f5fbf8] p-3"><p class="text-[0.68rem] font-bold uppercase tracking-[0.12em] text-[#087466]">{{ t('career.internship') }}</p><p class="mt-1 text-sm font-semibold text-[#315d56]">{{ record.internshipCompany || '—' }}@if (record.internshipRole) { · {{ record.internshipRole }} }</p></div>
                }
                @if (primaryProgress(record) !== null) {
                  <div class="mt-4"><div class="flex justify-between text-xs"><span class="font-semibold text-[#5e5366]">{{ t('career.progress') }}</span><span class="font-bold text-[#32124f]">{{ primaryProgress(record) }}%</span></div><div class="mt-1.5 h-2 overflow-hidden rounded-full bg-[#eee9f1]"><span class="block h-full rounded-full bg-[#6d3a91]" [style.width.%]="primaryProgress(record)"></span></div></div>
                }
                @if (record.activitiesSummary || record.nextSteps) {
                  <div class="mt-4 border-t border-[#eee8f1] pt-3"><p class="line-clamp-2 text-sm leading-6 text-[#615766]">{{ record.activitiesSummary || record.nextSteps }}</p></div>
                }
                @if (expandedRecordId() === record.id) {
                  <div class="mt-4 rounded-lg border border-[#e4dce9] bg-[#faf8fb] p-4">
                    <p class="mb-3 text-xs font-bold uppercase tracking-[0.12em] text-[#6d3a91]">{{ t('career.details') }}</p>
                    <div class="grid gap-x-6 gap-y-4 sm:grid-cols-2">
                      @if (record.careerInterests) { <div><p class="detail-label">{{ t('career.form.careerInterests') }}</p><p class="detail-value">{{ record.careerInterests }}</p></div> }
                      @if (record.targetMajors) { <div><p class="detail-label">{{ t('career.form.targetMajors') }}</p><p class="detail-value">{{ record.targetMajors }}</p></div> }
                      @if (record.targetUniversities) { <div><p class="detail-label">{{ t('career.form.targetUniversities') }}</p><p class="detail-value">{{ record.targetUniversities }}</p></div> }
                      @if (record.targetCountries) { <div><p class="detail-label">{{ t('career.form.targetCountries') }}</p><p class="detail-value">{{ record.targetCountries }}</p></div> }
                      @if (record.qualityRating != null) { <div><p class="detail-label">{{ t('career.form.qualityRating') }}</p><p class="detail-value">{{ record.qualityRating }}/5</p></div> }
                      @if (record.activitiesSummary) { <div><p class="detail-label">{{ t('career.form.activitiesSummary') }}</p><p class="detail-value">{{ record.activitiesSummary }}</p></div> }
                      @if (record.issues) { <div><p class="detail-label">{{ t('career.form.issues') }}</p><p class="detail-value">{{ record.issues }}</p></div> }
                      @if (record.solutions) { <div><p class="detail-label">{{ t('career.form.solutions') }}</p><p class="detail-value">{{ record.solutions }}</p></div> }
                      @if (record.studentSupport) { <div><p class="detail-label">{{ t('career.form.studentSupport') }}</p><p class="detail-value">{{ record.studentSupport }}</p></div> }
                      @if (record.studentFeedback) { <div><p class="detail-label">{{ t('career.form.studentFeedback') }}</p><p class="detail-value">{{ record.studentFeedback }}</p></div> }
                      @if (record.nextSteps) { <div><p class="detail-label">{{ t('career.form.nextSteps') }}</p><p class="detail-value">{{ record.nextSteps }}</p></div> }
                      @if (record.recommendations) { <div><p class="detail-label">{{ t('career.form.recommendations') }}</p><p class="detail-value">{{ record.recommendations }}</p></div> }
                      @if (record.proposals) { <div><p class="detail-label">{{ t('career.form.proposals') }}</p><p class="detail-value">{{ record.proposals }}</p></div> }
                      @if (record.internshipStartDate || record.internshipEndDate) { <div><p class="detail-label">{{ t('career.internshipDates') }}</p><p class="detail-value">{{ record.internshipStartDate ? formatDate(record.internshipStartDate) : '—' }} – {{ record.internshipEndDate ? formatDate(record.internshipEndDate) : '—' }}</p></div> }
                      @if (record.internshipProgressPercent != null) { <div><p class="detail-label">{{ t('career.form.internshipProgress') }}</p><p class="detail-value">{{ record.internshipProgressPercent }}%</p></div> }
                      @if (record.internshipQualityRating != null) { <div><p class="detail-label">{{ t('career.form.internshipQuality') }}</p><p class="detail-value">{{ record.internshipQualityRating }}/5</p></div> }
                      @if (record.studentSelfAssessment) { <div><p class="detail-label">{{ t('career.form.studentSelfAssessment') }}</p><p class="detail-value">{{ record.studentSelfAssessment }}</p></div> }
                      @if (record.hostAssessment) { <div><p class="detail-label">{{ t('career.form.hostAssessment') }}</p><p class="detail-value">{{ record.hostAssessment }}</p></div> }
                      @if (record.finalProduct) { <div class="sm:col-span-2"><p class="detail-label">{{ t('career.form.finalProduct') }}</p><p class="detail-value">{{ record.finalProduct }}</p></div> }
                    </div>
                  </div>
                }
                <div class="mt-auto flex items-end justify-between gap-3 pt-4">
                  <p class="text-xs text-[#8a8190]">{{ t('career.recordedBy') }} {{ record.updatedByName || record.recordedByName || '—' }}</p>
                  <div class="flex flex-wrap justify-end gap-2">
                    @if (hasExpandedDetails(record)) { <button type="button" class="rounded-md border border-[#d8d0df] px-3 py-1.5 text-xs font-semibold text-[#5b287d] hover:bg-[#f7f3fa]" [attr.aria-expanded]="expandedRecordId() === record.id" (click)="toggleDetails(record.id)">{{ t(expandedRecordId() === record.id ? 'career.hideDetails' : 'career.viewDetails') }}</button> }
                    <button type="button" class="rounded-md border border-[#d8d0df] px-3 py-1.5 text-xs font-semibold text-[#32124f] hover:bg-[#f7f3fa]" (click)="edit(record)">{{ t('common.edit') }}</button><button type="button" class="rounded-md border border-red-200 px-3 py-1.5 text-xs font-semibold text-red-700 hover:bg-red-50" (click)="remove(record)">{{ t('common.delete') }}</button>
                  </div>
                </div>
              </article>
            } @empty {
              <div class="col-span-full p-12 text-center"><span class="mx-auto grid h-14 w-14 place-items-center rounded-full bg-[#f3eff6] text-2xl text-[#5b287d]">⌁</span><h4 class="mt-4 font-semibold text-[#32124f]">{{ t('career.emptyTitle') }}</h4><p class="mt-1 text-sm text-[#7c7284]">{{ t('career.emptyHint') }}</p></div>
            }
          </div>
        }
        @if ((pageData()?.totalPages ?? 0) > 1) {
          <footer class="flex items-center justify-between gap-3 border-t border-[#ebe6ef] bg-[#faf9f6] px-5 py-4"><p class="text-sm text-[#7c7284]">{{ paginationLabel() }}</p><div class="flex gap-2"><button class="rounded-md border border-[#d8d0df] bg-white px-4 py-2 text-sm font-semibold text-[#32124f] disabled:opacity-40" type="button" [disabled]="(pageData()?.page ?? 1) <= 1" (click)="goToPage((pageData()?.page ?? 1) - 1)">{{ t('pagination.previous') }}</button><button class="rounded-md border border-[#d8d0df] bg-white px-4 py-2 text-sm font-semibold text-[#32124f] disabled:opacity-40" type="button" [disabled]="(pageData()?.page ?? 1) >= (pageData()?.totalPages ?? 1)" (click)="goToPage((pageData()?.page ?? 1) + 1)">{{ t('pagination.next') }}</button></div></footer>
        }
      </section>
    </section>
    @if (formOpen()) { <app-career-guidance-form-sheet [record]="editingRecord()" (close)="closeForm()" (saved)="onSaved()" /> }
  `,
  styles: [`
    .detail-label { font-size: .6875rem; font-weight: 700; letter-spacing: .1em; text-transform: uppercase; color: #75697c; }
    .detail-value { margin-top: .25rem; white-space: pre-line; overflow-wrap: anywhere; font-size: .8125rem; line-height: 1.35rem; color: #4f4655; }
  `],
})
export class CareerGuidanceComponent implements OnInit {
  private readonly service = inject(CareerGuidanceService);
  private readonly language = inject(LanguageService);
  private readonly toast = inject(ToastService);
  readonly pageData = signal<CareerGuidancePage | null>(null);
  readonly loading = signal(true); readonly loadError = signal(false); readonly formOpen = signal(false); readonly editingRecord = signal<CareerGuidanceRecord | null>(null);
  readonly expandedRecordId = signal<number | null>(null);
  readonly totalLabel = computed(() => this.t('career.totalResults').replace('{count}', String(this.pageData()?.totalCount ?? 0)));
  readonly recordTypes: CareerGuidanceRecordType[] = ['GeneralPlan', 'UniversityApplication', 'Preparation', 'Internship', 'Counseling'];
  search = ''; recordType = ''; status = ''; academicYear = ''; term = ''; private page = 1;
  ngOnInit(): void { this.load(); }
  applyFilters(): void { this.page = 1; this.load(); }
  goToPage(page: number): void { this.page = page; this.load(); }
  openCreate(): void { this.editingRecord.set(null); this.formOpen.set(true); }
  edit(record: CareerGuidanceRecord): void { this.editingRecord.set(record); this.formOpen.set(true); }
  closeForm(): void { this.formOpen.set(false); this.editingRecord.set(null); }
  onSaved(): void { this.closeForm(); this.toast.success(this.t('career.saved')); this.load(); }
  toggleDetails(id: number): void { this.expandedRecordId.update((current) => current === id ? null : id); }
  remove(record: CareerGuidanceRecord): void { if (!window.confirm(this.t('career.deleteConfirm').replace('{title}', record.title))) return; this.service.delete(record.id).subscribe({ next: () => { this.toast.success(this.t('career.deleted')); this.load(); }, error: () => this.toast.error(this.t('career.deleteError')) }); }
  typeLabel(value: string): string { return this.t(`career.type.${value}`); }
  statusLabel(value: string): string { return this.t(`career.status.${value}`); }
  typeClass(value: string): string { return value === 'UniversityApplication' ? 'bg-blue-50 text-blue-700' : value === 'Preparation' ? 'bg-violet-50 text-violet-700' : value === 'Internship' ? 'bg-teal-50 text-teal-700' : value === 'Counseling' ? 'bg-amber-50 text-amber-700' : 'bg-[#f4edf7] text-[#633582]'; }
  statusClass(value: string): string { return value === 'Completed' ? 'bg-emerald-50 text-emerald-700' : value === 'NeedsAttention' ? 'bg-red-50 text-red-700' : value === 'Planned' ? 'bg-slate-100 text-slate-600' : 'bg-violet-50 text-violet-700'; }
  primaryProgress(record: CareerGuidanceRecord): number | null { return record.recordType === 'Internship' ? record.internshipProgressPercent ?? null : record.recordType === 'Preparation' ? record.preparationProgressPercent ?? null : record.applicationProgressPercent ?? null; }
  hasExpandedDetails(record: CareerGuidanceRecord): boolean { return !!(record.careerInterests || record.targetMajors || record.targetUniversities || record.targetCountries || record.qualityRating != null || record.activitiesSummary || record.issues || record.solutions || record.studentSupport || record.studentFeedback || record.nextSteps || record.recommendations || record.proposals || record.internshipStartDate || record.internshipEndDate || record.internshipProgressPercent != null || record.internshipQualityRating != null || record.studentSelfAssessment || record.hostAssessment || record.finalProduct); }
  formatDate(value: string): string { return new Intl.DateTimeFormat(this.language.language() === 'vi' ? 'vi-VN' : 'en-GB', { day: '2-digit', month: 'short', year: 'numeric' }).format(new Date(value)); }
  paginationLabel(): string { return this.t('pagination.pageOf').replace('{page}', String(this.pageData()?.page ?? 1)).replace('{pages}', String(this.pageData()?.totalPages ?? 1)).replace('{total}', String(this.pageData()?.totalCount ?? 0)); }
  t(key: string): string { return this.language.t(key); }
  private load(): void { this.loading.set(true); this.loadError.set(false); this.service.list({ page: this.page, pageSize: 12, search: this.search, recordType: this.recordType, status: this.status, academicYear: this.academicYear, term: this.term }).subscribe({ next: (data) => { this.pageData.set(data); this.page = data.page; this.loading.set(false); }, error: () => { this.loading.set(false); this.loadError.set(true); } }); }
}
