import { Component, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgFor, NgIf } from '@angular/common';
import { AdminUsersService } from '../api/api/admin-users.service';
import { AdminUserDto, CreateAdminUserDto, UpdateAdminUserDto } from '../api/model/models';
import { LanguageService } from '../core/language/language.service';
import { UserAccessService } from './user-access.service';

type AccountForm = CreateAdminUserDto & { id?: string; newPassword?: string | null };

@Component({
  selector: 'app-account-management',
  imports: [FormsModule, NgFor, NgIf],
  template: `
    <section class="space-y-7">
      <div class="flex flex-wrap items-end justify-between gap-4">
        <div>
          <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">{{ languageService.t('accounts.eyebrow') }}</p>
          <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ languageService.t('accounts.title') }}</h2>
          <p class="mt-2 max-w-2xl text-[#6f6877]">{{ languageService.t('accounts.description') }}</p>
        </div>
        <button
          class="rounded-md bg-[#32124f] px-4 py-3 text-sm font-semibold text-white transition hover:bg-[#4b2070]"
          type="button"
          (click)="startCreate()"
        >
          {{ languageService.t('accounts.newAccount') }}
        </button>
      </div>

      <p *ngIf="error()" class="rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">{{ error() }}</p>
      <p *ngIf="success()" class="rounded-md border border-[#d8ead0] bg-[#f5fff0] px-4 py-3 text-sm text-[#3f6f24]">{{ success() }}</p>

      <section class="rounded-lg border border-[#ebe6ef] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)]">
        <div class="border-b border-[#ebe6ef] p-6">
          <h3 class="text-lg font-semibold text-[#32124f]">{{ languageService.t('accounts.accountsList') }}</h3>
          <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('accounts.accountsListHint') }}</p>
        </div>

        <div class="overflow-x-auto">
          <table class="w-full min-w-[820px] text-left">
            <thead class="bg-[#faf9f6] text-xs uppercase tracking-[0.12em] text-[#7c7284]">
              <tr>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('accounts.fullName') }}</th>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('accounts.username') }}</th>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('accounts.role') }}</th>
                <th class="px-6 py-4 font-semibold">{{ languageService.t('accounts.lastLogin') }}</th>
                <th class="px-6 py-4 text-right font-semibold">{{ languageService.t('accounts.actions') }}</th>
              </tr>
            </thead>
            <tbody class="divide-y divide-[#ebe6ef]">
              <tr *ngFor="let user of users()" class="text-sm">
                <td class="px-6 py-4">
                  <p class="font-semibold text-[#32124f]">{{ user.fullName }}</p>
                  <p class="mt-1 text-xs text-[#7c7284]">{{ user.email || '-' }}</p>
                </td>
                <td class="px-6 py-4 text-[#6f6877]">{{ user.username }}</td>
                <td class="px-6 py-4">
                  <span class="rounded bg-[#f3c246]/25 px-3 py-1 text-xs font-semibold text-[#7a5b12]">{{ roleLabel(user.role) }}</span>
                </td>
                <td class="px-6 py-4 text-[#6f6877]">{{ formatDate(user.lastLoginAt) }}</td>
                <td class="px-6 py-4">
                  <div class="flex justify-end gap-2">
                    <button
                      class="rounded-md border border-[#d8d0df] px-3 py-2 text-xs font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
                      type="button"
                      (click)="edit(user)"
                    >
                      {{ languageService.t('accounts.edit') }}
                    </button>
                    <button
                      class="rounded-md border border-red-200 px-3 py-2 text-xs font-semibold text-red-700 transition hover:bg-red-50"
                      type="button"
                      (click)="deleteUser(user)"
                    >
                      {{ languageService.t('accounts.delete') }}
                    </button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <div
        *ngIf="sheetMounted()"
        class="fixed inset-0 z-40"
        [class.pointer-events-none]="!sheetOpen()"
      >
        <button
          class="absolute inset-0 bg-[#24182f]/35 transition-opacity duration-300 ease-out"
          [class.opacity-100]="sheetOpen()"
          [class.opacity-0]="!sheetOpen()"
          type="button"
          [attr.aria-label]="languageService.t('accounts.closeSheet')"
          (click)="closeSheet()"
        ></button>

        <aside
          class="absolute right-0 top-0 flex h-full w-full max-w-xl flex-col bg-white shadow-[0_24px_80px_rgba(36,24,47,0.28)] transition-transform duration-300 ease-out will-change-transform"
          [class.translate-x-0]="sheetOpen()"
          [class.translate-x-full]="!sheetOpen()"
          role="dialog"
          aria-modal="true"
        >
          <div class="flex items-start justify-between gap-4 border-b border-[#ebe6ef] px-6 py-5">
            <div>
              <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">{{ languageService.t('accounts.eyebrow') }}</p>
              <h3 class="mt-2 text-xl font-semibold text-[#32124f]">
                {{ editingUserId() ? languageService.t('accounts.editAccount') : languageService.t('accounts.createAccount') }}
              </h3>
              <p class="mt-1 text-sm text-[#6f6877]">{{ languageService.t('accounts.formHint') }}</p>
            </div>
            <button
              class="grid h-9 w-9 place-items-center rounded-full border border-[#d8d0df] text-lg font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
              type="button"
              [attr.aria-label]="languageService.t('accounts.closeSheet')"
              (click)="closeSheet()"
            >
              ×
            </button>
          </div>

          <form class="flex min-h-0 flex-1 flex-col" (ngSubmit)="save()">
            <div class="min-h-0 flex-1 space-y-5 overflow-y-auto px-6 py-6">
              <p *ngIf="error()" class="rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">{{ error() }}</p>

              <label class="block">
                <span class="text-sm font-medium text-[#34243d]">{{ languageService.t('accounts.username') }}</span>
                <input
                  class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-4 py-3 outline-none transition focus:border-[#32124f] focus:ring-4 focus:ring-[#32124f]/10 disabled:bg-[#f7f4fa]"
                  name="username"
                  [(ngModel)]="form.username"
                  [disabled]="!!editingUserId()"
                  required
                />
              </label>

              @if (form.role === 'Teacher') {
                <section class="rounded-lg border border-[#ded7e4] bg-[#faf9fc] p-4">
                  <span class="text-sm font-medium text-[#34243d]">{{ languageService.t('accounts.dataAccess') }}</span>
                  <p class="mt-1 text-xs text-[#756a7d]">{{ languageService.t('accounts.dataAccessHint') }}</p>
                  <div class="mt-3 grid gap-2 sm:grid-cols-2">
                    @for (scope of accessScopes(); track scope) {
                      <label class="flex cursor-pointer items-center gap-3 rounded-md border border-[#ded7e4] bg-white px-3 py-2.5 text-sm">
                        <input type="checkbox" class="h-4 w-4 accent-[#4b2070]" [checked]="form.accessScopes.includes(scope)"
                          (change)="toggleAccessScope(scope)" />
                        <span>{{ languageService.t('access.scope.' + scope + '.title') }}</span>
                      </label>
                    }
                  </div>
                  @if (form.accessScopes.includes('Class') && !form.accessScopes.includes('All')) {
                    <p class="mt-4 text-xs font-semibold uppercase tracking-[0.12em] text-[#817389]">{{ languageService.t('access.classes') }}</p>
                    <div class="mt-2 grid max-h-44 gap-2 overflow-y-auto sm:grid-cols-2">
                      @for (homeroom of homerooms(); track homeroom) {
                        <label class="flex cursor-pointer items-center gap-2 rounded-md border border-[#e2dce7] bg-white px-3 py-2 text-sm">
                          <input type="checkbox" class="h-4 w-4 accent-[#4b2070]" [checked]="form.homerooms.includes(homeroom)"
                            (change)="toggleHomeroom(homeroom)" />
                          <span>{{ homeroom }}</span>
                        </label>
                      }
                    </div>
                  }
                </section>
              }

              <label class="block">
                <span class="text-sm font-medium text-[#34243d]">{{ languageService.t('accounts.email') }}</span>
                <input
                  class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-4 py-3 outline-none transition focus:border-[#32124f] focus:ring-4 focus:ring-[#32124f]/10"
                  name="email"
                  type="email"
                  [(ngModel)]="form.email"
                />
              </label>

              <label class="block">
                <span class="text-sm font-medium text-[#34243d]">{{ languageService.t('accounts.fullName') }}</span>
                <input
                  class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-4 py-3 outline-none transition focus:border-[#32124f] focus:ring-4 focus:ring-[#32124f]/10"
                  name="fullName"
                  [(ngModel)]="form.fullName"
                  required
                />
              </label>

              <label class="block">
                <span class="text-sm font-medium text-[#34243d]">{{ languageService.t('accounts.role') }}</span>
                <select
                  class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-4 py-3 outline-none transition focus:border-[#32124f] focus:ring-4 focus:ring-[#32124f]/10"
                  name="role"
                  [(ngModel)]="form.role"
                  required
                >
                  <option *ngFor="let role of roles()" [ngValue]="role">{{ roleLabel(role) }}</option>
                </select>
              </label>

              <label class="block">
                <span class="text-sm font-medium text-[#34243d]">
                  {{ editingUserId() ? languageService.t('accounts.newPasswordOptional') : languageService.t('accounts.password') }}
                </span>
                <input
                  class="mt-2 w-full rounded-md border border-[#d8d0df] bg-white px-4 py-3 outline-none transition focus:border-[#32124f] focus:ring-4 focus:ring-[#32124f]/10"
                  name="password"
                  type="password"
                  [(ngModel)]="passwordValue"
                  [required]="!editingUserId()"
                />
              </label>
            </div>

            <div class="flex items-center justify-end gap-3 border-t border-[#ebe6ef] bg-[#faf9f6] px-6 py-4">
              <button
                class="rounded-md border border-[#d8d0df] px-5 py-3 text-sm font-semibold text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1]"
                type="button"
                (click)="closeSheet()"
              >
                {{ languageService.t('accounts.cancel') }}
              </button>
              <button
                class="rounded-md bg-[#32124f] px-5 py-3 text-sm font-semibold text-white transition hover:bg-[#4b2070] disabled:cursor-not-allowed disabled:opacity-60"
                type="submit"
                [disabled]="saving()"
              >
                {{ saving() ? languageService.t('accounts.saving') : languageService.t('accounts.save') }}
              </button>
            </div>
          </form>
        </aside>
      </div>
    </section>
  `,
})
export class AccountManagementComponent implements OnInit {
  users = signal<AdminUserDto[]>([]);
  roles = signal<string[]>([]);
  accessScopes = signal<string[]>(['All', 'Health', 'Bus', 'Academic', 'Class']);
  homerooms = signal<string[]>([]);
  saving = signal(false);
  error = signal<string | null>(null);
  success = signal<string | null>(null);
  editingUserId = signal<string | null>(null);
  sheetMounted = signal(false);
  sheetOpen = signal(false);
  passwordValue = '';

