import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BASE_PATH } from '../api/variables';

export type SupportCategory = 'Learning' | 'Behaviour' | 'MentalHealth';
export type SupportStatus = 'Planned' | 'InProgress' | 'FollowUpRequired' | 'Completed';
export type SupportProgress =
  | 'NotYetAssessed'
  | 'NoChange'
  | 'SomeProgress'
  | 'SignificantProgress'
  | 'GoalAchieved'
  | 'NeedsMoreSupport';

export interface StudentSupportAction {
  id: number;
  studentId: number;
  studentName: string;
  sisUserId: string;
  gradeLevel?: string | null;
  homeroom?: string | null;
  actionDate: string;
  followUpDate?: string | null;
  academicYear: string;
  term?: string | null;
  category: SupportCategory;
  title: string;
  goal: string;
  actionTaken: string;
  result?: string | null;
  nextStep?: string | null;
  status: SupportStatus;
  progress: SupportProgress;
  subject?: string | null;
  shareWithFamily: boolean;
  createdByName: string;
  updatedByName?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface StudentSupportActionsPage {
  items: StudentSupportAction[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  activeCount: number;
  followUpDueCount: number;
  goalAchievedCount: number;
  availableAcademicYears: string[];
}

export interface StudentSupportJourneyPage extends StudentSupportActionsPage {
  learningCount: number;
  behaviourCount: number;
  mentalHealthCount: number;
}

export interface SaveStudentSupportAction {
  studentId: number;
  actionDate: string;
  followUpDate?: string | null;
  academicYear?: string | null;
  term?: string | null;
  category: SupportCategory;
  title: string;
  goal: string;
  actionTaken: string;
  result?: string | null;
  nextStep?: string | null;
  status: SupportStatus;
  progress: SupportProgress;
  subject?: string | null;
  shareWithFamily: boolean;
}

@Injectable({ providedIn: 'root' })
export class SupportActionsService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  list(filters: {
    page?: number;
    pageSize?: number;
    search?: string;
    category?: string;
    status?: string;
    academicYear?: string;
    studentId?: number;
  } = {}): Observable<StudentSupportActionsPage> {
    let params = new HttpParams()
      .set('page', filters.page ?? 1)
      .set('pageSize', filters.pageSize ?? 20);
    if (filters.search?.trim()) params = params.set('search', filters.search.trim());
    if (filters.category) params = params.set('category', filters.category);
    if (filters.status) params = params.set('status', filters.status);
    if (filters.academicYear) params = params.set('academicYear', filters.academicYear);
    if (filters.studentId) params = params.set('studentId', filters.studentId);
    return this.http.get<StudentSupportActionsPage>(`${this.basePath}/api/support-actions`, { params });
  }

  forStudent(
    studentId: number,
    academicYear = '',
    page = 1,
    pageSize = 5,
  ): Observable<StudentSupportJourneyPage> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (academicYear) params = params.set('academicYear', academicYear);
    return this.http.get<StudentSupportJourneyPage>(
      `${this.basePath}/api/support-actions/student/${studentId}`,
      { params },
    );
  }

  create(model: SaveStudentSupportAction): Observable<StudentSupportAction> {
    return this.http.post<StudentSupportAction>(`${this.basePath}/api/support-actions`, model);
  }

  update(id: number, model: SaveStudentSupportAction): Observable<StudentSupportAction> {
    return this.http.put<StudentSupportAction>(`${this.basePath}/api/support-actions/${id}`, model);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.basePath}/api/support-actions/${id}`);
  }
}
