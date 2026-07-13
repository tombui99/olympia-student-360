import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BASE_PATH } from '../api/variables';

export type RiskLevel = 'None' | 'Low' | 'Medium' | 'High';

export interface RiskFlag {
  code: string;
  severity: 'Low' | 'Medium' | 'High';
  value: number;
}

export interface StudentListItem {
  id: number;
  studentCode: string;
  fullName: string;
  gradeLevel?: string | null;
  homeroom?: string | null;
  teacherName?: string | null;
  behaviourCount: number;
  projectCount: number;
  riskLevel: RiskLevel;
  riskFlagCount: number;
}

export interface AtRiskStudent extends StudentListItem {
  flags: RiskFlag[];
}

export interface AtRiskStudentsPage {
  items: AtRiskStudent[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  highRiskCount: number;
  mediumRiskCount: number;
}

export interface StudentCommendation {
  id: number;
  studentId: number;
  studentCode: string;
  studentName: string;
  gradeLevel?: string | null;
  homeroom?: string | null;
  teacherName?: string | null;
  incidentDate: string;
  commendationType: string;
  notes?: string | null;
  recordedByName?: string | null;
}

export interface StudentCommendationsPage {
  items: StudentCommendation[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  totalCommendationCount: number;
  studentsRecognisedCount: number;
  recentCount: number;
  availableGrades: string[];
}

export interface CohortStats {
  key: string;
  label: string;
  studentCount: number;
  atRiskCount: number;
  highRiskCount: number;
  totalBehaviours: number;
  totalProjects: number;
  avgBehaviours: number;
}

export interface CohortsResponse {
  grades: CohortStats[];
  homerooms: CohortStats[];
}

export interface StudentSummary {
  totalBehaviours: number;
  behavioursLast30Days: number;
  totalProjects: number;
  activeProjects: number;
}

export interface StudentBehaviour {
  id: number;
  incidentDate: string;
  behaviourType: string;
  notes?: string | null;
  recordedByName?: string | null;
}

export interface StudentProject {
  id: number;
  projectName: string;
  subject?: string | null;
  status: string;
  teacherNotes?: string | null;
  createdAt: string;
}

export interface StudentProfileDetail {
  id: number;
  studentCode: string;
  fullName: string;
  gradeLevel?: string | null;
  homeroom?: string | null;
  teacherName?: string | null;
  riskLevel: RiskLevel;
  flags: RiskFlag[];
  summary: StudentSummary;
  behaviours: StudentBehaviour[];
  projects: StudentProject[];
}

@Injectable({ providedIn: 'root' })
export class StudentsService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  list(): Observable<StudentListItem[]> {
    return this.http.get<StudentListItem[]>(`${this.basePath}/api/students`);
  }

  atRisk(page = 1, pageSize = 10): Observable<AtRiskStudentsPage> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<AtRiskStudentsPage>(`${this.basePath}/api/students/at-risk`, { params });
  }

  commendations(
    page = 1,
    pageSize = 10,
    search = '',
    grade = '',
  ): Observable<StudentCommendationsPage> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) {
      params = params.set('search', search.trim());
    }
    if (grade) {
      params = params.set('grade', grade);
    }
    return this.http.get<StudentCommendationsPage>(`${this.basePath}/api/students/commendations`, { params });
  }

  cohorts(): Observable<CohortsResponse> {
    return this.http.get<CohortsResponse>(`${this.basePath}/api/students/cohorts`);
  }

  get(id: number): Observable<StudentProfileDetail> {
    return this.http.get<StudentProfileDetail>(`${this.basePath}/api/students/${id}`);
  }
}
