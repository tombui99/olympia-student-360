import {
  Component,
  ElementRef,
  EventEmitter,
  HostListener,
  Input,
  Output,
  signal,
  ViewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LanguageService } from '../core/language/language.service';

@Component({
  selector: 'app-behaviour-type-select',
  imports: [FormsModule],
  template: `
    <div class="relative">
      <button
        #trigger
        class="flex min-h-12 w-full items-center justify-between gap-3 rounded-md border border-[#d8d0df] bg-white px-3 py-2.5 text-left text-sm outline-none transition hover:border-[#bdaec8] focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10"
        type="button"
        role="combobox"
        aria-haspopup="listbox"
        [attr.aria-expanded]="open()"
        [attr.aria-controls]="listboxId"
        (click)="toggle()"
        (keydown)="onTriggerKeydown($event)"
      >
        @if (value) {
          <span class="flex min-w-0 items-center gap-2">
            <span
              class="h-2.5 w-2.5 shrink-0 rounded-full"
              [class.bg-emerald-500]="isPositive(value)"
              [class.bg-rose-500]="!isPositive(value)"
            ></span>
            <span class="truncate font-medium text-[#32124f]" [title]="value">{{ value }}</span>
          </span>
        } @else {
          <span class="text-[#8b8193]">{{ placeholder || languageService.t('imports.manual.selectBehaviour') }}</span>
        }
        <svg class="h-4 w-4 shrink-0 text-[#6f6877] transition" [class.rotate-180]="open()" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
          <path d="m6 9 6 6 6-6" />
        </svg>
      </button>

      @if (open()) {
        <div
          class="absolute left-0 right-0 z-50 mt-2 overflow-hidden rounded-xl border border-[#ded6e4] bg-white shadow-[0_18px_45px_rgba(50,18,79,0.2)]"
        >
          <div class="border-b border-[#ebe6ef] bg-[#faf9fb] p-3">
            <div class="relative">
              <svg class="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-[#8b8193]" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
                <circle cx="11" cy="11" r="7" /><path d="m20 20-3.5-3.5" />
              </svg>
              <input
                #searchInput
                class="w-full rounded-md border border-[#ddd5e3] bg-white py-2.5 pl-9 pr-3 text-sm outline-none focus:border-[#6d3a91] focus:ring-4 focus:ring-[#6d3a91]/10"
                type="search"
                autocomplete="off"
                [placeholder]="languageService.t('imports.manual.searchBehaviour')"
                [ngModel]="query()"
                [ngModelOptions]="{ standalone: true }"
                (ngModelChange)="setQuery($event)"
                (keydown)="onSearchKeydown($event)"
              />
            </div>
          </div>

          <div [id]="listboxId" class="max-h-80 overflow-y-auto overscroll-contain p-2" role="listbox">
            @if (filteredPositive().length) {
              <div class="mb-2 overflow-hidden rounded-lg border border-emerald-100">
                <div class="sticky top-0 z-10 flex items-center justify-between bg-emerald-50 px-3 py-2 text-xs font-bold uppercase tracking-[0.08em] text-emerald-800">
                  <span class="flex items-center gap-2"><span class="h-2 w-2 rounded-full bg-emerald-500"></span>{{ languageService.t('imports.manual.positiveBehaviours') }}</span>
                  <span>{{ filteredPositive().length }}</span>
                </div>
                @for (option of filteredPositive(); track option) {
                  <button
                    class="flex w-full items-start gap-2 border-t border-emerald-50 px-3 py-2.5 text-left text-sm leading-5 text-[#403549] transition hover:bg-emerald-50/70"
                    [class.bg-emerald-50]="activeOption() === option || value === option"
                    type="button"
                    role="option"
                    [attr.aria-selected]="value === option"
                    [title]="option"
                    (mouseenter)="activeOption.set(option)"
                    (click)="select(option)"
                  >
                    <span class="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-emerald-500"></span>
                    <span class="min-w-0 flex-1 break-words">{{ option }}</span>
                    @if (value === option) { <span class="shrink-0 font-bold text-emerald-700">✓</span> }
                  </button>
                }
              </div>
            }

            @if (filteredConcerning().length) {
              <div class="overflow-hidden rounded-lg border border-rose-100">
                <div class="sticky top-0 z-10 flex items-center justify-between bg-rose-50 px-3 py-2 text-xs font-bold uppercase tracking-[0.08em] text-rose-800">
                  <span class="flex items-center gap-2"><span class="h-2 w-2 rounded-full bg-rose-500"></span>{{ languageService.t('imports.manual.concerningBehaviours') }}</span>
                  <span>{{ filteredConcerning().length }}</span>
                </div>
                @for (option of filteredConcerning(); track option) {
                  <button
                    class="flex w-full items-start gap-2 border-t border-rose-50 px-3 py-2.5 text-left text-sm leading-5 text-[#403549] transition hover:bg-rose-50/70"
                    [class.bg-rose-50]="activeOption() === option || value === option"
                    type="button"
                    role="option"
                    [attr.aria-selected]="value === option"
                    [title]="option"
                    (mouseenter)="activeOption.set(option)"
                    (click)="select(option)"
                  >
                    <span class="mt-1.5 h-2 w-2 shrink-0 rounded-full bg-rose-500"></span>
                    <span class="min-w-0 flex-1 break-words">{{ option }}</span>
                    @if (value === option) { <span class="shrink-0 font-bold text-rose-700">✓</span> }
                  </button>
                }
              </div>
            }

            @if (!filteredOptions().length) {
              <p class="px-3 py-8 text-center text-sm text-[#7c7284]">{{ languageService.t('imports.manual.noBehaviours') }}</p>
            }
          </div>
        </div>
      }
    </div>
  `,
})
export class BehaviourTypeSelectComponent {
  private static nextId = 0;

