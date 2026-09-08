import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map, Observable, tap } from 'rxjs';
import { BASE_PATH } from '../api/variables';

export interface WorkspaceFilterSet {
  year: string;
  semester: string;
  cycle: string;
  campus: string;
  level: string;
  grade: string;
  homeroom: string;
  studentId: string;
  program: string;
  subject: string;
}

export interface WorkspaceFilterOptions {
  academicYears: string[];
  terms: string[];
  measurementCycles: string[];
  campuses: string[];
  educationLevels: string[];
  gradeLevels: string[];
  homerooms: string[];
  students: {
    id: number;
    sisUserId: string;
    fullName: string;
    campus?: string;
    educationLevel?: string;
    gradeLevel?: string;
    homeroom?: string;
    program?: string;
  }[];
  programs: string[];
  subjects: string[];
}

interface DashboardPreference {
  academicYear?: string | null;
  term?: string | null;
  measurementCycle?: string | null;
  campus?: string | null;
  educationLevel?: string | null;
  gradeLevel?: string | null;
  homeroom?: string | null;
  studentProfileId?: number | null;
  program?: string | null;
  subject?: string | null;
}

interface WorkspacePreference {
  academicYear?: string | null;
  term?: string | null;
  measurementCycle?: string | null;
  campus?: string | null;
  educationLevel?: string | null;
  gradeLevel?: string | null;
  homeroom?: string | null;
  studentProfileId?: number | null;
  program?: string | null;
  subject?: string | null;
  chartOrder: string[];
}

export function defaultFilterSet(): WorkspaceFilterSet {
  return { year: '', semester: '', cycle: '', campus: '', level: '', grade: '', homeroom: '', studentId: '', program: '', subject: '' };
}

export function emptyFilterOptions(): WorkspaceFilterOptions {
  return { academicYears: [], terms: [], measurementCycles: [], campuses: [], educationLevels: [], gradeLevels: [], homerooms: [], students: [], programs: [], subjects: [] };
}

@Injectable({ providedIn: 'root' })
export class WorkspaceFiltersService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);
  private preference: DashboardPreference = {};
  private workspacePreference: WorkspacePreference = { chartOrder: [] };

  options(): Observable<WorkspaceFilterOptions> {
    return this.http.get<WorkspaceFilterOptions>(`${this.basePath}/api/dashboard/filter-options`);
  }

  load(): Observable<WorkspaceFilterSet> {
    return this.http.get<DashboardPreference>(`${this.basePath}/api/dashboard/preference`).pipe(
      tap((preference) => this.preference = preference),
      map((p) => ({
          year: p.academicYear ?? '', semester: p.term ?? '', cycle: p.measurementCycle ?? '',
          campus: p.campus ?? '', level: p.educationLevel ?? '', grade: p.gradeLevel ?? '',
          homeroom: p.homeroom ?? '', studentId: p.studentProfileId?.toString() ?? '',
          program: p.program ?? '', subject: p.subject ?? '',
      })),
    );
  }

  save(filters: WorkspaceFilterSet): void {
    this.preference = {
      academicYear: filters.year || null, term: filters.semester || null,
      measurementCycle: filters.cycle || null, campus: filters.campus || null,
      educationLevel: filters.level || null, gradeLevel: filters.grade || null,
      homeroom: filters.homeroom || null, studentProfileId: filters.studentId ? Number(filters.studentId) : null,
      program: filters.program || null, subject: filters.subject || null,
    };
    this.http.put(`${this.basePath}/api/dashboard/preference`, this.preference).subscribe();
  }

  loadWorkspace(workspaceId: number): Observable<WorkspaceFilterSet> {
    return this.http.get<WorkspacePreference>(
      `${this.basePath}/api/reports/custom/${workspaceId}/preference`,
    ).pipe(
      tap((preference) => this.workspacePreference = {
        ...preference,
        chartOrder: preference.chartOrder ?? [],
      }),
      map((p) => this.toFilterSet(p)),
    );
  }

  saveWorkspace(workspaceId: number, filters: WorkspaceFilterSet): void {
    this.workspacePreference = {
      ...this.fromFilterSet(filters),
      chartOrder: this.workspacePreference.chartOrder ?? [],
    };
    this.http.put(
      `${this.basePath}/api/reports/custom/${workspaceId}/preference`,
      this.workspacePreference,
    ).subscribe();
  }

  loadWorkspaceChartOrder(): string[] {
    return this.workspacePreference.chartOrder ?? [];
  }

  saveWorkspaceChartOrder(workspaceId: number, order: string[], filters: WorkspaceFilterSet): void {
    this.workspacePreference.chartOrder = order;
    this.saveWorkspace(workspaceId, filters);
  }

  private toFilterSet(p: Omit<WorkspacePreference, 'chartOrder'>): WorkspaceFilterSet {
    return {
      year: p.academicYear ?? '', semester: p.term ?? '', cycle: p.measurementCycle ?? '',
      campus: p.campus ?? '', level: p.educationLevel ?? '', grade: p.gradeLevel ?? '',
      homeroom: p.homeroom ?? '', studentId: p.studentProfileId?.toString() ?? '',
      program: p.program ?? '', subject: p.subject ?? '',
    };
  }

  private fromFilterSet(filters: WorkspaceFilterSet): Omit<WorkspacePreference, 'chartOrder'> {
    return {
      academicYear: filters.year || null, term: filters.semester || null,
      measurementCycle: filters.cycle || null, campus: filters.campus || null,
      educationLevel: filters.level || null, gradeLevel: filters.grade || null,
      homeroom: filters.homeroom || null,
      studentProfileId: filters.studentId ? Number(filters.studentId) : null,
      program: filters.program || null, subject: filters.subject || null,
    };
  }
}
