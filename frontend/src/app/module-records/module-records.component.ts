import { AfterViewInit, Component, ElementRef, inject, OnDestroy, OnInit, signal, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { catchError, finalize, forkJoin, of } from 'rxjs';
import { LanguageService } from '../core/language/language.service';
import { AuthenticationService } from '../core/auth/auth.service';
import { ExcelImportSheetComponent } from '../imports/excel-import-sheet.component';
import { FieldDesignerSheetComponent } from '../imports/field-designer-sheet.component';
import { ImportModule, importModules } from '../imports/import-template-data';
import { ImportModuleField, ImportModuleFieldsService } from '../imports/import-module-fields.service';
import { ManualEntryOptions, ManualEntryService } from '../imports/manual-entry.service';
import { ManualEntrySheetComponent } from '../imports/manual-entry-sheet.component';
import { BehaviourTypeSelectComponent } from '../imports/behaviour-type-select.component';
import { defaultFilterSet, WorkspaceFiltersService, WorkspaceFilterSet } from '../workspace/workspace-filters.service';
import { ModuleRecord, ModuleRecordsService } from './module-records.service';

type RecordField = {
  key: string;
  required: boolean;
  type: 'text' | 'email' | 'textarea' | 'number' | 'date' | 'datetime-local' | 'select' | 'checkbox';
  options: string[];
  custom: boolean;
  label?: string;
  sectionKey?: string;
};

@Component({
  selector: 'app-module-records',
  imports: [
    FormsModule,
    RouterLink,
    ManualEntrySheetComponent,
    ExcelImportSheetComponent,
    FieldDesignerSheetComponent,
    BehaviourTypeSelectComponent,
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
          <label class="w-full sm:w-48">
            <span class="mb-1.5 block text-xs font-bold uppercase tracking-[0.1em] text-[#776c80]">{{ t('dashboard.period.year') }}</span>
            <select
              class="w-full rounded-md border border-[#ddd5e3] bg-[#faf9fb] px-3 py-2.5 text-sm font-semibold text-[#32124f] outline-none transition focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10 disabled:cursor-wait disabled:opacity-60"
              name="academicYear"
              [(ngModel)]="selectedYear"
              (ngModelChange)="onPeriodChange()"
              [disabled]="periodLoading()"
            >
              <option value="">{{ t('moduleRecords.allYears') }}</option>
              @for (year of years(); track year) {
                <option [value]="year">{{ formatYear(year) }}</option>
              }
            </select>
          </label>
          <label class="w-full sm:w-44">
            <span class="mb-1.5 block text-xs font-bold uppercase tracking-[0.1em] text-[#776c80]">{{ t('dashboard.period.term') }}</span>
            <select
              class="w-full rounded-md border border-[#ddd5e3] bg-[#faf9fb] px-3 py-2.5 text-sm font-semibold text-[#32124f] outline-none transition focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10 disabled:cursor-not-allowed disabled:opacity-60"
              name="reportingTerm"
              [(ngModel)]="selectedTerm"
              (ngModelChange)="onPeriodChange()"
              [disabled]="periodLoading() || !selectedYear"
            >
              <option value="all">{{ t('dashboard.period.term.all') }}</option>
              @for (term of terms(); track term) {
                <option [value]="term">{{ termLabel(term) }}</option>
              }
            </select>
          </label>
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

        <div
          #topScroll
          class="overflow-x-auto overflow-y-hidden border-b border-[#ebe6ef] bg-[#faf9fb]"
          [style.height.px]="tableOverflows() ? 16 : 0"
          [attr.aria-label]="t('moduleRecords.horizontalScroll')"
          (scroll)="syncHorizontalScroll('top')"
        >
          <div class="h-px" [style.width.px]="tableScrollWidth()"></div>
        </div>

        <div #tableScroll class="overflow-x-auto" (scroll)="syncHorizontalScroll('table')">
          <table #recordsTable class="w-full min-w-max text-left">
            <thead class="bg-[#faf9f6] text-xs uppercase tracking-[0.1em] text-[#7c7284]">
              <tr>
                @if (isHealthModule()) {
                  <th class="min-w-28 px-5 py-4 font-semibold">{{ t('imports.templateFields.class_name') }}</th>
                }
                <th class="min-w-56 px-5 py-4 font-semibold">{{ t('moduleRecords.student') }}</th>
                @for (field of tableFields(); track field.key) {
                  <th class="min-w-36 px-5 py-4 font-semibold" [class.min-w-48]="isBehaviourModule() && field.key === 'incident_date'">{{ tableFieldLabel(field) }}</th>
                }
                @if (isHealthModule()) {
                  <th class="min-w-32 px-5 py-4 font-semibold">{{ t('imports.templateFields.sis') }}</th>
                }
                @if (!isBehaviourModule()) {
                  <th class="min-w-40 px-5 py-4 font-semibold">{{ t('moduleRecords.enteredBy') }}</th>
                }
                <th class="sticky right-0 z-20 min-w-40 bg-[#f1edf4] px-5 py-4 text-right font-semibold text-[#5b287d] before:absolute before:inset-y-0 before:left-0 before:w-0.5 before:bg-[#8f70a1] before:content-['']">{{ t('moduleRecords.actions') }}</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-[#ebe6ef]">
              @for (record of records(); track record.id) {
                <tr class="group align-top text-sm transition hover:bg-[#fdfcff]">
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
                  @for (field of tableFields(); track field.key) {
                    <td class="max-w-72 px-5 py-4 text-[#5f5368]" [class.whitespace-nowrap]="field.type === 'date' || field.type === 'datetime-local'">
                      @if (isBehaviourModule() && field.key === 'incident_date') {
                        <span class="font-medium text-[#4f4357]">{{ displayValue(field.key, record.values[field.key]) || '—' }}</span>
                        @if (record.values['incident_time']) {
                          <span class="mt-1 block text-xs text-[#81758a]">{{ record.values['incident_time'] }}</span>
                        }
                      } @else if (isBadgeField(field.key) && record.values[field.key]) {
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
                  @if (!isBehaviourModule()) {
                    <td class="px-5 py-4 text-[#6f6877]">
                      {{ record.enteredByName || '—' }}
                      <span class="mt-1 block whitespace-nowrap text-xs text-[#9a91a3]">{{ formatDateTime(record.createdAt) }}</span>
                    </td>
                  }
                  <td class="sticky right-0 z-10 bg-[#fbf8fc] px-5 py-4 text-right transition before:absolute before:inset-y-0 before:left-0 before:w-0.5 before:bg-[#8f70a1] before:content-[''] group-hover:bg-[#f7f1fa]">
                    <div class="flex justify-end gap-2">
                      <button class="rounded-md border border-[#d8d0df] px-3 py-2 text-xs font-semibold text-[#32124f] hover:bg-[#f7f3fa]" type="button" (click)="edit(record)">{{ t('moduleRecords.edit') }}</button>
                      <button class="rounded-md border border-red-200 px-3 py-2 text-xs font-semibold text-red-700 hover:bg-red-50" type="button" (click)="remove(record)">{{ t('moduleRecords.delete') }}</button>
                    </div>
                  </td>
                </tr>
              }
              @if (loading()) {
                <tr><td class="px-6 py-12 text-center text-sm text-[#7c7284]" [attr.colspan]="tableColumnCount()">{{ t('moduleRecords.loading') }}</td></tr>
              } @else if (!records().length) {
                <tr><td class="px-6 py-12 text-center text-sm text-[#7c7284]" [attr.colspan]="tableColumnCount()">{{ t('moduleRecords.empty') }}</td></tr>
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
          (changed)="designerChanged()"
        />
      }

      @if (editing()) {
        <div class="fixed inset-0 z-[70]">
          <button class="absolute inset-0 h-full w-full bg-[#1d1025]/35" type="button" [attr.aria-label]="t('moduleRecords.close')" (click)="closeEdit()"></button>
          <aside class="absolute inset-y-0 right-0 flex w-full max-w-xl flex-col bg-white shadow-[-18px_0_45px_rgba(50,18,79,0.18)]" role="dialog" aria-modal="true">
            <header class="flex items-start justify-between gap-4 border-b border-[#ebe6ef] px-6 py-5">
              <div>
                <p class="text-xs font-semibold uppercase tracking-[0.18em] text-[#8a6811]">{{ t('moduleRecords.editEyebrow') }}</p>
                <h2 class="mt-2 text-2xl font-semibold text-[#32124f]">{{ t(module().titleKey) }}</h2>
                <p class="mt-1 text-sm text-[#6f6877]">{{ editing()!.studentName }} · {{ editing()!.sisUserId }}</p>
              </div>
              <button class="grid h-10 w-10 place-items-center rounded-full border border-[#ded6e4] text-xl text-[#5d5265]" type="button" (click)="closeEdit()">×</button>
            </header>
            <div class="flex-1 overflow-y-auto px-6 py-6">
              <form id="module-record-edit-form" class="grid grid-cols-1 gap-5 sm:grid-cols-6" (ngSubmit)="saveEdit()">
                <div class="sm:col-span-6">
                  <span class="text-sm font-semibold text-[#32124f]">{{ t('imports.manual.student') }} <span class="text-red-600">*</span></span>
                  <div class="mt-2 rounded-md border border-[#d8d0df] bg-[#faf8fb] px-4 py-3">
                    <p class="text-sm font-semibold text-[#32124f]">{{ editing()!.studentName }}</p>
                    <p class="mt-0.5 text-xs text-[#7c7284]">{{ editing()!.sisUserId }}</p>
                  </div>
                </div>
                @for (field of editFields(); track field.key) {
                  @if (field.sectionKey) {
                    <div class="border-t border-[#ebe6ef] pt-5 sm:col-span-6">
                      <p class="text-xs font-semibold uppercase tracking-[0.16em] text-[#8a6811]">{{ t(field.sectionKey) }}</p>
                    </div>
                  }
                  @if (field.key === 'behaviour_type') {
                    <div [class]="editFieldClass(field)">
                      <span class="text-sm font-semibold text-[#32124f]">{{ editFieldLabel(field) }}@if (field.required) { <span class="text-red-600"> *</span> }</span>
                      <app-behaviour-type-select
                        class="mt-2 block"
                        [positiveOptions]="options()?.positiveBehaviourTypes ?? []"
                        [concerningOptions]="options()?.concerningBehaviourTypes ?? []"
                        [value]="editValues[field.key]"
                        (valueChange)="editValues[field.key] = $event"
                      />
                    </div>
                  } @else {
                    <label [class]="editFieldClass(field) + (isCompactBehaviourField(field) ? ' grid grid-rows-[2.5rem_auto] content-start' : ' block')">
                    <span class="text-sm font-semibold text-[#32124f]">{{ editFieldLabel(field) }}@if (field.required) { <span class="text-red-600"> *</span> }</span>
                    @if (field.type === 'textarea') {
                      <textarea class="mt-2 min-h-28 w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" [name]="field.key" [required]="field.required" [(ngModel)]="editValues[field.key]"></textarea>
                    } @else if (field.type === 'checkbox') {
                      <span class="mt-2 flex min-h-12 items-center rounded-md border border-[#d8d0df] px-3 py-3">
                        <input
                          class="h-4 w-4 accent-[#5d2d7b]"
                          type="checkbox"
                          [name]="field.key"
                          [required]="field.required"
                          [ngModel]="editValues[field.key] === 'true'"
                          (ngModelChange)="editValues[field.key] = $event ? 'true' : 'false'"
                        />
                      </span>
                    } @else if (field.type === 'select') {
                      <select class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" [name]="field.key" [required]="field.required" [(ngModel)]="editValues[field.key]">
                        <option value="">{{ field.required ? t('moduleRecords.selectValue') : '' }}</option>
                        @for (option of optionsFor(field); track option) { <option [value]="option">{{ displayValue(field.key, option) }}</option> }
                      </select>
                    } @else {
                      <input class="w-full rounded-md border border-[#d8d0df] px-3 py-3 text-sm outline-none focus:border-[#6d3a91]" [class.mt-2]="!isCompactBehaviourField(field)" [type]="field.type" [name]="field.key" [required]="field.required" [(ngModel)]="editValues[field.key]" />
                    }
                    </label>
                  }
                }
                @if (editError()) { <p class="rounded-md bg-red-50 p-4 text-sm text-red-700 sm:col-span-6">{{ editError() }}</p> }
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
export class ModuleRecordsComponent implements OnInit, AfterViewInit, OnDestroy {
  @ViewChild('topScroll') private topScroll?: ElementRef<HTMLDivElement>;
  @ViewChild('tableScroll') private tableScroll?: ElementRef<HTMLDivElement>;
  @ViewChild('recordsTable') private recordsTable?: ElementRef<HTMLTableElement>;

  private readonly route = inject(ActivatedRoute);
  private readonly service = inject(ModuleRecordsService);
  private readonly fieldsService = inject(ImportModuleFieldsService);
  private readonly manualService = inject(ManualEntryService);
  private readonly authService = inject(AuthenticationService);
  private readonly workspaceFilters = inject(WorkspaceFiltersService);
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
  readonly years = signal<string[]>([]);
  readonly terms = signal<string[]>([]);
  readonly periodLoading = signal(true);
  readonly tableScrollWidth = signal(0);
  readonly tableOverflows = signal(false);
  search = '';
  selectedYear = '';
  selectedTerm = 'all';
  editValues: Record<string, string | null> = {};
  private currentFilters: WorkspaceFilterSet = defaultFilterSet();
  private tableResizeObserver?: ResizeObserver;

  ngOnInit(): void {
    forkJoin({ fields: this.fieldsService.list(this.moduleId), options: this.manualService.options() }).subscribe({
      next: ({ fields, options }) => { this.customFields.set(fields); this.options.set(options); },
      error: () => this.error.set(this.t('moduleRecords.optionsError')),
    });
    this.workspaceFilters.load().pipe(catchError(() => of(defaultFilterSet()))).subscribe((filters) => {
      this.currentFilters = filters;
      this.selectedYear = filters.year;
      this.selectedTerm = filters.semester || 'all';
      this.ensureSelectedPeriodOptions();
      this.load();
    });
    this.workspaceFilters.periodOptions().pipe(
      catchError(() => of({ academicYears: [] as string[], terms: [] as string[] })),
    ).subscribe((options) => {
      this.years.set([...options.academicYears]);
      this.terms.set([...options.terms]);
      this.ensureSelectedPeriodOptions();
      this.periodLoading.set(false);
    });
  }

  ngAfterViewInit(): void {
    this.tableResizeObserver = new ResizeObserver(() => this.updateTableScrollSize());
    if (this.recordsTable) this.tableResizeObserver.observe(this.recordsTable.nativeElement);
    if (this.tableScroll) this.tableResizeObserver.observe(this.tableScroll.nativeElement);
    queueMicrotask(() => this.updateTableScrollSize());
  }

  ngOnDestroy(): void {
    this.tableResizeObserver?.disconnect();
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

  editFields(): RecordField[] {
    const allFields = this.fields();
    const standardFields = allFields.filter((field) => !field.custom);
    const customFields = allFields.filter((field) => field.custom);
    const orderByModule: Record<string, string[]> = {
      'student-behaviours': [
        'incident_date', 'incident_time', 'behaviour_type', 'academic_year', 'term',
        'subject', 'reporter_name', 'recorded_by_email', 'recorded_by_title', 'decision',
        'conduct_deduction_level', 'occurrence_count', 'behaviour_points_deducted', 'notes',
      ],
      'student-projects': ['project_name', 'subject', 'status', 'teacher_notes'],
      'student-bus-attendance': ['attendance_date', 'trip_direction', 'violation_type', 'bus_code', 'route_name', 'notes'],
      'student-mental-health': ['record_date', 'academic_year', 'concern_type', 'risk_level', 'notes'],
      'student-health-visits': ['visit_date', 'classification', 'diagnosis', 'symptoms', 'treatment', 'medication', 'notes', 'notification_sent'],
    };
    const order = orderByModule[this.moduleId] ?? standardFields.map((field) => field.key);
    const standardByKey = new Map(standardFields.map((field) => [field.key, field]));
    const orderedStandard = order
      .map((key) => standardByKey.get(key))
      .filter((field): field is RecordField => !!field)
      .map((field) => ({
        ...field,
        sectionKey: this.moduleId === 'student-behaviours' && field.key === 'subject'
          ? 'imports.manual.behaviourDetails'
          : undefined,
      }));
    const orderedKeys = new Set(orderedStandard.map((field) => field.key));
    const remainingStandard = standardFields.filter((field) => !orderedKeys.has(field.key));
    const orderedCustom = customFields.map((field, index) => ({
      ...field,
      sectionKey: index === 0 ? 'imports.designer.customSection' : undefined,
    }));
    return [...orderedStandard, ...remainingStandard, ...orderedCustom];
  }

  editFieldClass(field: RecordField): string {
    if (field.custom) return 'sm:col-span-6';
    const halfWidthByModule: Record<string, Set<string>> = {
      'student-behaviours': new Set(['incident_date', 'incident_time', 'academic_year', 'term', 'recorded_by_email', 'recorded_by_title']),
      'student-projects': new Set(['subject', 'status']),
      'student-bus-attendance': new Set(['trip_direction', 'violation_type', 'bus_code', 'route_name']),
      'student-mental-health': new Set(['record_date', 'academic_year', 'concern_type', 'risk_level']),
    };
    if (halfWidthByModule[this.moduleId]?.has(field.key)) return 'sm:col-span-3';
    if (this.isCompactBehaviourField(field)) return 'sm:col-span-2';
    return 'sm:col-span-6';
  }

  isCompactBehaviourField(field: RecordField): boolean {
    return this.moduleId === 'student-behaviours' && [
      'conduct_deduction_level', 'occurrence_count', 'behaviour_points_deducted',
    ].includes(field.key);
  }

  tableFields(): RecordField[] {
    return this.isBehaviourModule()
      ? this.fields().filter((field) => field.key !== 'incident_time')
      : this.fields();
  }

  load(page = this.page()): void {
    this.loading.set(true); this.error.set(null);
    this.service.list(
      this.moduleId,
      page,
      this.pageSize(),
      this.search,
      this.selectedYear || undefined,
      this.selectedTerm,
    ).subscribe({
      next: (result) => {
        this.records.set(result.items); this.page.set(result.page); this.totalCount.set(result.totalCount);
        this.totalPages.set(result.totalPages); this.loading.set(false);
      },
      error: () => { this.error.set(this.t('moduleRecords.loadError')); this.loading.set(false); },
    });
  }

  clearSearch(): void { this.search = ''; this.load(1); }
  onPeriodChange(): void {
    if (!this.selectedYear) this.selectedTerm = 'all';
    this.currentFilters = {
      ...this.currentFilters,
      year: this.selectedYear,
      semester: this.selectedTerm,
    };
    this.workspaceFilters.save(this.currentFilters);
    this.load(1);
  }
  formatYear(year: string): string { return year.replace('-', '–'); }
  termLabel(term: string): string {
    const key = `dashboard.period.term.${term}`;
    const translated = this.t(key);
    return translated === key ? term : translated;
  }
  changePageSize(event: Event): void { this.pageSize.set(Number((event.target as HTMLSelectElement).value)); this.load(1); }
  pageLabel(): string { return this.t('pagination.pageOf').replace('{page}', String(this.page())).replace('{pages}', String(this.totalPages())).replace('{total}', String(this.totalCount())); }
  fieldLabel(field: RecordField): string { return field.custom ? field.label! : this.t(`imports.templateFields.${field.key}`); }
  editFieldLabel(field: RecordField): string {
    if (field.custom) return field.label!;
    const manualLabelByKey: Record<string, string> = {
      incident_date: 'imports.manual.incidentDate',
      incident_time: 'imports.manual.incidentTime',
      behaviour_type: 'imports.manual.behaviourType',
      notes: 'imports.manual.notes',
      reporter_name: 'imports.manual.reporterName',
      recorded_by_email: 'imports.manual.reporterEmail',
      recorded_by_title: 'imports.manual.reporterTitle',
      decision: 'imports.manual.decision',
      conduct_deduction_level: 'imports.manual.conductDeductionLevel',
      occurrence_count: 'imports.manual.occurrenceCount',
      behaviour_points_deducted: 'imports.manual.pointsDeducted',
      term: 'imports.manual.term',
      project_name: 'imports.manual.projectName',
      subject: 'imports.manual.subject',
      status: 'imports.manual.status',
      teacher_notes: 'imports.manual.teacherNotes',
      attendance_date: 'imports.manual.attendanceDate',
      trip_direction: 'imports.manual.tripDirection',
      violation_type: 'imports.manual.violationType',
      bus_code: 'imports.manual.busCode',
      route_name: 'imports.manual.routeName',
      record_date: 'imports.manual.recordDate',
      academic_year: 'imports.manual.academicYear',
      concern_type: 'imports.manual.concernType',
      risk_level: 'imports.manual.riskLevel',
      visit_date: 'imports.manual.visitDate',
      symptoms: 'imports.manual.symptoms',
      diagnosis: 'imports.manual.diagnosis',
      classification: 'imports.manual.classification',
      treatment: 'imports.manual.treatment',
      medication: 'imports.manual.medication',
      notification_sent: 'imports.manual.notificationSent',
    };
    return this.t(manualLabelByKey[field.key] ?? `imports.templateFields.${field.key}`);
  }
  tableFieldLabel(field: RecordField): string {
    return this.isBehaviourModule() && field.key === 'incident_date'
      ? this.t('imports.templateFields.incident_datetime')
      : this.fieldLabel(field);
  }
  tableColumnCount(): number {
    return this.tableFields().length + 2 + (this.isBehaviourModule() ? 0 : 1) + (this.isHealthModule() ? 2 : 0);
  }
  isBehaviourModule(): boolean { return this.moduleId === 'student-behaviours'; }
  isHealthModule(): boolean { return this.moduleId === 'student-health-visits'; }
  formatDateTime(value: string): string { return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(value)); }
  isBadgeField(key: string): boolean { return ['behaviour_type', 'status', 'risk_level', 'violation_type'].includes(key); }

  syncHorizontalScroll(source: 'top' | 'table'): void {
    const sourceElement = source === 'top' ? this.topScroll?.nativeElement : this.tableScroll?.nativeElement;
    const targetElement = source === 'top' ? this.tableScroll?.nativeElement : this.topScroll?.nativeElement;
    if (sourceElement && targetElement && targetElement.scrollLeft !== sourceElement.scrollLeft) {
      targetElement.scrollLeft = sourceElement.scrollLeft;
    }
  }

  private updateTableScrollSize(): void {
    const table = this.recordsTable?.nativeElement;
    const viewport = this.tableScroll?.nativeElement;
    if (!table || !viewport) return;
    this.tableScrollWidth.set(table.scrollWidth);
    this.tableOverflows.set(table.scrollWidth > viewport.clientWidth + 1);
  }

  private ensureSelectedPeriodOptions(): void {
    if (this.selectedYear && !this.years().includes(this.selectedYear)) {
      this.years.update((years) => [this.selectedYear, ...years]);
    }
    if (this.selectedTerm !== 'all' && !this.terms().includes(this.selectedTerm)) {
      this.terms.update((terms) => [...terms, this.selectedTerm]);
    }
  }

  displayValue(key: string, value: string | null | undefined): string {
    if (!value) return '';
    if (key === 'visit_date') return this.formatDateTime(value);
    if (key === 'term') return this.termLabel(value);
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
    if (this.options()?.concerningBehaviourTypes.some((item) => item.toLocaleLowerCase() === normalized)) return 'bg-red-50 text-red-700';
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
    this.reloadEntryOptions();
  }

  designerChanged(): void {
    this.reloadCustomFields();
    this.reloadEntryOptions();
  }

  reloadCustomFields(): void {
    this.fieldsService.list(this.moduleId).subscribe({
      next: (fields) => this.customFields.set(fields),
      error: () => this.error.set(this.t('moduleRecords.optionsError')),
    });
  }

  private reloadEntryOptions(): void {
    this.manualService.options(true).subscribe({
      next: (options) => this.options.set(options),
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
    if (field.key === 'term') return ['hk1', 'hk2'];
    const options = this.options(); if (!options) return [];
    return field.key === 'behaviour_type' ? [...options.positiveBehaviourTypes, ...options.concerningBehaviourTypes]
      : field.key === 'subject' && this.moduleId === 'student-projects' ? options.projectSubjects
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
    if (key === 'occurrence_count') return 'number';
    if (key === 'recorded_by_email') return 'email';
    if (key === 'subject') return this.moduleId === 'student-projects' ? 'select' : 'text';
    if (['behaviour_type', 'status', 'academic_year', 'term', 'concern_type', 'risk_level', 'trip_direction', 'violation_type'].includes(key)) return 'select';
    return 'text';
  }
}
