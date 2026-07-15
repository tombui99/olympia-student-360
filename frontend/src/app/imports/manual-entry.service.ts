import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { BASE_PATH } from '../api/variables';

export interface ManualEntryStudent {
  id: number;
  studentCode: string;
  fullName: string;
  gradeLevel?: string | null;
  homeroom?: string | null;
}

export interface ManualEntryOptions {
  behaviourTypes: string[];
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

@Injectable({ providedIn: 'root' })
export class ManualEntryService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  options() {
    return this.http.get<ManualEntryOptions>(`${this.basePath}/api/students/manual-entry-options`);
  }

  searchStudents(query: string) {
    return this.http.get<ManualEntryStudent[]>(`${this.basePath}/api/students/manual-entry-students`, {
      params: { query },
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
}
