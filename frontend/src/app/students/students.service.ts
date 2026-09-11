import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BASE_PATH } from '../api/variables';

export type RiskLevel = 'None' | 'Low' | 'Medium' | 'High';

export interface RiskFlag {
  code: string;
  severity: 'Low' | 'Medium' | 'High';
  value: number;
}

export interface StudentListItem {
  id: number;
  sisStudentCode?: string | null;
  sisUserId: string;
  fullName: string;
  gradeLevel?: string | null;
  homeroom?: string | null;
  behaviourCount: number;
  positiveBehaviourCount: number;
  concerningBehaviourCount: number;
  projectCount: number;
  mentalHealthCount: number;
  riskLevel: RiskLevel;
  riskFlagCount: number;
}

export interface StudentsPage {
  items: StudentListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  availableGrades: string[];
}

export interface AtRiskStudent extends StudentListItem {
  flags: RiskFlag[];
}

export interface StudentClassificationSettings {
  riskWindowDays: number;
  highRiskBehaviourCount: number;
  highRiskBusViolationCount: number;
  highRiskCanvasAverageBelow: number;
  commendationWindowDays: number;
  commendationPositiveBehaviourCount: number;
  commendationCanvasAverageAtLeast: number;
  commendationMaxBusViolations: number;
}

export interface AtRiskStudentsPage {
  items: AtRiskStudent[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  highRiskCount: number;
  mediumRiskCount: number;
}

export interface StudentCommendation {
  id: number;
  studentId: number;
  sisUserId: string;
  studentName: string;
  gradeLevel?: string | null;
  homeroom?: string | null;
  incidentDate: string;
  commendationType: string;
  notes?: string | null;
  recordedByName?: string | null;
  positiveBehaviourCount: number;
  averageCanvasGrade?: number | null;
  busViolationCount: number;
}

export interface StudentCommendationsPage {
  items: StudentCommendation[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  totalCommendationCount: number;
  studentsRecognisedCount: number;
  recentCount: number;
  availableGrades: string[];
}

export interface StudentSummary {
  totalBehaviours: number;
  positiveBehaviours: number;
  concerningBehaviours: number;
  behavioursLast30Days: number;
  totalProjects: number;
  activeProjects: number;
}

export interface StudentBehaviour {
  id: number;
  incidentDate: string;
  behaviourType: string;
  category: 'Positive' | 'Concerning' | 'Other';
  notes?: string | null;
  recordedByName?: string | null;
  academicYear?: string | null;
  term?: string | null;
  customFields?: Record<string, string>;
}

export interface StudentProject {
  id: number;
  projectName: string;
  subject?: string | null;
  status: string;
  teacherNotes?: string | null;
  createdAt: string;
  academicYear?: string | null;
  term?: string | null;
  customFields?: Record<string, string>;
}

export interface StudentMentalHealthRecord {
  id: number;
  recordDate: string;
  academicYear: string;
  concernType: string;
  riskLevel: string;
  notes?: string | null;
  recordedByName?: string | null;
  customFields?: Record<string, string>;
}

export interface StudentBusAttendanceRecord {
  id: number;
  attendanceDate: string;
  tripDirection: string;
  violationType: string;
  busCode?: string | null;
  routeName?: string | null;
  notes?: string | null;
  recordedByName?: string | null;
  academicYear?: string | null;
  term?: string | null;
  customFields?: Record<string, string>;
}

/** Health/injury clinic visit synced from SIS (read-only). */
export interface SISHealthVisit {
  id: number;
  examTime: string;
  fullName: string;
  gender?: string | null;
  className?: string | null;
  symptoms?: string | null;
  diagnosis?: string | null;
  classification?: string | null;
  treatment?: string | null;
  medication?: string | null;
  examinedBy?: string | null;
}

export interface StudentParent {
  id: number;
  fullName: string;
  relationship?: string | null;
  username?: string | null;
  phoneNumber?: string | null;
  email?: string | null;
  birthYear?: number | null;
  ageGroup?: string | null;
  occupation?: string | null;
  employmentSector?: string | null;
}

export interface StudentCanvasCourse {
  canvasCourseId: number;
  courseName: string;
  courseCode?: string | null;
  enrollmentTermId?: number | null;
  enrollmentTermName?: string | null;
  gradingPeriodId?: number | null;
  gradingPeriodTitle?: string | null;
  scoreScope: 'course_total' | 'grading_period';
  currentScore?: number | null;
  finalScore?: number | null;
}

export interface StudentCanvasSubmission {
  canvasSubmissionId: number;
  canvasAssignmentId: number;
  canvasCourseId: number;
  assignmentTitle: string;
  courseName?: string | null;
  subject?: string | null;
  workflowState: string;
  grade?: string | null;
  submissionType?: string | null;
  score?: number | null;
  pointsPossible?: number | null;
  dueAt?: string | null;
  submittedAt?: string | null;
  gradedAt?: string | null;
  late: boolean;
  missing: boolean;
  excused: boolean | null;
}

export interface StudentCanvasJourneyPoint {
  academicYear: string;
  term?: string | null;
  gradeLevel?: string | null;
  label: string;
  averageGrade?: number | null;
  courseCount: number;
}

export interface StudentCanvas {
  hasData: boolean;
  academicYear?: string | null;
  coursesEnrolled: number;
  averageCourseGrade?: number | null;
  averageAssignmentScore?: number | null;
  assignmentsSubmitted: number;
  assignmentsGraded: number;
  missingAssignments: number;
  lateSubmissions: number;
  courses: StudentCanvasCourse[];
  courseGradesPage: number;
  courseGradesPageSize: number;
  courseGradesTotalCount: number;
  courseGradesTotalPages: number;
  recentSubmissions: StudentCanvasSubmission[];
  recentSubmissionsPage: number;
  recentSubmissionsPageSize: number;
  recentSubmissionsTotalCount: number;
  recentSubmissionsTotalPages: number;
  journey: StudentCanvasJourneyPoint[];
}

export interface StudentCanvasCoursesPage {
  items: StudentCanvasCourse[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface StudentCanvasSubmissionsPage {
  items: StudentCanvasSubmission[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export type CanvasSubmissionCategory = 'graded' | 'submitted' | 'missing' | 'late';

export interface StudentProfileDetail {
  id: number;
  sisStudentCode?: string | null;
  sisUserId: string;
  fullName: string;
  foreignName?: string | null;
  email?: string | null;
  phoneNumber?: string | null;
  dateOfBirth?: string | null;
  gender?: string | null;
  gradeLevel?: string | null;
  homeroom?: string | null;
  homeroomCode?: string | null;
  sisStatus?: string | null;
  riskLevel: RiskLevel;
  flags: RiskFlag[];
  summary: StudentSummary;
  behaviourTrend: StudentBehaviourTrendPoint[];
  behaviours: StudentBehaviour[];
  projects: StudentProject[];
  mentalHealthRecords: StudentMentalHealthRecord[];
  busAttendanceRecords: StudentBusAttendanceRecord[];
  healthVisits: SISHealthVisit[];
  parents: StudentParent[];
  canvas: StudentCanvas;
}

export interface StudentBehaviourTrendPoint {
  year: number;
  month: number;
  total: number;
}

@Injectable({ providedIn: 'root' })
export class StudentsService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  list(
    page = 1,
    pageSize = 20,
    search = '',
    grade = '',
    atRiskOnly = false,
  ): Observable<StudentsPage> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) params = params.set('search', search.trim());
    if (grade) params = params.set('grade', grade);
    if (atRiskOnly) params = params.set('atRiskOnly', true);
    return this.http.get<StudentsPage>(`${this.basePath}/api/students`, { params });
  }

  atRisk(page = 1, pageSize = 10): Observable<AtRiskStudentsPage> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<AtRiskStudentsPage>(`${this.basePath}/api/students/at-risk`, { params });
  }

