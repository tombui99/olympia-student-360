import { HttpClient } from '@angular/common/http';
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
}

export interface DashboardOverview {
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
}

@Injectable({ providedIn: 'root' })
export class DashboardOverviewService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  overview(): Observable<DashboardOverview> {
    return this.http.get<DashboardOverview>(`${this.basePath}/api/dashboard/overview`);
  }
}
