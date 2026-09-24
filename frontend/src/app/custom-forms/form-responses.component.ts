import { Component, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { LanguageService } from '../core/language/language.service';
import { ToastService } from '../core/toast/toast.service';
import { CustomFormDetail, CustomFormsService, FormQuestion, FormResponse } from './custom-forms.service';

@Component({
  selector: 'app-form-responses',
  imports: [RouterLink],
  template: `
    <section class="space-y-6">
      <div class="flex flex-wrap items-end justify-between gap-4">
        <div class="flex items-start gap-3">
          <a routerLink="/forms" class="mt-1 grid h-10 w-10 shrink-0 place-items-center rounded-full border border-[#ded4e3] bg-white text-xl text-[#32124f] hover:bg-[#f7f3fa]" [attr.aria-label]="t('forms.backToForms')">←</a>
          <div><p class="text-sm uppercase tracking-[.18em] text-[#8a6811]">{{ t('forms.responseHistory') }}</p><h2 class="mt-1 text-3xl font-semibold text-[#32124f]">{{ form()?.title || t('forms.responsesTitle') }}</h2><p class="mt-2 text-sm text-[#746b7b]">{{ responseTotal() }} {{ t('forms.responses') }}</p></div>
        </div>
        @if (form()) { <button type="button" class="rounded-md border border-[#d8d0df] bg-white px-4 py-2.5 text-sm font-semibold text-[#32124f] hover:bg-[#f7f3fa]" (click)="exportResponses()">{{ t('forms.exportExcel') }}</button> }
      </div>

      @if (error()) { <div class="rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{{ error() }}</div> }
      @if (loading()) { <div class="rounded-xl border border-[#e7e0ea] bg-white p-10 text-center text-sm text-[#746b7b] shadow-[0_12px_35px_rgba(50,18,79,.07)]">{{ t('common.loading') }}</div> }
      @else if (form()) {
        <section class="overflow-hidden rounded-xl border border-[#e7e0ea] bg-white shadow-[0_12px_35px_rgba(50,18,79,.07)]">
          <div class="overflow-x-auto">
            <table class="w-full min-w-[980px] text-left text-sm">
              <thead class="bg-[#faf9f6] text-xs uppercase tracking-[.08em] text-[#746b7b]"><tr>
                <th class="px-5 py-4">{{ t('forms.submittedAt') }}</th><th class="px-5 py-4">{{ t('forms.student') }}</th><th class="px-5 py-4">{{ t('forms.class') }}</th>
                @for (question of historyQuestions(); track question.id) { <th class="max-w-52 px-5 py-4">{{ question.label }}</th> }
                <th class="px-5 py-4 text-right">{{ t('forms.actions') }}</th>
              </tr></thead>
              <tbody class="divide-y divide-[#eee8f1]">
                @for (response of responses(); track response.id) { <tr class="hover:bg-[#fcfafc]">
                  <td class="whitespace-nowrap px-5 py-4 text-[#746b7b]">{{ formatDate(response.submittedAt) }}</td>
                  <td class="px-5 py-4"><span class="font-semibold text-[#32124f]">{{ response.studentName }}</span><br><span class="text-xs text-[#746b7b]">{{ response.studentId }}</span></td>
                  <td class="px-5 py-4 text-[#746b7b]">{{ response.gradeLevel || '—' }} / {{ response.homeroom || '—' }}</td>
                  @for (question of historyQuestions(); track question.id) { <td class="max-w-52 truncate px-5 py-4 text-[#746b7b]">{{ display(response.answers[question.id]) }}</td> }
                  <td class="whitespace-nowrap px-5 py-4 text-right"><button class="mr-3 text-sm font-semibold text-[#5b287c]" (click)="responseDetail.set(response)">{{ t('forms.view') }}</button><button class="text-sm font-semibold text-red-700" (click)="removeResponse(response)">{{ t('forms.delete') }}</button></td>
                </tr> }
                @if (!responses().length) { <tr><td class="px-5 py-12 text-center text-[#746b7b]" [attr.colspan]="4 + historyQuestions().length">{{ t('forms.noResponses') }}</td></tr> }
              </tbody>
            </table>
          </div>
          @if (responsePages() > 1) { <div class="flex justify-end gap-2 border-t border-[#ebe6ef] p-4"><button class="rounded border px-3 py-2 text-xs disabled:opacity-40" [disabled]="responsePage() === 1" (click)="loadResponses(responsePage()-1)">‹</button><span class="px-3 py-2 text-xs">{{ responsePage() }} / {{ responsePages() }}</span><button class="rounded border px-3 py-2 text-xs disabled:opacity-40" [disabled]="responsePage() === responsePages()" (click)="loadResponses(responsePage()+1)">›</button></div> }
        </section>
      }
    </section>

    @if (responseDetail()) {
      <div class="fixed inset-0 z-[80] grid place-items-center bg-[#24182f]/50 p-4" (click)="responseDetail.set(null)">
        <article class="max-h-[90vh] w-full max-w-3xl overflow-y-auto rounded-xl bg-white p-6 shadow-2xl" (click)="$event.stopPropagation()">
          <div class="flex justify-between gap-3"><div><h3 class="text-xl font-semibold text-[#32124f]">{{ responseDetail()!.studentName }}</h3><p class="text-sm text-[#746b7b]">{{ responseDetail()!.studentId }} · {{ formatDate(responseDetail()!.submittedAt) }}</p></div><button class="text-2xl" (click)="responseDetail.set(null)">×</button></div>
          <div class="mt-6 space-y-4">@for (question of snapshotQuestions(responseDetail()!); track question.id) { <div class="rounded-lg bg-[#f8f5fa] p-4"><p class="text-sm font-semibold text-[#32124f]">{{ question.label }}</p><p class="mt-1 whitespace-pre-wrap text-sm text-[#5f5765]">{{ display(responseDetail()!.answers[question.id]) || '—' }}</p></div> }</div>
        </article>
      </div>
    }
  `,
})
export class FormResponsesComponent implements OnInit {
  formId = 0; form = signal<CustomFormDetail | null>(null); responses = signal<FormResponse[]>([]); responseDetail = signal<FormResponse | null>(null);
  loading = signal(true); error = signal<string | null>(null); responsePage = signal(1); responsePages = signal(0); responseTotal = signal(0);

  constructor(private route: ActivatedRoute, private service: CustomFormsService, private language: LanguageService, private toast: ToastService) {}

  ngOnInit() {
    this.formId = Number(this.route.snapshot.paramMap.get('id'));
    if (!Number.isInteger(this.formId) || this.formId <= 0) { this.error.set(this.t('forms.loadError')); this.loading.set(false); return; }
    this.service.detail(this.formId).subscribe({ next: form => { this.form.set(form); this.loadResponses(1); }, error: () => { this.error.set(this.t('forms.loadError')); this.loading.set(false); } });
  }

  t(key: string) { return this.language.t(key); }
  loadResponses(page: number) { this.service.responses(this.formId, page).subscribe({ next: result => { this.responses.set(result.items); this.responsePage.set(result.page); this.responsePages.set(result.totalPages); this.responseTotal.set(result.totalCount); this.loading.set(false); }, error: () => { this.error.set(this.t('forms.responsesLoadError')); this.loading.set(false); } }); }
  historyQuestions(): FormQuestion[] { return this.form()?.definition.sections.flatMap(section => section.questions) || []; }
  snapshotQuestions(response: FormResponse) { return response.questionSnapshot.sections.flatMap(section => section.questions); }
  display(value: unknown): string { if (value == null) return ''; if (Array.isArray(value)) return value.join(', '); if (typeof value === 'object') return Object.entries(value as Record<string, unknown>).map(([key, item]) => `${key}: ${this.display(item)}`).join('; '); return String(value); }
  formatDate(value: string) { return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(value)); }
  removeResponse(response: FormResponse) { if (!confirm(this.t('forms.confirmDeleteResponse'))) return; this.service.deleteResponse(this.formId, response.id).subscribe({ next: () => { this.toast.success(this.t('forms.responseDeleted')); this.loadResponses(this.responsePage()); }, error: () => this.toast.error(this.t('forms.responseDeleteError')) }); }
  exportResponses() { const form = this.form(); if (!form) return; this.service.exportResponses(this.formId).subscribe({ next: response => { const url = URL.createObjectURL(response.body!); const anchor = document.createElement('a'); anchor.href = url; anchor.download = `${form.title}-responses.xlsx`; anchor.click(); URL.revokeObjectURL(url); }, error: () => this.toast.error(this.t('forms.exportError')) }); }
}
