import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { NgIf } from '@angular/common';
import { AuthenticationService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/language/language.service';

@Component({
  selector: 'app-login',
  imports: [FormsModule, NgIf],
  template: `
    <main class="min-h-screen bg-[#f6f4ef] lg:grid lg:grid-cols-[1.05fr_0.95fr]">
      <section class="hidden bg-[#32124f] px-12 py-10 text-white lg:flex lg:flex-col">
        <div class="flex items-center gap-4">
          <img
            src="/assets/olympia_logo.png"
            [alt]="languageService.t('app.school')"
            class="h-14 w-14 shrink-0 object-contain"
          />
          <div>
            <p class="text-sm uppercase tracking-[0.18em] text-[#f3c246]">{{ languageService.t('app.school') }}</p>
            <h1 class="text-2xl font-semibold">{{ languageService.t('app.brand') }}</h1>
          </div>
        </div>

        <div class="mt-auto max-w-xl pb-16">
          <p class="text-sm uppercase tracking-[0.22em] text-[#f3c246]">{{ languageService.t('auth.platform') }}</p>
          <h2 class="mt-5 text-5xl font-semibold leading-tight">
            {{ languageService.t('auth.heroTitle') }}
          </h2>
          <p class="mt-6 text-lg leading-8 text-purple-100">
            {{ languageService.t('auth.heroBody') }}
          </p>
        </div>
      </section>

      <section class="flex min-h-screen items-center justify-center px-6 py-12">
        <div class="w-full max-w-md">
          <div class="mb-10 lg:hidden">
            <img
              src="/assets/olympia_logo.png"
              [alt]="languageService.t('app.school')"
              class="mb-5 h-16 w-16 object-contain"
            />
            <p class="text-sm uppercase tracking-[0.18em] text-[#7a5b12]">{{ languageService.t('app.school') }}</p>
            <h1 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ languageService.t('app.brand') }}</h1>
          </div>

          <div class="rounded-lg bg-white p-8 shadow-[0_18px_60px_rgba(50,18,79,0.12)]">
            <div class="flex items-center justify-between gap-4">
              <div>
                <h2 class="text-2xl font-semibold text-[#32124f]">{{ languageService.t('auth.signIn') }}</h2>
                <p class="mt-2 text-sm text-[#6f6877]">{{ languageService.t('auth.accountHint') }}</p>
              </div>
              <button
                class="rounded-md border border-[#d8d0df] px-3 py-2 text-xs font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
                type="button"
                (click)="languageService.toggle()"
              >
                {{ languageService.language() === 'en' ? languageService.t('language.vi') : languageService.t('language.en') }}
              </button>
            </div>

            <form class="mt-8 space-y-5" (ngSubmit)="submit()">
              <label class="block">
                <span class="text-sm font-medium text-[#34243d]">{{ languageService.t('auth.username') }}</span>
                <input
                  class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-4 py-3 outline-none transition focus:border-[#32124f] focus:ring-4 focus:ring-[#32124f]/10"
                  name="username"
                  autocomplete="username"
                  [(ngModel)]="username"
                  required
                />
              </label>

              <label class="block">
                <span class="text-sm font-medium text-[#34243d]">{{ languageService.t('auth.password') }}</span>
                <span class="relative mt-2 block">
                  <input
                    class="w-full rounded-md border border-[#d8d0df] bg-white py-3 pl-4 pr-12 outline-none transition focus:border-[#32124f] focus:ring-4 focus:ring-[#32124f]/10"
                    name="password"
                    [type]="showPassword() ? 'text' : 'password'"
                    autocomplete="current-password"
                    [(ngModel)]="password"
                    required
                  />
                  <button
                    class="absolute right-3 top-1/2 grid h-8 w-8 -translate-y-1/2 place-items-center rounded-full text-[#6f6877] transition hover:bg-[#f7f2dc] hover:text-[#32124f] focus:outline-none focus:ring-2 focus:ring-[#f3c246]/60"
                    type="button"
                    [attr.aria-label]="showPassword() ? languageService.t('auth.hidePassword') : languageService.t('auth.showPassword')"
                    [attr.title]="showPassword() ? languageService.t('auth.hidePassword') : languageService.t('auth.showPassword')"
                    (click)="showPassword.set(!showPassword())"
                  >
                    <svg
                      *ngIf="!showPassword()"
                      class="h-5 w-5"
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      stroke-width="2"
                      stroke-linecap="round"
                      stroke-linejoin="round"
                      aria-hidden="true"
                    >
                      <path d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7-10-7-10-7Z" />
                      <circle cx="12" cy="12" r="3" />
                    </svg>
                    <svg
                      *ngIf="showPassword()"
                      class="h-5 w-5"
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      stroke-width="2"
                      stroke-linecap="round"
                      stroke-linejoin="round"
                      aria-hidden="true"
                    >
                      <path d="M17.9 17.9A10.8 10.8 0 0 1 12 19c-6.5 0-10-7-10-7a18.4 18.4 0 0 1 4.2-5.1" />
                      <path d="M9.9 4.2A10.4 10.4 0 0 1 12 4c6.5 0 10 7 10 7a18 18 0 0 1-2.3 3.4" />
                      <path d="M14.1 14.1A3 3 0 0 1 9.9 9.9" />
                      <path d="M2 2l20 20" />
                    </svg>
                  </button>
                </span>
              </label>

              <p *ngIf="error()" class="rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">
                {{ error() }}
              </p>

              <button
                class="w-full rounded-md bg-[#32124f] px-4 py-3 font-semibold text-white transition hover:bg-[#4b2070] focus:outline-none focus:ring-4 focus:ring-[#f3c246]/40 disabled:cursor-not-allowed disabled:opacity-60"
                type="submit"
                [disabled]="loading()"
              >
                {{ loading() ? languageService.t('auth.signingIn') : languageService.t('auth.signIn') }}
              </button>
            </form>
          </div>
        </div>
      </section>
    </main>
  `,
})
export class LoginComponent {
  username = '';
  password = '';
  showPassword = signal(false);
  loading = signal(false);
  error = signal<string | null>(null);

  constructor(
    private readonly authService: AuthenticationService,
    private readonly router: Router,
    readonly languageService: LanguageService,
  ) {}

  submit() {
    this.loading.set(true);
    this.error.set(null);

    this.authService.login({ username: this.username, password: this.password }).subscribe({
      next: () => this.router.navigate(['/dashboard']),
      error: () => {
        this.error.set(this.languageService.t('auth.error'));
        this.loading.set(false);
      },
    });
  }
}
