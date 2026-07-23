import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BASE_PATH } from '../api/variables';

export interface HealthVisit {
  id: number;
  studentProfileId?: number | null;
  studentCode?: string | null;
  examTime: string;
  fullName: string;
  gender?: string | null;
  className?: string | null;
  symptoms?: string | null;
  diagnosis?: string | null;
  classification?: string | null;
  treatment?: string | null;
  medication?: string | null;
  examinedBy?: string | null;
}

export interface HealthVisitsPage {
  items: HealthVisit[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  availableClasses: string[];
  availableClassifications: string[];
}

export interface HealthVisitFilters {
  search: string;
  className: string;
  classification: string;
  from: string;
  to: string;
}

@Injectable({ providedIn: 'root' })
export class HealthVisitsService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  list(
    page: number,
    pageSize: number,
    filters: HealthVisitFilters,
  ): Observable<HealthVisitsPage> {
    let params = new HttpParams()
      .set('page', page)
      .set('pageSize', pageSize);

    if (filters.search.trim()) params = params.set('search', filters.search.trim());
    if (filters.className) params = params.set('className', filters.className);
    if (filters.classification) params = params.set('classification', filters.classification);
    if (filters.from) params = params.set('from', filters.from);
    if (filters.to) params = params.set('to', filters.to);

    return this.http.get<HealthVisitsPage>(`${this.basePath}/api/health-visits`, { params });
  }
}
