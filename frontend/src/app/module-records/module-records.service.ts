import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { BASE_PATH } from '../api/variables';

export interface ModuleRecord {
  id: number;
  moduleId: string;
  studentProfileId: number;
  sisUserId: string;
  studentName: string;
  gradeLevel?: string | null;
  homeroom?: string | null;
  enteredByName?: string | null;
  createdAt: string;
  values: Record<string, string | null>;
}

export interface ModuleRecordsPage {
  items: ModuleRecord[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

@Injectable({ providedIn: 'root' })
export class ModuleRecordsService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  list(moduleId: string, page: number, pageSize: number, search: string) {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    return this.http.get<ModuleRecordsPage>(`${this.basePath}/api/module-records/${moduleId}`, { params });
  }

  update(moduleId: string, id: number, values: Record<string, string | null>) {
    return this.http.put<void>(`${this.basePath}/api/module-records/${moduleId}/${id}`, { values });
  }

  delete(moduleId: string, id: number) {
    return this.http.delete<void>(`${this.basePath}/api/module-records/${moduleId}/${id}`);
  }
}