  @Input() positiveOptions: string[] = [];
  @Input() concerningOptions: string[] = [];
  @Input() value: string | null = '';
  @Input() placeholder = '';
  @Output() readonly valueChange = new EventEmitter<string>();

  @ViewChild('trigger') private trigger?: ElementRef<HTMLButtonElement>;
  @ViewChild('searchInput') private searchInput?: ElementRef<HTMLInputElement>;

  readonly open = signal(false);
  readonly query = signal('');
  readonly activeOption = signal<string | null>(null);
  readonly listboxId = `behaviour-type-listbox-${BehaviourTypeSelectComponent.nextId++}`;

  constructor(
    private readonly host: ElementRef<HTMLElement>,
    readonly languageService: LanguageService,
  ) {}

  @HostListener('document:mousedown', ['$event'])
  onDocumentMouseDown(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) this.close();
  }

  toggle(): void {
    this.open() ? this.close() : this.openMenu();
  }

  openMenu(): void {
    this.open.set(true);
    this.query.set('');
    this.activeOption.set(this.value || this.filteredOptions()[0] || null);
    queueMicrotask(() => this.searchInput?.nativeElement.focus());
  }

  close(focusTrigger = false): void {
    this.open.set(false);
    this.query.set('');
    if (focusTrigger) queueMicrotask(() => this.trigger?.nativeElement.focus());
  }

  setQuery(value: string): void {
    this.query.set(value);
    this.activeOption.set(this.filteredOptions()[0] || null);
  }

  select(option: string): void {
    this.value = option;
    this.valueChange.emit(option);
    this.close(true);
  }

  filteredPositive(): string[] {
    return this.filter(this.positiveOptions);
  }

  filteredConcerning(): string[] {
    return this.filter(this.concerningOptions);
  }

  filteredOptions(): string[] {
    return [...this.filteredPositive(), ...this.filteredConcerning()];
  }

  isPositive(option: string): boolean {
    return this.positiveOptions.some((item) => item.toLocaleLowerCase() === option.toLocaleLowerCase());
  }

  onTriggerKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' || event.key === ' ' || event.key === 'ArrowDown') {
      event.preventDefault();
      this.openMenu();
    }
  }

  onSearchKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.close(true);
      return;
    }
    const options = this.filteredOptions();
    if (!options.length) return;
    const currentIndex = Math.max(0, options.indexOf(this.activeOption() ?? ''));
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.activeOption.set(options[Math.min(currentIndex + 1, options.length - 1)]);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.activeOption.set(options[Math.max(currentIndex - 1, 0)]);
    } else if (event.key === 'Enter') {
      event.preventDefault();
      this.select(this.activeOption() || options[0]);
    }
  }

  private filter(options: string[]): string[] {
    const query = this.query().trim().toLocaleLowerCase();
    return query ? options.filter((option) => option.toLocaleLowerCase().includes(query)) : options;
  }
}
