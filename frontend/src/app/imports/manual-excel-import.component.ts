import { Component, OnInit, signal } from '@angular/core';
import { NgFor, NgIf } from '@angular/common';
import { AuthenticationService } from '../core/auth/auth.service';
import { LanguageService } from '../core/language/language.service';
import {
  ImportModuleCategoryId,
  ImportRecord,
  importModuleCategories,
  importModules,
} from './import-template-data';
import { ManualEntrySheetComponent } from './manual-entry-sheet.component';
import { ImportHistoryService } from './import-history.service';
import { ExcelImportSheetComponent } from './excel-import-sheet.component';
import { FieldDesignerSheetComponent } from './field-designer-sheet.component';
import { ImportHistoryDetailSheetComponent } from './import-history-detail-sheet.component';

@Component({
  selector: 'app-manual-excel-import',
  imports: [NgFor, NgIf, ManualEntrySheetComponent, ExcelImportSheetComponent, FieldDesignerSheetComponent, ImportHistoryDetailSheetComponent],
  template: `
    <section class="space-y-7">
      <div class="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">{{ languageService.t('imports.eyebrow') }}</p>
          <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ languageService.t('imports.title') }}</h2>
          <p class="mt-2 max-w-3xl text-[#6f6877]">{{ languageService.t('imports.description') }}</p>
        </div>
      </div>

      @if (savedModuleTitleKey()) {
        <div class="flex items-center justify-between gap-4 rounded-md border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800">
          <span>
            {{ languageService.t(savedAction() === 'Excel' ? 'imports.excel.saved' : 'imports.manual.saved').replace('{module}', languageService.t(savedModuleTitleKey()!)) }}
          </span>
          <button class="font-semibold" type="button" (click)="savedModuleTitleKey.set(null)" aria-label="Dismiss">×</button>
        </div>
      }

      <div class="space-y-6">
        <section *ngFor="let category of categories" class="rounded-xl border border-[#e9e2ed] bg-[#f8f5f8] p-4 sm:p-5">
          <div class="mb-4 flex flex-wrap items-end justify-between gap-2">
            <div>
              <h3 class="text-base font-semibold text-[#32124f]">{{ languageService.t(category.titleKey) }}</h3>
              <p class="mt-1 text-sm text-[#746b7b]">{{ languageService.t(category.descriptionKey) }}</p>
            </div>
            <span class="text-xs font-semibold text-[#8a6811]">
              {{ modulesForCategory(category.id).length }} {{ languageService.t('imports.categories.modules') }}
            </span>
          </div>

          <div class="grid gap-3 md:grid-cols-2 xl:grid-cols-3">
            <article
              *ngFor="let module of modulesForCategory(category.id)"
              class="flex min-h-48 flex-col rounded-lg border border-[#e2dbe7] bg-white p-4 text-left shadow-[0_6px_18px_rgba(50,18,79,0.05)] transition hover:-translate-y-0.5 hover:border-[#cfc0d8] hover:shadow-[0_10px_24px_rgba(50,18,79,0.09)]"
            >
              <div class="flex items-start gap-3">
                <span class="grid h-9 w-9 shrink-0 place-items-center rounded-md bg-[#32124f] text-[#f3c246]">
                  <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                    <path d="M12 3v12" />
                    <path d="m7 10 5 5 5-5" />
                    <path d="M5 21h14" />
                  </svg>
                </span>
                <div class="min-w-0">
                  <h4 class="font-semibold leading-6 text-[#32124f]">{{ languageService.t(module.titleKey) }}</h4>
                  <span class="mt-1 inline-block rounded-full bg-[#e8f5ef] px-2.5 py-0.5 text-[11px] font-semibold text-[#31706f]">
                    {{ languageService.t('imports.manual.available') }}
                  </span>
                </div>
              </div>
              <p class="mt-3 line-clamp-2 text-sm leading-5 text-[#6f6877]">{{ languageService.t(module.descriptionKey) }}</p>
              <button
                *ngIf="canDesign() && module.supportsCustomFields !== false"
                class="mt-3 inline-flex items-center gap-1.5 self-start text-xs font-semibold text-[#7a5b12] transition hover:text-[#32124f]"
                type="button"
                (click)="openDesigner(module.id)"
              >
                <svg class="h-3.5 w-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
                  <path d="M12 20h9" />
                  <path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4Z" />
                </svg>
                {{ languageService.t('imports.designer.customize') }}
              </button>
              <div
                [class]="module.supportsManualEntry !== false
                  ? 'mt-auto grid grid-cols-[1fr_auto_1fr] items-center gap-2 pt-4'
                  : 'mt-auto pt-4'"
              >
                <button
                  class="rounded-md border border-[#d8d0df] px-3 py-2 text-sm font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
                  type="button"
                  [attr.data-testid]="'import-excel-' + module.id"
                  (click)="openExcelImport(module.id)"
                >
                  {{ languageService.t('imports.manual.importExcel') }}
                </button>
                <span *ngIf="module.supportsManualEntry !== false" class="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-[#f3eff6] text-[9px] font-bold uppercase leading-none text-[#6f6877]">
                  {{ languageService.t('imports.manual.or') }}
                </span>
                <button
                  *ngIf="module.supportsManualEntry !== false"
                  class="rounded-md bg-[#32124f] px-3 py-2 text-sm font-semibold text-white transition hover:bg-[#4b2070]"
                  type="button"
                  [attr.data-testid]="'add-manual-' + module.id"
                  (click)="openManualEntry(module.id)"
                >
                  {{ languageService.t('imports.manual.add') }}
                </button>
              </div>
            </article>
          </div>
        </section>
      </div>

      <section class="rounded-lg border border-[#ebe6ef] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
        <div class="border-b border-[#ebe6ef] p-6">
          <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('imports.historyTitle') }}</h3>
          <p class="mt-1 text-sm text-[#6f6877]">{{ historyHint() }}</p>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full min-w-[780px] text-left">
            <thead class="bg-[#faf9f6] text-xs uppercase tracking-[0.12em] text-[#7c7284]">
              <tr>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('imports.fileName') }}</th>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('imports.module') }}</th>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('imports.importedBy') }}</th>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('imports.importedAt') }}</th>
                <th class="px-6 py-4 text-right font-semibold">{{ languageService.t('imports.historyDetail.actions') }}</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-[#ebe6ef]">
              <tr *ngFor="let record of visibleRecords()" class="text-sm">
                <td class="px-6 py-4 font-semibold text-[#32124f]">
                  {{ record.fileName || languageService.t('imports.manual.entryLabel') }}
                </td>
                <td class="px-6 py-4 text-[#6f6877]">{{ languageService.t(record.moduleTitleKey) }}</td>
                <td class="px-6 py-4 text-[#6f6877]">{{ record.importedByName }}</td>
                <td class="px-6 py-4 text-[#6f6877]">{{ formatDate(record.importedAt) }}</td>
                <td class="px-6 py-4 text-right">
                  <button *ngIf="record.moduleId !== 'sis-students'" class="rounded-md border border-[#d8d0df] px-3 py-2 text-xs font-semibold text-[#32124f] hover:border-[#9d7caf] hover:bg-[#f8f4fa]" type="button" (click)="historyDetailId.set(record.id)">
                    {{ languageService.t('imports.historyDetail.view') }}
                  </button>
                </td>
              </tr>
              <tr *ngIf="historyLoading()">
                <td class="px-6 py-8 text-center text-sm text-[#7c7284]" colspan="5">
                  {{ languageService.t('imports.historyLoading') }}
                </td>
              </tr>
              <tr *ngIf="!historyLoading() && historyError()">
                <td class="px-6 py-8 text-center text-sm text-red-700" colspan="5">
                  {{ historyError() }}
                </td>
              </tr>
              <tr *ngIf="!historyLoading() && !historyError() && visibleRecords().length === 0">
                <td class="px-6 py-8 text-center text-sm text-[#7c7284]" colspan="5">
                  {{ languageService.t('imports.noHistory') }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
        <div *ngIf="!historyLoading() && historyTotalCount() > 0" class="flex flex-wrap items-center justify-between gap-3 border-t border-[#ebe6ef] px-5 py-4">
          <div class="flex items-center gap-2 text-xs text-[#6f6877]">
            <span>{{ historyPageLabel() }}</span>
            <label class="ml-2 flex items-center gap-2">
              {{ languageService.t('pagination.perPage') }}
              <select class="rounded-md border border-[#d8d0df] bg-white px-2 py-1.5 text-xs outline-none focus:border-[#6d3a91]" [value]="historyPageSize()" (change)="changeHistoryPageSize($event)">
                <option value="10">10</option>
                <option value="20">20</option>
                <option value="50">50</option>
              </select>
            </label>
          </div>
          <div class="flex gap-2">
            <button class="rounded-md border border-[#d8d0df] px-4 py-2 text-xs font-semibold text-[#32124f] transition hover:bg-[#f7f3fa] disabled:cursor-not-allowed disabled:opacity-40" type="button" [disabled]="historyPage() <= 1" (click)="previousHistoryPage()">
              {{ languageService.t('pagination.previous') }}
            </button>
            <button class="rounded-md border border-[#d8d0df] px-4 py-2 text-xs font-semibold text-[#32124f] transition hover:bg-[#f7f3fa] disabled:cursor-not-allowed disabled:opacity-40" type="button" [disabled]="historyPage() >= historyTotalPages()" (click)="nextHistoryPage()">
              {{ languageService.t('pagination.next') }}
            </button>
          </div>
        </div>
      </section>

      @if (manualModuleId()) {
        <app-manual-entry-sheet
          [moduleId]="manualModuleId()!"
          (close)="closeManualEntry()"
          (saved)="manualEntrySaved($event)"
        />
      }
      @if (excelModuleId()) {
        <app-excel-import-sheet
          [moduleId]="excelModuleId()!"
          (close)="closeExcelImport()"
          (imported)="excelImportSaved($event)"
        />
      }
      @if (designerModuleId(); as moduleId) {
        <app-field-designer-sheet
          [moduleId]="moduleId"
          [moduleTitleKey]="moduleTitleKeyFor(moduleId)"
          (close)="designerModuleId.set(null)"
        />
      }
      @if (historyDetailId(); as historyId) {
        <app-import-history-detail-sheet
          [historyId]="historyId"
          (close)="historyDetailId.set(null)"
          (changed)="loadHistory()"
          (deleted)="historyDeleted()"
        />
      }
    </section>
  `,
})
export class ManualExcelImportComponent implements OnInit {
  modules = importModules;
  categories = importModuleCategories;
  records = signal<ImportRecord[]>([]);
  manualModuleId = signal<string | null>(null);
  excelModuleId = signal<string | null>(null);
  designerModuleId = signal<string | null>(null);
  historyDetailId = signal<number | null>(null);
  savedModuleTitleKey = signal<string | null>(null);
  savedAction = signal<'Manual' | 'Excel'>('Manual');
  historyLoading = signal(true);
  historyError = signal<string | null>(null);
  historyPage = signal(1);
  historyPageSize = signal(20);
  historyTotalCount = signal(0);
  historyTotalPages = signal(0);

