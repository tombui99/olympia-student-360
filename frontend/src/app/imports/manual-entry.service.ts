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
  students: ManualEntryStudent[];
  behaviourTypes: string[];
  projectSubjects: string[];
  projectStatuses: string[];
  academicYears: string[];
  healthIncidentTypes: string[];
  healthSeverityLevels: number[];
  healthBodyAreas: string[];
  healthLocations: string[];
}

export interface CreateBehaviourRequest {
  incidentDate: string;
  behaviourType: string;
  notes?: string | null;
}

export interface CreateProjectRequest {
  projectName: string;
  subject: string;
  status: string;
  teacherNotes?: string | null;
}

export interface CreateHealthIncidentRequest {
  incidentDate: string;
  academicYear: string;
  incidentType: string;
  severityLevel: number;
  bodyArea?: string | null;
  location?: string | null;
  cause: string;
  actionTaken?: string | null;
}

@Injectable({ providedIn: 'root' })
export class ManualEntryService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  options() {
    return this.http.get<ManualEntryOptions>(`${this.basePath}/api/students/manual-entry-options`);
  }

  createBehaviour(studentId: number, request: CreateBehaviourRequest) {
    return this.http.post(`${this.basePath}/api/students/${studentId}/behaviours`, request);
  }

  createProject(studentId: number, request: CreateProjectRequest) {
    return this.http.post(`${this.basePath}/api/students/${studentId}/projects`, request);
  }

  createHealthIncident(studentId: number, request: CreateHealthIncidentRequest) {
    return this.http.post(`${this.basePath}/api/students/${studentId}/health-incidents`, request);
  }
}
