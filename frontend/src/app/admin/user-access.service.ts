import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { BASE_PATH } from '../api/variables';

export interface TeacherAccessUser {
  userId: string;
  username: string;
  email?: string | null;
  fullName: string;
  scopes: string[];
  homerooms: string[];
  schoolLevels: string[];
  gradeLevels?: string[];
}

export interface UserAccessManagement {
  users: TeacherAccessUser[];
  availableScopes: string[];
  availableHomerooms: string[];
  availableSchoolLevels: string[];
  availableGradeLevels?: string[];
}

@Injectable({ providedIn: 'root' })
export class UserAccessService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  load(): Observable<UserAccessManagement> {
    return this.http.get<UserAccessManagement>(`${this.basePath}/api/user-access`);
  }

  update(userId: string, scopes: string[], homerooms: string[], schoolLevels: string[], gradeLevels: string[] = []): Observable<TeacherAccessUser> {
    return this.http.put<TeacherAccessUser>(`${this.basePath}/api/user-access/${userId}`, {
      scopes,
      homerooms,
      schoolLevels,
      gradeLevels,
    });
  }
}
