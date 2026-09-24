import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { BASE_PATH } from '../api/variables';

export type BehaviourCategory = 'Positive' | 'Concerning' | 'Other';
export type BehaviourDrilldownDimension = 'all' | 'category' | 'type' | 'grade' | 'student';

export interface BehaviourTypeSummary {
  behaviourType: string;
  category: BehaviourCategory;
  recordCount: number;
  studentCount: number;
  sharePercentage: number;
}

export interface BehaviourGradeSummary {
  grade: string;
  recordCount: number;
  studentCount: number;
  positiveCount: number;
  concerningCount: number;
  otherCount: number;
}

export interface BehaviourStudentSummary {
  key: string;
  studentProfileId?: number | null;
  sisUserId?: string | null;
  studentName: string;
  grade?: string | null;
  homeroom?: string | null;
  recordCount: number;
  positiveCount: number;
  concerningCount: number;
  otherCount: number;
}

export interface BehaviourReportSummary {
  totalRecords: number;
  studentCount: number;
  behaviourTypeCount: number;
  positiveCount: number;
  concerningCount: number;
  otherCount: number;
  topTypes: BehaviourTypeSummary[];
  byGrade: BehaviourGradeSummary[];
  byStudent: BehaviourStudentSummary[];
}

export interface BehaviourReportDetail {
  id: number;
  studentProfileId?: number | null;
  sisUserId?: string | null;
  studentName: string;
  grade?: string | null;
  homeroom?: string | null;
  incidentDate: string;
  behaviourType: string;
  category: BehaviourCategory;
  notes?: string | null;
  subject?: string | null;
  decision?: string | null;
  recordedBy?: string | null;
}

export interface BehaviourReportDetailsPage {
  items: BehaviourReportDetail[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface BehaviourReportDetailsQuery {
  dimension: BehaviourDrilldownDimension;
  value?: string;
  studentProfileId?: number;
  studentKey?: string;
  search?: string;
}

@Injectable({ providedIn: 'root' })
export class BehaviourReportsService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  summary(academicYear: string, term: string) {
    return this.http.get<BehaviourReportSummary>(
      `${this.basePath}/api/reports/behaviours/summary`,
      { params: this.periodParams(academicYear, term) },
    );
  }

  details(
    academicYear: string,
    term: string,
    query: BehaviourReportDetailsQuery,
    page: number,
    pageSize: number,
  ) {
    let params = this.periodParams(academicYear, term)
      .set('dimension', query.dimension)
      .set('page', page)
      .set('pageSize', pageSize);
    if (query.value) params = params.set('value', query.value);
    if (query.studentProfileId != null) params = params.set('studentProfileId', query.studentProfileId);
    if (query.studentKey) params = params.set('studentKey', query.studentKey);
    if (query.search?.trim()) params = params.set('search', query.search.trim());
    return this.http.get<BehaviourReportDetailsPage>(
      `${this.basePath}/api/reports/behaviours/details`,
      { params },
    );
  }

  private periodParams(academicYear: string, term: string): HttpParams {
    let params = new HttpParams().set('academicYear', academicYear);
    if (term && term !== 'all') params = params.set('term', term);
    return params;
  }
}
