import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BASE_PATH } from '../api/variables';

export type LearningReportKey =
  | 'missing-frequency' | 'homework-achievement' | 'reading-completion'
  | 'grading-coverage';

export interface LearningReportQuery {
  reportKey: LearningReportKey;
  academicYear: string;
  term: string;
  month: number | null;
  courseIds: number[];
  assignmentIds: number[];
  assignmentGroupNames: string[];
  maxMissing: number;
  minScore: number;
  minCompletion: number;
  page: number;
  pageSize?: number;
  export?: boolean;
}

export interface LearningCourseOption { id: number; name: string; }
export interface LearningAssignmentOption { id: number; courseId: number; name: string; }
export interface LearningReportOptions { courses: LearningCourseOption[]; assignmentGroups: string[]; }
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

  options(): Observable<LearningReportOptions> {
    return this.http.get<LearningReportOptions>(`${this.url}/options`);
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
