import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { BASE_PATH } from '../api/variables';

export type HealthReportType = 'diagnoses' | 'frequent-students' | 'student-visits';
export type HealthReportGrouping = 'week' | 'month' | 'year';

export interface HealthReportRow {
  key: string;
  periodStart: string;
  periodEnd: string;
  visitId?: number | null;
  studentProfileId?: number | null;
  sisUserId?: string | null;
  studentName: string;
  className?: string | null;
  diagnosis?: string | null;
  classification?: string | null;
  visitCount: number;
  studentCount: number;
  latestVisit: string;
}

export interface HealthReportPage {
  items: HealthReportRow[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  totalVisits: number;
}

export interface HealthVisitReportDetail {
  id: number;
  studentProfileId?: number | null;
  sisUserId?: string | null;
  studentName: string;
  className?: string | null;
  examTime: string;
  classification?: string | null;
  diagnosis?: string | null;
  symptoms?: string | null;
  treatment?: string | null;
  medication?: string | null;
  notes?: string | null;
  notificationSent: boolean;
  examinedBy?: string | null;
  customFields: Record<string, string>;
}

export interface HealthVisitReportDetailsPage {
  items: HealthVisitReportDetail[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface HealthReportDetailsQuery {
  periodStart: string;
  periodEnd: string;
  visitId?: number;
  diagnosis?: string;
  studentProfileId?: number;
  sisUserId?: string;
  studentName?: string;
  className?: string;
}

@Injectable({ providedIn: 'root' })
export class HealthReportsService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  summary(
    report: HealthReportType,
    groupBy: HealthReportGrouping,
    academicYear: string,
    term: string,
    search: string,
    page: number,
    pageSize: number,
  ) {
    let params = new HttpParams()
      .set('report', report)
      .set('groupBy', groupBy)
      .set('academicYear', academicYear)
      .set('page', page)
      .set('pageSize', pageSize);
    if (term && term !== 'all') params = params.set('term', term);
    if (search.trim()) params = params.set('search', search.trim());
    return this.http.get<HealthReportPage>(`${this.basePath}/api/reports/health-visits/summary`, { params });
  }

  details(query: HealthReportDetailsQuery, page: number, pageSize: number) {
    let params = new HttpParams()
      .set('periodStart', query.periodStart)
      .set('periodEnd', query.periodEnd)
      .set('page', page)
      .set('pageSize', pageSize);
    if (query.visitId != null) params = params.set('visitId', query.visitId);
    if (query.diagnosis) params = params.set('diagnosis', query.diagnosis);
    if (query.studentProfileId != null) params = params.set('studentProfileId', query.studentProfileId);
    else if (query.sisUserId) params = params.set('sisUserId', query.sisUserId);
    else if (query.studentName) {
      params = params.set('studentName', query.studentName);
      if (query.className) params = params.set('className', query.className);
    }
    return this.http.get<HealthVisitReportDetailsPage>(`${this.basePath}/api/reports/health-visits/details`, { params });
  }
}
