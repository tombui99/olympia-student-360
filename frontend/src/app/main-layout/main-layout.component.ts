import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthenticationService } from '../core/auth/auth.service';
import { LanguageService } from '../core/language/language.service';

@Component({
  selector: 'app-main-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="min-h-screen bg-[#f6f4ef] text-[#24182f] lg:grid lg:grid-cols-[280px_1fr]">
      <aside class="bg-[#32124f] px-5 py-6 text-white lg:min-h-screen">
        <div class="flex items-center gap-3">
          <div class="grid h-11 w-11 place-items-center rounded bg-[#f3c246] text-lg font-bold text-[#32124f]">O</div>
          <div>
            <p class="text-xs uppercase tracking-[0.18em] text-[#f3c246]">{{ languageService.t('app.shortSchool') }}</p>
            <p class="font-semibold">{{ languageService.t('app.brand') }}</p>
          </div>
        </div>

        <nav class="mt-9 space-y-1">
          <a
            routerLink="/dashboard"
            routerLinkActive="bg-[#f3c246] text-[#32124f]"
            class="flex items-center gap-3 rounded-md px-4 py-3 text-sm font-medium text-purple-50 transition hover:bg-white/10"
          >
            <span class="h-2.5 w-2.5 rounded-full bg-current"></span>
            {{ languageService.t('nav.dashboard') }}
          </a>
          <div class="flex items-center gap-3 rounded-md px-4 py-3 text-sm font-medium text-purple-200/70">
            <span class="h-2.5 w-2.5 rounded-full bg-current"></span>
            {{ languageService.t('nav.students') }}
          </div>
          <div class="flex items-center gap-3 rounded-md px-4 py-3 text-sm font-medium text-purple-200/70">
            <span class="h-2.5 w-2.5 rounded-full bg-current"></span>
            {{ languageService.t('nav.attendance') }}
          </div>
          <div class="flex items-center gap-3 rounded-md px-4 py-3 text-sm font-medium text-purple-200/70">
            <span class="h-2.5 w-2.5 rounded-full bg-current"></span>
            {{ languageService.t('nav.reports') }}
          </div>
        </nav>
      </aside>

      <div class="min-w-0">
        <header class="flex flex-wrap items-center justify-between gap-4 border-b border-[#e4dde8] bg-white px-6 py-4 lg:px-8">
          <div>
            <p class="text-sm text-[#7c7284]">{{ languageService.t('app.school') }}</p>
            <h1 class="text-xl font-semibold text-[#32124f]">{{ languageService.t('app.headerTitle') }}</h1>
          </div>
          <div class="flex items-center gap-4">
            <button
              class="rounded-md border border-[#d8d0df] px-3 py-2 text-xs font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
              type="button"
              (click)="languageService.toggle()"
            >
              {{ languageService.language() === 'en' ? languageService.t('language.vi') : languageService.t('language.en') }}
            </button>
            <div class="text-right">
              <p class="text-sm font-semibold text-[#32124f]">{{ authService.fullName() || languageService.t('app.userFallback') }}</p>
              <p class="text-xs text-[#7c7284]">{{ authService.jobTitle() || authService.role() || languageService.t('app.roleFallback') }}</p>
            </div>
            <button
              class="rounded-md border border-[#d8d0df] px-4 py-2 text-sm font-medium text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
              type="button"
              (click)="authService.logout()"
            >
              {{ languageService.t('nav.signOut') }}
            </button>
          </div>
        </header>

        <main class="px-6 py-7 lg:px-8">
          <router-outlet />
        </main>
      </div>
    </div>
  `,
})
export class MainLayoutComponent {
  constructor(
    readonly authService: AuthenticationService,
    readonly languageService: LanguageService,
  ) {}
}
