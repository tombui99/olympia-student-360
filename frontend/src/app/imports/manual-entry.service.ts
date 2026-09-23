import { HttpClient, HttpContext } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { BASE_PATH } from '../api/variables';
import { SKIP_GLOBAL_API_LOADING } from '../core/loading/api-loading.interceptor';

export interface ManualEntryStudent {
  id: number;
  sisUserId: string;
  fullName: string;
  gradeLevel?: string | null;
  homeroom?: string | null;
}

export interface ManualEntryOptions {
  positiveBehaviourTypes: string[];
  concerningBehaviourTypes: string[];
  projectSubjects: string[];
  projectStatuses: string[];
  academicYears: string[];
  mentalHealthConcernTypes: string[];
  mentalHealthRiskLevels: string[];
  busTripDirections: string[];
  busViolationTypes: string[];
}

export interface CreateBehaviourRequest {
  incidentDate: string;
  behaviourType: string;
  notes?: string | null;
  customFields?: Record<string, string>;
}

export interface CreateProjectRequest {
  projectName: string;
  subject: string;
  status: string;
  teacherNotes?: string | null;
  customFields?: Record<string, string>;
}

export interface CreateMentalHealthRequest {
  recordDate: string;
  academicYear: string;
  concernType: string;
  riskLevel: string;
  notes?: string | null;
  customFields?: Record<string, string>;
}

export interface CreateBusAttendanceRequest {
  attendanceDate: string;
  tripDirection: string;
  violationType: string;
  busCode?: string | null;
  routeName?: string | null;
  notes?: string | null;
  customFields?: Record<string, string>;
}

export interface CreateHealthVisitRequest {
  visitDate: string;
  symptoms: string;
  diagnosis?: string | null;
  classification?: string | null;
  treatment?: string | null;
  medication?: string | null;
  notes?: string | null;
  notificationSent: boolean;
  examinedBy?: string | null;
  customFields?: Record<string, string>;
}

@Injectable({ providedIn: 'root' })
export class ManualEntryService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  options(skipGlobalLoading = false) {
    const context = skipGlobalLoading
      ? new HttpContext().set(SKIP_GLOBAL_API_LOADING, true)
      : undefined;
    return this.http.get<ManualEntryOptions>(`${this.basePath}/api/students/manual-entry-options`, { context });
  }

  searchStudents(query: string, skipGlobalLoading = false) {
    const context = skipGlobalLoading
      ? new HttpContext().set(SKIP_GLOBAL_API_LOADING, true)
      : undefined;
    return this.http.get<ManualEntryStudent[]>(`${this.basePath}/api/students/manual-entry-students`, {
      params: { query },
      context,
    });
  }

  createBehaviour(studentId: number, request: CreateBehaviourRequest) {
    return this.http.post(`${this.basePath}/api/students/${studentId}/behaviours`, request);
  }

  createProject(studentId: number, request: CreateProjectRequest) {
    return this.http.post(`${this.basePath}/api/students/${studentId}/projects`, request);
  }

  createMentalHealth(studentId: number, request: CreateMentalHealthRequest) {
    return this.http.post(`${this.basePath}/api/students/${studentId}/mental-health`, request);
  }

  createBusAttendance(studentId: number, request: CreateBusAttendanceRequest) {
    return this.http.post(`${this.basePath}/api/students/${studentId}/bus-attendance`, request);
  }

  createHealthVisit(studentId: number, request: CreateHealthVisitRequest) {
    return this.http.post(`${this.basePath}/api/students/${studentId}/health-visits`, request);
  }
}
