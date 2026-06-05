import { Component } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { FontAwesomeModule } from '@fortawesome/angular-fontawesome';
import {
  faChartLine,
  faClockRotateLeft,
  faFileExcel,
  faGaugeHigh,
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
          @for (item of navItems; track item.path) {
            @if (!item.adminOnly || authService.getUserRole() === 'Admin') {
              <a
                [routerLink]="item.path"
                routerLinkActive="nav-link-active"
                class="nav-link flex items-center gap-3 rounded-md px-4 py-3 text-sm font-semibold transition"
              >
                <fa-icon class="nav-icon" [icon]="item.icon" />
                <span>{{ languageService.t(item.labelKey) }}</span>
              </a>
            }
          }
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
  styles: `
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
  `,
})
export class MainLayoutComponent {
  readonly navItems: NavItem[] = [
    { path: '/dashboard', labelKey: 'nav.dashboard', icon: faGaugeHigh },
    { path: '/reports', labelKey: 'nav.reports', icon: faChartLine },
    { path: '/imports', labelKey: 'nav.imports', icon: faFileExcel },
    { path: '/admin/accounts', labelKey: 'nav.accounts', icon: faUsersGear, adminOnly: true },
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