  readonly visibleRecords = this.records.asReadonly();

  constructor(
    private readonly authService: AuthenticationService,
    private readonly importHistoryService: ImportHistoryService,
    readonly languageService: LanguageService,
  ) {}

  ngOnInit(): void {
    this.loadHistory(1);
  }

  historyHint(): string {
    const role = this.authService.getUserRole();
    return role === 'Admin' || role === 'SchoolAdministration' || role === 'Principal'
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

  modulesForCategory(categoryId: ImportModuleCategoryId) {
    return this.modules.filter((module) => module.category === categoryId);
  }

  canDesign(): boolean {
    const role = this.authService.getUserRole();
    return role === 'Admin' || role === 'SchoolAdministration' || role === 'Principal';
  }

  openDesigner(moduleId: string): void {
    this.designerModuleId.set(moduleId);
  }

  moduleTitleKeyFor(moduleId: string): string {
    return this.modules.find((module) => module.id === moduleId)?.titleKey ?? '';
  }

  openManualEntry(moduleId: string): void {
    this.excelModuleId.set(null);
    this.manualModuleId.set(moduleId);
  }

  closeManualEntry(): void {
    this.manualModuleId.set(null);
  }

  manualEntrySaved(moduleId: string): void {
    const module = this.modules.find((candidate) => candidate.id === moduleId);
    this.savedModuleTitleKey.set(module?.titleKey ?? null);
    this.savedAction.set('Manual');
    this.closeManualEntry();
    this.loadHistory(1);
  }

  openExcelImport(moduleId: string): void {
    this.manualModuleId.set(null);
    this.excelModuleId.set(moduleId);
  }

  closeExcelImport(): void {
    this.excelModuleId.set(null);
  }

  excelImportSaved(moduleId: string): void {
    const module = this.modules.find((candidate) => candidate.id === moduleId);
    this.savedModuleTitleKey.set(module?.titleKey ?? null);
    this.savedAction.set('Excel');
    this.closeExcelImport();
    this.loadHistory(1);
  }

  historyDeleted(): void {
    this.historyDetailId.set(null);
    this.loadHistory(this.historyPage());
  }

  loadHistory(page = this.historyPage()): void {
    this.historyLoading.set(true);
    this.historyError.set(null);
    this.importHistoryService.list(page, this.historyPageSize()).subscribe({
      next: (response) => {
        this.records.set(response.items);
        this.historyPage.set(response.page);
        this.historyTotalCount.set(response.totalCount);
        this.historyTotalPages.set(response.totalPages);
        this.historyLoading.set(false);
      },
      error: () => {
        this.historyError.set(this.languageService.t('imports.historyLoadError'));
        this.historyLoading.set(false);
      },
    });
  }

  previousHistoryPage(): void {
    if (this.historyPage() > 1) this.loadHistory(this.historyPage() - 1);
  }

  nextHistoryPage(): void {
    if (this.historyPage() < this.historyTotalPages()) this.loadHistory(this.historyPage() + 1);
  }

  changeHistoryPageSize(event: Event): void {
    this.historyPageSize.set(Number((event.target as HTMLSelectElement).value));
    this.loadHistory(1);
  }

  historyPageLabel(): string {
    return this.languageService.t('pagination.pageOf')
      .replace('{page}', String(this.historyPage()))
      .replace('{pages}', String(this.historyTotalPages()))
      .replace('{total}', String(this.historyTotalCount()));
  }

}