  form: AccountForm = this.emptyForm();

  constructor(
    private readonly adminUsersService: AdminUsersService,
    private readonly userAccessService: UserAccessService,
    readonly languageService: LanguageService,
  ) {}

  ngOnInit() {
    this.load();
  }

  load() {
    this.userAccessService.load().subscribe({
      next: (data) => {
        this.accessScopes.set(data.availableScopes);
        this.homerooms.set(data.availableHomerooms);
      },
      error: () => undefined,
    });
    this.adminUsersService.apiAdminUsersRolesGet().subscribe({
      next: (roles) => this.roles.set(roles),
      error: () => this.error.set(this.languageService.t('accounts.loadError')),
    });

    this.adminUsersService.apiAdminUsersGet().subscribe({
      next: (users) => this.users.set(users),
      error: () => this.error.set(this.languageService.t('accounts.loadError')),
    });
  }

  startCreate() {
    this.editingUserId.set(null);
    this.passwordValue = '';
    this.form = this.emptyForm();
    this.clearMessages();
    this.openSheet();
  }

  edit(user: AdminUserDto) {
    this.editingUserId.set(user.id);
    this.passwordValue = '';
    this.form = {
      id: user.id,
      username: user.username,
      password: '',
      fullName: user.fullName,
      email: user.email,
      role: user.role,
      newPassword: null,
      accessScopes: user.accessScopes?.includes('All')
        ? [...this.accessScopes()]
        : user.accessScopes?.length ? user.accessScopes : [...this.accessScopes()],
      homerooms: user.homerooms || [],
    };
    this.clearMessages();
    this.openSheet();
  }

