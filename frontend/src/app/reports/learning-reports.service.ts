import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { BASE_PATH } from '../api/variables';

export type LearningReportKey =
  | 'missing-frequency' | 'homework-achievement' | 'reading-completion'
  | 'grading-coverage';

export interface LearningReportQuery {
  reportKey: LearningReportKey;
  academicYear: string;
  term: string;
  month: number | null;
  weekStart: string | null;
  courseIds: number[];
  subaccountIds: number[];
  assignmentIds: number[];
  assignmentGroupNames: string[];
  assessmentCategory: 'all' | 'HS1' | 'HS2' | 'HS3' | 'GPA';
  maxMissing: number;
  minScore: number;
  minCompletion: number;
  page: number;
  pageSize?: number;
  export?: boolean;
}

export interface LearningCourseOption { id: number; name: string; }
export interface LearningSubaccountOption {
  id: number;
  name: string;
  path: string;
  accountIds: number[];
  gradeLevels: string[];
}
export interface LearningAssignmentOption { id: number; courseId: number; name: string; }
export interface LearningReportOptions {
  courses: LearningCourseOption[];
  subaccounts: LearningSubaccountOption[];
  assignmentGroups: string[];
  assessmentCategories: Array<'all' | 'HS1' | 'HS2' | 'HS3' | 'GPA'>;
}
export interface LearningReportRow {
  studentId: number | null;
  sisUserId: string | null;
  name: string;
  grade: string | null;
  homeroom: string | null;
  missing: number;
  submitted: number;
  graded: number;
  score: number | null;
  rate: number | null;
  status: 'green' | 'amber' | 'red' | 'review';
}
export interface LearningReportResult {
  rows: LearningReportRow[];
  totalCount: number;
  aggregateAvailable: boolean;
  notice: string | null;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class LearningReportsService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);
  private readonly url = `${this.basePath}/api/learning-reports`;
  private readonly optionsRequest = this.http.get<LearningReportOptions>(`${this.url}/options`).pipe(
    shareReplay({ bufferSize: 1, refCount: false }),
  );

  options(): Observable<LearningReportOptions> {
    return this.optionsRequest;
  }
  assignments(courseIds: number[]): Observable<LearningAssignmentOption[]> {
    let params = new HttpParams();
    for (const id of courseIds) params = params.append('courseIds', id);
    return this.http.get<LearningAssignmentOption[]>(`${this.url}/assignments`, { params });
  }
  run(query: LearningReportQuery): Observable<LearningReportResult> {
    return this.http.post<LearningReportResult>(`${this.url}/run`, query);
  }
}
