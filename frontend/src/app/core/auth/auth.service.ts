import { Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, map, Observable, of, tap } from 'rxjs';
import { AuthService as ApiAuthService } from '../../api/api/auth.service';
import { AuthResponseDto, CurrentUserDto, LoginDto } from '../../api/model/models';

@Injectable({ providedIn: 'root' })
export class AuthenticationService {
  private readonly tokenKey = 'olympia_student_360_token';
  private readonly roleKey = 'olympia_student_360_role';
  private readonly usernameKey = 'olympia_student_360_username';
  private readonly fullNameKey = 'olympia_student_360_fullname';

  private readonly fullNameSignal = signal<string | null>(this.getFullName());
  readonly fullName = this.fullNameSignal.asReadonly();
  private readonly roleSignal = signal<string | null>(this.getUserRole());
  readonly role = this.roleSignal.asReadonly();
  private readonly usernameSignal = signal<string | null>(this.getUsername());
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
      catchError(() => {
        this.clearUserState();
        this.currentUserLoaded = true;
        return of(false);
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

  private applyUserState(response: AuthResponseDto | CurrentUserDto) {
    if ('token' in response) {
      localStorage.setItem(this.tokenKey, response.token);
    }
    localStorage.setItem(this.roleKey, response.role);
    localStorage.setItem(this.usernameKey, response.username);
    localStorage.setItem(this.fullNameKey, response.fullName);

    this.fullNameSignal.set(response.fullName);
    this.roleSignal.set(response.role);
    this.usernameSignal.set(response.username);
    this.authenticatedSignal.set(true);
  }

  private clearUserState() {
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.roleKey);
    localStorage.removeItem(this.usernameKey);
    localStorage.removeItem(this.fullNameKey);
    this.fullNameSignal.set(null);
    this.roleSignal.set(null);
    this.usernameSignal.set(null);
    this.authenticatedSignal.set(false);
    this.currentUserLoaded = false;
  }
}