  closeSheet() {
    if (this.saving()) {
      return;
    }

    this.sheetOpen.set(false);
    window.setTimeout(() => this.sheetMounted.set(false), 300);
  }

  save() {
    this.clearMessages();

    if (!this.isFormValid()) {
      this.error.set(this.languageService.t('accounts.validationRequired'));
      return;
    }

    const editingId = this.editingUserId();
    this.saving.set(true);

    if (editingId) {
      const payload: UpdateAdminUserDto = {
        fullName: this.form.fullName,
        email: this.form.email,
        role: this.form.role,
        newPassword: this.passwordValue || null,
        accessScopes: this.form.role === 'Teacher' ? this.normalizedAccessScopes() : [],
        homerooms: this.form.role === 'Teacher' ? this.form.homerooms : [],
      };

      this.adminUsersService.apiAdminUsersIdPut(editingId, payload).subscribe({
        next: () => this.afterSave(this.languageService.t('accounts.updated')),
        error: (error) => this.handleError(error),
      });
      return;
    }

    const payload: CreateAdminUserDto = {
      username: this.form.username,
      password: this.passwordValue,
      fullName: this.form.fullName,
      email: this.form.email,
      role: this.form.role,
      accessScopes: this.form.role === 'Teacher' ? this.normalizedAccessScopes() : [],
      homerooms: this.form.role === 'Teacher' ? this.form.homerooms : [],
    };

    this.adminUsersService.apiAdminUsersPost(payload).subscribe({
      next: () => this.afterSave(this.languageService.t('accounts.created')),
      error: (error) => this.handleError(error),
    });
  }

