import { Component, OnInit, computed, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { LanguageService } from '../core/language/language.service';
import { TeacherAccessUser, UserAccessService } from './user-access.service';

@Component({
  selector: 'app-user-access-management',
  imports: [FormsModule],
  template: `
    <section class="space-y-6">
      <div>
        <p class="text-sm uppercase tracking-[0.18em] text-[#8a6811]">{{ language.t('access.eyebrow') }}</p>
        <h2 class="mt-2 text-3xl font-semibold text-[#32124f]">{{ language.t('access.title') }}</h2>
        <p class="mt-2 max-w-3xl text-[#6f6877]">{{ language.t('access.description') }}</p>
      </div>

      @if (error()) {
        <p class="rounded-md bg-red-50 px-4 py-3 text-sm text-red-700">{{ error() }}</p>
      }
      @if (success()) {
        <p class="rounded-md border border-[#d8ead0] bg-[#f5fff0] px-4 py-3 text-sm text-[#3f6f24]">{{ success() }}</p>
      }

      <div class="grid min-h-[610px] overflow-hidden rounded-xl border border-[#ded7e4] bg-white shadow-[0_12px_35px_rgba(50,18,79,0.08)] lg:grid-cols-[300px_1fr]">
        <aside class="border-b border-[#e7e1eb] bg-[#fbfafc] p-4 lg:border-b-0 lg:border-r">
          <label class="block">
            <span class="text-xs font-bold uppercase tracking-[0.14em] text-[#817389]">{{ language.t('access.teachers') }}</span>
            <input class="mt-3 w-full rounded-md border border-[#d8d0df] px-3 py-2.5 text-sm outline-none focus:border-[#4b2070]"
              [ngModel]="search()" (ngModelChange)="search.set($event)" [placeholder]="language.t('access.search')" />
          </label>
          <div class="mt-4 space-y-2">
            @for (user of filteredUsers(); track user.userId) {
              <button type="button" class="w-full rounded-lg border px-4 py-3 text-left transition"
                [class.border-[#5b35f2]]="selectedUser()?.userId === user.userId"
                [class.bg-[#5337ed]]="selectedUser()?.userId === user.userId"
                [class.text-white]="selectedUser()?.userId === user.userId"
                [class.border-[#e0dae5]]="selectedUser()?.userId !== user.userId"
                [class.hover:bg-[#f5f1f8]]="selectedUser()?.userId !== user.userId"
                (click)="selectUser(user)">
                <span class="block text-sm font-semibold">{{ user.fullName }}</span>
                <span class="mt-1 block truncate text-xs opacity-75">{{ user.email || user.username }}</span>
              </button>
            } @empty {
              <p class="px-2 py-5 text-sm text-[#756a7d]">{{ language.t('access.noTeachers') }}</p>
            }
          </div>
        </aside>

        <main class="p-5 sm:p-7">
          @if (selectedUser(); as user) {
            <div class="flex flex-wrap items-start justify-between gap-4">
              <div>
                <h3 class="text-xl font-semibold text-[#23172e]">{{ language.t('access.assigned') }}</h3>
                <p class="mt-1 text-sm text-[#716779]">{{ user.fullName }} · {{ selectedCount() }} {{ language.t('access.selected') }}</p>
              </div>
              <button type="button" class="rounded-lg bg-[#5739ed] px-5 py-3 text-sm font-semibold text-white hover:bg-[#462bd2] disabled:opacity-60"
                [disabled]="saving()" (click)="save()">
                {{ saving() ? language.t('access.saving') : language.t('access.save') }}
              </button>
            </div>

            <div class="mt-6 grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
              @for (scope of scopes(); track scope) {
                <button type="button" class="relative min-h-28 rounded-xl border p-4 text-left transition"
                  [class.border-[#5b35f2]]="hasScope(scope)" [class.bg-[#f0f1ff]]="hasScope(scope)"
                  [class.ring-2]="hasScope(scope)" [class.ring-[#5b35f2]/15]="hasScope(scope)"
                  [class.border-[#ddd7e3]]="!hasScope(scope)" (click)="toggleScope(scope)">
                  <span class="absolute right-4 top-4 grid h-5 w-5 place-items-center rounded-md border text-xs font-bold"
                    [class.border-[#5b35f2]]="hasScope(scope)" [class.bg-[#5b35f2]]="hasScope(scope)"
                    [class.text-white]="hasScope(scope)">{{ hasScope(scope) ? '✓' : '' }}</span>
                  <span class="block pr-8 text-xs font-bold uppercase tracking-[0.13em] text-[#8a7fa2]">{{ scope }}</span>
                  <strong class="mt-2 block text-base text-[#23172e]">{{ language.t('access.scope.' + scope + '.title') }}</strong>
                  <span class="mt-1 block text-xs leading-5 text-[#6f6877]">{{ language.t('access.scope.' + scope + '.description') }}</span>
                </button>
              }
            </div>

            @if (hasScope('Class') && !hasScope('All')) {
              <section class="mt-6 rounded-xl border border-[#ded7e4] bg-[#faf9fc] p-5">
                <h4 class="font-semibold text-[#32124f]">{{ language.t('access.classes') }}</h4>
                <p class="mt-1 text-sm text-[#746a7c]">{{ language.t('access.classesHint') }}</p>
                <div class="mt-4 grid max-h-64 gap-2 overflow-y-auto sm:grid-cols-2 lg:grid-cols-3">
                  @for (homeroom of homerooms(); track homeroom) {
                    <label class="flex cursor-pointer items-center gap-3 rounded-md border border-[#e0dae5] bg-white px-3 py-2.5 text-sm">
                      <input type="checkbox" class="h-4 w-4 accent-[#4b2070]" [checked]="selectedHomerooms().has(homeroom)"
                        (change)="toggleHomeroom(homeroom)" />
                      <span>{{ homeroom }}</span>
                    </label>
                  }
                </div>
              </section>
            }
            @if (hasScope('SchoolLevel') && !hasScope('All')) {
              <section class="mt-6 rounded-xl border border-[#ded7e4] bg-[#faf9fc] p-5">
                <h4 class="font-semibold text-[#32124f]">{{ language.t('access.schoolLevels') }}</h4>
                <p class="mt-1 text-sm text-[#746a7c]">{{ language.t('access.schoolLevelsHint') }}</p>
                <div class="mt-4 grid gap-2 sm:grid-cols-2 lg:grid-cols-3">
                  @for (schoolLevel of schoolLevels(); track schoolLevel) {
                    <label class="flex cursor-pointer items-center gap-3 rounded-md border border-[#e0dae5] bg-white px-3 py-2.5 text-sm">
                      <input type="checkbox" class="h-4 w-4 accent-[#4b2070]" [checked]="selectedSchoolLevels().has(schoolLevel)"
                        (change)="toggleSchoolLevel(schoolLevel)" />
                      <span>{{ language.t('access.schoolLevel.' + schoolLevel + '.title') }}</span>
                    </label>
                  }
                </div>
              </section>
            }
          } @else {
            <div class="grid min-h-[420px] place-items-center text-center text-[#746a7c]">
              <div><p class="text-lg font-semibold text-[#32124f]">{{ language.t('access.chooseTeacher') }}</p></div>
            </div>
          }
        </main>
      </div>
    </section>
  `,
})
export class UserAccessManagementComponent implements OnInit {
  readonly users = signal<TeacherAccessUser[]>([]);
  readonly scopes = signal<string[]>([]);
  readonly homerooms = signal<string[]>([]);
  readonly schoolLevels = signal<string[]>([]);
  readonly selectedUser = signal<TeacherAccessUser | null>(null);
  readonly selectedScopes = signal(new Set<string>());
  readonly selectedHomerooms = signal(new Set<string>());
  readonly selectedSchoolLevels = signal(new Set<string>());
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  readonly search = signal('');

  readonly filteredUsers = computed(() => {
    const term = this.search().trim().toLocaleLowerCase();
    return term ? this.users().filter((user) =>
      `${user.fullName} ${user.username} ${user.email || ''}`.toLocaleLowerCase().includes(term)) : this.users();
  });
  readonly selectedCount = computed(() => this.selectedScopes().has('All')
    ? this.scopes().length : this.selectedScopes().size);

  constructor(private readonly accessService: UserAccessService, readonly language: LanguageService) {}

  ngOnInit(): void {
    this.accessService.load().subscribe({
      next: (data) => {
        this.users.set(data.users);
        this.scopes.set(data.availableScopes);
        this.homerooms.set(data.availableHomerooms);
        this.schoolLevels.set(data.availableSchoolLevels);
        if (data.users.length) this.selectUser(data.users[0]);
      },
      error: () => this.error.set(this.language.t('access.loadError')),
    });
  }

  selectUser(user: TeacherAccessUser): void {
    this.selectedUser.set(user);
    this.selectedScopes.set(new Set(user.scopes.includes('All') ? this.scopes() : user.scopes));
    this.selectedHomerooms.set(new Set(user.homerooms));
    this.selectedSchoolLevels.set(new Set(user.schoolLevels));
    this.error.set(null);
    this.success.set(null);
  }

  hasScope(scope: string): boolean { return this.selectedScopes().has(scope); }

  toggleScope(scope: string): void {
    const next = new Set(this.selectedScopes());
    if (scope === 'All') {
      if (next.has('All')) next.clear(); else this.scopes().forEach((item) => next.add(item));
    } else if (next.has(scope)) {
      next.delete(scope);
      next.delete('All');
    } else {
      next.add(scope);
      if (this.scopes().filter((item) => item !== 'All').every((item) => next.has(item))) next.add('All');
    }
    this.selectedScopes.set(next);
  }

  toggleHomeroom(homeroom: string): void {
    const next = new Set(this.selectedHomerooms());
    next.has(homeroom) ? next.delete(homeroom) : next.add(homeroom);
    this.selectedHomerooms.set(next);
  }

  toggleSchoolLevel(schoolLevel: string): void {
    const next = new Set(this.selectedSchoolLevels());
    next.has(schoolLevel) ? next.delete(schoolLevel) : next.add(schoolLevel);
    this.selectedSchoolLevels.set(next);
  }

  save(): void {
    const user = this.selectedUser();
    if (!user || this.selectedScopes().size === 0) {
      this.error.set(this.language.t('access.selectOne'));
      return;
    }
    if (this.hasScope('Class') && !this.hasScope('All') && this.selectedHomerooms().size === 0) {
      this.error.set(this.language.t('access.selectClass'));
      return;
    }
    if (this.hasScope('SchoolLevel') && !this.hasScope('All') && this.selectedSchoolLevels().size === 0) {
      this.error.set(this.language.t('access.selectSchoolLevel'));
      return;
    }
    const scopes = this.hasScope('All') ? ['All'] : [...this.selectedScopes()];
    const homerooms = scopes.includes('Class') ? [...this.selectedHomerooms()] : [];
    const schoolLevels = scopes.includes('SchoolLevel') ? [...this.selectedSchoolLevels()] : [];
    this.saving.set(true);
    this.error.set(null);
    this.accessService.update(user.userId, scopes, homerooms, schoolLevels).subscribe({
      next: (updated) => {
        this.users.update((users) => users.map((item) => item.userId === updated.userId ? updated : item));
        this.selectUser(updated);
        this.success.set(this.language.t('access.saved'));
        this.saving.set(false);
      },
      error: (response) => {
        this.error.set(response?.error?.message || this.language.t('access.saveError'));
        this.saving.set(false);
      },
    });
  }
}
