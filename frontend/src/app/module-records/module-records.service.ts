import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { BASE_PATH } from '../api/variables';

export interface ModuleRecord {
  id: number;
  moduleId: string;
  studentProfileId?: number | null;
  sisUserId: string;
  studentName: string;
  gradeLevel?: string | null;
  canvasGradeLevel?: string | null;
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

export interface SupplementalSummary {
  totalCount: number;
  distinctStudents: number;
  attentionCount: number;
  distribution: { label: string; records: number; students: number; share: number }[];
}

@Injectable({ providedIn: 'root' })
export class ModuleRecordsService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  list(
    moduleId: string,
    page: number,
    pageSize: number,
    search: string,
    academicYear?: string,
    reportingTerm?: string,
  ) {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    if (academicYear?.trim()) params = params.set('academicYear', academicYear.trim());
    if (reportingTerm?.trim() && reportingTerm !== 'all') {
      params = params.set('term', reportingTerm.trim());
    }
    return this.http.get<ModuleRecordsPage>(`${this.basePath}/api/module-records/${moduleId}`, { params });
  }

  supplementalSummary(moduleId: string, field: string, academicYear?: string, reportingTerm?: string) {
    let params = new HttpParams().set('moduleId', moduleId).set('field', field);
    if (academicYear?.trim()) params = params.set('academicYear', academicYear.trim());
    if (reportingTerm?.trim() && reportingTerm !== 'all') params = params.set('term', reportingTerm.trim());
    return this.http.get<SupplementalSummary>(`${this.basePath}/api/reports/supplemental/summary`, { params });
  }

  update(moduleId: string, id: number, values: Record<string, string | null>) {
    return this.http.put<void>(`${this.basePath}/api/module-records/${moduleId}/${id}`, { values });
  }

  delete(moduleId: string, id: number) {
    return this.http.delete<void>(`${this.basePath}/api/module-records/${moduleId}/${id}`);
  }
}
