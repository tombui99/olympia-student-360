import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { BASE_PATH } from '../variables';
import {
  AdminUserDto,
  AdminUserOptionsDto,
  AdminUserPageDto,
  CreateAdminUserDto,
  UpdateAdminUserDto,
} from '../model/models';

@Injectable({ providedIn: 'root' })
export class AdminUsersService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  apiAdminUsersGet(
    page: number,
    pageSize: number,
    search?: string,
    role?: string,
  ): Observable<AdminUserPageDto> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);
    if (search?.trim()) params = params.set('search', search.trim());
    if (role?.trim()) params = params.set('role', role.trim());
    return this.http.get<AdminUserPageDto>(`${this.basePath}/api/admin/users`, { params });
  }

  apiAdminUsersRolesGet(): Observable<string[]> {
    return this.http.get<string[]>(`${this.basePath}/api/admin/users/roles`);
  }

  apiAdminUsersOptionsGet(): Observable<AdminUserOptionsDto> {
    return this.http.get<AdminUserOptionsDto>(`${this.basePath}/api/admin/users/options`);
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
