import { Component, computed, inject, Input, OnChanges, OnInit, signal, SimpleChanges } from '@angular/core';
import { NgClass } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LanguageService } from '../core/language/language.service';
import { ToastService } from '../core/toast/toast.service';
import { ManualEntryStudent } from '../imports/manual-entry.service';
import {
  CareerGuidancePage,
  CareerGuidanceRecord,
  CareerGuidanceRecordType,
  CareerGuidanceService,
  CareerGuidanceStatus,
} from './career-guidance.service';
import { CareerGuidanceFormSheetComponent } from './career-guidance-form-sheet.component';

@Component({
  selector: 'app-student-career-guidance',
  imports: [NgClass, RouterLink, CareerGuidanceFormSheetComponent],
  template: `
    <section class="space-y-4">
      <div class="overflow-hidden rounded-lg border border-[#dfd5e5] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
        <div class="flex flex-wrap items-start justify-between gap-4 bg-gradient-to-r from-[#f2eef8] via-white to-[#fff6db] px-5 py-4">
          <div class="flex min-w-0 gap-3">
            <span class="grid h-11 w-11 shrink-0 place-items-center rounded-lg bg-[#32124f] text-[#f3c246]">
              <svg class="h-6 w-6" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 11 12 5l9 6-9 6-9-6Z"/><path d="M7 13.5V18c3 2 7 2 10 0v-4.5"/><path d="M21 11v5"/></svg>
            </span>
            <div>
              <p class="text-[10px] font-semibold uppercase tracking-[0.16em] text-[#8a6811]">{{ text('STUDENT CAREER ROADMAP', 'LỘ TRÌNH HƯỚNG NGHIỆP') }}</p>
              <h3 class="mt-0.5 text-lg font-semibold text-[#32124f]">{{ text('Career and university journey', 'Hành trình hướng nghiệp và đại học') }}</h3>
              <p class="mt-0.5 max-w-3xl text-xs leading-5 text-[#6f6877]">{{ text('A shared view of this student’s career direction, university preparation, counselling and internship experience.', 'Theo dõi định hướng nghề nghiệp, chuẩn bị hồ sơ đại học, tư vấn và trải nghiệm thực tập của học sinh.') }}</p>
            </div>
          </div>
          <button type="button" class="inline-flex items-center gap-2 rounded-md bg-[#32124f] px-4 py-2.5 text-sm font-semibold text-white hover:bg-[#4b2070]" (click)="openCreate()">
            <span class="text-lg leading-none" aria-hidden="true">＋</span>{{ text('Add record', 'Thêm hồ sơ') }}
          </button>
        </div>

        <div class="grid border-t border-[#e9e2ed] sm:grid-cols-4">
          <div class="border-b border-[#e9e2ed] px-4 py-3 sm:border-b-0 sm:border-r"><p class="metric-label">{{ text('Total records', 'Tổng hồ sơ') }}</p><p class="metric-value">{{ data()?.totalCount ?? 0 }}</p></div>
          <div class="border-b border-[#e9e2ed] px-4 py-3 sm:border-b-0 sm:border-r"><p class="metric-label text-violet-700">{{ text('In progress', 'Đang thực hiện') }}</p><p class="metric-value">{{ data()?.inProgressCount ?? 0 }}</p></div>
          <div class="border-b border-[#e9e2ed] px-4 py-3 sm:border-b-0 sm:border-r"><p class="metric-label text-amber-700">{{ text('Needs attention', 'Cần lưu ý') }}</p><p class="metric-value">{{ data()?.needsAttentionCount ?? 0 }}</p></div>
          <div class="px-4 py-3"><p class="metric-label text-emerald-700">{{ text('Completed', 'Hoàn thành') }}</p><p class="metric-value">{{ data()?.completedCount ?? 0 }}</p></div>
        </div>
      </div>

      @if (loading()) {
        <div class="grid min-h-52 place-items-center rounded-lg border border-[#ebe6ef] bg-white"><span class="h-9 w-9 animate-spin rounded-full border-4 border-[#e6dfea] border-t-[#6d3a91]"></span></div>
      } @else if (loadError()) {
        <div class="rounded-lg border border-red-200 bg-red-50 p-6 text-center"><p class="text-sm text-red-700">{{ text('Career records could not be loaded.', 'Không thể tải hồ sơ hướng nghiệp.') }}</p><button class="mt-3 text-sm font-semibold text-red-800 underline" type="button" (click)="load()">{{ text('Try again', 'Thử lại') }}</button></div>
      } @else if (!records().length) {
        <div class="rounded-lg border border-dashed border-[#d6cadd] bg-white p-10 text-center">
          <span class="mx-auto grid h-14 w-14 place-items-center rounded-full bg-[#fff7dd] text-2xl text-[#8a6811]">⌁</span>
          <h4 class="mt-4 font-semibold text-[#32124f]">{{ text('No career plan has been recorded', 'Chưa có lộ trình hướng nghiệp') }}</h4>
          <p class="mx-auto mt-1 max-w-xl text-sm text-[#7c7284]">{{ text('Start with the student’s interests, goals and the next action the school will support.', 'Bắt đầu với sở thích, mục tiêu của học sinh và hoạt động hỗ trợ tiếp theo của nhà trường.') }}</p>
          <button class="mt-5 rounded-md border border-[#6d3a91] px-4 py-2 text-sm font-semibold text-[#5b287d] hover:bg-[#f7f1fb]" type="button" (click)="openCreate()">{{ text('Create the first record', 'Tạo hồ sơ đầu tiên') }}</button>
        </div>
      } @else {
        <div class="rounded-lg border border-[#ebe6ef] bg-white px-4 py-4 shadow-[0_12px_35px_rgba(50,18,79,0.07)] sm:px-5">
          <div class="mb-4 flex flex-wrap items-center justify-between gap-3">
            <div><h4 class="font-semibold text-[#32124f]">{{ text('Career journey', 'Hành trình hướng nghiệp') }}</h4><p class="mt-0.5 text-xs text-[#7c7284]">{{ text('Newest updates are shown first.', 'Cập nhật mới nhất được hiển thị trước.') }}</p></div>
            <a class="text-xs font-semibold text-[#5b287d] hover:underline" routerLink="/career-guidance">{{ text('View all students', 'Xem tất cả học sinh') }} →</a>
          </div>

          <div class="relative ml-2 border-l-2 border-[#e2d9e7] pl-7">
            @for (item of records(); track item.id; let last = $last) {
              <article class="relative" [class.pb-4]="!last">
                <span class="absolute -left-[2.29rem] top-1 h-4 w-4 rounded-full border-[3px] border-white shadow-sm" [ngClass]="typeDot(item.recordType)"></span>
                <div class="rounded-lg border border-[#e8e1ec] p-4 transition hover:border-[#cfc0d8] hover:shadow-[0_8px_24px_rgba(50,18,79,0.07)]">
                  <div class="flex flex-wrap items-start justify-between gap-3">
                    <div class="min-w-0">
                      <div class="flex flex-wrap items-center gap-2"><span class="rounded-full px-3 py-1 text-sm font-semibold" [ngClass]="typeClass(item.recordType)">{{ typeLabel(item.recordType) }}</span><span class="text-xs text-[#8a8190]">{{ formatDate(item.recordDate) }} · {{ item.academicYear.replace('-', '–') }}</span></div>
                      <h5 class="mt-2 text-base font-semibold text-[#32124f]">{{ item.title }}</h5>
                    </div>
                    <span class="rounded-full px-3 py-1 text-sm font-semibold" [ngClass]="statusClass(item.status)">{{ statusLabel(item.status) }}</span>
                  </div>

                  @if (hasDirection(item)) {
                    <div class="mt-3 grid gap-2 sm:grid-cols-2 xl:grid-cols-4">
                      @if (item.careerInterests) { <div class="info-box"><span>{{ text('Career interests', 'Lĩnh vực quan tâm') }}</span><strong>{{ item.careerInterests }}</strong></div> }
                      @if (item.targetMajors) { <div class="info-box"><span>{{ text('Target majors', 'Ngành mục tiêu') }}</span><strong>{{ item.targetMajors }}</strong></div> }
                      @if (item.targetUniversities) { <div class="info-box"><span>{{ text('Universities', 'Trường đại học') }}</span><strong>{{ item.targetUniversities }}</strong></div> }
                      @if (item.targetCountries) { <div class="info-box"><span>{{ text('Countries', 'Quốc gia') }}</span><strong>{{ item.targetCountries }}</strong></div> }
                    </div>
                  }

                  @if (item.applicationProgressPercent != null || item.preparationProgressPercent != null) {
                    <div class="mt-3 grid gap-3 sm:grid-cols-2">
                      @if (item.applicationProgressPercent != null) { <div><div class="mb-1 flex justify-between text-xs font-semibold text-[#5f5666]"><span>{{ text('Application progress', 'Tiến độ nộp hồ sơ') }}</span><span>{{ item.applicationProgressPercent }}%</span></div><div class="h-2 overflow-hidden rounded-full bg-[#eee8f1]"><span class="block h-full rounded-full bg-gradient-to-r from-[#6d3a91] to-[#d2a92d]" [style.width.%]="item.applicationProgressPercent"></span></div></div> }
                      @if (item.preparationProgressPercent != null) { <div><div class="mb-1 flex justify-between text-xs font-semibold text-[#5f5666]"><span>{{ text('Preparation progress', 'Tiến độ chuẩn bị') }}</span><span>{{ item.preparationProgressPercent }}%</span></div><div class="h-2 overflow-hidden rounded-full bg-[#eee8f1]"><span class="block h-full rounded-full bg-blue-500" [style.width.%]="item.preparationProgressPercent"></span></div></div> }
                    </div>
                  }

                  @if (item.recordType === 'Internship' && (item.internshipCompany || item.internshipRole)) {
                    <div class="mt-3 rounded-md border border-[#e1eadb] bg-[#f7fbf5] p-3 text-sm"><strong class="text-[#356146]">{{ item.internshipCompany }}</strong>@if (item.internshipRole) { <span class="text-[#59685d]"> · {{ item.internshipRole }}</span> }</div>
                  }

                  @if (item.activitiesSummary || item.issues || item.solutions || item.nextSteps) {
                    <div class="mt-3 grid gap-3 lg:grid-cols-3">
                      @if (item.activitiesSummary) { <div><p class="detail-label">{{ text('Progress summary', 'Tổng hợp tiến độ') }}</p><p class="detail-copy">{{ item.activitiesSummary }}</p></div> }
                      @if (item.issues || item.solutions) { <div><p class="detail-label text-amber-700">{{ text('Issues & solutions', 'Vấn đề & giải pháp') }}</p><p class="detail-copy">{{ item.issues }}@if (item.issues && item.solutions) { <span> · </span> }{{ item.solutions }}</p></div> }
                      @if (item.nextSteps) { <div><p class="detail-label text-emerald-700">{{ text('Next steps', 'Bước tiếp theo') }}</p><p class="detail-copy">{{ item.nextSteps }}</p></div> }
                    </div>
                  }

                  @if (expandedRecordId() === item.id) {
                    <div class="mt-3 rounded-lg border border-[#e4dce9] bg-[#faf8fb] p-4">
                      <p class="mb-3 text-[11px] font-bold uppercase tracking-[0.12em] text-[#6d3a91]">{{ text('Full record details', 'Chi tiết đầy đủ') }}</p>
                      <div class="grid gap-x-6 gap-y-4 sm:grid-cols-2">
                        @if (item.qualityRating != null) { <div><p class="expanded-label">{{ text('Quality rating', 'Đánh giá chất lượng') }}</p><p class="expanded-value">{{ item.qualityRating }}/5</p></div> }
                        @if (item.activitiesSummary) { <div><p class="expanded-label">{{ text('Activities and progress', 'Hoạt động và tiến độ') }}</p><p class="expanded-value">{{ item.activitiesSummary }}</p></div> }
                        @if (item.issues) { <div><p class="expanded-label">{{ text('Issues or barriers', 'Vấn đề hoặc trở ngại') }}</p><p class="expanded-value">{{ item.issues }}</p></div> }
                        @if (item.solutions) { <div><p class="expanded-label">{{ text('Solutions taken', 'Giải pháp đã thực hiện') }}</p><p class="expanded-value">{{ item.solutions }}</p></div> }
                        @if (item.studentSupport) { <div><p class="expanded-label">{{ text('Support provided', 'Hỗ trợ dành cho học sinh') }}</p><p class="expanded-value">{{ item.studentSupport }}</p></div> }
                        @if (item.studentFeedback) { <div><p class="expanded-label">{{ text('Student feedback', 'Ý kiến phản hồi của học sinh') }}</p><p class="expanded-value">{{ item.studentFeedback }}</p></div> }
                        @if (item.nextSteps) { <div><p class="expanded-label">{{ text('Next steps', 'Bước tiếp theo') }}</p><p class="expanded-value">{{ item.nextSteps }}</p></div> }
                        @if (item.recommendations) { <div><p class="expanded-label">{{ text('Recommendations', 'Khuyến nghị') }}</p><p class="expanded-value">{{ item.recommendations }}</p></div> }
                        @if (item.proposals) { <div><p class="expanded-label">{{ text('Proposals', 'Đề xuất') }}</p><p class="expanded-value">{{ item.proposals }}</p></div> }
                        @if (item.internshipStartDate || item.internshipEndDate) { <div><p class="expanded-label">{{ text('Internship dates', 'Thời gian thực tập') }}</p><p class="expanded-value">{{ item.internshipStartDate ? formatDate(item.internshipStartDate) : '—' }} – {{ item.internshipEndDate ? formatDate(item.internshipEndDate) : '—' }}</p></div> }
                        @if (item.internshipProgressPercent != null) { <div><p class="expanded-label">{{ text('Internship progress', 'Tiến độ thực tập') }}</p><p class="expanded-value">{{ item.internshipProgressPercent }}%</p></div> }
                        @if (item.internshipQualityRating != null) { <div><p class="expanded-label">{{ text('Internship quality', 'Chất lượng chương trình') }}</p><p class="expanded-value">{{ item.internshipQualityRating }}/5</p></div> }
                        @if (item.studentSelfAssessment) { <div><p class="expanded-label">{{ text('Student self-assessment', 'Học sinh tự đánh giá') }}</p><p class="expanded-value">{{ item.studentSelfAssessment }}</p></div> }
                        @if (item.hostAssessment) { <div><p class="expanded-label">{{ text('Host assessment', 'Đánh giá của đơn vị thực tập') }}</p><p class="expanded-value">{{ item.hostAssessment }}</p></div> }
                        @if (item.finalProduct) { <div class="sm:col-span-2"><p class="expanded-label">{{ text('Final report or product', 'Báo cáo hoặc sản phẩm cuối kỳ') }}</p><p class="expanded-value">{{ item.finalProduct }}</p></div> }
                      </div>
                    </div>
                  }

                  <div class="mt-3 flex flex-wrap items-center gap-3 border-t border-[#eee8f1] pt-2.5 text-[11px] text-[#7c7284]">
                    <span>{{ text('Recorded by', 'Người ghi nhận') }} <strong class="text-[#5f5666]">{{ item.updatedByName || item.recordedByName }}</strong></span>
                    <span class="ml-auto flex flex-wrap gap-3">
                      @if (hasExpandedDetails(item)) { <button class="font-semibold text-[#5b287d] hover:underline" type="button" [attr.aria-expanded]="expandedRecordId() === item.id" (click)="toggleDetails(item.id)">{{ expandedRecordId() === item.id ? text('Hide details', 'Thu gọn') : text('View details', 'Xem chi tiết') }}</button> }
                      <button class="font-semibold text-[#5b287d] hover:underline" type="button" (click)="edit(item)">{{ text('Edit', 'Sửa') }}</button><button class="font-semibold text-red-700 hover:underline" type="button" (click)="remove(item)">{{ text('Delete', 'Xóa') }}</button>
                    </span>
                  </div>
                </div>
              </article>
            }
          </div>

          @if ((data()?.totalPages ?? 0) > 1) {
            <footer class="mt-4 flex flex-wrap items-center justify-between gap-3 border-t border-[#ebe6ef] pt-4"><p class="text-xs text-[#7c7284]">{{ paginationLabel() }}</p><div class="flex gap-2"><button class="pager" type="button" [disabled]="(data()?.page ?? 1) <= 1 || loading()" (click)="goToPage((data()?.page ?? 1) - 1)">{{ text('Previous', 'Trước') }}</button><button class="pager" type="button" [disabled]="(data()?.page ?? 1) >= (data()?.totalPages ?? 1) || loading()" (click)="goToPage((data()?.page ?? 1) + 1)">{{ text('Next', 'Tiếp') }}</button></div></footer>
          }
        </div>
      }
    </section>

    @if (formOpen()) {
      <app-career-guidance-form-sheet [fixedStudent]="fixedStudent()" [record]="editing()" (close)="closeForm()" (saved)="saved()" />
    }
  `,
  styles: [`
    .metric-label { font-size: .75rem; font-weight: 600; letter-spacing: .08em; text-transform: uppercase; color: #6f6877; }
    .metric-value { margin-top: .125rem; font-size: 1.25rem; font-weight: 600; color: #32124f; }
    .info-box { min-width: 0; border: 1px solid #ebe6ef; border-radius: .375rem; background: #faf9fb; padding: .625rem .75rem; }
    .info-box span { display: block; font-size: .625rem; font-weight: 600; letter-spacing: .08em; text-transform: uppercase; color: #7c7284; }
    .info-box strong { display: block; margin-top: .25rem; overflow-wrap: anywhere; font-size: .8125rem; font-weight: 600; color: #3f2752; }
    .detail-label { font-size: .625rem; font-weight: 600; letter-spacing: .1em; text-transform: uppercase; color: #6d3a91; }
    .detail-copy { margin-top: .125rem; display: -webkit-box; overflow: hidden; -webkit-line-clamp: 3; -webkit-box-orient: vertical; font-size: .8125rem; line-height: 1.25rem; color: #5f5666; }
    .expanded-label { font-size: .625rem; font-weight: 700; letter-spacing: .09em; text-transform: uppercase; color: #75697c; }
    .expanded-value { margin-top: .25rem; white-space: pre-line; overflow-wrap: anywhere; font-size: .8125rem; line-height: 1.3rem; color: #4f4655; }
    .pager { border: 1px solid #d8d0df; border-radius: .375rem; background: white; padding: .5rem .75rem; font-size: .75rem; font-weight: 600; color: #32124f; }
    .pager:disabled { cursor: not-allowed; opacity: .4; }
  `],
})
export class StudentCareerGuidanceComponent implements OnInit, OnChanges {
  @Input({ required: true }) studentId = 0;
  @Input() studentName = '';
  @Input() sisUserId = '';
  @Input() gradeLevel: string | null = null;
  @Input() homeroom: string | null = null;
  @Input() academicYear = '';
  @Input() term = 'all';

