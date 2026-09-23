import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BASE_PATH } from '../api/variables';

export interface DashboardTrendPoint {
  year: number;
  month: number;
  total: number;
}

export interface DashboardGrade {
  grade: string;
  students: number;
  atRisk: number;
  averageCanvasScorePercentage?: number | null;
}

export interface DashboardClass {
  homeroom: string;
  grade: string;
  students: number;
  atRisk: number;
  averageCanvasScorePercentage?: number | null;
}

export interface DashboardAttentionStudent {
  id: number;
  sisUserId: string;
  fullName: string;
  gradeLevel: string | null;
  homeroom: string | null;
  riskLevel: 'High' | 'Medium' | 'None';
}

export interface DashboardOverview {
  periodYear: string;
  periodTerm: string;
  lastSuccessfulCanvasSyncAtUtc?: string | null;
  includedGradeLevels?: string[];
  totalStudents: number;
  atRiskTotal: number;
  atRiskHigh: number;
  atRiskMedium: number;
  onTrack: number;
  totalBehaviours: number;
  behavioursLast30Days: number;
  totalProjects: number;
  activeProjects: number;
  behaviourTrend: DashboardTrendPoint[];
  gradeDistribution: DashboardGrade[];
  classDistribution: DashboardClass[];
  topAtRisk: DashboardAttentionStudent[];
}

@Injectable({ providedIn: 'root' })
export class DashboardOverviewService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  overview(year?: string, term?: string): Observable<DashboardOverview> {
    let params = new HttpParams();
    if (year) {
      params = params.set('year', year);
    }
    if (term) {
      params = params.set('term', term);
    }
    return this.http.get<DashboardOverview>(`${this.basePath}/api/dashboard/overview`, { params });
  }
}