  deleteUser(user: AdminUserDto) {
    this.clearMessages();
    const confirmed = window.confirm(this.languageService.t('accounts.deleteConfirm'));
    if (!confirmed) {
      return;
    }

    this.adminUsersService.apiAdminUsersIdDelete(user.id).subscribe({
      next: () => {
        this.success.set(this.languageService.t('accounts.deleted'));
        this.load();
      },
      error: (error) => this.handleError(error),
    });
  }

  formatDate(value?: string | null): string {
    if (!value) {
      return '-';
    }

    return new Intl.DateTimeFormat(undefined, {
      dateStyle: 'medium',
      timeStyle: 'short',
      timeZone: 'Asia/Ho_Chi_Minh',
    }).format(new Date(value));
  }

  roleLabel(role: string): string {
    const key = role === 'Principal' ? 'SchoolAdministration' : role;
    return this.languageService.t(`accounts.roles.${key}`);
  }

  toggleAccessScope(scope: string): void {
    const selected = new Set(this.form.accessScopes);
    if (scope === 'All') {
      if (selected.has('All')) selected.clear(); else this.accessScopes().forEach((item) => selected.add(item));
    } else if (selected.has(scope)) {
      selected.delete(scope);
      selected.delete('All');
    } else {
      selected.add(scope);
      if (this.accessScopes().filter((item) => item !== 'All').every((item) => selected.has(item))) selected.add('All');
    }
    this.form.accessScopes = [...selected];
  }

  toggleHomeroom(homeroom: string): void {
    const selected = new Set(this.form.homerooms);
    selected.has(homeroom) ? selected.delete(homeroom) : selected.add(homeroom);
    this.form.homerooms = [...selected];
  }

  private normalizedAccessScopes(): string[] {
    return this.form.accessScopes.includes('All') ? ['All'] : this.form.accessScopes;
  }

  private afterSave(message: string) {
    this.saving.set(false);
    this.editingUserId.set(null);
    this.passwordValue = '';
    this.form = this.emptyForm();
    this.closeSheet();
    this.error.set(null);
    this.success.set(message);
    this.load();
  }

  private handleError(error: any) {
    const apiMessage = error?.error?.message || error?.error?.Message;
    const apiErrors = error?.error?.errors || error?.error?.Errors;
    const validationErrors =
      apiErrors && typeof apiErrors === 'object'
        ? Object.values(apiErrors).flat().join(' ')
        : null;

    this.error.set(
      Array.isArray(apiErrors)
        ? apiErrors.join(' ')
        : validationErrors || apiMessage || this.languageService.t('accounts.saveError'),
    );
    this.saving.set(false);
  }

  private clearMessages() {
    this.error.set(null);
    this.success.set(null);
  }

  private isFormValid(): boolean {
    const hasBaseFields = Boolean(
      this.form.username?.trim() &&
        this.form.fullName?.trim() &&
        this.form.role?.trim(),
    );

    if (!hasBaseFields) {
      return false;
    }

    if (this.form.role === 'Teacher' && (!this.form.accessScopes.length ||
      (this.form.accessScopes.includes('Class') && !this.form.accessScopes.includes('All') && !this.form.homerooms.length))) {
      return false;
    }

    return Boolean(this.editingUserId() || this.passwordValue.trim());
  }

  private openSheet() {
    this.sheetMounted.set(true);
    window.setTimeout(() => this.sheetOpen.set(true));
  }

  private emptyForm(): AccountForm {
    return {
      username: '',
      password: '',
      fullName: '',
      email: '',
      role: 'Teacher',
      newPassword: null,
      accessScopes: [...this.accessScopes()],
      homerooms: [],
    };
  }
}
