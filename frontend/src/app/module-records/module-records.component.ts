import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { finalize, forkJoin } from 'rxjs';
import { LanguageService } from '../core/language/language.service';
import { AuthenticationService } from '../core/auth/auth.service';
import { ExcelImportSheetComponent } from '../imports/excel-import-sheet.component';
import { FieldDesignerSheetComponent } from '../imports/field-designer-sheet.component';
import { ImportModule, importModules } from '../imports/import-template-data';
import { ImportModuleField, ImportModuleFieldsService } from '../imports/import-module-fields.service';
import { ManualEntryOptions, ManualEntryService } from '../imports/manual-entry.service';
import { ManualEntrySheetComponent } from '../imports/manual-entry-sheet.component';
import { ModuleRecord, ModuleRecordsService } from './module-records.service';

type RecordField = {
  key: string;
  required: boolean;
  type: 'text' | 'textarea' | 'number' | 'date' | 'datetime-local' | 'select' | 'checkbox';
  options: string[];
  custom: boolean;
  label?: string;
};

@Component({
  selector: 'app-module-records',
  imports: [
    FormsModule,
    RouterLink,
    ManualEntrySheetComponent,
    ExcelImportSheetComponent,
    FieldDesignerSheetComponent,
  ],
  template: `
    <section class="space-y-7">
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">{{ t('moduleRecords.eyebrow') }}</p>
          <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ t(module().titleKey) }}</h2>
          <p class="mt-2 max-w-3xl text-[#6f6877]">{{ t(module().descriptionKey) }}</p>
        </div>
        <div class="flex flex-wrap items-center justify-end gap-2">
          @if (canDesignFields() && module().supportsCustomFields !== false) {
            <button
              class="inline-flex items-center gap-2 rounded-md border border-[#d8d0df] bg-white px-4 py-2.5 text-sm font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
              type="button"
              (click)="designerOpen.set(true)"
            >
              <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 20h9"/><path d="M16.5 3.5a2.121 2.121 0 0 1 3 3L7 19l-4 1 1-4Z"/></svg>
              {{ t('imports.designer.customize') }}
            </button>
          }
          <button
            class="inline-flex items-center gap-2 rounded-md border border-[#d8d0df] bg-white px-4 py-2.5 text-sm font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
            type="button"
            (click)="excelImportOpen.set(true)"
          >
            <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 3v12"/><path d="m7 10 5 5 5-5"/><path d="M5 21h14"/></svg>
            {{ t('imports.manual.importExcel') }}
          </button>
          <button
            class="rounded-md bg-[#32124f] px-5 py-2.5 text-sm font-semibold text-white transition hover:bg-[#4b2070]"
            type="button"
            (click)="creating.set(true)"
          >{{ t('moduleRecords.addRecord') }}</button>
        </div>
      </div>

      <section class="rounded-lg border border-[#e6dfeb] bg-white p-5 shadow-[0_12px_35px_rgba(50,18,79,0.06)]">
        <form class="flex flex-wrap items-end gap-3" (ngSubmit)="load(1)">
          <label class="min-w-64 flex-1">
            <span class="mb-1.5 block text-xs font-bold uppercase tracking-[0.1em] text-[#776c80]">{{ t('moduleRecords.search') }}</span>
            <input
              class="w-full rounded-md border border-[#ddd5e3] bg-[#faf9fb] px-3 py-2.5 text-sm outline-none transition focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10"
              name="search"
              [(ngModel)]="search"
              [placeholder]="t('moduleRecords.searchPlaceholder')"
            />
          </label>
          <button class="rounded-md border border-[#d8d0df] px-4 py-2.5 text-sm font-semibold text-[#32124f] hover:border-[#f3c246] hover:bg-[#fff8e1]" type="submit">
            {{ t('moduleRecords.applySearch') }}
          </button>
          <button class="rounded-md px-4 py-2.5 text-sm font-semibold text-[#6d3a91] hover:bg-[#f7f3fa]" type="button" (click)="clearSearch()">
            {{ t('moduleRecords.clearSearch') }}
          </button>
        </form>
      </section>

      @if (message()) {
        <p class="rounded-md border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800">{{ message() }}</p>
      }
      @if (error()) {
        <p class="rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">{{ error() }}</p>
      }

      <section class="overflow-hidden rounded-lg border border-[#e6dfeb] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
        <div class="flex flex-wrap items-center justify-between gap-3 border-b border-[#e6dfeb] px-6 py-5">
          <div>
            <h3 class="text-lg font-semibold text-[#32124f]">{{ t('moduleRecords.registerTitle') }}</h3>
            <p class="mt-1 text-sm text-[#6f6877]">{{ totalCount() }} {{ t('moduleRecords.records') }}</p>
          </div>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full min-w-max text-left">
            <thead class="bg-[#faf9f6] text-xs uppercase tracking-[0.1em] text-[#7c7284]">
              <tr>
                @if (isHealthModule()) {
                  <th class="min-w-28 px-5 py-4 font-semibold">{{ t('imports.templateFields.class_name') }}</th>
                }
                <th class="min-w-56 px-5 py-4 font-semibold">{{ t('moduleRecords.student') }}</th>
                @for (field of fields(); track field.key) {
                  <th class="min-w-36 px-5 py-4 font-semibold">{{ fieldLabel(field) }}</th>
                }
                @if (isHealthModule()) {
                  <th class="min-w-32 px-5 py-4 font-semibold">{{ t('imports.templateFields.sis') }}</th>
                }
                <th class="min-w-40 px-5 py-4 font-semibold">{{ t('moduleRecords.enteredBy') }}</th>
                <th class="sticky right-0 min-w-40 bg-[#faf9f6] px-5 py-4 text-right font-semibold">{{ t('moduleRecords.actions') }}</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-[#ebe6ef]">
              @for (record of records(); track record.id) {
                <tr class="align-top text-sm transition hover:bg-[#fdfcff]">
                  @if (isHealthModule()) {
                    <td class="whitespace-nowrap px-5 py-4 font-semibold text-[#32124f]">{{ record.homeroom || '—' }}</td>
                  }
                  <td class="px-5 py-4">
                    @if (record.studentProfileId) {
                      <a class="font-semibold text-[#32124f] hover:text-[#6d3a91] hover:underline" [routerLink]="['/students', record.studentProfileId]">{{ record.studentName }}</a>
                    } @else {
                      <span class="font-semibold text-[#32124f]">{{ record.studentName }}</span>
                    }
                    @if (!isHealthModule()) {
                      <span class="mt-1 block text-xs text-[#8b8193]">
                        {{ record.sisUserId }}@if (record.gradeLevel) { · {{ t('imports.manual.grade') }} {{ record.gradeLevel }}}@if (record.homeroom) { · {{ record.homeroom }}}
                      </span>
                    }
                  </td>
                  @for (field of fields(); track field.key) {
                    <td class="max-w-72 px-5 py-4 text-[#5f5368]" [class.whitespace-nowrap]="field.type === 'date' || field.type === 'datetime-local'">
                      @if (isBadgeField(field.key) && record.values[field.key]) {
                        <span class="rounded-full px-2.5 py-1 text-xs font-semibold" [class]="badgeClass(record.values[field.key])">
                          {{ displayValue(field.key, record.values[field.key]) }}
                        </span>
                      } @else {
                        <span [class.line-clamp-3]="field.type === 'textarea'">{{ displayValue(field.key, record.values[field.key]) || '—' }}</span>
                      }
                    </td>
                  }
                  @if (isHealthModule()) {
                    <td class="whitespace-nowrap px-5 py-4 text-[#6f6877]">{{ record.sisUserId || '—' }}</td>
                  }
                  <td class="px-5 py-4 text-[#6f6877]">
                    {{ record.enteredByName || '—' }}
                    <span class="mt-1 block whitespace-nowrap text-xs text-[#9a91a3]">{{ formatDateTime(record.createdAt) }}</span>
                  </td>
                  <td class="sticky right-0 bg-white px-5 py-4 text-right">
                    <div class="flex justify-end gap-2">
                      <button class="rounded-md border border-[#d8d0df] px-3 py-2 text-xs font-semibold text-[#32124f] hover:bg-[#f7f3fa]" type="button" (click)="edit(record)">{{ t('moduleRecords.edit') }}</button>
                      <button class="rounded-md border border-red-200 px-3 py-2 text-xs font-semibold text-red-700 hover:bg-red-50" type="button" (click)="remove(record)">{{ t('moduleRecords.delete') }}</button>
                    </div>
                  </td>
                </tr>
              }
              @if (loading()) {
                <tr><td class="px-6 py-12 text-center text-sm text-[#7c7284]" [attr.colspan]="fields().length + 3 + (isHealthModule() ? 2 : 0)">{{ t('moduleRecords.loading') }}</td></tr>
              } @else if (!records().length) {
                <tr><td class="px-6 py-12 text-center text-sm text-[#7c7284]" [attr.colspan]="fields().length + 3 + (isHealthModule() ? 2 : 0)">{{ t('moduleRecords.empty') }}</td></tr>
              }
            </tbody>
          </table>
        </div>

        @if (!loading() && totalCount() > 0) {
          <div class="flex flex-wrap items-center justify-between gap-3 border-t border-[#ebe6ef] px-5 py-4">
            <div class="flex items-center gap-2 text-xs text-[#6f6877]">
              <span>{{ pageLabel() }}</span>
              <label class="ml-2 flex items-center gap-2">
                {{ t('pagination.perPage') }}
                <select class="rounded-md border border-[#d8d0df] bg-white px-2 py-1.5 text-xs" [value]="pageSize()" (change)="changePageSize($event)">
                  <option value="10">10</option><option value="20">20</option><option value="50">50</option>
                </select>
              </label>
            </div>
            <div class="flex gap-2">
              <button class="rounded-md border border-[#d8d0df] px-4 py-2 text-xs font-semibold text-[#32124f] disabled:opacity-40" type="button" [disabled]="page() <= 1" (click)="load(page() - 1)">{{ t('pagination.previous') }}</button>
              <button class="rounded-md border border-[#d8d0df] px-4 py-2 text-xs font-semibold text-[#32124f] disabled:opacity-40" type="button" [disabled]="page() >= totalPages()" (click)="load(page() + 1)">{{ t('pagination.next') }}</button>
            </div>
          </div>
        }
      </section>

      @if (creating()) {
        <app-manual-entry-sheet [moduleId]="moduleId" (close)="creating.set(false)" (saved)="created()" />
      }

      @if (excelImportOpen()) {
        <app-excel-import-sheet
          [moduleId]="moduleId"
          (close)="excelImportOpen.set(false)"
          (imported)="excelImported()"
        />
      }

      @if (designerOpen()) {
        <app-field-designer-sheet
          [moduleId]="moduleId"
          [moduleTitleKey]="module().titleKey"
          (close)="designerClosed()"
          (changed)="reloadCustomFields()"
        />
      }

      @if (editing()) {
        <div class="fixed inset-0 z-[70]">
          <button class="absolute inset-0 h-full w-full bg-[#1d1025]/35" type="button" [attr.aria-label]="t('moduleRecords.close')" (click)="closeEdit()"></button>
          <aside class="absolute inset-y-0 right-0 flex w-full max-w-xl flex-col bg-white shadow-[-18px_0_45px_rgba(50,18,79,0.18)]" role="dialog" aria-modal="true">
            <header class="flex items-start justify-between gap-4 border-b border-[#ebe6ef] px-6 py-5">
              <div>
                <p class="text-xs font-semibold uppercase tracking-[0.18em] text-[#8a6811]">{{ t('moduleRecords.editEyebrow') }}</p>
                <h2 class="mt-2 text-2xl font-semibold text-[#32124f]">{{ editing()!.studentName }}</h2>
                <p class="mt-1 text-sm text-[#6f6877]">{{ editing()!.sisUserId }}</p>
              </div>
              <button class="grid h-10 w-10 place-items-center rounded-full border border-[#ded6e4] text-xl text-[#5d5265]" type="button" (click)="closeEdit()">×</button>
            </header>
            <div class="flex-1 overflow-y-auto px-6 py-6">
              <form id="module-record-edit-form" class="space-y-5" (ngSubmit)="saveEdit()">
                @for (field of fields(); track field.key) {
                  <label class="block">
                    <span class="text-sm font-semibold text-[#32124f]">{{ fieldLabel(field) }}@if (field.required) { <span class="text-red-600">*</span> }</span>
                    @if (field.type === 'textarea') {
                      <textarea class="mt-2 min-h-28 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" [name]="field.key" [required]="field.required" [(ngModel)]="editValues[field.key]"></textarea>
                    } @else if (field.type === 'select' || field.type === 'checkbox') {
                      <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" [name]="field.key" [required]="field.required" [(ngModel)]="editValues[field.key]">
                        <option value="">{{ t('moduleRecords.selectValue') }}</option>
                        @for (option of optionsFor(field); track option) { <option [value]="option">{{ displayValue(field.key, option) }}</option> }
                      </select>
                    } @else {
                      <input class="mt-2 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" [type]="field.type" [name]="field.key" [required]="field.required" [(ngModel)]="editValues[field.key]" />
                    }
                  </label>
                }
                @if (editError()) { <p class="rounded-md bg-red-50 p-4 text-sm text-red-700">{{ editError() }}</p> }
              </form>
            </div>
            <footer class="flex justify-end gap-3 border-t border-[#ebe6ef] bg-[#faf9f6] px-6 py-4">
              <button class="rounded-md border border-[#d8d0df] px-5 py-2.5 text-sm font-semibold text-[#32124f]" type="button" (click)="closeEdit()">{{ t('moduleRecords.cancel') }}</button>
              <button class="rounded-md bg-[#32124f] px-5 py-2.5 text-sm font-semibold text-white disabled:opacity-45" type="submit" form="module-record-edit-form" [disabled]="saving()">{{ saving() ? t('moduleRecords.saving') : t('moduleRecords.save') }}</button>
            </footer>
          </aside>
        </div>
      }
    </section>
  `,
})
export class ModuleRecordsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(ModuleRecordsService);
  private readonly fieldsService = inject(ImportModuleFieldsService);
  private readonly manualService = inject(ManualEntryService);
  private readonly authService = inject(AuthenticationService);
  readonly languageService = inject(LanguageService);

  readonly moduleId = String(this.route.snapshot.data['moduleId']);
  readonly records = signal<ModuleRecord[]>([]);
  readonly customFields = signal<ImportModuleField[]>([]);
  readonly options = signal<ManualEntryOptions | null>(null);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly message = signal<string | null>(null);
  readonly page = signal(1);
  readonly pageSize = signal(20);
  readonly totalCount = signal(0);
  readonly totalPages = signal(0);
  readonly creating = signal(false);
  readonly excelImportOpen = signal(false);
  readonly designerOpen = signal(false);
  readonly editing = signal<ModuleRecord | null>(null);
  readonly saving = signal(false);
  readonly editError = signal<string | null>(null);
  search = '';
  editValues: Record<string, string | null> = {};

  ngOnInit(): void {
    forkJoin({ fields: this.fieldsService.list(this.moduleId), options: this.manualService.options() }).subscribe({
      next: ({ fields, options }) => { this.customFields.set(fields); this.options.set(options); },
      error: () => this.error.set(this.t('moduleRecords.optionsError')),
    });
    this.load();
  }

  module(): ImportModule {
    return importModules.find((item) => item.id === this.moduleId)!;
  }

  fields(): RecordField[] {
    const module = this.module();
    const standard = module.headers.slice(2).map((key): RecordField => ({
      key,
      required: module.requiredHeaders.includes(key),
      type: this.standardFieldType(key),
      options: [],
      custom: false,
    }));
    return [...standard, ...this.customFields().map((field): RecordField => ({
      key: field.fieldKey,
      required: field.required,
      type: field.fieldType,
      options: field.options,
      custom: true,
      label: field.label,
    }))];
  }

  load(page = this.page()): void {
    this.loading.set(true); this.error.set(null);
    this.service.list(this.moduleId, page, this.pageSize(), this.search).subscribe({
      next: (result) => {
        this.records.set(result.items); this.page.set(result.page); this.totalCount.set(result.totalCount);
        this.totalPages.set(result.totalPages); this.loading.set(false);
      },
      error: () => { this.error.set(this.t('moduleRecords.loadError')); this.loading.set(false); },
    });
  }

  clearSearch(): void { this.search = ''; this.load(1); }
  changePageSize(event: Event): void { this.pageSize.set(Number((event.target as HTMLSelectElement).value)); this.load(1); }
  pageLabel(): string { return this.t('pagination.pageOf').replace('{page}', String(this.page())).replace('{pages}', String(this.totalPages())).replace('{total}', String(this.totalCount())); }
  fieldLabel(field: RecordField): string { return field.custom ? field.label! : this.t(`imports.templateFields.${field.key}`); }
  isHealthModule(): boolean { return this.moduleId === 'student-health-visits'; }
  formatDateTime(value: string): string { return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(value)); }
  isBadgeField(key: string): boolean { return ['behaviour_type', 'status', 'risk_level', 'violation_type'].includes(key); }

  displayValue(key: string, value: string | null | undefined): string {
    if (!value) return '';
    if (key === 'visit_date') return this.formatDateTime(value);
    if (value === 'true' || value === 'false') return this.t(`moduleRecords.boolean.${value}`);
    const slug = value.toLocaleLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
    const prefix = key === 'concern_type' ? 'imports.mentalHealthOptions.concern.'
      : key === 'risk_level' ? 'imports.mentalHealthOptions.risk.'
      : key === 'trip_direction' ? 'imports.busOptions.direction.'
      : key === 'violation_type' ? 'imports.busOptions.violation.' : '';
    if (!prefix) return value;
    const translated = this.t(prefix + slug);
    return translated === prefix + slug ? value : translated;
  }

  badgeClass(value: string | null): string {
    const normalized = value?.toLocaleLowerCase() ?? '';
    if (normalized.includes('high') || normalized.includes('no seatbelt') || normalized.includes('concerning')) return 'bg-red-50 text-red-700';
    if (normalized.includes('medium') || normalized.includes('late') || normalized.includes('progress')) return 'bg-amber-50 text-amber-800';
    return 'bg-emerald-50 text-emerald-700';
  }

  edit(record: ModuleRecord): void {
    this.editing.set(record);
    this.editValues = Object.fromEntries(this.fields().map((field) => [field.key, record.values[field.key] ?? '']));
    this.editError.set(null);
  }

  closeEdit(): void { this.editing.set(null); this.editError.set(null); }
  saveEdit(): void {
    const record = this.editing(); if (!record) return;
    this.saving.set(true); this.editError.set(null);
    this.service.update(this.moduleId, record.id, this.editValues).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => { this.closeEdit(); this.message.set(this.t('moduleRecords.saved')); this.load(); },
      error: (response) => this.editError.set(response?.error?.message || this.t('moduleRecords.saveError')),
    });
  }

  remove(record: ModuleRecord): void {
    if (!window.confirm(this.t('moduleRecords.confirmDelete').replace('{student}', record.studentName))) return;
    this.service.delete(this.moduleId, record.id).subscribe({
      next: () => { this.message.set(this.t('moduleRecords.deleted')); this.load(); },
      error: () => this.error.set(this.t('moduleRecords.deleteError')),
    });
  }

  created(): void { this.creating.set(false); this.message.set(this.t('moduleRecords.created')); this.load(1); }
  excelImported(): void {
    this.excelImportOpen.set(false);
    this.message.set(
      this.t('imports.excel.saved').replace('{module}', this.t(this.module().titleKey)),
    );
    this.load(1);
  }

  designerClosed(): void {
    this.designerOpen.set(false);
    this.reloadCustomFields();
  }

  reloadCustomFields(): void {
    this.fieldsService.list(this.moduleId).subscribe({
      next: (fields) => this.customFields.set(fields),
      error: () => this.error.set(this.t('moduleRecords.optionsError')),
    });
  }

  canDesignFields(): boolean {
    const role = this.authService.getUserRole();
    return role === 'Admin' || role === 'SchoolAdministration' || role === 'Principal';
  }

  optionsFor(field: RecordField): string[] {
    if (field.type === 'checkbox') return ['true', 'false'];
    if (field.custom) return field.options;
    const options = this.options(); if (!options) return [];
    return field.key === 'behaviour_type' ? [...options.positiveBehaviourTypes, ...options.concerningBehaviourTypes]
      : field.key === 'subject' ? options.projectSubjects
      : field.key === 'status' ? options.projectStatuses
      : field.key === 'academic_year' ? options.academicYears
      : field.key === 'concern_type' ? options.mentalHealthConcernTypes
      : field.key === 'risk_level' ? options.mentalHealthRiskLevels
      : field.key === 'trip_direction' ? options.busTripDirections
      : field.key === 'violation_type' ? options.busViolationTypes : [];
  }

  t(key: string): string { return this.languageService.t(key); }
  private standardFieldType(key: string): RecordField['type'] {
    if (key === 'visit_date') return 'datetime-local';
    if (key.endsWith('_date')) return 'date';
    if (['notes', 'teacher_notes', 'symptoms', 'diagnosis', 'treatment'].includes(key)) return 'textarea';
    if (key === 'notification_sent') return 'checkbox';
    if (['behaviour_type', 'subject', 'status', 'academic_year', 'concern_type', 'risk_level', 'trip_direction', 'violation_type'].includes(key)) return 'select';
    return 'text';
  }
}
