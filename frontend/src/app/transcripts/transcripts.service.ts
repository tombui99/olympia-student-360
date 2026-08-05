import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { BASE_PATH } from '../api/variables';

export interface TranscriptStudent {
  id: number;
  studentCode: string;
  fullName: string;
  gradeLevel?: string | null;
  homeroom?: string | null;
}

export interface TranscriptStudentsPage {
  items: TranscriptStudent[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface TranscriptSubject {
  subject: string;
  termScores: Record<string, number | null>;
  semester1Final?: number | null;
  semester2Final?: number | null;
  yearAverage?: number | null;
  credit: number;
}

export interface SchoolTranscriptGrade {
  gradeLevel: string;
  averageScore?: number | null;
  gpa?: number | null;
  terms: string[];
  subjects: TranscriptSubject[];
}

export interface SchoolTranscriptYear {
  academicYear: string;
  averageScore?: number | null;
  grades: SchoolTranscriptGrade[];
}

export interface SchoolTranscript {
  studentProfileId?: number | null;
  studentCode?: string | null;
  studentName?: string | null;
  dateOfBirth?: string | null;
  gradeLevel?: string | null;
  homeroom?: string | null;
  gender?: string | null;
  overallAverage?: number | null;
  overallGpa?: number | null;
  totalAcademicYears: number;
  totalStudents: number;
  totalSubjects: number;
  years: SchoolTranscriptYear[];
}

export interface TranscriptScoreRecord {
  canvasScoreId: number;
  studentProfileId: number;
  studentCode: string;
  studentName: string;
  subject: string;
  courseName: string;
  term: string;
  score: number;
  scoreSource: 'override' | 'final' | 'current';
  recordedAt: string;
}

export interface SchoolTranscriptGradeDetail {
  academicYear: string;
  gradeLevel: string;
  averageScore?: number | null;
  gpa?: number | null;
  totalStudents: number;
  totalSubjects: number;
  totalRecords: number;
  matchingRecords: number;
  terms: string[];
  subjects: TranscriptSubject[];
  records: TranscriptScoreRecord[];
  page: number;
  pageSize: number;
  totalPages: number;
}

@Injectable({ providedIn: 'root' })
export class TranscriptsService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  getOverall(studentId?: number) {
    return this.http.get<SchoolTranscript>(`${this.basePath}/api/transcripts/overall`, {
      params: studentId ? { studentId } : {},
    });
  }

  getStudents(search: string, limit = 10) {
    return this.http.get<TranscriptStudent[]>(`${this.basePath}/api/transcripts/students`, {
      params: { search, limit },
    });
  }

  getStudentDirectory(page = 1, pageSize = 20, search = '') {
    return this.http.get<TranscriptStudentsPage>(`${this.basePath}/api/transcripts/student-directory`, {
      params: {
        page,
        pageSize,
        ...(search.trim() ? { search: search.trim() } : {}),
      },
    });
  }

  getGradeDetails(academicYear: string, gradeLevel: string, page = 1, pageSize = 25,
    subject = '', term = '') {
    return this.http.get<SchoolTranscriptGradeDetail>(
      `${this.basePath}/api/transcripts/${encodeURIComponent(academicYear)}/grades/${encodeURIComponent(gradeLevel)}`,
      { params: {
        page,
        pageSize,
        ...(subject ? { subject } : {}),
        ...(term ? { term } : {}),
      } },
    );
  }
}
