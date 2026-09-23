import { Component, EventEmitter, HostListener, inject, Input, OnInit, Output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { LanguageService } from '../core/language/language.service';
import { FieldTypeIconComponent } from './field-type-icon.component';
import {
  CUSTOM_FIELD_TYPES,
  CustomFieldType,
  ImportModuleField,
  ImportModuleFieldsService,
} from './import-module-fields.service';

@Component({
  selector: 'app-field-designer-sheet',
  imports: [FormsModule, FieldTypeIconComponent],
  styles: `
    :host { position: fixed; inset: 0; z-index: 70; }
    .sheet-backdrop { animation: fade-in 160ms ease-out; }
    .sheet-panel { animation: slide-in 220ms ease-out; }
    @keyframes fade-in { from { opacity: 0; } to { opacity: 1; } }
    @keyframes slide-in { from { transform: translateX(100%); } to { transform: translateX(0); } }
  `,
  template: `
    <div class="sheet-backdrop absolute inset-0 bg-[#1d1025]/35" (click)="close.emit()" aria-hidden="true"></div>
    <aside
      class="sheet-panel absolute inset-y-0 right-0 flex w-full max-w-xl flex-col bg-white shadow-[-18px_0_45px_rgba(50,18,79,0.18)]"
      role="dialog"
      aria-modal="true"
      [attr.aria-label]="languageService.t('imports.designer.title')"
    >
      <header class="flex items-start justify-between gap-4 border-b border-[#ebe6ef] px-6 py-5">
        <div>
          <p class="text-xs font-semibold uppercase tracking-[0.18em] text-[#8a6811]">
            {{ languageService.t('imports.designer.eyebrow') }}
          </p>
          <h2 class="mt-2 text-2xl font-semibold text-[#32124f]">{{ languageService.t(moduleTitleKey) }}</h2>
          <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('imports.designer.hint') }}</p>
        </div>
        <button
          class="grid h-10 w-10 shrink-0 place-items-center rounded-full border border-[#ded6e4] text-xl text-[#5d5265] hover:bg-[#f7f3fa]"
          type="button"
          (click)="close.emit()"
          [attr.aria-label]="languageService.t('imports.manual.close')"
        >×</button>
      </header>

      <div class="flex-1 space-y-6 overflow-y-auto px-6 py-6" (scroll)="closeFieldTypeMenu()">
        <!-- Existing custom fields -->
        <section>
          <h3 class="text-sm font-semibold uppercase tracking-[0.12em] text-[#7c7284]">
            {{ languageService.t('imports.designer.currentFields') }}
          </h3>
          @if (loading()) {
            <p class="mt-3 rounded-md bg-[#faf9f6] p-4 text-sm text-[#6f6877]">{{ languageService.t('imports.manual.loading') }}</p>
          } @else if (fields().length === 0) {
            <p class="mt-3 rounded-md border border-dashed border-[#d8d0df] bg-[#faf9f6] p-4 text-sm text-[#7c7284]">
              {{ languageService.t('imports.designer.empty') }}
            </p>
          } @else {
            <ul class="mt-3 space-y-2">
              @for (field of fields(); track field.id) {
                <li class="flex items-start justify-between gap-3 rounded-md border border-[#ebe6ef] p-3">
                  <div class="min-w-0">
                    <p class="text-sm font-semibold text-[#32124f]">
                      {{ field.label }}
                      @if (field.required) { <span class="text-[#a9485d]">*</span> }
                    </p>
                    <p class="mt-0.5 text-xs text-[#7c7284]">
                      {{ languageService.t('imports.designer.type.' + field.fieldType) }}
                      · <code class="text-[#8a6811]">{{ field.fieldKey }}</code>
                      @if (field.fieldType === 'select' && field.options.length) {
                        · {{ field.options.join(', ') }}
                      }
                    </p>
                  </div>
                  <div class="flex shrink-0 items-center gap-1">
                    <button class="rounded-md border border-[#d8d0df] px-2.5 py-1 text-xs font-semibold text-[#32124f] hover:bg-[#fff8e1]" type="button" (click)="startEdit(field)">
                      {{ languageService.t('imports.designer.edit') }}
                    </button>
                    <button class="rounded-md border border-red-200 px-2.5 py-1 text-xs font-semibold text-red-700 hover:bg-red-50" type="button" (click)="requestDelete(field)">
                      {{ languageService.t('imports.designer.delete') }}
                    </button>
                  </div>
                </li>
              }
            </ul>
          }
        </section>

        <!-- Add / edit form -->
        <section class="rounded-lg border border-[#ebe6ef] bg-[#faf9f6] p-5">
          <h3 class="text-sm font-semibold text-[#32124f]">
            {{ languageService.t(editingId() ? 'imports.designer.editField' : 'imports.designer.addField') }}
          </h3>
          <div class="mt-4 space-y-4">
            <label class="block">
              <span class="text-sm font-medium text-[#34243d]">{{ languageService.t('imports.designer.label') }}</span>
              <input class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-3 py-2.5 text-sm outline-none" name="label" [(ngModel)]="label" maxlength="150" />
            </label>
            <div class="grid gap-4 sm:grid-cols-2">
              <div class="block">
                <span class="text-sm font-medium text-[#34243d]">{{ languageService.t('imports.designer.fieldType') }}</span>
                <button
                  class="mt-2 flex w-full items-center gap-3 rounded-md border border-[#d8d0df] bg-white px-3 py-2.5 text-left text-sm font-medium text-[#332b38] transition hover:bg-[#f7f5f8] focus:border-[#6d3a91] focus:outline-none focus:ring-2 focus:ring-[#6d3a91]/15"
                  type="button"
                  aria-haspopup="listbox"
                  [attr.aria-expanded]="fieldTypeMenuOpen()"
                  (click)="toggleFieldTypeMenu($event)"
                >
                  <app-field-type-icon class="shrink-0 text-[#646970]" [type]="fieldType" />
                  <span class="min-w-0 flex-1 truncate">{{ languageService.t('imports.designer.type.' + fieldType) }}</span>
                  <svg class="h-4 w-4 shrink-0 text-[#77717b] transition" [class.rotate-180]="fieldTypeMenuOpen()" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true"><path fill-rule="evenodd" d="M5.2 7.4a.75.75 0 0 1 1.05-.1L10 10.4l3.75-3.1a.75.75 0 1 1 .95 1.16l-4.23 3.5a.75.75 0 0 1-.95 0l-4.23-3.5a.75.75 0 0 1-.1-1.06Z" clip-rule="evenodd" /></svg>
                </button>
                @if (fieldTypeMenuOpen()) {
                  <button class="fixed inset-0 z-10 cursor-default" type="button" tabindex="-1" [attr.aria-label]="languageService.t('imports.designer.closeFieldTypeMenu')" (click)="closeFieldTypeMenu()"></button>
                  <div
                    class="fixed z-20 overflow-y-auto overscroll-contain rounded-lg border border-[#dedbe1] bg-white py-1.5 shadow-[0_10px_30px_rgba(32,25,38,.22)]"
                    role="listbox"
                    [attr.aria-label]="languageService.t('imports.designer.fieldType')"
                    [style.left.px]="fieldTypeMenuPosition().left"
                    [style.top.px]="fieldTypeMenuPosition().top"
                    [style.bottom.px]="fieldTypeMenuPosition().bottom"
                    [style.width.px]="fieldTypeMenuPosition().width"
                    [style.max-height.px]="fieldTypeMenuPosition().maxHeight"
                  >
                    @for (type of fieldTypes; track type) {
                      <button
                        class="flex w-full items-center gap-3 px-4 py-2.5 text-left text-[13px] font-medium leading-5 text-[#29252c] transition hover:bg-[#f1f3f8] focus:bg-[#f1f3f8] focus:outline-none"
                        type="button"
                        role="option"
                        [class.bg-[#eaf1ff]]="fieldType === type"
                        [attr.aria-selected]="fieldType === type"
                        (click)="selectFieldType(type)"
                      >
                        <app-field-type-icon class="shrink-0 text-[#646970]" [type]="type" />
                        <span>{{ languageService.t('imports.designer.type.' + type) }}</span>
                      </button>
                    }
                  </div>
                }
              </div>
              <label class="flex items-center gap-2 pt-8 text-sm text-[#34243d]">
                <input type="checkbox" name="required" [(ngModel)]="required" />
                {{ languageService.t('imports.designer.required') }}
              </label>
            </div>
            @if (fieldType === 'select') {
              <label class="block">
                <span class="text-sm font-medium text-[#34243d]">{{ languageService.t('imports.designer.options') }}</span>
                <textarea class="mt-2 min-h-24 w-full resize-y rounded-md border border-[#d8d0df] bg-white px-3 py-2.5 text-sm outline-none" name="optionsText" [(ngModel)]="optionsText" [placeholder]="languageService.t('imports.designer.optionsHint')"></textarea>
              </label>
            }
            @if (formError()) {
              <p class="rounded-md bg-red-50 p-3 text-sm text-red-700">{{ formError() }}</p>
            }
            <div class="flex items-center justify-end gap-2">
              @if (editingId()) {
                <button class="rounded-md border border-[#d8d0df] px-4 py-2 text-sm font-semibold text-[#32124f] hover:bg-white" type="button" (click)="resetForm()">
                  {{ languageService.t('imports.designer.cancelEdit') }}
                </button>
              }
              <button
                class="rounded-md bg-[#32124f] px-4 py-2 text-sm font-semibold text-white transition hover:bg-[#4b2070] disabled:cursor-not-allowed disabled:opacity-45"
                type="button"
                [disabled]="saving() || !label.trim()"
                (click)="saveDraft()"
              >
                {{ languageService.t(editingId() ? 'imports.designer.saveField' : 'imports.designer.addField') }}
              </button>
            </div>
          </div>
        </section>
      </div>

      <footer class="flex items-center justify-end border-t border-[#ebe6ef] bg-[#faf9f6] px-6 py-4">
        <button class="rounded-md bg-[#32124f] px-5 py-2.5 text-sm font-semibold text-white hover:bg-[#4b2070]" type="button" (click)="close.emit()">
          {{ languageService.t('imports.designer.done') }}
        </button>
      </footer>
    </aside>

    @if (pendingDelete(); as field) {
      <div class="absolute inset-0 z-20 grid place-items-center p-4">
        <button class="absolute inset-0 bg-[#24182f]/45" type="button" [attr.aria-label]="languageService.t('imports.designer.deleteCancel')" (click)="cancelDelete()"></button>
        <section class="relative w-full max-w-md overflow-hidden rounded-xl bg-white shadow-[0_24px_80px_rgba(36,24,47,0.3)]" role="alertdialog" aria-modal="true" [attr.aria-label]="languageService.t('imports.designer.deleteConfirmTitle')">
          <div class="p-6">
            <div class="grid h-11 w-11 place-items-center rounded-full bg-red-50 text-red-700">
              <svg class="h-5 w-5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 6h18"/><path d="M8 6V4h8v2"/><path d="M19 6l-1 14H6L5 6"/><path d="M10 11v5M14 11v5"/></svg>
            </div>
            <h3 class="mt-4 text-xl font-semibold text-[#32124f]">{{ languageService.t('imports.designer.deleteConfirmTitle') }}</h3>
            <p class="mt-2 text-sm leading-6 text-[#6f6877]">{{ deleteConfirmationText(field.label) }}</p>
            @if (deleteError()) {
              <p class="mt-4 rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">{{ deleteError() }}</p>
            }
          </div>
          <div class="flex justify-end gap-3 border-t border-[#ebe6ef] bg-[#faf9f6] px-6 py-4">
            <button class="rounded-md border border-[#d8d0df] px-4 py-2.5 text-sm font-semibold text-[#32124f] disabled:opacity-50" type="button" [disabled]="deleting()" (click)="cancelDelete()">
              {{ languageService.t('imports.designer.deleteCancel') }}
            </button>
            <button class="rounded-md bg-red-700 px-4 py-2.5 text-sm font-semibold text-white transition hover:bg-red-800 disabled:cursor-not-allowed disabled:opacity-50" type="button" [disabled]="deleting()" (click)="confirmDelete()">
              {{ languageService.t(deleting() ? 'imports.designer.deleting' : 'imports.designer.deleteConfirm') }}
            </button>
          </div>
        </section>
      </div>
    }
  `,
})
export class FieldDesignerSheetComponent implements OnInit {
  @Input({ required: true }) moduleId!: string;
  @Input({ required: true }) moduleTitleKey!: string;
  @Output() readonly close = new EventEmitter<void>();
  @Output() readonly changed = new EventEmitter<void>();

  private readonly fieldsService = inject(ImportModuleFieldsService);
  readonly languageService = inject(LanguageService);

  readonly fieldTypes = CUSTOM_FIELD_TYPES;
  readonly fields = signal<ImportModuleField[]>([]);
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly formError = signal<string | null>(null);
  readonly editingId = signal<number | null>(null);
  readonly pendingDelete = signal<ImportModuleField | null>(null);
  readonly deleting = signal(false);
  readonly deleteError = signal<string | null>(null);
  readonly fieldTypeMenuOpen = signal(false);
  readonly fieldTypeMenuPosition = signal<{ left: number; top: number | null; bottom: number | null; width: number; maxHeight: number }>({
    left: 0,
    top: 0,
    bottom: null,
    width: 240,
    maxHeight: 320,
  });

  label = '';
  fieldType: CustomFieldType = 'text';
  required = false;
  optionsText = '';

  ngOnInit(): void {
    this.reload();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.fieldTypeMenuOpen()) this.closeFieldTypeMenu();
    else if (this.pendingDelete()) this.cancelDelete();
    else this.close.emit();
  }

  @HostListener('window:resize')
  closeFieldTypeMenuOnResize(): void {
    this.closeFieldTypeMenu();
  }

  toggleFieldTypeMenu(event: MouseEvent): void {
    if (this.fieldTypeMenuOpen()) {
      this.closeFieldTypeMenu();
      return;
    }
    const trigger = event.currentTarget as HTMLElement;
    const rect = trigger.getBoundingClientRect();
    const viewportWidth = window.innerWidth;
    const viewportHeight = window.innerHeight;
    const idealMenuHeight = 278;
    const gap = 6;
    const edge = 12;
    const width = Math.min(Math.max(240, rect.width), viewportWidth - edge * 2);
    const availableBelow = viewportHeight - rect.bottom - gap - edge;
    const availableAbove = rect.top - gap - edge;
    const openAbove = availableBelow < Math.min(idealMenuHeight, availableAbove);
    const left = Math.max(edge, Math.min(rect.left, viewportWidth - width - edge));
    this.fieldTypeMenuPosition.set(openAbove
      ? { left, top: null, bottom: viewportHeight - rect.top + gap, width, maxHeight: Math.max(160, availableAbove) }
      : { left, top: rect.bottom + gap, bottom: null, width, maxHeight: Math.max(160, availableBelow) });
    this.fieldTypeMenuOpen.set(true);
  }

  closeFieldTypeMenu(): void {
    this.fieldTypeMenuOpen.set(false);
  }

  selectFieldType(type: CustomFieldType): void {
    this.fieldType = type;
    this.closeFieldTypeMenu();
  }

  private reload(): void {
    this.loading.set(true);
    this.fieldsService.list(this.moduleId).subscribe({
      next: (fields) => {
        this.fields.set(fields);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  startEdit(field: ImportModuleField): void {
    this.editingId.set(field.id);
    this.label = field.label;
    this.fieldType = field.fieldType;
    this.required = field.required;
    this.optionsText = field.options.join('\n');
    this.formError.set(null);
  }

  resetForm(): void {
    this.editingId.set(null);
    this.label = '';
    this.fieldType = 'text';
    this.required = false;
    this.optionsText = '';
    this.formError.set(null);
  }

  saveDraft(): void {
    const options = this.optionsText.split(/\r?\n/).map((o) => o.trim()).filter(Boolean);
    if (this.fieldType === 'select' && options.length === 0) {
      this.formError.set(this.languageService.t('imports.designer.needOptions'));
      return;
    }
    const payload = {
      label: this.label.trim(),
      fieldType: this.fieldType,
      required: this.required,
      options,
      sortOrder: this.editingId() ? this.currentSortOrder() : this.fields().length,
    };

    this.saving.set(true);
    this.formError.set(null);
    const request = this.editingId()
      ? this.fieldsService.update(this.moduleId, this.editingId()!, payload)
      : this.fieldsService.create(this.moduleId, payload);

    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.resetForm();
        this.reload();
        this.changed.emit();
      },
      error: (error) => this.formError.set(error?.error?.message || this.languageService.t('imports.designer.saveError')),
    });
  }

  requestDelete(field: ImportModuleField): void {
    this.pendingDelete.set(field);
    this.deleteError.set(null);
  }

  cancelDelete(): void {
    if (this.deleting()) return;
    this.pendingDelete.set(null);
    this.deleteError.set(null);
  }

  deleteConfirmationText(fieldLabel: string): string {
    return this.languageService.t('imports.designer.deleteConfirmBody').replace('{field}', fieldLabel);
  }

  confirmDelete(): void {
    const field = this.pendingDelete();
    if (!field || this.deleting()) return;
    this.deleting.set(true);
    this.deleteError.set(null);
    this.fieldsService.remove(this.moduleId, field.id).pipe(finalize(() => this.deleting.set(false))).subscribe({
      next: () => {
        if (this.editingId() === field.id) this.resetForm();
        this.pendingDelete.set(null);
        this.reload();
        this.changed.emit();
      },
      error: () => this.deleteError.set(this.languageService.t('imports.designer.deleteError')),
    });
  }

  private currentSortOrder(): number {
    return this.fields().find((f) => f.id === this.editingId())?.sortOrder ?? this.fields().length;
  }
}
