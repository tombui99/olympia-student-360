import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BASE_PATH } from '../api/variables';

export type CareerGuidanceRecordType =
  | 'GeneralPlan'
  | 'UniversityApplication'
  | 'Preparation'
  | 'Internship'
  | 'Counseling';

export type CareerGuidanceStatus = 'Planned' | 'InProgress' | 'NeedsAttention' | 'Completed';

export interface CareerGuidanceRecord {
  id: number;
  studentId: number;
  studentName: string;
  sisUserId: string;
  gradeLevel?: string | null;
  homeroom?: string | null;
  recordType: CareerGuidanceRecordType;
  recordDate: string;
  academicYear: string;
  term?: string | null;
  title: string;
  status: CareerGuidanceStatus;
  targetCountries?: string | null;
  targetUniversities?: string | null;
  targetMajors?: string | null;
  careerInterests?: string | null;
  applicationProgressPercent?: number | null;
  preparationProgressPercent?: number | null;
  qualityRating?: number | null;
  activitiesSummary?: string | null;
  issues?: string | null;
  solutions?: string | null;
  studentSupport?: string | null;
  studentFeedback?: string | null;
  nextSteps?: string | null;
  recommendations?: string | null;
  proposals?: string | null;
  internshipCompany?: string | null;
  internshipRole?: string | null;
  internshipStartDate?: string | null;
  internshipEndDate?: string | null;
  internshipProgressPercent?: number | null;
  internshipQualityRating?: number | null;
  studentSelfAssessment?: string | null;
  hostAssessment?: string | null;
  finalProduct?: string | null;
  recordedByName: string;
  updatedByName?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface CareerGuidancePage {
  items: CareerGuidanceRecord[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  activeCount: number;
  inProgressCount: number;
  needsAttentionCount: number;
  completedCount: number;
  availableAcademicYears: string[];
}

export type SaveCareerGuidanceRecord = Omit<
  CareerGuidanceRecord,
  'id' | 'studentName' | 'sisUserId' | 'gradeLevel' | 'homeroom' | 'academicYear' | 'recordedByName' | 'updatedByName' | 'createdAt' | 'updatedAt'
> & { academicYear?: string | null };

@Injectable({ providedIn: 'root' })
export class CareerGuidanceService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  list(filters: {
    page?: number;
    pageSize?: number;
    search?: string;
    recordType?: string;
    status?: string;
    academicYear?: string;
    term?: string;
    studentId?: number;
  } = {}): Observable<CareerGuidancePage> {
    let params = new HttpParams()
      .set('page', filters.page ?? 1)
      .set('pageSize', filters.pageSize ?? 20);
    if (filters.search?.trim()) params = params.set('search', filters.search.trim());
    if (filters.recordType) params = params.set('recordType', filters.recordType);
    if (filters.status) params = params.set('status', filters.status);
    if (filters.academicYear) params = params.set('academicYear', filters.academicYear);
    if (filters.term) params = params.set('term', filters.term);
    if (filters.studentId) params = params.set('studentId', filters.studentId);
    return this.http.get<CareerGuidancePage>(`${this.basePath}/api/career-guidance`, { params });
  }

  forStudent(studentId: number, academicYear = '', term = 'all', page = 1, pageSize = 5): Observable<CareerGuidancePage> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (academicYear) params = params.set('academicYear', academicYear);
    if (term) params = params.set('term', term);
    return this.http.get<CareerGuidancePage>(`${this.basePath}/api/career-guidance/student/${studentId}`, { params });
  }

  create(model: SaveCareerGuidanceRecord): Observable<CareerGuidanceRecord> {
    return this.http.post<CareerGuidanceRecord>(`${this.basePath}/api/career-guidance`, model);
  }

  update(id: number, model: SaveCareerGuidanceRecord): Observable<CareerGuidanceRecord> {
    return this.http.put<CareerGuidanceRecord>(`${this.basePath}/api/career-guidance/${id}`, model);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.basePath}/api/career-guidance/${id}`);
  }
}
