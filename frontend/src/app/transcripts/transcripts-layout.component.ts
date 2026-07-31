import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { LanguageService } from '../core/language/language.service';

@Component({
  selector: 'app-transcripts-layout',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  template: `
    <section class="mx-auto max-w-[1500px]">
      <nav class="no-print mb-6 inline-flex max-w-full gap-1 overflow-x-auto rounded-lg border border-[#ded6e3] bg-white p-1 shadow-[0_6px_20px_rgba(50,18,79,0.04)]"
        [attr.aria-label]="t('transcript.workspace')">
        <a routerLink="/transcripts" [routerLinkActiveOptions]="{ exact: true }" routerLinkActive="transcript-tab-active"
          class="shrink-0 rounded-md px-5 py-2.5 text-sm font-semibold text-[#5f5268] transition hover:bg-[#f3edf6] hover:text-[#32124f]">
          {{ t('transcript.summaryNav') }}
        </a>
        <a routerLink="/transcripts/student" routerLinkActive="transcript-tab-active"
          class="shrink-0 rounded-md px-5 py-2.5 text-sm font-semibold text-[#5f5268] transition hover:bg-[#f3edf6] hover:text-[#32124f]">
          {{ t('transcript.studentNav') }}
        </a>
      </nav>
      <main class="min-w-0">
        <router-outlet />
      </main>
    </section>
  `,
  styles: `
    .transcript-tab-active,
    .transcript-tab-active:hover {
      background: #32124f;
      color: #ffffff;
    }
  `,
})
export class TranscriptsLayoutComponent {
  readonly languageService = inject(LanguageService);
  t(key: string): string { return this.languageService.t(key); }
}
