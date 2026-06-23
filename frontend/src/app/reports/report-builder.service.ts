import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { catchError, map, Observable, of } from 'rxjs';
import { BASE_PATH } from '../api/variables';

export type ChartKind = 'bar' | 'line' | 'pie' | 'scatter' | 'card';

export interface MetricVariable {
  id: string;
  label: string;
  category: string;
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
      label: 'Attendance rate',
      category: 'Attendance',
      value: '94%',
      unit: '%',
      trend: '+2.1%',
      series: [88, 91, 90, 93, 92, 94],
    },
    {
      id: 'assignment-completion',
      label: 'Assignment completion',
      category: 'Canvas',
      value: '87%',
      unit: '%',
      trend: '+4.3%',
      series: [72, 76, 79, 81, 84, 87],
    },
    {
      id: 'academic-risk',
      label: 'Academic risk alerts',
      category: 'Academic',
      value: '18',
      unit: '',
      trend: '-6',
      series: [29, 27, 25, 23, 20, 18],
    },
    {
      id: 'wellbeing-checkins',
      label: 'Wellbeing check-ins',
      category: 'Wellbeing',
      value: '126',
      unit: '',
      trend: '+14',
      series: [68, 74, 83, 91, 109, 126],
    },
    {
      id: 'late-submissions',
      label: 'Late submissions',
      category: 'Canvas',
      value: '42',
      unit: '',
      trend: '-11',
      series: [76, 71, 64, 55, 49, 42],
    },
    {
      id: 'intervention-actions',
      label: 'Intervention actions',
      category: 'Support',
      value: '33',
      unit: '',
      trend: '+8',
      series: [12, 16, 19, 24, 29, 33],
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
