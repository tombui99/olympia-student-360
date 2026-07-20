import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { BASE_PATH } from '../api/variables';

export interface TranscriptSubject {
  subject: string;
  termScores: Record<string, number | null>;
  yearAverage?: number | null;
}

export interface SchoolTranscriptGrade {
  gradeLevel: string;
  averageScore?: number | null;
  terms: string[];
  subjects: TranscriptSubject[];
}

export interface SchoolTranscriptYear {
  academicYear: string;
  averageScore?: number | null;
  grades: SchoolTranscriptGrade[];
}

export interface SchoolTranscript {
  overallAverage?: number | null;
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

  getOverall() {
    return this.http.get<SchoolTranscript>(`${this.basePath}/api/transcripts/overall`);
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
