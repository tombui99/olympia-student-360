import { Injectable, signal } from '@angular/core';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { catchError, map, Observable, of, tap } from 'rxjs';
import { AuthService as ApiAuthService } from '../../api/api/auth.service';
import {
  AuthResponseDto,
  ChangePasswordDto,
  CurrentUserDto,
  LoginDto,
  UserAccessCapabilitiesDto,
} from '../../api/model/models';

@Injectable({ providedIn: 'root' })
export class AuthenticationService {
  private readonly tokenKey = 'olympia_student_360_token';
  private readonly roleKey = 'olympia_student_360_role';
  private readonly usernameKey = 'olympia_student_360_username';
  private readonly fullNameKey = 'olympia_student_360_fullname';
  private readonly accessKey = 'olympia_student_360_access';

  private readonly fullNameSignal = signal<string | null>(this.getFullName());
  readonly fullName = this.fullNameSignal.asReadonly();
  private readonly roleSignal = signal<string | null>(this.getUserRole());
  readonly role = this.roleSignal.asReadonly();
  private readonly accessSignal = signal<UserAccessCapabilitiesDto>(this.getAccess());
  readonly access = this.accessSignal.asReadonly();
  private readonly usernameSignal = signal<string | null>(this.getUsername());
  readonly username = this.usernameSignal.asReadonly();
  private readonly authenticatedSignal = signal(!!this.getToken());
  private currentUserLoaded = false;

  constructor(
    private readonly apiAuthService: ApiAuthService,
    private readonly router: Router,
  ) {}

  login(credentials: LoginDto) {
    return this.apiAuthService.apiAuthLoginPost(credentials).pipe(
      tap((response) => {
        this.applyUserState(response);
        this.currentUserLoaded = true;
      }),
    );
  }

  changePassword(credentials: ChangePasswordDto) {
    return this.apiAuthService.apiAuthChangePasswordPost(credentials);
  }

  logout() {
    this.apiAuthService.apiAuthLogoutPost().subscribe({
      next: () => undefined,
      error: () => undefined,
    });
    this.clearUserState();
    this.router.navigate(['/login']);
  }

  ensureCurrentUser(): Observable<boolean> {
    if (this.authenticatedSignal() && this.currentUserLoaded) {
      return of(true);
    }

    return this.apiAuthService.apiAuthMeGet().pipe(
      tap((user) => {
        this.applyUserState(user);
        this.currentUserLoaded = true;
      }),
      map(() => true),
      catchError((error: HttpErrorResponse) => {
        if (error.status === 401 || error.status === 403) {
          this.clearUserState();
          return of(false);
        }
        // A gateway or network outage does not invalidate the saved session.
        // Keep it and leave currentUserLoaded false so the next navigation retries.
        return of(this.isAuthenticated());
      }),
    );
  }

  isAuthenticated(): boolean {
    return !!this.getToken();
  }

  getToken(): string | null {
    return localStorage.getItem(this.tokenKey);
  }

  getUserRole(): string | null {
    return localStorage.getItem(this.roleKey);
  }

  getFullName(): string | null {
    return localStorage.getItem(this.fullNameKey);
  }

  getUsername(): string | null {
    return localStorage.getItem(this.usernameKey);
  }

  canAccess(capability: 'academic' | 'health' | 'bus' | 'workspace' | 'atRisk' | 'commendations' | 'support' | 'forms'): boolean {
    if (this.getUserRole() !== 'Teacher') return true;
    const access = this.accessSignal();
    return {
      academic: access.canAccessAcademic,
      health: access.canAccessHealth,
      bus: access.canAccessBus,
      workspace: access.canAccessWorkspace,
      atRisk: access.canAccessAtRisk,
      commendations: access.canAccessCommendations,
      support: access.canAccessSupport ?? (access.canAccessAcademic || access.canAccessHealth),
      forms: access.canAccessForms ?? (access.canAccessAcademic || access.canAccessHealth || access.canAccessBus),
    }[capability];
  }

  canManageForms(): boolean {
    if (this.getUserRole() !== 'Teacher') return true;
    const access = this.accessSignal();
    return access.canManageForms ?? (access.hasAll || access.scopes.some((scope) =>
      ['Academic', 'Health', 'Bus'].includes(scope)));
  }

  private getAccess(): UserAccessCapabilitiesDto {
    try {
      const stored = localStorage.getItem(this.accessKey);
      if (stored) return JSON.parse(stored) as UserAccessCapabilitiesDto;
    } catch { /* use backward-compatible default */ }
    return {
      isTeacher: false, hasAll: true, canAccessAcademic: true, canAccessHealth: true,
      canAccessBus: true, canAccessWorkspace: true, canAccessAtRisk: true,
      canAccessCommendations: true, restrictsClasses: false, scopes: ['All'], homerooms: [],
      schoolLevels: [], restrictsStudents: false, canAccessSupport: true,
      canAccessForms: true, canManageForms: true,
    };
  }

  private applyUserState(response: AuthResponseDto | CurrentUserDto) {
    if ('token' in response) {
      localStorage.setItem(this.tokenKey, response.token);
    }
    localStorage.setItem(this.roleKey, response.role);
    localStorage.setItem(this.usernameKey, response.username);
    localStorage.setItem(this.fullNameKey, response.fullName);
    localStorage.setItem(this.accessKey, JSON.stringify(response.access));

    this.fullNameSignal.set(response.fullName);
    this.roleSignal.set(response.role);
    this.accessSignal.set(response.access);
    this.usernameSignal.set(response.username);
    this.authenticatedSignal.set(true);
  }

  private clearUserState() {
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.roleKey);
    localStorage.removeItem(this.usernameKey);
    localStorage.removeItem(this.fullNameKey);
    localStorage.removeItem(this.accessKey);
    this.fullNameSignal.set(null);
    this.roleSignal.set(null);
    this.accessSignal.set(this.getAccess());
    this.usernameSignal.set(null);
    this.authenticatedSignal.set(false);
    this.currentUserLoaded = false;
  }
}
