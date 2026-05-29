import { Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { tap } from 'rxjs';
import { AuthService as ApiAuthService } from '../../api/api/auth.service';
import { AuthResponseDto, LoginDto } from '../../api/model/models';

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

  constructor(
    private readonly apiAuthService: ApiAuthService,
    private readonly router: Router,
  ) {}

  login(credentials: LoginDto) {
    return this.apiAuthService.apiAuthLoginPost(credentials).pipe(
      tap((response) => {
        this.applyUserState(response);
      }),
    );
  }

  logout() {
    localStorage.removeItem(this.tokenKey);
    localStorage.removeItem(this.roleKey);
    localStorage.removeItem(this.usernameKey);
    localStorage.removeItem(this.fullNameKey);
    this.fullNameSignal.set(null);
    this.roleSignal.set(null);
    this.router.navigate(['/login']);
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

  private applyUserState(response: AuthResponseDto) {
    localStorage.setItem(this.tokenKey, response.token);
    localStorage.setItem(this.roleKey, response.role);
    localStorage.setItem(this.usernameKey, response.username);
    localStorage.setItem(this.fullNameKey, response.fullName);

    this.fullNameSignal.set(response.fullName);
    this.roleSignal.set(response.role);
  }
}
