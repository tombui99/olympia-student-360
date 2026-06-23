import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BASE_PATH } from '../api/variables';

export interface AssignmentTeacher {
  id: string;
  username: string;
  email?: string | null;
  fullName: string;
  assignedStudentCount: number;
}

export interface AssignmentStudent {
  id: number;
  studentCode: string;
  fullName: string;
  gradeLevel?: string | null;
  homeroom?: string | null;
  teacherId?: string | null;
}

export interface TeacherStudentAssignments {
  teachers: AssignmentTeacher[];
  students: AssignmentStudent[];
}

@Injectable({ providedIn: 'root' })
export class TeacherStudentAssignmentService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  load(): Observable<TeacherStudentAssignments> {
    return this.http.get<TeacherStudentAssignments>(
      `${this.basePath}/api/admin/teacher-student-assignments`,
    );
  }

  save(teacherId: string, studentIds: number[]): Observable<void> {
    return this.http.put<void>(
      `${this.basePath}/api/admin/teacher-student-assignments/${teacherId}`,
      { studentIds },
    );
  }
}
