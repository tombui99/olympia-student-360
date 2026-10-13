import { Component, ElementRef, EventEmitter, HostListener, inject, Input, Output, signal } from '@angular/core';
import { LanguageService } from '../core/language/language.service';

export interface SearchableSelectOption {
  value: string | number;
  label: string;
  detail?: string;
}

let nextSelectId = 0;

@Component({
  selector: 'app-searchable-multi-select',
  template: `
    <div class="relative min-w-0">
      <label class="text-sm font-semibold text-[#32124f]" [for]="inputId">{{ label }}</label>
      <div class="relative mt-1">
        <input [id]="inputId" type="text" inputmode="search" autocomplete="off" role="combobox"
          aria-autocomplete="list" [attr.aria-controls]="listId" [attr.aria-expanded]="open()"
          [attr.aria-activedescendant]="open() && activeIndex() >= 0 ? listId + '-option-' + activeIndex() : null"
          [attr.aria-label]="searchPlaceholder" [placeholder]="searchPlaceholder"
          class="h-11 w-full rounded-md border border-[#d8d0df] bg-white pl-9 pr-10 text-sm font-normal text-[#32124f] outline-none focus:border-[#6d3a91] focus:ring-2 focus:ring-[#eee5f3]"
          [value]="search()" (focus)="show()" (input)="changeSearch($event)" (keydown)="onKeydown($event)" />
        <svg class="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[#817589]" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><circle cx="11" cy="11" r="7"/><path d="m20 20-3.5-3.5"/></svg>
        @if (search()) {
          <button type="button" class="absolute right-2 top-1/2 -translate-y-1/2 rounded px-1.5 py-1 text-lg leading-none text-[#6f6877] hover:bg-[#f5f0f7]" [attr.aria-label]="t('reports.learningReview.clearSearch')" (click)="clearSearch()">×</button>
        }
      </div>
      @if (open()) {
        <div [id]="listId" role="listbox" aria-multiselectable="true"
          class="absolute z-40 mt-1 w-full overflow-hidden rounded-md border border-[#d8d0df] bg-white shadow-[0_14px_34px_rgba(50,18,79,0.18)]">
          <div class="max-h-64 overflow-y-auto py-1">
            @if (loading) {
              <p class="flex items-center gap-2 px-3 py-3 text-sm text-[#817589]" role="status">
                <span class="h-4 w-4 animate-spin rounded-full border-2 border-[#e6dfea] border-t-[#6d3a91]"></span>
                {{ t('reports.loading') }}
              </p>
            } @else {
              @for (option of visibleOptions(); track option.value; let index = $index) {
                <button type="button" role="option" [id]="listId + '-option-' + index" [attr.aria-selected]="isSelected(option.value)"
                  class="flex w-full items-start gap-3 px-3 py-2 text-left text-sm text-[#32124f] hover:bg-[#f7f3fa]"
                  [style.backgroundColor]="index === activeIndex() ? '#f5f0f7' : null"
                  (mousedown)="$event.preventDefault()" (click)="toggle(option.value)">
                  <span class="mt-0.5 flex h-4 w-4 shrink-0 items-center justify-center rounded border border-[#9b7bb1] text-xs" [style.backgroundColor]="isSelected(option.value) ? '#32124f' : null" [style.color]="isSelected(option.value) ? 'white' : null">@if (isSelected(option.value)) { ✓ }</span>
                  <span class="min-w-0"><span class="block break-words">{{ option.label }}</span>@if (option.detail) { <span class="block text-xs text-[#817589]">{{ option.detail }}</span> }</span>
                </button>
              } @empty {
                <p class="px-3 py-3 text-sm text-[#817589]">{{ t('reports.learningReview.noOptions') }}</p>
              }
            }
          </div>
          @if (!loading && matchCount() > visibleLimit) {
            <p class="border-t border-[#eee8f1] px-3 py-2 text-xs text-[#817589]">{{ t('reports.learningReview.narrowSearch') }}</p>
          }
        </div>
      }
      @if (selected.length) {
        <div class="mt-2 flex items-center justify-between gap-2 text-xs text-[#6f6877]">
          <span>{{ t('reports.learningReview.selectedCount') }}: {{ selected.length }}</span>
          <button type="button" class="shrink-0 font-semibold text-[#5f3182] underline-offset-2 hover:underline" (click)="clearSelection()">{{ t('reports.learningReview.clearSelection') }}</button>
        </div>
        <div class="mt-1 flex max-h-28 flex-wrap content-start gap-1.5 overflow-y-auto pr-1" [attr.aria-label]="t('reports.learningReview.selectedCount')">
          @for (value of selected; track value) {
            <span class="inline-flex max-w-full items-start gap-1 rounded-md border border-[#dfd2e8] bg-[#f5eff8] px-2 py-1 text-xs text-[#32124f]">
              <span class="min-w-0 break-words">{{ optionLabel(value) }}</span>
              <button type="button" class="shrink-0 rounded px-0.5 font-bold leading-tight text-[#6f4c83] hover:bg-[#e9ddef]" [attr.aria-label]="t('reports.learningReview.removeSelection') + ': ' + optionLabel(value)" (click)="remove(value)">×</button>
            </span>
          }
        </div>
      } @else {
        <p class="mt-1 text-xs font-normal text-[#817589]">{{ emptySelectionLabel }}</p>
      }
      @if (hint) { <p class="mt-1 text-xs font-normal text-[#817589]">{{ hint }}</p> }
      @if (loading && !open()) { <p class="mt-1 text-xs text-[#817589]" role="status">{{ t('reports.loading') }}</p> }
    </div>
  `,
})
export class SearchableMultiSelectComponent {
  @Input({ required: true }) label = '';
  @Input({ required: true }) searchPlaceholder = '';
  @Input({ required: true }) emptySelectionLabel = '';
  @Input() hint = '';
  @Input() loading = false;
  @Input() options: SearchableSelectOption[] = [];
  @Input() selected: Array<string | number> = [];
  @Output() selectedChange = new EventEmitter<Array<string | number>>();

  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly language = inject(LanguageService);
  readonly inputId = `learning-select-${++nextSelectId}`;
  readonly listId = `${this.inputId}-options`;
  readonly visibleLimit = 50;
  readonly open = signal(false);
  readonly search = signal('');
  readonly activeIndex = signal(-1);

