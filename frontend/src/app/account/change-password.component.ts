import { Component, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { AuthenticationService } from '../core/auth/auth.service';
import { LanguageService } from '../core/language/language.service';

@Component({
  selector: 'app-change-password',
  imports: [FormsModule],
  template: `
    <section class="mx-auto max-w-4xl space-y-7">
      <header>
        <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">
          {{ languageService.t('changePassword.eyebrow') }}
        </p>
        <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">
          {{ languageService.t('changePassword.title') }}
        </h2>
        <p class="mt-2 max-w-2xl text-[#6f6877]">
          {{ languageService.t('changePassword.description') }}
        </p>
      </header>

      <div class="grid gap-6 lg:grid-cols-[minmax(0,1fr)_18rem]">
        <section class="rounded-lg border border-[#ebe6ef] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
          <div class="border-b border-[#ebe6ef] px-6 py-5">
            <h3 class="text-lg font-semibold text-[#32124f]">
              {{ languageService.t('changePassword.formTitle') }}
            </h3>
            <p class="mt-1 text-sm text-[#6f6877]">
              {{ authService.getUsername() }}
            </p>
          </div>

          <form class="space-y-5 p-6" (ngSubmit)="submit()">
            @if (error()) {
              <div
                class="rounded-md border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700"
                role="alert"
              >
                {{ error() }}
              </div>
            }

            @if (success()) {
              <div
                class="rounded-md border border-[#d8ead0] bg-[#f5fff0] px-4 py-3 text-sm text-[#3f6f24]"
                role="status"
                aria-live="polite"
              >
                {{ languageService.t('changePassword.success') }}
              </div>
            }

            <label class="block" for="current-password">
              <span class="text-sm font-medium text-[#34243d]">
                {{ languageService.t('changePassword.currentPassword') }}
              </span>
              <span class="relative mt-2 block">
                <input
                  id="current-password"
                  class="w-full rounded-md border border-[#d8d0df] bg-white py-3 pl-4 pr-24 outline-none transition focus:border-[#32124f] focus:ring-4 focus:ring-[#32124f]/10"
                  name="currentPassword"
                  [type]="showCurrentPassword() ? 'text' : 'password'"
                  autocomplete="current-password"
                  [(ngModel)]="currentPassword"
                  (ngModelChange)="clearFeedback()"
                  required
                />
                <button
                  class="absolute right-2 top-1/2 -translate-y-1/2 rounded px-3 py-1.5 text-xs font-semibold text-[#6f6877] transition hover:bg-[#f7f2dc] hover:text-[#32124f] focus:outline-none focus:ring-2 focus:ring-[#f3c246]/60"
                  type="button"
                  [attr.aria-label]="languageService.t(showCurrentPassword() ? 'auth.hidePassword' : 'auth.showPassword')"
                  (click)="showCurrentPassword.update((visible) => !visible)"
                >
                  {{ languageService.t(showCurrentPassword() ? 'changePassword.hide' : 'changePassword.show') }}
                </button>
              </span>
            </label>

            <label class="block" for="new-password">
              <span class="text-sm font-medium text-[#34243d]">
                {{ languageService.t('changePassword.newPassword') }}
              </span>
              <span class="relative mt-2 block">
                <input
                  id="new-password"
                  class="w-full rounded-md border border-[#d8d0df] bg-white py-3 pl-4 pr-24 outline-none transition focus:border-[#32124f] focus:ring-4 focus:ring-[#32124f]/10"
                  name="newPassword"
                  [type]="showNewPassword() ? 'text' : 'password'"
                  autocomplete="new-password"
                  [(ngModel)]="newPassword"
                  (ngModelChange)="clearFeedback()"
                  required
                />
                <button
                  class="absolute right-2 top-1/2 -translate-y-1/2 rounded px-3 py-1.5 text-xs font-semibold text-[#6f6877] transition hover:bg-[#f7f2dc] hover:text-[#32124f] focus:outline-none focus:ring-2 focus:ring-[#f3c246]/60"
                  type="button"
                  [attr.aria-label]="languageService.t(showNewPassword() ? 'auth.hidePassword' : 'auth.showPassword')"
                  (click)="showNewPassword.update((visible) => !visible)"
                >
                  {{ languageService.t(showNewPassword() ? 'changePassword.hide' : 'changePassword.show') }}
                </button>
              </span>
            </label>

            <label class="block" for="confirm-password">
              <span class="text-sm font-medium text-[#34243d]">
                {{ languageService.t('changePassword.confirmPassword') }}
              </span>
              <span class="relative mt-2 block">
                <input
                  id="confirm-password"
                  class="w-full rounded-md border border-[#d8d0df] bg-white py-3 pl-4 pr-24 outline-none transition focus:border-[#32124f] focus:ring-4 focus:ring-[#32124f]/10"
                  name="confirmPassword"
                  [type]="showConfirmPassword() ? 'text' : 'password'"
                  autocomplete="new-password"
                  [(ngModel)]="confirmPassword"
                  (ngModelChange)="clearFeedback()"
                  required
                />
                <button
                  class="absolute right-2 top-1/2 -translate-y-1/2 rounded px-3 py-1.5 text-xs font-semibold text-[#6f6877] transition hover:bg-[#f7f2dc] hover:text-[#32124f] focus:outline-none focus:ring-2 focus:ring-[#f3c246]/60"
                  type="button"
                  [attr.aria-label]="languageService.t(showConfirmPassword() ? 'auth.hidePassword' : 'auth.showPassword')"
                  (click)="showConfirmPassword.update((visible) => !visible)"
                >
                  {{ languageService.t(showConfirmPassword() ? 'changePassword.hide' : 'changePassword.show') }}
                </button>
              </span>
            </label>

            <div class="flex justify-end border-t border-[#ebe6ef] pt-5">
              <button
                class="rounded-md bg-[#32124f] px-5 py-3 text-sm font-semibold text-white transition hover:bg-[#4b2070] focus:outline-none focus:ring-4 focus:ring-[#f3c246]/40 disabled:cursor-not-allowed disabled:opacity-60"
                type="submit"
                [disabled]="saving()"
              >
                {{ languageService.t(saving() ? 'changePassword.saving' : 'changePassword.submit') }}
              </button>
            </div>
          </form>
        </section>

        <aside class="h-fit rounded-lg border border-[#eadcae] bg-[#fffaf0] p-5">
          <div class="grid h-10 w-10 place-items-center rounded-full bg-[#f3c246]/30 text-xl" aria-hidden="true">
            🔐
          </div>
          <h3 class="mt-4 font-semibold text-[#32124f]">
            {{ languageService.t('changePassword.requirementsTitle') }}
          </h3>
          <p class="mt-2 text-sm leading-6 text-[#6f6877]">
            {{ languageService.t('changePassword.requirements') }}
          </p>
          <p class="mt-4 text-sm leading-6 text-[#6f6877]">
            {{ languageService.t('changePassword.securityNote') }}
          </p>
        </aside>
      </div>
    </section>
  `,
})
export class ChangePasswordComponent {
  currentPassword = '';
  newPassword = '';
  confirmPassword = '';

  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal(false);
  readonly showCurrentPassword = signal(false);
  readonly showNewPassword = signal(false);
  readonly showConfirmPassword = signal(false);

  constructor(
    readonly authService: AuthenticationService,
    readonly languageService: LanguageService,
  ) {}

  submit(): void {
    this.error.set(null);
    this.success.set(false);

    if (!this.currentPassword || !this.newPassword || !this.confirmPassword) {
      this.error.set(this.languageService.t('changePassword.requiredError'));
      return;
    }

    if (this.newPassword !== this.confirmPassword) {
      this.error.set(this.languageService.t('changePassword.matchError'));
      return;
    }

    if (this.currentPassword === this.newPassword) {
      this.error.set(this.languageService.t('changePassword.sameError'));
      return;
    }

    this.saving.set(true);
    this.authService.changePassword({
      currentPassword: this.currentPassword,
      newPassword: this.newPassword,
      confirmPassword: this.confirmPassword,
    }).subscribe({
      next: () => {
        this.currentPassword = '';
        this.newPassword = '';
        this.confirmPassword = '';
        this.showCurrentPassword.set(false);
        this.showNewPassword.set(false);
        this.showConfirmPassword.set(false);
        this.saving.set(false);
        this.success.set(true);
      },
      error: (response) => {
        this.error.set(this.apiErrorMessage(response));
        this.saving.set(false);
      },
    });
  }

  clearFeedback(): void {
    this.error.set(null);
    this.success.set(false);
  }

  private apiErrorMessage(response: any): string {
    const apiErrors = response?.error?.errors || response?.error?.Errors;
    const apiMessage = response?.error?.message || response?.error?.Message;
    const validationErrors =
      apiErrors && typeof apiErrors === 'object' && !Array.isArray(apiErrors)
        ? Object.values(apiErrors).flat().join(' ')
        : null;

    return Array.isArray(apiErrors)
      ? apiErrors.join(' ')
      : validationErrors || apiMessage || this.languageService.t('changePassword.saveError');
  }
}
