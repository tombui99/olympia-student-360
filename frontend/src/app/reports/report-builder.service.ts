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
  value: number | null;
  unit: string;
  trend: number | null;
  baselineValue: number | null;
  kpiTarget: number | null;
  series: { periodStart: string; value: number }[];
}

export interface CustomChart {
  id: string;
  title: string;
  kind: ChartKind;
  metrics: string[];
  /** Draw the cohort-average (TBC) reference line on bar/line charts. */
  showBaseline?: boolean;
  /** Optional KPI target reference line on bar/line charts. */
  kpiTarget?: number | null;
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

  readonly metricVariables: MetricVariable[] = [];

  loadMetricVariables(params: Record<string, string>): Observable<MetricVariable[]> {
    return this.http.get<MetricVariable[]>(`${this.basePath}/api/dashboard/metrics`, { params }).pipe(
      map((metrics) => {
        this.metricVariables.splice(0, this.metricVariables.length, ...metrics);
        return metrics;
      }),
      catchError(() => of([])),
    );
  }

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
      title: '',
      description: '',
      charts: [],

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