  t(key: string): string { return this.language.t(key); }
  show(): void { this.open.set(true); this.activeIndex.set(-1); }
  changeSearch(event: Event): void {
    this.search.set((event.target as HTMLInputElement).value);
    this.show();
  }
  visibleOptions(): SearchableSelectOption[] { return this.matches().slice(0, this.visibleLimit); }
  matchCount(): number { return this.matches().length; }
  isSelected(value: string | number): boolean { return this.selected.includes(value); }
  toggle(value: string | number): void {
    this.selectedChange.emit(this.isSelected(value)
      ? this.selected.filter(item => item !== value)
      : [...this.selected, value]);
    this.open.set(true);
  }
  remove(value: string | number): void {
    this.selectedChange.emit(this.selected.filter(item => item !== value));
  }
  clearSelection(): void { this.selectedChange.emit([]); this.open.set(false); }
  clearSearch(): void { this.search.set(''); this.show(); }
  optionLabel(value: string | number): string {
    return this.options.find(option => option.value === value)?.label ?? String(value);
  }
  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') { this.open.set(false); return; }
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      this.open.set(true);
      const last = this.visibleOptions().length - 1;
      this.activeIndex.set(last < 0 ? -1 : event.key === 'ArrowUp' && this.activeIndex() < 0
        ? last : Math.max(0, Math.min(last,
          this.activeIndex() + (event.key === 'ArrowDown' ? 1 : -1))));
    } else if (event.key === 'Enter') {
      event.preventDefault();
      if (!this.open()) { this.show(); return; }
      const option = this.visibleOptions()[Math.max(0, this.activeIndex())];
      if (option) this.toggle(option.value);
    }
  }

  @HostListener('document:pointerdown', ['$event'])
  onOutsidePointer(event: PointerEvent): void {
    if (!this.host.nativeElement.contains(event.target as Node)) this.open.set(false);
  }
  @HostListener('document:focusin', ['$event'])
  onOutsideFocus(event: FocusEvent): void {
    if (!this.host.nativeElement.contains(event.target as Node)) this.open.set(false);
  }

  private matches(): SearchableSelectOption[] {
    const term = this.fold(this.search().trim());
    return term ? this.options.filter(option => this.fold(option.label).includes(term) ||
      this.fold(option.detail ?? '').includes(term)) : this.options;
  }
  private fold(value: string): string {
    return value.toLocaleLowerCase().replace(/đ/g, 'd').normalize('NFD').replace(/[\u0300-\u036f]/g, '');
  }
}
