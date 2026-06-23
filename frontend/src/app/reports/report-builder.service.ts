import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { catchError, map, Observable, of } from 'rxjs';
import { BASE_PATH } from '../api/variables';

export type ChartKind = 'bar' | 'line' | 'pie' | 'scatter' | 'card';

export interface MetricVariable {
  id: string;
  label: string;
  category: string;
  source: string;
  value: string;
  unit: string;
  trend: string;
  series: number[];
}

export interface CustomChart {
  id: string;
  title: string;
  kind: ChartKind;
  metrics: string[];
}

export interface CustomReport {
  id: string;
  serverId?: number;
  owner: string;
  title: string;
  description: string;
  charts: CustomChart[];

  sharedWith: string[];
  updatedAt: string;
}

export interface SystemUser {
  id: string;
  username: string;
  email?: string | null;
  fullName: string;
}

interface ApiCustomReport {
  id: number;
  ownerUsername: string;
  title: string;
  description: string;
  definition: {
    localId?: string;
    charts?: CustomChart[];
  };

  sharedWith: string[];
  updatedAt?: string;
}

@Injectable({ providedIn: 'root' })
export class ReportBuilderService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  readonly metricVariables: MetricVariable[] = [
    {
      id: 'attendance-rate',
      label: 'Daily attendance rate',
      category: 'Attendance',
      source: 'SIS',
      value: '94.2%',
      unit: '%',
      trend: '+1.8 pp',
      series: [92.4, 92.8, 93.1, 93.6, 93.9, 94.2],
    },
    {
      id: 'assignment-completion',
      label: 'Assignment completion rate',
      category: 'Learning',
      source: 'Canvas',
      value: '88.6%',
      unit: '%',
      trend: '+3.6 pp',
      series: [82.1, 83.4, 84.7, 86.2, 87.5, 88.6],
    },
    {
      id: 'academic-risk',
      label: 'Students at academic risk',
      category: 'Academic',
      source: 'Warehouse',
      value: '73',
      unit: '',
      trend: '-12 students',
      series: [104, 98, 91, 85, 79, 73],
    },
    {
      id: 'wellbeing-checkins',
      label: 'Wellbeing check-ins logged',
      category: 'Wellbeing',
      source: 'BaseERP',
      value: '186',
      unit: '',
      trend: '+31',
      series: [112, 126, 139, 151, 169, 186],
    },
    {
      id: 'late-submissions',
      label: 'Late submissions (30 days)',
      category: 'Learning',
      source: 'Canvas',
      value: '128',
      unit: '',
      trend: '-24',
      series: [191, 178, 164, 153, 141, 128],
    },
    {
      id: 'intervention-actions',
      label: 'Intervention action completion',
      category: 'Support',
      source: 'BaseERP',
      value: '91%',
      unit: '%',
      trend: '+8.0 pp',
      series: [76, 79, 82, 85, 88, 91],
    },
    {
      id: 'active-enrollment',
      label: 'Active student enrollment',
      category: 'Enrollment',
      source: 'SIS',
      value: '1,248',
      unit: '',
      trend: '+18 students',
      series: [1196, 1204, 1217, 1225, 1230, 1248],
    },
    {
      id: 'course-participation',
      label: 'Weekly course participation',
      category: 'Engagement',
      source: 'Canvas',
      value: '79.6%',
      unit: '%',
      trend: '+5.2 pp',
      series: [68.9, 71.2, 73.8, 75.4, 77.9, 79.6],
    },
    {
      id: 'open-support-cases',
      label: 'Open student support cases',
      category: 'Support',
      source: 'BaseERP',
      value: '34',
      unit: '',
      trend: '-9 cases',
      series: [58, 53, 49, 44, 39, 34],
    },
    {
      id: 'student-profile-completeness',
      label: 'Student profile completeness',
      category: 'DataQuality',
      source: 'Warehouse',
      value: '97.4%',
      unit: '%',
      trend: '+2.6 pp',
      series: [89.8, 91.5, 93.1, 94.6, 96.2, 97.4],
    },
  ];

  loadReports(): CustomReport[] {
    return [];
  }

  saveReports(_reports: CustomReport[]) {
    // Backend database is the source of truth for workspace reports.
  }

  loadRemoteReports(): Observable<CustomReport[]> {
    return this.http.get<ApiCustomReport[]>(`${this.basePath}/api/reports/custom`).pipe(
      map((reports) => reports.map((report) => this.fromApiReport(report))),
    );
  }

  saveRemoteReport(report: CustomReport): Observable<CustomReport> {
    const payload = {
      title: report.title,
      description: report.description,
      definition: {
        localId: report.id,
        charts: report.charts,
      },

      sharedWith: report.sharedWith,
    };

    const request = report.serverId
      ? this.http.put<ApiCustomReport>(`${this.basePath}/api/reports/custom/${report.serverId}`, payload)
      : this.http.post<ApiCustomReport>(`${this.basePath}/api/reports/custom`, payload);

    return request.pipe(map((savedReport) => this.fromApiReport(savedReport)));
  }

  deleteRemoteReport(report: CustomReport): Observable<boolean> {
    if (!report.serverId) {
      return of(true);
    }

    return this.http.delete<void>(`${this.basePath}/api/reports/custom/${report.serverId}`).pipe(map(() => true));
  }

  loadSystemUsers(): Observable<SystemUser[]> {
    return this.http.get<SystemUser[]>(`${this.basePath}/api/users/directory`).pipe(
      catchError(() => of([])),
    );
  }

  createReport(owner: string): CustomReport {
    const now = new Date().toISOString();

    return {
      id: crypto.randomUUID(),
      owner,
      title: 'New chart workspace',
      description: 'Build a dashboard from approved student metrics and Card templates.',
      charts: [
        {
          id: crypto.randomUUID(),
          title: 'Attendance snapshot',
          kind: 'bar',
          metrics: ['attendance-rate', 'assignment-completion'],
        },
      ],

      sharedWith: [],
      updatedAt: now,
    };
  }

  private fromApiReport(report: ApiCustomReport): CustomReport {
    return {
      id: report.definition.localId ?? `server-${report.id}`,
      serverId: report.id,
      owner: report.ownerUsername,
      title: report.title,
      description: report.description,
      charts: report.definition.charts ?? [],

      sharedWith: report.sharedWith ?? [],
      updatedAt: report.updatedAt ?? new Date().toISOString(),
    };
  }
}
