import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { BASE_PATH } from '../variables';
import { AuthResponseDto, CurrentUserDto, LoginDto } from '../model/models';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  apiAuthLoginPost(loginDto: LoginDto): Observable<AuthResponseDto> {
    return this.http.post<AuthResponseDto>(`${this.basePath}/api/Auth/login`, loginDto);
  }

  apiAuthMeGet(): Observable<CurrentUserDto> {
    return this.http.get<CurrentUserDto>(`${this.basePath}/api/Auth/me`);
  }
}
