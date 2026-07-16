import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BASE_PATH } from '../api/variables';

export interface SISClassListItem {
  id: number;
  sisClassCode: string;
  className: string;
  academicYear: string;
  term?: string | null;
  gradeLevel?: string | null;
  homeroom?: string | null;
  campus?: string | null;
  teacherCount: number;
  studentCount: number;
}

export interface SISClassTeacher {
  id: string;
  username: string;
  fullName: string;
  email?: string | null;
  assignmentRole?: string | null;
}

export interface SISClassStudent {
  id: number;
  studentCode: string;
  fullName: string;
  gradeLevel?: string | null;
  homeroom?: string | null;
}

export interface SISClassDetail extends SISClassListItem {
  teachers: SISClassTeacher[];
  students: SISClassStudent[];
}

export interface SISClassesPage {
  items: SISClassListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  academicYears: string[];
}

@Injectable({ providedIn: 'root' })
export class ClassesService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  list(page: number, pageSize: number, search: string, academicYear: string): Observable<SISClassesPage> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    if (academicYear) params = params.set('academicYear', academicYear);
    return this.http.get<SISClassesPage>(`${this.basePath}/api/classes`, { params });
  }

  detail(id: number): Observable<SISClassDetail> {
    return this.http.get<SISClassDetail>(`${this.basePath}/api/classes/${id}`);
  }
}
