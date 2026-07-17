import { HttpClient, HttpResponse } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
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
  supportsDrilldown?: boolean;
  series: { periodStart: string; value: number }[];
}

export interface MetricVariableGroup {
  dataset: string;
  metrics: MetricVariable[];
}

/**
 * The metric API exposes business category and source lineage. For selection, users
 * need the more concrete source dataset; Canvas is the only source currently backed
 * by two distinct warehouse datasets.
 */
export function metricDataset(metric: MetricVariable): string {
  if (metric.id === 'canvas-course-grade-avg') {
    return 'CanvasCourseScores';
  }
  if (metric.source === 'Canvas') {
    return 'CanvasSubmissions';
  }
  return metric.source;
}

export function groupMetricVariables(metrics: MetricVariable[]): MetricVariableGroup[] {
  const preferredOrder = [
    'CanvasCourseScores',
    'CanvasSubmissions',
    'StudentBehaviours',
    'StudentProjects',
    'StudentMentalHealth',
    'SISHealthVisits',
    'StudentBusAttendance',
    'SIS',
    'BaseERP',
    'Warehouse',
  ];
  const grouped = new Map<string, MetricVariable[]>();
  for (const metric of metrics) {
    const dataset = metricDataset(metric);
    grouped.set(dataset, [...(grouped.get(dataset) ?? []), metric]);
  }

  return [...grouped.entries()]
    .sort(([left], [right]) => {
      const leftIndex = preferredOrder.indexOf(left);
      const rightIndex = preferredOrder.indexOf(right);
      if (leftIndex !== -1 || rightIndex !== -1) {
        return (leftIndex === -1 ? preferredOrder.length : leftIndex)
          - (rightIndex === -1 ? preferredOrder.length : rightIndex);
      }
      return left.localeCompare(right);
    })
    .map(([dataset, datasetMetrics]) => ({ dataset, metrics: datasetMetrics }));
}

export interface MetricDrilldownRow {
  studentId: number;
  studentCode: string;
  fullName: string;
  gradeLevel?: string | null;
  homeroom?: string | null;
  value: number;
  recordCount: number;
  records: DrilldownRecord[];
}

export interface DrilldownRecord {
  date?: string | null;
  value?: number | null;
  fields: Record<string, string | null>;
}

export interface MetricDrilldown {
  metricId: string;
  label: string;
  unit: string;
  aggregation: 'count' | 'average';
  aggregateValue: number | null;
  students: MetricDrilldownRow[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

/** Ad-hoc query spec for a Canvas data-explorer chart. */
export interface ExplorerSpec {
  dataset: string;
  dimension: string;
  measure: string;
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
  /** When set, this chart is built from raw Canvas fields instead of metric variables. */
  explorer?: ExplorerSpec;
}

export interface CanvasExploreField {
  key: string;
  label: string;
  unit: string;
}

export interface CanvasExploreDataset {
  key: string;
  label: string;
  dimensions: CanvasExploreField[];
  measures: CanvasExploreField[];
}

export interface CanvasExploreOptions {
  datasets: CanvasExploreDataset[];
}

export interface CanvasExploreRow {
  key: string;
  value: number;
}

export interface CanvasExploreResult {
  dataset: string;
  dimension: string;
  measure: string;
  unit: string;
  aggregation: string;
  rows: CanvasExploreRow[];
}

export interface CanvasExploreDrilldownRow extends DrilldownRecord {
  studentId?: number | null;
  studentCode?: string | null;
  fullName?: string | null;
  gradeLevel?: string | null;
  homeroom?: string | null;
}

export interface CanvasExploreDrilldown {
  dataset: string;
  groupKey: string;
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  records: CanvasExploreDrilldownRow[];
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

export interface WorkspaceAiReportRequest {
  language: 'en' | 'vi';
  audience: 'leadership' | 'teachers' | 'parents';
  tone: 'constructive' | 'formal' | 'concise';
  detailLevel: 'brief' | 'standard' | 'detailed';
  additionalInstructions: string;
  includeRecommendations: boolean;
  filters: { label: string; value: string }[];
  charts: WorkspaceAiChartSnapshot[];
}

export interface WorkspaceAiChartSnapshot {
  chartId: string;
  title: string;
  chartType: string;
  source: string;
  imageDataUrl?: string | null;
  series: {
    name: string;
    unit: string;
    points: { label: string; value: number | null }[];
  }[];
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

  readonly metricVariables = signal<MetricVariable[]>([]);
  private metricRequestVersion = 0;

  loadMetricVariables(params: Record<string, string>): Observable<MetricVariable[]> {
    const requestVersion = ++this.metricRequestVersion;
    return this.http.get<MetricVariable[]>(`${this.basePath}/api/dashboard/metrics`, { params }).pipe(
      map((metrics) => {
        if (requestVersion === this.metricRequestVersion) {
          this.metricVariables.set(metrics);
        }
        return metrics;
      }),
      catchError(() => of([])),
    );
  }

  loadMetricDrilldown(params: Record<string, string>): Observable<MetricDrilldown> {
    return this.http.get<MetricDrilldown>(`${this.basePath}/api/dashboard/metric-drilldown`, { params });
  }

  loadExploreOptions(): Observable<CanvasExploreOptions> {
    return this.http.get<CanvasExploreOptions>(`${this.basePath}/api/dashboard/canvas-explore/options`).pipe(
      catchError(() => of({ datasets: [] } as CanvasExploreOptions)),
    );
  }

  loadExplore(params: Record<string, string>): Observable<CanvasExploreResult> {
    return this.http.get<CanvasExploreResult>(`${this.basePath}/api/dashboard/canvas-explore`, { params });
  }

  loadExploreDrilldown(params: Record<string, string>): Observable<CanvasExploreDrilldown> {
    return this.http.get<CanvasExploreDrilldown>(
      `${this.basePath}/api/dashboard/canvas-explore/drilldown`, { params },
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

  generateAiReport(reportId: number, request: WorkspaceAiReportRequest): Observable<HttpResponse<Blob>> {
    return this.http.post(
      `${this.basePath}/api/reports/custom/${reportId}/ai-report`,
      request,
      { observe: 'response', responseType: 'blob' },
    );
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
