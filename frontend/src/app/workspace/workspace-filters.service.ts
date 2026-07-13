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
  students: { id: number; studentCode: string; fullName: string; gradeLevel?: string; homeroom?: string }[];
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
  chartOrders: Record<string, string[]>;
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
  private preference: DashboardPreference = { chartOrders: {} };

  options(): Observable<WorkspaceFilterOptions> {
    return this.http.get<WorkspaceFilterOptions>(`${this.basePath}/api/dashboard/filter-options`);
  }

  load(): Observable<WorkspaceFilterSet> {
    return this.http.get<DashboardPreference>(`${this.basePath}/api/dashboard/preference`).pipe(
      tap((preference) => this.preference = { ...preference, chartOrders: preference.chartOrders ?? {} }),
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
      chartOrders: this.preference.chartOrders ?? {},
    };
    this.http.put(`${this.basePath}/api/dashboard/preference`, this.preference).subscribe();
  }

  loadChartOrder(workspaceId: string): string[] {
    return this.preference.chartOrders?.[workspaceId] ?? [];
  }

  saveChartOrder(workspaceId: string, order: string[], filters: WorkspaceFilterSet): void {
    this.preference.chartOrders = { ...(this.preference.chartOrders ?? {}), [workspaceId]: order };
    this.save(filters);
  }
}
