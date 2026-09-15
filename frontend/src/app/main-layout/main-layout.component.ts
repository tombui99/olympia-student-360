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
  faUserGraduate,
  faTriangleExclamation,
  faAward,
  faUsersGear,
  faFileLines,
  faKitMedical,
  faClipboardList,
  faChartColumn,
} from '@fortawesome/free-solid-svg-icons';
import { IconDefinition } from '@fortawesome/fontawesome-svg-core';
import { AuthenticationService } from '../core/auth/auth.service';
import { LanguageService } from '../core/language/language.service';
import { ApiLoadingService } from '../core/loading/api-loading.service';

interface NavItem {
  path: string;
  labelKey: string;
  icon: IconDefinition;
  adminOnly?: boolean;
}

interface NavSection {
  titleKey: string;
  items: NavItem[];
}

@Component({
  selector: 'app-main-layout',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FontAwesomeModule],
  template: `
    <div class="layout-shell min-h-screen bg-[#f6f4ef] text-[#24182f]" [class.sidebar-collapsed]="sidebarCollapsed()">
      @if (mobileMenuOpen()) {
        <button
          class="mobile-nav-backdrop no-print"
          type="button"
          aria-label="Close navigation"
          (click)="closeMobileNavigation()"
        ></button>
      }
      <aside
        class="sidebar no-print flex flex-col overflow-hidden bg-[#32124f] px-5 py-6 text-white"
        [class.sidebar-compact]="sidebarCollapsed()"
        [class.mobile-open]="mobileMenuOpen()"
      >
        <div class="sidebar-header flex items-center justify-between gap-2">
          <div class="flex min-w-0 items-center gap-3">
            <img
              src="/assets/olympia_logo.png"
              [alt]="languageService.t('app.school')"
              class="h-12 w-12 shrink-0 object-contain"
            />
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

        <nav class="mt-9 min-h-0 flex-1 overflow-y-auto">
          @for (section of navSections; track section.titleKey; let first = $first) {
            @if (visibleItems(section).length) {
              <div class="nav-section space-y-1" [class.mt-6]="!first">
                @if (!sidebarCollapsed()) {
                  <p class="nav-section-title px-4 pb-1 text-[0.65rem] font-bold uppercase tracking-[0.16em] text-[#b79bd0]">
                    {{ languageService.t(section.titleKey) }}
                  </p>
                } @else if (!first) {
                  <div class="nav-section-divider mx-auto mb-2 h-px w-7 bg-white/15" aria-hidden="true"></div>
                }
                @for (item of visibleItems(section); track item.path) {
                  <a
                    [routerLink]="item.path"
                    routerLinkActive="nav-link-active"
                    class="nav-link flex items-center gap-3 rounded-md px-4 py-3 text-sm font-semibold transition"
                    [class.justify-center]="sidebarCollapsed()"
                    [attr.aria-label]="sidebarCollapsed() ? languageService.t(item.labelKey) : null"
                    [attr.title]="sidebarCollapsed() ? languageService.t(item.labelKey) : null"
                    (click)="closeMobileNavigation()"
                  >
                    <fa-icon class="nav-icon" [icon]="item.icon" />
                    @if (!sidebarCollapsed()) {
                      <span>{{ languageService.t(item.labelKey) }}</span>
                    }
                  </a>
                }
              </div>
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
                <p class="text-xs text-[#cdbed8]">{{ roleLabel(authService.role()) }}</p>
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

      <div class="content-column min-w-0">
        <header class="no-print flex items-center justify-between gap-3 border-b border-[#e4dde8] bg-white px-4 py-3 sm:px-6 sm:py-4 lg:px-8">
          <button
            class="grid h-10 w-10 shrink-0 place-items-center rounded-md border border-[#e4dde8] text-[#32124f] transition hover:border-[#f3c246] hover:bg-[#fff8e1] lg:hidden"
            type="button"
            aria-label="Open navigation"
            [attr.aria-expanded]="mobileMenuOpen()"
            (click)="openMobileNavigation()"
          >
            <fa-icon [icon]="menuIcon" />
          </button>
          <div class="min-w-0 flex-1">
            <p class="text-sm text-[#7c7284]">{{ languageService.t('app.school') }}</p>
            <h1 class="truncate text-base font-semibold text-[#32124f] sm:text-xl">{{ languageService.t('app.headerTitle') }}</h1>
          </div>
          <div class="flex shrink-0 items-center justify-end">
            <div
              class="flex h-10 items-center rounded-full border border-[#e9e3ef] bg-[#f7f5fa] p-1 shadow-[0_8px_24px_rgba(50,18,79,0.08)]"
              role="group"
              aria-label="Language"
            >
              <button
                class="h-8 rounded-full px-2.5 text-xs font-bold transition sm:px-4"
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
                class="h-8 rounded-full px-2.5 text-xs font-bold transition sm:px-4"
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

        <main class="relative min-h-[calc(100vh-73px)] px-4 py-5 sm:px-6 sm:py-7 lg:px-8">
          @if (apiLoading.loading()) {
            <div
              class="absolute inset-0 z-40 grid min-h-[calc(100vh-73px)] place-items-center bg-[#f6f4ef] px-6"
              role="status"
              aria-live="polite"
              aria-atomic="true"
            >
              <div class="text-center">
                <span class="mx-auto block h-12 w-12 animate-spin rounded-full border-4 border-[#e5ddea] border-t-[#6d3a91]"></span>
                <p class="mt-4 text-sm font-semibold text-[#5f5368]">
                  {{ languageService.t('common.loading') }}
                </p>
              </div>
            </div>
          }
          <div [attr.aria-busy]="apiLoading.loading()">
            <router-outlet />
          </div>
        </main>
      </div>
    </div>
  `,
  styles: `
    .layout-shell {
      display: grid;
      grid-template-columns: minmax(0, 1fr);
      position: relative;
      isolation: isolate;
    }

    .layout-shell {
      transition: grid-template-columns 180ms ease, padding 180ms ease;
    }

    .sidebar {
      position: fixed;
      inset: 0 auto 0 0;
      z-index: 60;
      width: min(82vw, 20rem);
      overflow-y: auto;
      transform: translateX(-105%);
      transition: transform 180ms ease, padding 180ms ease;
    }

    .sidebar.mobile-open {
      transform: translateX(0);
    }

    .sidebar nav {
      scrollbar-color: rgb(255 255 255 / 0.28) transparent;
      scrollbar-width: thin;
    }

    .sidebar nav::-webkit-scrollbar {
      width: 5px;
    }

    .sidebar nav::-webkit-scrollbar-track {
      background: transparent;
    }

    .sidebar nav::-webkit-scrollbar-thumb {
      border-radius: 999px;
      background: rgb(255 255 255 / 0.28);
    }

    .mobile-nav-backdrop {
      position: fixed;
      inset: 0;
      z-index: 50;
      border: 0;
      background: rgb(36 24 47 / 0.5);
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
        display: block;
      }

      .layout-shell::before {
        position: absolute;
        inset: 0 auto 0 0;
        z-index: 0;
        width: 280px;
        background: #32124f;
        content: '';
        pointer-events: none;
      }

      .layout-shell.sidebar-collapsed {
        grid-template-columns: none;
      }

      .layout-shell.sidebar-collapsed::before {
        width: 88px;
      }

      .content-column {
        position: relative;
        z-index: 1;
        min-height: 100vh;
        margin-left: 280px;
        background: #f6f4ef;
        transition: margin-left 180ms ease;
      }

      .layout-shell.sidebar-collapsed .content-column {
        margin-left: 88px;
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

      .sidebar {
        position: fixed;
        top: 0;
        right: auto;
        bottom: 0;
        left: 0;
        width: 280px;
        height: auto;
        min-height: 0;
        max-height: none;
        transform: none;
      }

      .sidebar-compact {
        width: 88px;
      }
    }
  `,
})
export class MainLayoutComponent {
  readonly sidebarCollapsed = signal(false);
  readonly mobileMenuOpen = signal(false);
  readonly menuIcon = faBars;
  readonly signOutIcon = faRightFromBracket;
  readonly navSections: NavSection[] = [
    {
      titleKey: 'navSection.overview',
      items: [{ path: '/dashboard', labelKey: 'nav.dashboard', icon: faGaugeHigh }],
    },
    {
      titleKey: 'navSection.students',
      items: [
        { path: '/students', labelKey: 'nav.students', icon: faUserGraduate },
        { path: '/transcripts', labelKey: 'nav.transcripts', icon: faFileLines },
        { path: '/at-risk', labelKey: 'nav.atRisk', icon: faTriangleExclamation },
        { path: '/commendations', labelKey: 'nav.commendations', icon: faAward },
      ],
    },
    {
      titleKey: 'navSection.tools',
      items: [
        { path: '/reports', labelKey: 'nav.reports', icon: faChartColumn },
        { path: '/workspace', labelKey: 'nav.workspace', icon: faTableColumns },
        { path: '/imports', labelKey: 'nav.imports', icon: faFileExcel },
        { path: '/forms', labelKey: 'nav.forms', icon: faClipboardList },
      ],
    },
    {
      titleKey: 'navSection.data',
      items: [
        { path: '/behaviours', labelKey: 'nav.behaviours', icon: faAward },
        { path: '/projects', labelKey: 'nav.projects', icon: faClipboardList },
        { path: '/bus-attendance', labelKey: 'nav.busAttendance', icon: faTableColumns },
        { path: '/mental-health', labelKey: 'nav.mentalHealth', icon: faKitMedical },
        { path: '/health-visits', labelKey: 'nav.healthVisits', icon: faKitMedical },
      ],
    },
    {
      titleKey: 'navSection.administration',
      items: [
        { path: '/admin/accounts', labelKey: 'nav.accounts', icon: faUsersGear, adminOnly: true },
        {
          path: '/admin/sync-history',
          labelKey: 'nav.syncHistory',
          icon: faClockRotateLeft,
          adminOnly: true,
        },
      ],
    },
  ];

  constructor(
    readonly authService: AuthenticationService,
    readonly languageService: LanguageService,
    readonly apiLoading: ApiLoadingService,
  ) {}

  visibleItems(section: NavSection): NavItem[] {
    const isAdmin = this.authService.getUserRole() === 'Admin';
    return section.items.filter((item) => !item.adminOnly || isAdmin);
  }

  roleLabel(role: string | null): string {
    if (!role) {
      return this.languageService.t('app.roleFallback');
    }
    const key = role === 'Principal' ? 'SchoolAdministration' : role;
    return this.languageService.t(`accounts.roles.${key}`);
  }

  toggleSidebar(): void {
    if (typeof window !== 'undefined' && window.matchMedia('(max-width: 1023px)').matches) {
      this.mobileMenuOpen.update((open) => !open);
      return;
    }
    this.sidebarCollapsed.update((collapsed) => !collapsed);
  }

  closeMobileNavigation(): void {
    this.mobileMenuOpen.set(false);
  }

  openMobileNavigation(): void {
    this.sidebarCollapsed.set(false);
    this.mobileMenuOpen.set(true);
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
