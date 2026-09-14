import { NgFor, NgIf } from '@angular/common';
import { Component, EventEmitter, inject, Input, Output } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LanguageService } from '../core/language/language.service';
import { SystemUser } from '../reports/report-builder.service';

@Component({
  selector: 'app-workspace-share-picker',
  imports: [FormsModule, NgFor, NgIf],
  template: `
    <div class="relative">
      <button
        *ngIf="open"
        class="fixed inset-0 z-20 cursor-default"
        type="button"
        tabindex="-1"
        [attr.aria-label]="languageService.t('workspace.closeUserSearch')"
        (click)="close()"
      ></button>

      <div class="relative z-30">
        <div
          class="flex items-center gap-2 rounded-md border border-[#d8d0df] bg-white px-3 transition focus-within:border-[#32124f] focus-within:ring-4 focus-within:ring-[#32124f]/10"
          role="combobox"
          aria-haspopup="listbox"
          [attr.aria-expanded]="open"
          aria-controls="workspace-share-options"
        >
          <svg
            class="h-4 w-4 shrink-0 text-[#7c7284]"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
            stroke-linecap="round"
            aria-hidden="true"
          >
            <circle cx="11" cy="11" r="7"></circle>
            <path d="m20 20-3.5-3.5"></path>
          </svg>
          <input
            class="min-w-0 flex-1 bg-transparent py-3 text-sm text-[#32124f] outline-none placeholder:text-[#8b8291]"
            type="search"
            autocomplete="off"
            [attr.aria-label]="languageService.t('workspace.searchUsers')"
            [placeholder]="languageService.t('workspace.searchUsers')"
            [(ngModel)]="query"
            [ngModelOptions]="{ standalone: true }"
            (focus)="open = true"
            (click)="open = true"
            (keydown.escape)="close()"
          />
          <span
            *ngIf="selectedUsernames.length"
            class="shrink-0 rounded-full bg-[#f1e8f5] px-2 py-0.5 text-xs font-semibold text-[#5a267c]"
          >
            {{ selectedUsernames.length }} {{ languageService.t('workspace.selected') }}
          </span>
          <svg
            class="h-4 w-4 shrink-0 text-[#765c85] transition-transform"
            [class.rotate-180]="open"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            stroke-width="2.4"
            stroke-linecap="round"
            stroke-linejoin="round"
            aria-hidden="true"
          >
            <path d="m6 9 6 6 6-6"></path>
          </svg>
        </div>

        <div
          *ngIf="open"
          id="workspace-share-options"
          class="relative z-30 mt-2 max-h-56 overflow-y-auto rounded-md border border-[#d8d0df] bg-white p-2 shadow-[0_12px_32px_rgba(50,18,79,0.14)]"
          role="listbox"
          aria-multiselectable="true"
        >
          <label
            *ngFor="let user of filteredUsers; trackBy: trackUser"
            class="flex cursor-pointer items-start gap-3 rounded px-3 py-2.5 text-sm transition hover:bg-[#faf9f6]"
            role="option"
            [attr.aria-selected]="isSelected(user.username)"
          >
            <input
              class="mt-1 accent-[#4a1b73]"
              type="checkbox"
              [checked]="isSelected(user.username)"
              (change)="toggle(user.username)"
            />
            <span class="min-w-0 flex-1">
              <span class="block truncate font-semibold text-[#32124f]">{{ user.fullName }}</span>
              <span class="mt-0.5 flex flex-wrap items-center gap-1.5 text-xs text-[#7c7284]">
                <span class="truncate">{{ displayEmail(user) }}</span>
                <span class="rounded-full bg-[#f3c246]/20 px-2 py-0.5 font-semibold text-[#765408]">
                  {{ roleLabel(user.role) }}
                </span>
              </span>
            </span>
          </label>

          <p *ngIf="!users.length" class="px-3 py-3 text-sm text-[#6f6877]">
            {{ languageService.t('workspace.noUsersAvailable') }}
          </p>
          <p *ngIf="users.length && !filteredUsers.length" class="px-3 py-3 text-sm text-[#6f6877]">
            {{ languageService.t('workspace.noUsersFound') }}
          </p>
        </div>
      </div>

      <div class="mt-4">
        <div class="flex items-center justify-between gap-3">
          <p class="text-xs font-semibold uppercase tracking-[0.12em] text-[#746a7b]">
            {{ languageService.t('workspace.sharedUsers') }}
          </p>
          <span class="rounded-full bg-[#f1e8f5] px-2 py-0.5 text-xs font-semibold text-[#5a267c]">
            {{ selectedUsers.length }}
          </span>
        </div>

        <div *ngIf="selectedUsers.length; else noSharedUsers" class="mt-2 space-y-2">
          <div
            *ngFor="let user of selectedUsers; trackBy: trackUser"
            class="flex items-center gap-3 rounded-md border border-[#e5dee9] bg-white px-3 py-2.5"
          >
            <span class="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-[#f1e8f5] text-xs font-bold text-[#5a267c]">
              {{ initials(user.fullName) }}
            </span>
            <span class="min-w-0 flex-1">
              <span class="block truncate text-sm font-semibold text-[#32124f]">{{ user.fullName }}</span>
              <span class="mt-0.5 flex flex-wrap items-center gap-1.5 text-xs text-[#7c7284]">
                <span class="truncate">{{ displayEmail(user) }}</span>
                <span class="rounded-full bg-[#f3c246]/20 px-2 py-0.5 font-semibold text-[#765408]">
                  {{ roleLabel(user.role) }}
                </span>
              </span>
            </span>
            <button
              class="grid h-8 w-8 shrink-0 place-items-center rounded-full text-lg text-[#756b7b] transition hover:bg-red-50 hover:text-red-700"
              type="button"
              [attr.aria-label]="languageService.t('workspace.removeUser') + ' ' + user.fullName"
              (click)="toggle(user.username)"
            >
              <span aria-hidden="true">×</span>
            </button>
          </div>
        </div>

        <ng-template #noSharedUsers>
          <p class="mt-2 rounded-md border border-dashed border-[#d8d0df] bg-white px-3 py-3 text-sm text-[#7c7284]">
            {{ languageService.t('workspace.noSharedUsers') }}
          </p>
        </ng-template>
      </div>
    </div>
  `,
})
export class WorkspaceSharePickerComponent {
  readonly languageService = inject(LanguageService);

