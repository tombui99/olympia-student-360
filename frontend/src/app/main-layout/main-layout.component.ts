import { Component } from '@angular/core';
import { NgIf } from '@angular/common';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthenticationService } from '../core/auth/auth.service';
import { LanguageService } from '../core/language/language.service';

@Component({
  selector: 'app-main-layout',
  imports: [NgIf, RouterOutlet, RouterLink, RouterLinkActive],
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
          <a
            routerLink="/reports"
            routerLinkActive="bg-[#f3c246] text-[#32124f]"
            class="flex items-center gap-3 rounded-md px-4 py-3 text-sm font-medium text-purple-50 transition hover:bg-white/10"
          >
            <span class="h-2.5 w-2.5 rounded-full bg-current"></span>
            {{ languageService.t('nav.reports') }}
          </a>
          <a
            *ngIf="authService.getUserRole() === 'Admin'"
            routerLink="/admin/accounts"
            routerLinkActive="bg-[#f3c246] text-[#32124f]"
            class="flex items-center gap-3 rounded-md px-4 py-3 text-sm font-medium text-purple-50 transition hover:bg-white/10"
          >
            <span class="h-2.5 w-2.5 rounded-full bg-current"></span>
            {{ languageService.t('nav.accounts') }}
          </a>
        </nav>
      </aside>

      <div class="min-w-0">
        <header class="flex flex-wrap items-center justify-between gap-4 border-b border-[#e4dde8] bg-white px-6 py-4 lg:px-8">
          <div>
            <p class="text-sm text-[#7c7284]">{{ languageService.t('app.school') }}</p>
            <h1 class="text-xl font-semibold text-[#32124f]">{{ languageService.t('app.headerTitle') }}</h1>
          </div>
          <div class="flex flex-wrap items-center justify-end gap-4">
            <div
              class="flex h-10 items-center rounded-full border border-[#e9e3ef] bg-[#f7f5fa] p-1 shadow-[0_8px_24px_rgba(50,18,79,0.08)]"
              role="group"
              aria-label="Language"
            >
              <button
                class="h-8 rounded-full px-4 text-xs font-bold transition"
                [class.bg-white]="languageService.language() === 'en'"
                [class.text-[#4b2070]]="languageService.language() === 'en'"
                [class.shadow-[0_4px_14px_rgba(50,18,79,0.12)]]="languageService.language() === 'en'"
                [class.text-[#8b8193]]="languageService.language() !== 'en'"
                type="button"
                (click)="languageService.use('en')"
              >
                {{ languageService.t('language.en') }}
              </button>
              <button
                class="h-8 rounded-full px-4 text-xs font-bold transition"
                [class.bg-white]="languageService.language() === 'vi'"
                [class.text-[#4b2070]]="languageService.language() === 'vi'"
                [class.shadow-[0_4px_14px_rgba(50,18,79,0.12)]]="languageService.language() === 'vi'"
                [class.text-[#8b8193]]="languageService.language() !== 'vi'"
                type="button"
                (click)="languageService.use('vi')"
              >
                {{ languageService.t('language.vi') }}
              </button>
            </div>
            <div class="flex items-center gap-3">
              <div class="grid h-10 w-10 place-items-center rounded-full bg-[#32124f] text-sm font-bold text-[#f3c246]">
                {{ userInitials() }}
              </div>
              <div class="text-right">
                <p class="text-sm font-semibold text-[#32124f]">{{ authService.fullName() || languageService.t('app.userFallback') }}</p>
                <p class="text-xs text-[#7c7284]">{{ authService.role() || languageService.t('app.roleFallback') }}</p>
              </div>
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

  userInitials(): string {
    const name = (this.authService.fullName() || this.languageService.t('app.userFallback')).trim();
    const initials = name
      .split(/\s+/)
      .filter(Boolean)
      .slice(0, 2)
      .map((part) => part[0]?.toUpperCase())
      .join('');

    return initials || 'U';
  }
}
