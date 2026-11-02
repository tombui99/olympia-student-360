import { HttpClient, HttpContext } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { BASE_PATH } from '../api/variables';
import { TranscriptPreferencesService } from './transcript-preferences.service';
import { SKIP_GLOBAL_API_LOADING } from '../core/loading/api-loading.interceptor';

export interface TranscriptCatalog {
  years: { academicYear: string; gradeLevels: string[] }[];
  subjectOptions: TranscriptSubjectOption[];
}

export interface TranscriptStudent {
  id: number;
  sisUserId: string;
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
  englishName?: string | null;
  vietnameseName?: string | null;
  subject: string;
  subjectCode: string;
  courseNames: string[];
  classScores: TranscriptClassScore[];
  semester1Final?: number | null;
  semester2Final?: number | null;
  yearAverage?: number | null;
  credit: number;
}

export interface TranscriptClassScore {
  canvasCourseId: number;
  className: string;
  courseName: string;
  semester1Final?: number | null;
  semester2Final?: number | null;
  yearAverage?: number | null;
  credit: number;
  recordCount: number;
}

export interface TranscriptSubjectOption {
  subjectCode: string;
  subject: string;
  gradeLevels?: string[];
}

export interface SchoolTranscriptGrade {
  gradeLevel: string;
  averageScore?: number | null;
  gpa?: number | null;
  classOptions: string[];
  subjects: TranscriptSubject[];
  subjectPage: number;
  subjectPageSize: number;
  totalSubjects: number;
  totalSubjectPages: number;
}

export interface SchoolTranscriptYear {
  academicYear: string;
  showAverage?: boolean;
  averageScore?: number | null;
  grades: SchoolTranscriptGrade[];
}

export interface SchoolTranscript {
  record?: TranscriptRecord;
  canEditRecord?: boolean;
  transcriptName?: string | null;
  academicYearOptions?: string[];
  studentProfileId?: number | null;
  sisUserId?: string | null;
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
  subjectOptions: TranscriptSubjectOption[];
  years: SchoolTranscriptYear[];
}

export interface TranscriptRecord {
  revision: number;
  transcriptName?: string | null;
  enrollmentDate?: string | null;
  graduationDate?: string | null;
  graduationExamDate?: string | null;
  ibReleaseDate?: string | null;
  notes?: string | null;
  ibCorePoints?: number | null;
  years: TranscriptYearRecord[];
}

export interface TranscriptYearRecord {
  academicYear: string;
  gradeLevel?: string | null;
  showAverage?: boolean | null;
  credits: { subjectCode: string; credit: number; englishName?: string | null; vietnameseName?: string | null }[];
  ibSubjects: TranscriptIbSubject[];
}

export interface TranscriptIbSubject {
  subjectCode: string;
  subjectName: string;
  level?: string | null;
  predictedGrade?: string | null;
  term1?: string | null;
  term2?: string | null;
  term3?: string | null;
  term4?: string | null;
}

export interface TranscriptScoreRecord {
  warehouseScoreFactId: number;
  canvasEnrollmentId: number;
  canvasUserId: number;
  canvasCourseId: number;
  studentProfileId: number;
  sisUserId: string;
  studentName: string;
  courseName: string;
  courseCode?: string | null;
  enrollmentTermId?: number | null;
  enrollmentTermName?: string | null;
  gradingPeriodId?: number | null;
  gradingPeriodTitle?: string | null;
  scoreScope: 'course_total' | 'grading_period';
  score?: number | null;
  scoreSource: 'enrollment_override' | 'grades_override' | 'final' | 'current' | 'unavailable';
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
  classOptions: string[];
  subjectOptions: TranscriptSubjectOption[];
  subjects: TranscriptSubject[];
  subjectPage: number;
  subjectPageSize: number;
  totalSubjectPages: number;
  records: TranscriptScoreRecord[];
  page: number;
  pageSize: number;
  totalPages: number;
}

@Injectable({ providedIn: 'root' })
export class TranscriptsService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);
  private readonly preferences = inject(TranscriptPreferencesService);

  saveRecord(studentId: number, record: TranscriptRecord) {
    return this.http.put<TranscriptRecord>(`${this.basePath}/api/transcripts/students/${studentId}/record`, record, {
      context: new HttpContext().set(SKIP_GLOBAL_API_LOADING, true),
    });
  }

  getCatalog() {
    return this.http.get<TranscriptCatalog>(`${this.basePath}/api/transcripts/catalog`, {
      context: new HttpContext().set(SKIP_GLOBAL_API_LOADING, true),
    });
  }

  getOverall(studentId?: number, subjectPage?: number, subjectPageSize = 10, academicYear?: string, gradeLevel?: string) {
    return this.http.get<SchoolTranscript>(`${this.basePath}/api/transcripts/overall`, {
      context: new HttpContext().set(SKIP_GLOBAL_API_LOADING, true),
      params: {
        ...(studentId ? { studentId } : {}),
        ...(academicYear ? { academicYear } : {}),
        ...(gradeLevel ? { gradeLevel } : {}),
        ...(subjectPage ? { subjectPage, subjectPageSize } : {}),
        ...this.subjectExclusionParams(),
      },
    });
  }

  getStudents(search: string, limit = 10) {
    return this.http.get<TranscriptStudent[]>(`${this.basePath}/api/transcripts/students`, {
      context: new HttpContext().set(SKIP_GLOBAL_API_LOADING, true),
      params: { search, limit },
    });
  }

  getStudentDirectory(page = 1, pageSize = 20, search = '') {
    return this.http.get<TranscriptStudentsPage>(`${this.basePath}/api/transcripts/student-directory`, {
      context: new HttpContext().set(SKIP_GLOBAL_API_LOADING, true),
      params: {
        page,
        pageSize,
        ...(search.trim() ? { search: search.trim() } : {}),
      },
    });
  }

  getGradeDetails(academicYear: string, gradeLevel: string, page = 1, pageSize = 25,
    subject = '', term = '', subjectPage = 1, subjectPageSize = 10, className = '') {
    return this.http.get<SchoolTranscriptGradeDetail>(
      `${this.basePath}/api/transcripts/${encodeURIComponent(academicYear)}/grades/${encodeURIComponent(gradeLevel)}`,
      { context: new HttpContext().set(SKIP_GLOBAL_API_LOADING, true), params: {
        page,
        pageSize,
        subjectPage,
        subjectPageSize,
        ...(subject ? { subject } : {}),
        ...(term ? { term } : {}),
        ...(className ? { className } : {}),
        ...this.subjectExclusionParams(),
      } },
    );
  }

  private subjectExclusionParams(): { excludedSubjects?: string[] } {
    const excludedSubjects = this.preferences.excludedSubjectCodes();
    return excludedSubjects.length ? { excludedSubjects } : {};
  }
}