  @Input() users: SystemUser[] = [];
  @Input() selectedUsernames: string[] = [];
  @Output() readonly selectedUsernamesChange = new EventEmitter<string[]>();

  query = '';
  open = false;

  get filteredUsers(): SystemUser[] {
    const query = this.query.trim().toLocaleLowerCase();
    if (!query) {
      return this.users;
    }

    return this.users.filter((user) =>
      [user.fullName, this.displayEmail(user), user.role, this.roleLabel(user.role)]
        .filter(Boolean)
        .some((value) => value!.toLocaleLowerCase().includes(query)),
    );
  }

  get selectedUsers(): SystemUser[] {
    const selected = new Set(this.selectedUsernames);
    return this.users.filter((user) => selected.has(user.username));
  }

  isSelected(username: string): boolean {
    return this.selectedUsernames.includes(username);
  }

  toggle(username: string): void {
    const selected = new Set(this.selectedUsernames);
    if (selected.has(username)) {
      selected.delete(username);
    } else {
      selected.add(username);
    }
    this.selectedUsernamesChange.emit([...selected]);
  }

  displayEmail(user: SystemUser): string {
    return user.email?.trim() || user.username;
  }

  roleLabel(role?: string | null): string {
    if (!role) {
      return this.languageService.t('app.roleFallback');
    }
    const key = role === 'Principal' ? 'SchoolAdministration' : role;
    return this.languageService.t(`accounts.roles.${key}`);
  }

  initials(fullName: string): string {
    return fullName
      .trim()
      .split(/\s+/)
      .slice(-2)
      .map((part) => part.charAt(0))
      .join('')
      .toLocaleUpperCase();
  }

  close(): void {
    this.open = false;
  }

  trackUser(_index: number, user: SystemUser): string {
    return user.id;
  }
}
