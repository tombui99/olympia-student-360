import { Component, signal } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import {
  faClockRotateLeft,
  faTableColumns,
  faFileExcel,
  faGaugeHigh,
  faBars,
  faRightFromBracket,
  faArrowRightArrowLeft,
  faUsersGear,
} from '@fortawesome/free-solid-svg-icons';
import { IconDefinition } from '@fortawesome/fontawesome-svg-core';
import { AuthenticationService } from '../core/auth/auth.service';
import { LanguageService } from '../core/language/language.service';

interface NavItem {
  path: string;
  labelKey: string;
  icon: IconDefinition;
  adminOnly?: boolean;
}

@Component({
  selector: 'app-main-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FontAwesomeModule],
  template: `
    <div class="layout-shell min-h-screen bg-[#f6f4ef] text-[#24182f]" [class.sidebar-collapsed]="sidebarCollapsed()">
      <aside
        class="sidebar flex flex-col bg-[#32124f] px-5 py-6 text-white lg:sticky lg:top-0 lg:h-screen lg:self-start lg:overflow-y-auto"
        [class.sidebar-compact]="sidebarCollapsed()"
      >
        <div class="sidebar-header flex items-center justify-between gap-2">
          <div class="flex min-w-0 items-center gap-3">
            <div class="grid h-11 w-11 shrink-0 place-items-center rounded bg-[#f3c246] text-lg font-bold text-[#32124f]">O</div>
            @if (!sidebarCollapsed()) {
              <div class="min-w-0">
                <p class="text-xs uppercase tracking-[0.18em] text-[#f3c246]">{{ languageService.t('app.shortSchool') }}</p>
                <p class="font-semibold">{{ languageService.t('app.brand') }}</p>
              </div>
            }
          </div>
          <button
            class="sidebar-toggle grid h-9 w-9 shrink-0 place-items-center rounded-md text-[#f3c246] transition hover:bg-white/10 hover:text-white focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#f3c246]"
            type="button"
            [attr.aria-label]="languageService.t(sidebarCollapsed() ? 'sidebar.expand' : 'sidebar.collapse')"
            [attr.title]="languageService.t(sidebarCollapsed() ? 'sidebar.expand' : 'sidebar.collapse')"
            [attr.aria-expanded]="!sidebarCollapsed()"
            (click)="toggleSidebar()"
          >
            <fa-icon [icon]="menuIcon" />
          </button>
        </div>

        <nav class="mt-9 space-y-1">
          @for (item of navItems; track item.path) {
            @if (!item.adminOnly || authService.getUserRole() === 'Admin') {
              <a
                [routerLink]="item.path"
                routerLinkActive="nav-link-active"
                class="nav-link flex items-center gap-3 rounded-md px-4 py-3 text-sm font-semibold transition"
                [class.justify-center]="sidebarCollapsed()"
                [attr.aria-label]="sidebarCollapsed() ? languageService.t(item.labelKey) : null"
                [attr.title]="sidebarCollapsed() ? languageService.t(item.labelKey) : null"
              >
                <fa-icon class="nav-icon" [icon]="item.icon" />
                @if (!sidebarCollapsed()) {
                  <span>{{ languageService.t(item.labelKey) }}</span>
                }
              </a>
            }
          }
        </nav>

        <div class="sidebar-account mt-auto border-t border-white/15 pt-5">
          <div class="flex items-center gap-3" [class.justify-center]="sidebarCollapsed()">
            <div class="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-[#f3c246] text-sm font-bold text-[#32124f]">
              {{ userInitials() }}
            </div>
            @if (!sidebarCollapsed()) {
              <div class="min-w-0">
                <p class="truncate text-sm font-semibold text-white">{{ authService.fullName() || languageService.t('app.userFallback') }}</p>
                <p class="text-xs text-[#cdbed8]">{{ authService.role() || languageService.t('app.roleFallback') }}</p>
              </div>
            }
          </div>
          <button
            class="mt-4 flex w-full items-center justify-center gap-2 rounded-md border border-white/25 px-4 py-2 text-sm font-medium text-white transition hover:border-[#f3c246] hover:bg-white/10"
            type="button"
            [attr.aria-label]="languageService.t('nav.signOut')"
            [attr.title]="sidebarCollapsed() ? languageService.t('nav.signOut') : null"
            (click)="authService.logout()"
          >
            <fa-icon [icon]="signOutIcon" />
            @if (!sidebarCollapsed()) {
              <span>{{ languageService.t('nav.signOut') }}</span>
            }
          </button>
        </div>
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
          </div>
        </header>

        <main class="px-6 py-7 lg:px-8">
          <router-outlet />
        </main>
      </div>
    </div>
  `,
  styles: `
    .layout-shell {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
    }

    .sidebar,
    .layout-shell {
      transition: grid-template-columns 180ms ease, padding 180ms ease;
    }

    .nav-link {
      color: #f7f1ff;
    }

    .nav-icon {
      width: 1.125rem;
      color: #f3c246;
    }

    .nav-link:hover {
      background: rgb(255 255 255 / 0.12);
      color: #ffffff;
    }

    .nav-link:hover .nav-icon {
      color: #ffd968;
    }

    .nav-link-active,
    .nav-link-active:hover {
      background: #f3c246;
      color: #241036;
      box-shadow: inset 0 0 0 1px rgb(255 255 255 / 0.24);
    }

    .nav-link-active .nav-icon,
    .nav-link-active:hover .nav-icon {
      color: #241036;
    }

    @media (min-width: 1024px) {
      .layout-shell {
        grid-template-columns: 280px minmax(0, 1fr);
      }

      .layout-shell.sidebar-collapsed {
        grid-template-columns: 88px minmax(0, 1fr);
      }

      .sidebar-compact {
        padding-inline: 0.75rem;
      }

      .sidebar-compact .sidebar-header {
        flex-direction: column;
      }

      .sidebar-compact nav {
        margin-top: 1.5rem;
      }

      .sidebar-compact .nav-link {
        padding-inline: 0;
      }

      .sidebar-compact .sidebar-account button {
        padding-inline: 0;
      }
    }
  `,
})
export class MainLayoutComponent {
  readonly sidebarCollapsed = signal(false);
  readonly menuIcon = faBars;
  readonly signOutIcon = faRightFromBracket;
  readonly navItems: NavItem[] = [
    { path: '/dashboard', labelKey: 'nav.dashboard', icon: faGaugeHigh },
    { path: '/workspace', labelKey: 'nav.workspace', icon: faTableColumns },
    { path: '/imports', labelKey: 'nav.imports', icon: faFileExcel },
    { path: '/admin/accounts', labelKey: 'nav.accounts', icon: faUsersGear, adminOnly: true },
    {
      path: '/admin/teacher-student-assignments',
      labelKey: 'nav.teacherAssignments',
      icon: faArrowRightArrowLeft,
      adminOnly: true,
    },
    {
      path: '/admin/sync-history',
      labelKey: 'nav.syncHistory',
      icon: faClockRotateLeft,
      adminOnly: true,
    },
  ];

  constructor(
    readonly authService: AuthenticationService,
    readonly languageService: LanguageService,
  ) {}

  toggleSidebar(): void {
    this.sidebarCollapsed.update((collapsed) => !collapsed);
  }

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
