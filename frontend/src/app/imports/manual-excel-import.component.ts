import { Component, computed, signal } from '@angular/core';
import { NgClass, NgFor, NgIf } from '@angular/common';
import { RouterLink } from '@angular/router';
import { AuthenticationService } from '../core/auth/auth.service';
import { LanguageService } from '../core/language/language.service';
import { ImportRecord, importModules } from './import-template-data';

@Component({
  selector: 'app-manual-excel-import',
  imports: [NgClass, NgFor, NgIf, RouterLink],
  template: `
    <section class="space-y-7">
      <div class="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">{{ languageService.t('imports.eyebrow') }}</p>
          <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ languageService.t('imports.title') }}</h2>
          <p class="mt-2 max-w-3xl text-[#6f6877]">{{ languageService.t('imports.description') }}</p>
        </div>
        <span class="rounded-full border border-[#f3c246]/50 bg-[#fff8e1] px-4 py-2 text-sm font-semibold text-[#7a5b12]">
          {{ languageService.t('imports.mockStatus') }}
        </span>
      </div>

      <section class="grid gap-4 lg:grid-cols-3">
        <a
          *ngFor="let module of modules"
          class="rounded-lg border border-[#ebe6ef] bg-white p-5 text-left shadow-[0_12px_35px_rgba(50,18,79,0.08)] transition hover:-translate-y-0.5 hover:border-[#f3c246]"
          [routerLink]="['/imports', module.id]"
        >
          <div class="flex items-start justify-between gap-4">
            <div class="grid h-11 w-11 place-items-center rounded bg-[#32124f] text-sm font-bold text-[#f3c246]">
              XLS
            </div>
            <span class="rounded-full bg-[#f7f2dc] px-3 py-1 text-xs font-semibold text-[#7a5b12]">
              {{ languageService.t('imports.openModule') }}
            </span>
          </div>
          <h3 class="mt-4 text-lg font-semibold text-[#32124f]">{{ languageService.t(module.titleKey) }}</h3>
          <p class="mt-2 text-sm leading-6 text-[#6f6877]">{{ languageService.t(module.descriptionKey) }}</p>
          <div class="mt-5 flex flex-wrap gap-2">
            <span
              *ngFor="let header of module.requiredHeaders"
              class="rounded bg-[#faf9f6] px-3 py-1 text-xs font-semibold text-[#6f6877]"
            >
              {{ header }}
            </span>
          </div>
        </a>
      </section>

      <section class="rounded-lg border border-[#ebe6ef] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
        <div class="border-b border-[#ebe6ef] p-6">
          <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('imports.historyTitle') }}</h3>
          <p class="mt-1 text-sm text-[#6f6877]">{{ historyHint() }}</p>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full min-w-[920px] text-left">
            <thead class="bg-[#faf9f6] text-xs uppercase tracking-[0.12em] text-[#7c7284]">
              <tr>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('imports.fileName') }}</th>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('imports.module') }}</th>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('imports.importedBy') }}</th>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('imports.importedAt') }}</th>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('imports.result') }}</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-[#ebe6ef]">
              <tr *ngFor="let record of visibleRecords()" class="text-sm">
                <td class="px-6 py-4 font-semibold text-[#32124f]">{{ record.fileName }}</td>
                <td class="px-6 py-4 text-[#6f6877]">{{ languageService.t(record.moduleTitleKey) }}</td>
                <td class="px-6 py-4 text-[#6f6877]">{{ record.importedByName }}</td>
                <td class="px-6 py-4 text-[#6f6877]">{{ formatDate(record.importedAt) }}</td>
                <td class="px-6 py-4">
                  <span
                    class="rounded px-3 py-1 text-xs font-semibold"
                    [ngClass]="record.status === 'Valid' ? 'bg-green-50 text-green-700' : 'bg-red-50 text-red-700'"
                  >
                    {{ record.status === 'Valid' ? languageService.t('imports.valid') : languageService.t('imports.rejected') }}
                  </span>
                  <p class="mt-1 text-xs text-[#7c7284]">
                    {{ record.validRows }} {{ languageService.t('imports.validRowsShort') }} / {{ record.invalidRows }} {{ languageService.t('imports.invalidRowsShort') }}
                  </p>
                </td>
              </tr>
              <tr *ngIf="visibleRecords().length === 0">
                <td class="px-6 py-8 text-center text-sm text-[#7c7284]" colspan="5">
                  {{ languageService.t('imports.noHistory') }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </section>
  `,
})
export class ManualExcelImportComponent {
  modules = importModules;
  records = signal<ImportRecord[]>([]);

  visibleRecords = computed(() => {
    const role = this.authService.getUserRole();
    if (role === 'Admin' || role === 'Principal') {
      return this.records();
    }

    const username = this.authService.getUsername() || 'unknown';
    return this.records().filter((record) => record.importedBy === username);
  });

  constructor(
    private readonly authService: AuthenticationService,
    readonly languageService: LanguageService,
  ) {}

  historyHint(): string {
    const role = this.authService.getUserRole();
    return role === 'Admin' || role === 'Principal'
      ? this.languageService.t('imports.historyAllHint')
      : this.languageService.t('imports.historyTeacherHint');
  }

  formatDate(value: string): string {
    return new Intl.DateTimeFormat(undefined, {
      dateStyle: 'medium',
      timeStyle: 'short',
      timeZone: 'Asia/Ho_Chi_Minh',
    }).format(new Date(value));
  }

}
