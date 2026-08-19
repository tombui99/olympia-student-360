import { CommonModule } from '@angular/common';
import { Component, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthenticationService } from '../core/auth/auth.service';
import { LanguageService } from '../core/language/language.service';
import { ToastService } from '../core/toast/toast.service';
import { CustomFormsService, CustomFormSummary } from './custom-forms.service';

@Component({
  selector: 'app-custom-forms',
  imports: [CommonModule, RouterLink],
  template: `
    <section class="space-y-6">
      <div class="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p class="text-sm uppercase tracking-[.18em] text-[#8a6811]">{{ t('forms.eyebrow') }}</p>
          <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ t('forms.title') }}</h2>
          <p class="mt-2 max-w-3xl text-[#6f6877]">{{ t('forms.description') }}</p>
        </div>
        @if (canManage()) {
          <a routerLink="/forms/new" class="rounded-md bg-[#32124f] px-5 py-2.5 text-sm font-semibold text-white hover:bg-[#4b2070]">{{ t('forms.create') }}</a>
        }
      </div>

      @if (error()) { <div class="rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700">{{ error() }}</div> }

      <section class="rounded-xl border border-[#e7e0ea] bg-white shadow-[0_12px_35px_rgba(50,18,79,.07)]">
        <div class="border-b border-[#ebe6ef] px-5 py-4"><h3 class="font-semibold text-[#32124f]">{{ t('forms.allForms') }}</h3></div>
        @if (loading()) { <p class="p-8 text-center text-sm text-[#746b7b]">{{ t('common.loading') }}</p> }
        @else if (!forms().length) { <p class="p-10 text-center text-sm text-[#746b7b]">{{ t('forms.empty') }}</p> }
        @else {
          <div class="grid gap-4 p-5 md:grid-cols-2 xl:grid-cols-3">
            @for (form of forms(); track form.id) {
              <article class="flex min-h-56 flex-col rounded-lg border border-[#e5dce9] p-5">
                <div class="min-w-0"><h4 class="truncate font-semibold text-[#32124f]">{{ form.title }}</h4><p class="mt-1 line-clamp-2 text-sm text-[#746b7b]">{{ form.description || t('forms.noDescription') }}</p></div>
                <div class="mt-4 grid gap-2 text-xs text-[#746b7b]" [class.grid-cols-2]="canManage()">@if (canManage()) { <span>{{ form.responseCount }} {{ t('forms.responses') }}</span> }<span [class.text-right]="canManage()">{{ formatDate(form.updatedAt || form.createdAt) }}</span></div>
                <div class="mt-auto flex flex-wrap gap-2 pt-5">
                  <button type="button" class="inline-flex h-10 items-center justify-center rounded-md border border-[#d8d0df] px-4 text-sm font-semibold leading-none text-[#32124f] transition hover:border-[#bcaac7] hover:bg-[#f7f3fa]" (click)="copyLink(form)">{{ t('forms.copyLink') }}</button>
                  @if (canManage()) {
                    <a [routerLink]="['/forms', form.id]" class="inline-flex h-10 items-center justify-center rounded-md border border-[#d8d0df] px-4 text-sm font-semibold leading-none text-[#32124f] transition hover:border-[#bcaac7] hover:bg-[#f7f3fa]">{{ t('forms.edit') }}</a>
                    <a [routerLink]="['/forms', form.id, 'responses']" class="inline-flex h-10 items-center justify-center rounded-md bg-[#32124f] px-4 text-sm font-semibold leading-none text-white transition hover:bg-[#4b2070]">{{ t('forms.viewResponses') }}</a>
                  }
                </div>
              </article>
            }
          </div>
        }
      </section>

      @if (canManage()) {
        <section class="rounded-xl border border-[#ded8e2] bg-[#f4f1f5] shadow-[0_8px_24px_rgba(50,18,79,.05)]">
          <div class="border-b border-[#ded8e2] px-5 py-4"><div class="flex items-center gap-3"><span class="grid h-8 w-8 place-items-center rounded-md bg-[#e7e1e9] text-[#62586a]">▣</span><div><h3 class="font-semibold text-[#32124f]">{{ t('forms.archivedForms') }}</h3><p class="mt-0.5 text-xs text-[#746b7b]">{{ t('forms.archivedDescription') }}</p></div></div></div>
          @if (archivedLoading()) { <p class="p-8 text-center text-sm text-[#746b7b]">{{ t('common.loading') }}</p> }
          @else if (!archivedForms().length) { <p class="p-8 text-center text-sm text-[#746b7b]">{{ t('forms.noArchived') }}</p> }
          @else {
            <div class="grid gap-4 p-5 md:grid-cols-2 xl:grid-cols-3">
              @for (form of archivedForms(); track form.id) {
                <article class="flex min-h-48 flex-col rounded-lg border border-[#d9d2dd] bg-white/75 p-5">
                  <div class="flex items-start justify-between gap-3"><div class="min-w-0"><h4 class="truncate font-semibold text-[#4d4353]">{{ form.title }}</h4><p class="mt-1 line-clamp-2 text-sm text-[#7c7381]">{{ form.description || t('forms.noDescription') }}</p></div><span class="rounded-full bg-[#e9e5eb] px-2.5 py-1 text-xs font-semibold text-[#655b6b]">{{ t('forms.archived') }}</span></div>
                  <div class="mt-4 grid grid-cols-2 gap-2 text-xs text-[#7c7381]"><span>{{ form.responseCount }} {{ t('forms.responses') }}</span><span class="text-right">{{ formatDate(form.updatedAt || form.createdAt) }}</span></div>
                  <div class="mt-auto pt-5"><button type="button" class="inline-flex h-10 items-center justify-center rounded-md border border-[#8f70a2] bg-white px-4 text-sm font-semibold leading-none text-[#51256e] transition hover:bg-[#f5eef9]" (click)="restore(form)">{{ t('forms.restore') }}</button></div>
                </article>
              }
            </div>
          }
        </section>
      }
    </section>
  `,
})
export class CustomFormsComponent implements OnInit {
  forms = signal<CustomFormSummary[]>([]); loading = signal(true); error = signal<string | null>(null);
  archivedForms = signal<CustomFormSummary[]>([]); archivedLoading = signal(false);
  constructor(private service: CustomFormsService, private auth: AuthenticationService, private language: LanguageService, private toast: ToastService) {}
  ngOnInit() { this.load(); }
  t(key: string) { return this.language.t(key); }
  canManage() { return ['Admin', 'SchoolAdministration', 'Principal'].includes(this.auth.getUserRole() || ''); }
  load() {
    this.loading.set(true);
    this.service.list().subscribe({ next: forms => { this.forms.set(forms); this.loading.set(false); }, error: () => { this.error.set(this.t('forms.loadError')); this.loading.set(false); } });
    if (this.canManage()) {
      this.archivedLoading.set(true);
      this.service.archived().subscribe({ next: forms => { this.archivedForms.set(forms); this.archivedLoading.set(false); }, error: () => { this.error.set(this.t('forms.loadError')); this.archivedLoading.set(false); } });
    }
  }
  copyLink(form: CustomFormSummary) { navigator.clipboard.writeText(`${location.origin}/f/${form.publicId}`).then(() => this.toast.success(this.t('forms.linkCopied'))).catch(() => this.toast.error(this.t('forms.copyLinkError'))); }
  restore(form: CustomFormSummary) { this.service.restore(form.id).subscribe({ next: () => { this.toast.success(this.t('forms.restored')); this.load(); }, error: () => this.toast.error(this.t('forms.saveError')) }); }
  formatDate(value: string) { return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(value)); }
}
