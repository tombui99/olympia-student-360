import { HttpClient, HttpContext } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BASE_PATH } from '../api/variables';
import { SKIP_GLOBAL_API_LOADING } from '../core/loading/api-loading.interceptor';

export type CustomFieldType = 'text' | 'textarea' | 'number' | 'date' | 'select' | 'checkbox';

export interface ImportModuleField {
  id: number;
  moduleId: string;
  fieldKey: string;
  label: string;
  fieldType: CustomFieldType;
  required: boolean;
  options: string[];
  sortOrder: number;
}

export interface SaveImportModuleField {
  label: string;
  fieldType: CustomFieldType;
  required: boolean;
  options: string[];
  sortOrder: number;
  fieldKey?: string;
}

export interface StudentRecordFieldOption {
  id: number;
  moduleId: string;
  fieldKey: string;
  value: string;
  optionGroup?: 'positive' | 'concerning' | null;
  sortOrder: number;
}

export interface SaveStudentRecordFieldOption {
  fieldKey: string;
  value: string;
  optionGroup?: 'positive' | 'concerning' | null;
  sortOrder: number;
}

export const CUSTOM_FIELD_TYPES: CustomFieldType[] = [
  'text', 'textarea', 'number', 'date', 'select', 'checkbox',
];

@Injectable({ providedIn: 'root' })
export class ImportModuleFieldsService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  private url(moduleId: string): string {
    return `${this.basePath}/api/imports/modules/${moduleId}/fields`;
  }

  list(moduleId: string, skipGlobalLoading = false): Observable<ImportModuleField[]> {
    const context = skipGlobalLoading
      ? new HttpContext().set(SKIP_GLOBAL_API_LOADING, true)
      : undefined;
    return this.http.get<ImportModuleField[]>(this.url(moduleId), { context });
  }

  create(moduleId: string, field: SaveImportModuleField): Observable<ImportModuleField> {
    return this.http.post<ImportModuleField>(this.url(moduleId), field);
  }

  update(moduleId: string, id: number, field: SaveImportModuleField): Observable<ImportModuleField> {
    return this.http.put<ImportModuleField>(`${this.url(moduleId)}/${id}`, field);
  }

  remove(moduleId: string, id: number): Observable<void> {
    return this.http.delete<void>(`${this.url(moduleId)}/${id}`);
  }

  listStandardOptions(moduleId: string): Observable<StudentRecordFieldOption[]> {
    return this.http.get<StudentRecordFieldOption[]>(`${this.basePath}/api/imports/modules/${moduleId}/field-options`);
  }

  createStandardOption(moduleId: string, option: SaveStudentRecordFieldOption): Observable<StudentRecordFieldOption> {
    return this.http.post<StudentRecordFieldOption>(
      `${this.basePath}/api/imports/modules/${moduleId}/field-options`, option,
    );
  }

  updateStandardOption(moduleId: string, id: number, option: SaveStudentRecordFieldOption): Observable<StudentRecordFieldOption> {
    return this.http.put<StudentRecordFieldOption>(
      `${this.basePath}/api/imports/modules/${moduleId}/field-options/${id}`, option,
    );
  }

  removeStandardOption(moduleId: string, id: number): Observable<void> {
    return this.http.delete<void>(`${this.basePath}/api/imports/modules/${moduleId}/field-options/${id}`);
  }
}
