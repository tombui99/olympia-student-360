import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BASE_PATH } from '../api/variables';

export interface SubjectSupportCohort {
  grade: string;
  homeroom: string;
  students: number;
  atRisk: number;
  averageCanvasScorePercentage: number | null;
  scoredStudents: number;
  noScoreStudents: number;
}
export interface SubjectSupportSummary {
  periodYear: string;
  periodTerm: string;
  subjectId: number | null;
  subjectName: string | null;
  lowScoreThreshold: number;
  subjects: Array<{ id: number; name: string }>;
  gradeDistribution: SubjectSupportCohort[];
  classDistribution: SubjectSupportCohort[];
}
export interface SubjectSupportStudent {
  id: number;
  sisStudentCode: string | null;
  sisUserId: string;
  fullName: string;
  gradeLevel: string | null;
  homeroom: string | null;
  averageCanvasScorePercentage: number | null;
  needsSupport: boolean;
}
export interface SubjectSupportStudentPage {
  items: SubjectSupportStudent[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}
export interface SubjectSupportStudentQuery {
  subjectId: number;
  year: string;
  term: string;
  grade: string;
  homeroom: string;
  supportOnly: boolean;
  search: string;
  page: number;
  pageSize: number;
}
@Injectable({ providedIn: 'root' })
export class SubjectSupportReportsService {
  private readonly http = inject(HttpClient);
  private readonly url = `${inject(BASE_PATH)}/api/reports/subject-support`;
  summary(year: string, term: string, subjectId: number | null): Observable<SubjectSupportSummary> {
    let params = new HttpParams().set('term', term);
    if (year) params = params.set('year', year);
    if (subjectId != null) params = params.set('subjectId', subjectId);
    return this.http.get<SubjectSupportSummary>(this.url, { params });
  }
  students(query: SubjectSupportStudentQuery): Observable<SubjectSupportStudentPage> {
    return this.http.get<SubjectSupportStudentPage>(`${this.url}/students`, { params: new HttpParams({ fromObject: { ...query } }) });
  }
}
