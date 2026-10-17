import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { BASE_PATH } from '../api/variables';

export type MealReportType = 'ordered-no-ticket' | 'ordered-not-eaten' | 'no-order' | 'coffee-frequency';
export type MealGroupBy = 'student' | 'week' | 'month' | 'grade' | 'class';
export type MealTypeFilter = 'breakfast' | 'lunch' | 'snack';

export interface MealReportRow {
  key: string;
  label: string;
  studentProfileId?: number | null;
  erpStudentId?: string | null;
  grade?: string | null;
  className?: string | null;
  periodStart?: string | null;
  latestDay?: string | null;
  count: number;
  units: number;
  studentCount: number;
}

export interface MealReportPage {
  items: MealReportRow[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  totalEvents: number;
  totalUnits: number;
  totalStudents: number;
  builtAtUtc: string;
}

export interface MealReportOptions {
  grades: string[];
  classes: string[];
}

export interface MealReportDetail {
  id: number;
  studentProfileId?: number | null;
  erpStudentId: string;
  studentName: string;
  grade?: string | null;
  className?: string | null;
  eventDay: string;
  meal?: string | null;
  orderStatus?: string | null;
  ticketStatus?: string | null;
  productCode?: string | null;
  productName?: string | null;
  quantity: number;
}

export interface MealReportDetailsPage {
  items: MealReportDetail[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface MealReportQuery {
  report: Exclude<MealReportType, 'ordered-not-eaten'>;
  groupBy: MealGroupBy;
  from: string;
  to: string;
  meal?: MealTypeFilter;
  grade?: string;
  className?: string;
  search?: string;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class ErpMealReportsService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  options(from: string, to: string) {
    return this.http.get<MealReportOptions>(`${this.basePath}/api/reports/student-meals/options`, {
      params: new HttpParams().set('from', from).set('to', to),
    });
  }

  summary(query: MealReportQuery, exportMode = false) {
    let params = new HttpParams()
      .set('report', query.report).set('groupBy', query.groupBy)
      .set('from', query.from).set('to', query.to)
      .set('page', query.page).set('pageSize', query.pageSize);
    if (exportMode) params = params.set('export', true);
    if (query.meal) params = params.set('meal', query.meal);
    if (query.grade) params = params.set('grade', query.grade);
    if (query.className) params = params.set('className', query.className);
    if (query.search) params = params.set('search', query.search);
    return this.http.get<MealReportPage>(`${this.basePath}/api/reports/student-meals/summary`, { params });
  }

  details(query: MealReportQuery, key: string) {
    let params = new HttpParams()
      .set('report', query.report).set('groupBy', query.groupBy).set('key', key)
      .set('from', query.from).set('to', query.to)
      .set('page', query.page).set('pageSize', query.pageSize);
    if (query.meal) params = params.set('meal', query.meal);
    if (query.grade) params = params.set('grade', query.grade);
    if (query.className) params = params.set('className', query.className);
    if (query.search) params = params.set('search', query.search);
    return this.http.get<MealReportDetailsPage>(`${this.basePath}/api/reports/student-meals/details`, { params });
  }
}
