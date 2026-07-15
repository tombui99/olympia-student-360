import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { BASE_PATH } from '../api/variables';

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

  list(moduleId: string): Observable<ImportModuleField[]> {
    return this.http.get<ImportModuleField[]>(this.url(moduleId));
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
}