  private readonly service = inject(CareerGuidanceService);
  private readonly language = inject(LanguageService);
  private readonly toast = inject(ToastService);
  readonly data = signal<CareerGuidancePage | null>(null);
  readonly records = computed(() => this.data()?.items ?? []);
  readonly loading = signal(true);
  readonly loadError = signal(false);
  readonly formOpen = signal(false);
  readonly editing = signal<CareerGuidanceRecord | null>(null);
  readonly expandedRecordId = signal<number | null>(null);
  fixedStudent(): ManualEntryStudent { return { id: this.studentId, fullName: this.studentName, sisUserId: this.sisUserId, gradeLevel: this.gradeLevel, homeroom: this.homeroom }; }
  private page = 1;
  private readonly pageSize = 5;

  ngOnInit(): void { this.load(); }
  ngOnChanges(changes: SimpleChanges): void {
    if ((changes['studentId'] && !changes['studentId'].firstChange) || (changes['academicYear'] && !changes['academicYear'].firstChange) || (changes['term'] && !changes['term'].firstChange)) { this.page = 1; this.load(); }
  }
  text(en: string, vi: string): string { return this.language.language() === 'vi' ? vi : en; }
  openCreate(): void { this.editing.set(null); this.formOpen.set(true); }
  edit(item: CareerGuidanceRecord): void { this.editing.set(item); this.formOpen.set(true); }
  closeForm(): void { this.formOpen.set(false); this.editing.set(null); }
  saved(): void { this.closeForm(); this.toast.success(this.text('Career record saved.', 'Đã lưu hồ sơ hướng nghiệp.')); this.page = 1; this.load(); }
  toggleDetails(id: number): void { this.expandedRecordId.update((current) => current === id ? null : id); }
  remove(item: CareerGuidanceRecord): void {
    if (!window.confirm(this.text(`Delete “${item.title}”?`, `Xóa “${item.title}”?`))) return;
    this.service.delete(item.id).subscribe({ next: () => { this.toast.success(this.text('Career record deleted.', 'Đã xóa hồ sơ hướng nghiệp.')); this.load(); }, error: () => this.toast.error(this.text('Unable to delete this record.', 'Không thể xóa hồ sơ này.')) });
  }
  goToPage(page: number): void { const total = this.data()?.totalPages ?? 1; if (page < 1 || page > total || page === this.page) return; this.page = page; this.load(); }
  paginationLabel(): string { const value = this.data(); return this.text(`Page ${value?.page ?? 1} of ${value?.totalPages ?? 1} · ${value?.totalCount ?? 0} records`, `Trang ${value?.page ?? 1}/${value?.totalPages ?? 1} · ${value?.totalCount ?? 0} hồ sơ`); }
  formatDate(value: string): string { return new Intl.DateTimeFormat(this.language.language() === 'vi' ? 'vi-VN' : 'en-GB', { day: '2-digit', month: 'short', year: 'numeric' }).format(new Date(value)); }
  hasDirection(item: CareerGuidanceRecord): boolean { return !!(item.careerInterests || item.targetMajors || item.targetUniversities || item.targetCountries); }
  hasExpandedDetails(item: CareerGuidanceRecord): boolean { return !!(item.qualityRating != null || item.activitiesSummary || item.issues || item.solutions || item.studentSupport || item.studentFeedback || item.nextSteps || item.recommendations || item.proposals || item.internshipStartDate || item.internshipEndDate || item.internshipProgressPercent != null || item.internshipQualityRating != null || item.studentSelfAssessment || item.hostAssessment || item.finalProduct); }
  typeLabel(value: CareerGuidanceRecordType): string { const labels: Record<CareerGuidanceRecordType, [string, string]> = { GeneralPlan: ['Career plan', 'Kế hoạch hướng nghiệp'], UniversityApplication: ['University application', 'Hồ sơ đại học'], Preparation: ['Preparation', 'Chuẩn bị hồ sơ'], Internship: ['Internship', 'Thực tập'], Counseling: ['Counselling', 'Tư vấn'] }; return this.text(...labels[value]); }
  statusLabel(value: CareerGuidanceStatus): string { const labels: Record<CareerGuidanceStatus, [string, string]> = { Planned: ['Planned', 'Đã lên kế hoạch'], InProgress: ['In progress', 'Đang thực hiện'], NeedsAttention: ['Needs attention', 'Cần lưu ý'], Completed: ['Completed', 'Hoàn thành'] }; return this.text(...labels[value]); }
  typeClass(value: CareerGuidanceRecordType): string { return value === 'UniversityApplication' ? 'bg-indigo-50 text-indigo-700' : value === 'Internship' ? 'bg-teal-50 text-teal-700' : value === 'Counseling' ? 'bg-amber-50 text-amber-700' : value === 'Preparation' ? 'bg-blue-50 text-blue-700' : 'bg-purple-50 text-purple-700'; }
  typeDot(value: CareerGuidanceRecordType): string { return value === 'UniversityApplication' ? 'bg-indigo-500' : value === 'Internship' ? 'bg-teal-500' : value === 'Counseling' ? 'bg-amber-500' : value === 'Preparation' ? 'bg-blue-500' : 'bg-purple-500'; }
  statusClass(value: CareerGuidanceStatus): string { return value === 'Completed' ? 'bg-emerald-50 text-emerald-700' : value === 'NeedsAttention' ? 'bg-red-50 text-red-700' : value === 'Planned' ? 'bg-slate-100 text-slate-600' : 'bg-violet-50 text-violet-700'; }
  load(): void {
    if (!this.studentId) return;
    this.loading.set(true); this.loadError.set(false);
    this.service.forStudent(this.studentId, this.academicYear, this.term, this.page, this.pageSize).subscribe({ next: (data) => { this.data.set(data); this.page = data.page; this.loading.set(false); }, error: () => { this.loading.set(false); this.loadError.set(true); } });
  }
}