  commendations(
    page = 1,
    pageSize = 10,
    search = '',
    grade = '',
  ): Observable<StudentCommendationsPage> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search.trim()) {
      params = params.set('search', search.trim());
    }
    if (grade) {
      params = params.set('grade', grade);
    }
    return this.http.get<StudentCommendationsPage>(`${this.basePath}/api/students/commendations`, { params });
  }

  classificationSettings(): Observable<StudentClassificationSettings> {
    return this.http.get<StudentClassificationSettings>(`${this.basePath}/api/students/classification-settings`);
  }

  saveClassificationSettings(settings: StudentClassificationSettings): Observable<StudentClassificationSettings> {
    return this.http.put<StudentClassificationSettings>(
      `${this.basePath}/api/students/classification-settings`, settings,
    );
  }

  get(id: number, academicYear?: string): Observable<StudentProfileDetail> {
    const params = academicYear ? new HttpParams().set('academicYear', academicYear) : undefined;
    return this.http.get<StudentProfileDetail>(`${this.basePath}/api/students/${id}`, { params });
  }

  canvasCourseGrades(
    id: number,
    page: number,
    pageSize: number,
    academicYear?: string,
  ): Observable<StudentCanvasCoursesPage> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (academicYear) params = params.set('academicYear', academicYear);
    return this.http.get<StudentCanvasCoursesPage>(
      `${this.basePath}/api/students/${id}/canvas/course-grades`,
      { params },
    );
  }

  canvasRecentSubmissions(
    id: number,
    page: number,
    pageSize: number,
  ): Observable<StudentCanvasSubmissionsPage> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<StudentCanvasSubmissionsPage>(
      `${this.basePath}/api/students/${id}/canvas/recent-submissions`,
      { params },
    );
  }

  canvasSubmissions(
    id: number,
    category: CanvasSubmissionCategory,
    page: number,
    pageSize: number,
    academicYear?: string | null,
  ): Observable<StudentCanvasSubmissionsPage> {
    let params = new HttpParams()
      .set('category', category)
      .set('page', page)
      .set('pageSize', pageSize);
    if (academicYear) params = params.set('academicYear', academicYear);
    return this.http.get<StudentCanvasSubmissionsPage>(
      `${this.basePath}/api/students/${id}/canvas/submissions`,
      { params },
    );
  }
}
