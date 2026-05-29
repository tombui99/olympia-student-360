import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { BASE_PATH } from '../variables';
import { AdminUserDto, CreateAdminUserDto, UpdateAdminUserDto } from '../model/models';

@Injectable({ providedIn: 'root' })
export class AdminUsersService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  apiAdminUsersGet(): Observable<AdminUserDto[]> {
    return this.http.get<AdminUserDto[]>(`${this.basePath}/api/admin/users`);
  }

  apiAdminUsersRolesGet(): Observable<string[]> {
    return this.http.get<string[]>(`${this.basePath}/api/admin/users/roles`);
  }

  apiAdminUsersPost(payload: CreateAdminUserDto): Observable<AdminUserDto> {
    return this.http.post<AdminUserDto>(`${this.basePath}/api/admin/users`, payload);
  }

  apiAdminUsersIdPut(id: string, payload: UpdateAdminUserDto): Observable<AdminUserDto> {
    return this.http.put<AdminUserDto>(`${this.basePath}/api/admin/users/${id}`, payload);
  }

  apiAdminUsersIdDelete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.basePath}/api/admin/users/${id}`);
  }
}
