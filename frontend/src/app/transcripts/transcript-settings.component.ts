import { Component, computed, DestroyRef, ElementRef, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { LanguageService } from '../core/language/language.service';
import { TranscriptPreferencesService } from './transcript-preferences.service';
import { TranscriptSubjectOption, TranscriptsService } from './transcripts.service';

@Component({
  selector: 'app-transcript-settings',
  template: `
    <button type="button" class="inline-flex items-center gap-2 rounded-lg border border-[#d8cede] bg-white px-4 py-3 text-sm font-semibold text-[#32124f] transition hover:bg-[#f3edf6]"
      aria-haspopup="dialog" (click)="open()">
      <svg class="h-4 w-4" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" aria-hidden="true">
        <path d="M3 6h7m4 0h7M3 12h11m4 0h3M3 18h3m4 0h11M10 3v6m4 0v6m-8 0v6" />
      </svg>
      {{ t('transcript.settings.title') }}
      @if (preferences.excludedSubjectCodes().length) {
        <span class="rounded-full bg-[#eee5f2] px-2 py-0.5 text-xs">{{ preferences.excludedSubjectCodes().length }}</span>
      }
    </button>

    <dialog #settingsDialog class="no-print m-auto max-h-[85vh] w-[calc(100%_-_2rem)] max-w-2xl overflow-hidden rounded-xl border-0 bg-white p-0 text-[#35293b] shadow-[0_28px_90px_rgba(36,24,47,0.3)]"
      aria-labelledby="transcript-settings-title" aria-describedby="transcript-settings-description"
      (cancel)="$event.preventDefault(); close()" (click)="closeOnBackdrop($event)">
      <div class="flex max-h-[85vh] flex-col">
        <header class="flex items-start justify-between gap-4 border-b border-[#e8e1eb] px-6 py-5">
          <div>
            <h2 id="transcript-settings-title" class="text-xl font-semibold text-[#32124f]">{{ t('transcript.settings.title') }}</h2>
            <p id="transcript-settings-description" class="mt-2 text-sm leading-6 text-[#6f6877]">{{ t('transcript.settings.description') }}</p>
          </div>
          <button type="button" class="grid h-9 w-9 shrink-0 place-items-center rounded-full border border-[#d8cede] text-xl text-[#32124f] hover:bg-[#f4eef7]"
            [attr.aria-label]="t('transcript.settings.close')" (click)="close()">×</button>
        </header>

        <div class="min-h-0 overflow-y-auto px-6 py-5" [attr.aria-busy]="loading()">
          <p class="rounded-lg bg-[#f6f1f8] px-4 py-3 text-xs leading-5 text-[#695d70]">{{ t('transcript.settings.scope') }}</p>
          @if (loading()) {
            <p class="py-12 text-center text-sm text-[#766d7d]" role="status">{{ t('transcript.settings.loading') }}</p>
          } @else if (error()) {
            <div class="py-8 text-center text-sm" role="alert">
              <p class="text-[#8b2727]">{{ t('transcript.settings.error') }}</p>
              <button type="button" class="mt-3 rounded-md border border-[#d8cede] px-4 py-2 font-semibold text-[#32124f]" (click)="loadSubjects()">{{ t('transcript.retry') }}</button>
            </div>
          } @else {
            <label class="mt-5 block text-sm font-semibold text-[#32124f]">
              {{ t('transcript.settings.search') }}
              <input type="search" class="mt-2 block w-full rounded-lg border border-[#d8cede] px-3 py-2.5 text-sm font-normal outline-none focus:border-[#6d3a91] focus:ring-2 focus:ring-[#6d3a91]/15"
                [value]="search()" (input)="search.set($any($event.target).value)" />
            </label>
            <fieldset class="mt-5">
              <legend class="mb-2 text-sm font-semibold text-[#32124f]">{{ t('transcript.settings.excludeLabel') }}</legend>
              <div class="divide-y divide-[#ece6ef] rounded-lg border border-[#ded6e3]">
                @for (subject of filteredOptions(); track subject.subjectCode) {
                  <label class="flex cursor-pointer items-center gap-3 px-4 py-3 text-sm hover:bg-[#faf8fb]">
                    <input type="checkbox" class="h-4 w-4 shrink-0 accent-[#6d3a91]"
                      [checked]="draftExclusions().includes(subject.subjectCode)" (change)="toggleSubject(subject.subjectCode)" />
                    <span class="flex-1">{{ subject.subject }}</span>
                    @if (draftExclusions().includes(subject.subjectCode)) {
                      <span class="text-xs font-semibold text-[#6d3a91]">{{ t('transcript.settings.hidden') }}</span>
                    }
                  </label>
                } @empty {
                  <p class="px-4 py-8 text-center text-sm text-[#766d7d]">{{ t('transcript.settings.noSubjects') }}</p>
                }
              </div>
            </fieldset>
          }
        </div>

        <footer class="flex flex-wrap items-center justify-between gap-3 border-t border-[#e8e1eb] bg-[#faf8fb] px-6 py-4">
          <div>
            <p class="text-xs text-[#746a7b]" role="status">{{ exclusionLabel() }}</p>
            <button type="button" class="mt-1 text-sm font-semibold text-[#6d3a91] hover:underline" (click)="draftExclusions.set([])">{{ t('transcript.settings.restoreAll') }}</button>
          </div>
          <div class="flex gap-3">
            <button type="button" class="rounded-lg border border-[#d8cede] px-4 py-2.5 text-sm font-semibold text-[#32124f]" (click)="close()">{{ t('workspace.cancel') }}</button>
            <button type="button" class="rounded-lg bg-[#32124f] px-4 py-2.5 text-sm font-semibold text-white disabled:opacity-40"
              [disabled]="loading() || error()" (click)="apply()">{{ t('transcript.settings.apply') }}</button>
          </div>
        </footer>
      </div>
    </dialog>
  `,
  styles: `dialog::backdrop { background: rgba(36, 24, 47, .45); }`,
})
export class TranscriptSettingsComponent {
  readonly preferences = inject(TranscriptPreferencesService);
  private readonly service = inject(TranscriptsService);
  private readonly language = inject(LanguageService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly dialog = viewChild.required<ElementRef<HTMLDialogElement>>('settingsDialog');
  private request?: Subscription;
  readonly options = signal<TranscriptSubjectOption[]>([]);
  readonly loading = signal(false);
  readonly error = signal(false);
  readonly search = signal('');
  readonly draftExclusions = signal<string[]>([]);
  readonly filteredOptions = computed(() => {
    const search = this.search().trim().toLocaleLowerCase();
    const excluded = new Set(this.draftExclusions());
    return this.options()
      .filter((subject) => subject.subject.toLocaleLowerCase().includes(search))
      .sort((left, right) => Number(excluded.has(right.subjectCode)) - Number(excluded.has(left.subjectCode)));
  });

  open(): void {
    this.draftExclusions.set([...this.preferences.excludedSubjectCodes()]);
    this.search.set('');
    this.dialog().nativeElement.showModal();
    this.loadSubjects();
  }

  loadSubjects(): void {
    this.request?.unsubscribe();
    this.loading.set(true);
    this.error.set(false);
    this.request = this.service.getOverall(undefined, 1).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (record) => {
        const options = record.subjectOptions;
        const knownCodes = new Set(options.map((subject) => subject.subjectCode));
        // Keep saved exclusions editable even when a subject no longer has results.
        this.options.set([...options, ...this.draftExclusions().filter((code) => !knownCodes.has(code))
          .map((subjectCode) => ({ subjectCode, subject: subjectCode }))]);
        this.loading.set(false);
      },
      error: () => { this.loading.set(false); this.error.set(true); },
    });
  }

  toggleSubject(code: string): void {
    this.draftExclusions.update((codes) => codes.includes(code)
      ? codes.filter((item) => item !== code) : [...codes, code]);
  }

  apply(): void {
    this.preferences.saveExcludedSubjects(this.draftExclusions());
    this.close();
  }

  close(): void {
    this.request?.unsubscribe();
    this.dialog().nativeElement.close();
  }

  closeOnBackdrop(event: MouseEvent): void {
    if (event.target === this.dialog().nativeElement) this.close();
  }

  exclusionLabel(): string {
    return this.t('transcript.settings.exclusionCount').replace('{count}', String(this.draftExclusions().length));
  }

  t(key: string): string { return this.language.t(key); }
}
