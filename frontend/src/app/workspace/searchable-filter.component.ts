import { NgFor, NgIf } from '@angular/common';
import {
  Component,
  EventEmitter,
  HostListener,
  Input,
  OnChanges,
  OnDestroy,
  Output,
  SimpleChanges,
} from '@angular/core';
import { WorkspaceFilterSearchOption } from './workspace-filters.service';

@Component({
  selector: 'app-searchable-filter',
  imports: [NgFor, NgIf],
  template: `
    <div class="relative min-w-0">
      <span class="filter-label">{{ label }}</span>
      <div class="relative">
        <svg
          class="pointer-events-none absolute left-2.5 top-1/2 h-3.5 w-3.5 -translate-y-1/2 text-[#8e8495]"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          stroke-width="2"
          aria-hidden="true"
        >
          <circle cx="11" cy="11" r="7"></circle>
          <path d="m20 20-3.5-3.5"></path>
        </svg>
        <input
          class="filter-search"
          type="text"
          role="combobox"
          autocomplete="off"
          [value]="inputValue"
          [placeholder]="placeholder"
          [attr.aria-label]="label"
          [attr.aria-expanded]="open"
          [attr.aria-controls]="listboxId"
          (focus)="onFocus()"
          (input)="onInput($event)"
          (keydown)="onKeydown($event)"
        />
        <button
          *ngIf="selectedValue || inputValue"
          class="absolute right-1.5 top-1/2 grid h-6 w-6 -translate-y-1/2 place-items-center rounded text-[#7c7284] hover:bg-[#f1edf4] hover:text-[#32124f]"
          type="button"
          [attr.aria-label]="clearLabel"
          (click)="clear()"
        >
          <svg class="h-3.5 w-3.5" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" aria-hidden="true">
            <path d="m6 6 12 12M18 6 6 18"></path>
          </svg>
        </button>
      </div>

      <div
        *ngIf="open"
        class="absolute left-0 right-0 z-40 mt-1 overflow-hidden rounded-md border border-[#ded6e4] bg-white shadow-[0_14px_36px_rgba(50,18,79,0.18)]"
      >
        <div *ngIf="loading" class="px-3 py-2.5 text-xs text-[#7c7284]">{{ loadingLabel }}</div>
        <div *ngIf="!loading && !inputValue.trim()" class="px-3 py-2.5 text-xs text-[#7c7284]">
          {{ typeToSearchLabel }}
        </div>
        <div *ngIf="!loading && inputValue.trim() && !options.length" class="px-3 py-2.5 text-xs text-[#7c7284]">
          {{ noResultsLabel }}
        </div>
        <div *ngIf="!loading && options.length" [id]="listboxId" role="listbox" class="max-h-56 overflow-y-auto py-1">
          <button
            *ngFor="let option of options; let index = index; trackBy: trackOption"
            class="block w-full px-3 py-2 text-left text-xs text-[#32124f] transition hover:bg-[#fff8e1]"
            [class.active-option]="index === activeIndex"
            type="button"
            role="option"
            [attr.aria-selected]="option.value === selectedValue"
            (mousedown)="$event.preventDefault()"
            (click)="choose(option)"
          >
            {{ option.label }}
          </button>
        </div>
      </div>
    </div>
  `,
  styles: `
    :host {
      display: block;
      min-width: 0;
    }

    .filter-label {
      display: block;
      margin-bottom: 0.35rem;
      font-size: 0.7rem;
      font-weight: 700;
      letter-spacing: 0.08em;
      line-height: 1rem;
      text-transform: uppercase;
      color: #6f6877;
    }

    .filter-search {
      width: 100%;
      min-width: 0;
      border-radius: 0.375rem;
      border: 1px solid #e1dbe6;
      background: #ffffff;
      padding: 0.5rem 2.1rem 0.5rem 2rem;
      font-size: 0.8rem;
      color: #32124f;
      outline: none;
    }

    .filter-search:focus {
      border-color: #6d3a91;
    }

    .active-option {
      background: #f1edf4;
    }
  `,
})
export class SearchableFilterComponent implements OnChanges, OnDestroy {
  private static nextId = 0;

  @Input({ required: true }) label = '';
  @Input() placeholder = '';
  @Input() selectedValue = '';
  @Input() selectedLabel = '';
  @Input() options: WorkspaceFilterSearchOption[] = [];
  @Input() loading = false;
  @Input() loadingLabel = 'Searching...';
  @Input() typeToSearchLabel = 'Type to search';
  @Input() noResultsLabel = 'No matches found';
  @Input() clearLabel = 'Clear filter';

  @Output() readonly searchQuery = new EventEmitter<string>();
  @Output() readonly selectionChange = new EventEmitter<WorkspaceFilterSearchOption | null>();

  readonly listboxId = `lazy-filter-options-${SearchableFilterComponent.nextId++}`;
  inputValue = '';
  open = false;
  activeIndex = -1;
  private searchTimer?: ReturnType<typeof setTimeout>;

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['selectedValue'] || changes['selectedLabel']) {
      this.inputValue = this.selectedValue ? this.selectedLabel || this.selectedValue : '';
    }
    if (changes['options']) {
      this.activeIndex = this.options.length ? 0 : -1;
    }
  }

  ngOnDestroy(): void {
    if (this.searchTimer) clearTimeout(this.searchTimer);
  }

  @HostListener('click', ['$event'])
  stopClick(event: MouseEvent): void {
    event.stopPropagation();
  }

  @HostListener('document:click')
  close(): void {
    this.open = false;
    this.inputValue = this.selectedValue ? this.selectedLabel || this.selectedValue : '';
  }

  onFocus(): void {
    this.open = true;
  }

  onInput(event: Event): void {
    this.inputValue = (event.target as HTMLInputElement).value;
    this.open = true;
    this.activeIndex = -1;
    if (this.searchTimer) clearTimeout(this.searchTimer);
    const query = this.inputValue.trim();
    if (!query) {
      this.searchQuery.emit('');
      return;
    }
    this.searchTimer = setTimeout(() => this.searchQuery.emit(query), 250);
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      this.close();
      return;
    }
    if (!this.options.length) return;
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.activeIndex = Math.min(this.activeIndex + 1, this.options.length - 1);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.activeIndex = Math.max(this.activeIndex - 1, 0);
    } else if (event.key === 'Enter' && this.activeIndex >= 0) {
      event.preventDefault();
      this.choose(this.options[this.activeIndex]);
    }
  }

  choose(option: WorkspaceFilterSearchOption): void {
    this.inputValue = option.label;
    this.open = false;
    this.selectionChange.emit(option);
  }

  clear(): void {
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.inputValue = '';
    this.open = false;
    this.searchQuery.emit('');
    this.selectionChange.emit(null);
  }

  trackOption(_index: number, option: WorkspaceFilterSearchOption): string {
    return option.value;
  }
}
