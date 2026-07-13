import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { map } from 'rxjs';
import { BASE_PATH } from '../api/variables';
import { ImportRecord, importModules } from './import-template-data';

type ImportHistoryResponse = {
  id: number;
  moduleId: string;
  source: 'Manual' | 'Excel';
  fileName: string;
  importedAt: string;
  importedBy: string;
  importedByName: string;
  status: 'Valid' | 'Rejected';
  validRows: number;
  invalidRows: number;
};

@Injectable({ providedIn: 'root' })
export class ImportHistoryService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  list() {
    return this.http
      .get<ImportHistoryResponse[]>(`${this.basePath}/api/import-history`)
      .pipe(map((records) => records.map((record) => this.toRecord(record))));
  }

  importExcel(moduleId: string, fileName: string, rows: Record<string, string>[]) {
    const endpoint = moduleId === 'student-behaviours' ? 'student-behaviours' : 'student-projects';
    const mappedRows = moduleId === 'student-behaviours'
      ? rows.map((row) => ({
          studentId: row['student_id'],
          studentName: row['student_name'],
          incidentDate: row['incident_date'],
          behaviourType: row['behaviour_type'],
          notes: row['notes'] || null,
        }))
      : rows.map((row) => ({
          studentId: row['student_id'],
          studentName: row['student_name'],
          projectName: row['project_name'],
          subject: row['subject'],
          status: row['status'],
          teacherNotes: row['teacher_notes'] || null,
        }));

    return this.http
      .post<ImportHistoryResponse>(`${this.basePath}/api/imports/${endpoint}`, {
        fileName,
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
}
