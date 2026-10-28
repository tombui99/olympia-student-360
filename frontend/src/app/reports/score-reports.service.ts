import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable, shareReplay } from 'rxjs';
import { BASE_PATH } from '../api/variables';
import { AssessmentCategory, GradeHomeroomOption, LeadershipSubaccountOption } from './leadership-reports.service';

export type ScoreReportViewKey =
  | 'subject-grade-average'
  | 'subject-letter-distribution'
  | 'grade-letter-distribution'
  | 'class-letter-distribution'
  | 'score-histogram'
  | 'assessment-average'
  | 'current-course-score'
  | 'class-subject-distribution'
  | 'top-students'
  | 'bottom-students';

export type EnglishProgramme = 'ESL' | 'ELA';

export interface ScoreReportOptions {
  academicYears: string[];
  terms: string[];
  grades: string[];
  homerooms: string[];
  cohorts: GradeHomeroomOption[];
  subaccounts: LeadershipSubaccountOption[];
  assessmentCategories: string[];
  subjects: Array<{ code: string; name: string }>;
  sections: Array<{ name: string; subaccountIds: number[] }>;
  schoolLevels: string[];
}

export interface ScoreReportQuery {
  viewKey: ScoreReportViewKey;
  academicYear: string;
  term: string;
  month: number | null;
  weekStart: string | null;
  grade: string;
  homeroom: string;
  schoolLevel: string;
  canvasSectionId: number | null;
  canvasSectionName: string;
  subjectCode: string;
  canvasCourseId: number | null;
  subaccountIds: number[];
  assessmentCategory: AssessmentCategory;
  englishProgrammes: EnglishProgramme[];
  rankingLimit: number;
}

export interface ScoreReportRow {
  key: string;
  label: string;
  subjectCode: string | null;
  subjectName: string | null;
  grade: string | null;
  homeroom: string | null;
  canvasSectionId: number | null;
  sectionName: string | null;
  studentId: number | null;
  sisUserId: string | null;
  averageScore: number | null;
  scoreSum: number;
  scoreCount: number;
  studentCount: number;
  aPlus: number;
  a: number;
  b: number;
  c: number;
  d: number;
  cdPercent: number | null;
  binStart: number | null;
  binEnd: number | null;
}

export interface ScoreReportTotal {
  scoreSum: number;
  scoreCount: number;
  studentCount: number;
  averageScore: number | null;
  aPlus: number;
  a: number;
  b: number;
  c: number;
  d: number;
  cdPercent: number | null;
}

export interface ScoreReportResult {
  viewKey: ScoreReportViewKey;
  aggregateAvailable: boolean;
  notice: string | null;
  scoreSource: string;
  unit: 'score10' | 'percent';
  rows: ScoreReportRow[];
  total: ScoreReportTotal;
}

@Injectable({ providedIn: 'root' })
export class ScoreReportsService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);
  private readonly url = `${this.basePath}/api/score-reports`;
  private readonly optionsRequest = this.http.get<ScoreReportOptions>(`${this.url}/options`).pipe(
    shareReplay({ bufferSize: 1, refCount: false }),
  );

  options(): Observable<ScoreReportOptions> {
    return this.optionsRequest;
  }

  run(query: ScoreReportQuery): Observable<ScoreReportResult> {
    return this.http.post<ScoreReportResult>(`${this.url}/run`, query);
  }
}
