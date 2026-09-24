import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { BASE_PATH } from '../api/variables';

type ExcelImportResult = {
  moduleId: string;
  importedRows: number;
};

@Injectable({ providedIn: 'root' })
export class ExcelImportService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  importExcel(
    moduleId: string,
    fileName: string,
    rows: Record<string, string>[],
    customFieldKeys: string[] = [],
    dataStartRow = 2,
  ) {
    const endpoint = moduleId;
    const customFieldsFor = (row: Record<string, string>): Record<string, string> =>
      Object.fromEntries(customFieldKeys.map((key) => [key, row[key] ?? '']));

    const mappedRows = moduleId === 'student-behaviours'
      ? rows.map((row) => ({
          studentId: row['student_id'],
          studentName: row['student_name'],
          incidentDate: row['incident_date'],
          behaviourType: row['behaviour_type'],
          notes: row['notes'] || null,
          incidentTimeText: row['incident_time'] || null,
          subject: row['subject'] || null,
          academicYear: row['academic_year'] || null,
          term: row['term'] || null,
          recordedByEmail: row['recorded_by_email'] || null,
          recordedByTitle: row['recorded_by_title'] || null,
          decision: row['decision'] || null,
          conductDeductionLevel: row['conduct_deduction_level'] || null,
          occurrenceCount: row['occurrence_count'] ? Number(row['occurrence_count']) : null,
          behaviourPointsDeducted: row['behaviour_points_deducted'] || null,
          sourceRecordId: row['source_record_id'] ? Number(row['source_record_id']) : null,
          sourceStudentName: row['student_name'] || null,
          sourceGradeLevel: row['source_grade_level'] || null,
          studentMatchStatus: row['student_match_status'] || null,
          studentMatchNotes: row['student_match_notes'] || null,
          sourceDataIssues: row['source_data_issues'] || null,
          customFields: customFieldsFor(row),
        }))
      : moduleId === 'student-projects' ? rows.map((row) => ({
          studentId: row['student_id'],
          studentName: row['student_name'],
          projectName: row['project_name'],
          subject: row['subject'],
          status: row['status'],
          teacherNotes: row['teacher_notes'] || null,
          customFields: customFieldsFor(row),
        })) : moduleId === 'student-bus-attendance' ? rows.map((row) => ({
          studentId: row['student_id'],
          studentName: row['student_name'],
          attendanceDate: row['attendance_date'],
          tripDirection: row['trip_direction'],
          violationType: row['violation_type'],
          busCode: row['bus_code'] || null,
          routeName: row['route_name'] || null,
          notes: row['notes'] || null,
          customFields: customFieldsFor(row),
        })) : moduleId === 'student-mental-health' ? rows.map((row) => ({
          studentId: row['student_id'],
          studentName: row['student_name'],
          recordDate: row['record_date'],
          academicYear: row['academic_year'],
          concernType: row['concern_type'],
          riskLevel: row['risk_level'],
          notes: row['notes'] || null,
          customFields: customFieldsFor(row),
        })) : rows.map((row) => ({
          studentId: row['student_id'],
          studentName: row['student_name'],
          visitDate: row['visit_date'],
          symptoms: row['symptoms'],
          diagnosis: row['diagnosis'] || null,
          classification: row['classification'] || null,
          treatment: row['treatment'] || null,
          medication: row['medication'] || null,
          notes: row['notes'] || null,
          notificationSent: this.booleanValue(row['notification_sent']),
          customFields: customFieldsFor(row),
        }));

    return this.http.post<ExcelImportResult>(`${this.basePath}/api/imports/${endpoint}`, {
      fileName,
      dataStartRow,
      rows: mappedRows,
    });
  }

  private booleanValue(value: string | undefined): boolean {
    return ['true', '1', 'yes', 'y', 'on', 'có'].includes(value?.trim().toLocaleLowerCase() ?? '');
  }
}
