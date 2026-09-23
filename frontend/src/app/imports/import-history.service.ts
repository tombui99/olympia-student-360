import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map } from 'rxjs';
import { BASE_PATH } from '../api/variables';
import { ImportRecord, importModules } from './import-template-data';

type ImportHistoryResponse = {
  id: number;
  moduleId: string;
  fileName: string;
  importedAt: string;
  importedBy: string;
  importedByName: string;
  status: 'Valid' | 'Rejected';
  validRows: number;
  invalidRows: number;
};

export type ImportHistoryRecordResponse = {
  id: number;
  sisUserId: string;
  studentName: string;
  values: Record<string, string | null>;
};

export type ImportHistoryDetailResponse = ImportHistoryResponse & {
  records: ImportHistoryRecordResponse[];
};

type ImportHistoryPageResponse = {
  items: ImportHistoryResponse[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
};

@Injectable({ providedIn: 'root' })
export class ImportHistoryService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  list(page = 1, pageSize = 20) {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http
      .get<ImportHistoryPageResponse>(`${this.basePath}/api/import-history`, { params })
      .pipe(map((response) => ({
        ...response,
        items: response.items.map((record) => this.toRecord(record)),
      })));
  }

  detail(id: number) {
    return this.http.get<ImportHistoryDetailResponse>(`${this.basePath}/api/import-history/${id}`);
  }

  rename(id: number, fileName: string) {
    return this.http.put<void>(`${this.basePath}/api/import-history/${id}`, { fileName });
  }

  updateRecord(id: number, recordId: number, values: Record<string, string | null>) {
    return this.http.put<void>(`${this.basePath}/api/import-history/${id}/records/${recordId}`, { values });
  }

  deleteRecord(id: number, recordId: number) {
    return this.http.delete<void>(`${this.basePath}/api/import-history/${id}/records/${recordId}`);
  }

  deleteHistory(id: number) {
    return this.http.delete<void>(`${this.basePath}/api/import-history/${id}`);
  }

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

    return this.http
      .post<ImportHistoryResponse>(`${this.basePath}/api/imports/${endpoint}`, {
        fileName,
        dataStartRow,
        rows: mappedRows,
      })
      .pipe(map((record) => this.toRecord(record)));
  }

  private toRecord(record: ImportHistoryResponse): ImportRecord {
    const module = importModules.find((candidate) => candidate.id === record.moduleId);
    return {
      ...record,
      moduleTitleKey: module?.titleKey ?? record.moduleId,
    };
  }

  private booleanValue(value: string | undefined): boolean {
    return ['true', '1', 'yes', 'y', 'on', 'có'].includes(value?.trim().toLocaleLowerCase() ?? '');
  }
}
