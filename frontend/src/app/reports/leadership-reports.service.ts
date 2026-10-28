import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { BASE_PATH } from '../api/variables';

export type LeadershipReportKey = 'learning-engagement' | 'assessment-results';
export type AssessmentCategory = 'all' | 'HS1' | 'HS2' | 'HS3' | 'GPA';

export interface LeadershipSubaccountOption {
  id: number;
  name: string;
  path: string;
  accountIds: number[];
  gradeLevels: string[];
}

export interface GradeHomeroomOption {
  grade: string;
  homeroom: string;
}

export interface LeadershipReportOptions {
  academicYears: string[];
  terms: string[];
  grades: string[];
  homerooms: string[];
  cohorts: GradeHomeroomOption[];
  subaccounts: LeadershipSubaccountOption[];
  assessmentCategories: string[];
}

export interface LeadershipReportQuery {
  reportKey: LeadershipReportKey;
  academicYear: string;
  term: string;
  month: number | null;
  weekStart: string | null;
  grade: string;
  homeroom: string;
  subaccountIds: number[];
  assessmentCategory: AssessmentCategory;
  page: number;
  pageSize: number;
  export?: boolean;
  includeSummary?: boolean;
}

export interface LeadershipReportSummary {
  studentCount: number;
  courseCount: number;
  activityCount: number;
  submitted: number;
  missing: number;
  late: number;
  graded: number;
  averageScorePercent: number | null;
  completionRatePercent: number | null;
  gradingRatePercent: number | null;
}

export interface LeadershipBreakdownRow {
  key: string;
  label: string;
  count: number;
  studentCount: number;
  courseCount: number;
  submitted: number;
  missing: number;
  late: number;
  graded: number;
  averageScorePercent: number | null;
  completionRatePercent: number | null;
  gradingRatePercent: number | null;
}

export interface LeadershipReportBreakdowns {
  byGrade: LeadershipBreakdownRow[];
  byHomeroom: LeadershipBreakdownRow[];
  bySubaccount: LeadershipBreakdownRow[];
  byCourse: LeadershipBreakdownRow[];
  byAssessmentCategory: LeadershipBreakdownRow[];
  byMonth: LeadershipBreakdownRow[];
}

export interface LeadershipReportRow {
  studentId: number | null;
  sisUserId: string | null;
  name: string;
  grade: string | null;
  homeroom: string | null;
  courseId: number;
  courseName: string;
  subaccountId: number | null;
  subaccountName: string | null;
  assessmentCategory: string | null;
  assignmentGroupName: string | null;
  activityCount: number;
  submitted: number;
  missing: number;
  late: number;
  graded: number;
  averageScorePercent: number | null;
  scoreSum: number;
  scoreCount: number;
  month: string | null;
}

export interface LeadershipFinalScoreBreakdown {
  key: string;
  label: string;
  studentCount: number;
  courseCount: number;
  scoreCount: number;
  averageScorePercent: number | null;
}

export interface LeadershipFinalCourseScores {
  scoreSource: 'canvas-course-final-score-percent';
  summary: {
    studentCount: number;
    courseCount: number;
    scoreCount: number;
    averageScorePercent: number | null;
  };
  byCourse: LeadershipFinalScoreBreakdown[];
  byGrade: LeadershipFinalScoreBreakdown[];
  byHomeroom: LeadershipFinalScoreBreakdown[];
  byMonth: LeadershipFinalScoreBreakdown[];
}

export interface LeadershipReportResult {
  reportKey: LeadershipReportKey;
  aggregateAvailable: boolean;
  notice: string | null;
  scoreSource: string;
  summary: LeadershipReportSummary;
  breakdowns: LeadershipReportBreakdowns;
  finalCourseScores: LeadershipFinalCourseScores | null;
  rows: LeadershipReportRow[];
  totalCount: number;
  page: number;
  pageSize: number;
}

@Injectable({ providedIn: 'root' })
export class LeadershipReportsService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);
  private readonly url = `${this.basePath}/api/leadership-reports`;
  private readonly optionsRequest = this.http.get<LeadershipReportOptions>(`${this.url}/options`).pipe(
    shareReplay({ bufferSize: 1, refCount: false }),
  );

  options(): Observable<LeadershipReportOptions> {
    return this.optionsRequest;
  }

  run(query: LeadershipReportQuery): Observable<LeadershipReportResult> {
    return this.http.post<LeadershipReportResult>(`${this.url}/run`, query);
  }
}
