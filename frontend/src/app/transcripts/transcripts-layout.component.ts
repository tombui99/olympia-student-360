import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { LanguageService } from '../core/language/language.service';
import { TranscriptSettingsComponent } from './transcript-settings.component';

@Component({
  selector: 'app-transcripts-layout',
  imports: [RouterLink, RouterLinkActive, RouterOutlet, TranscriptSettingsComponent],
  template: `
    <section class="mx-auto max-w-[1500px]">
      <div class="no-print mb-6 flex flex-wrap items-center justify-between gap-3">
        <nav class="inline-flex max-w-full flex-wrap gap-1 rounded-lg border border-[#ded6e3] bg-white p-1 shadow-[0_6px_20px_rgba(50,18,79,0.04)]"
          [attr.aria-label]="t('transcript.workspace')">
          <a routerLink="/transcripts/sis" routerLinkActive="transcript-tab-active" #sisTab="routerLinkActive" ariaCurrentWhenActive="page"
            class="rounded-md px-4 py-2.5 text-sm font-semibold text-[#5f5268] transition hover:bg-[#f3edf6] hover:text-[#32124f]">
            {{ t('sis.nav') }}
          </a>
          <a routerLink="/transcripts/sis-summary" routerLinkActive="transcript-tab-active" #sisSummaryTab="routerLinkActive" ariaCurrentWhenActive="page"
            class="rounded-md px-4 py-2.5 text-sm font-semibold text-[#5f5268] transition hover:bg-[#f3edf6] hover:text-[#32124f]">
            {{ t('sis.summaryNav') }}
          </a>
          <a routerLink="/transcripts/student" routerLinkActive="transcript-tab-active"
            ariaCurrentWhenActive="page" class="rounded-md px-4 py-2.5 text-sm font-semibold text-[#5f5268] transition hover:bg-[#f3edf6] hover:text-[#32124f]">
            {{ t('transcript.studentNav') }}
          </a>
          <a routerLink="/transcripts/summary" [routerLinkActiveOptions]="{ exact: true }" routerLinkActive="transcript-tab-active"
            ariaCurrentWhenActive="page" class="rounded-md px-4 py-2.5 text-sm font-semibold text-[#5f5268] transition hover:bg-[#f3edf6] hover:text-[#32124f]">
            {{ t('transcript.summaryNav') }}
          </a>
        </nav>
        @if (!sisTab.isActive && !sisSummaryTab.isActive) { <app-transcript-settings /> }
      </div>
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
