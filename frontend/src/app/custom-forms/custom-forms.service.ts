import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { BASE_PATH } from '../api/variables';

export type FormQuestionType =
  | 'shortText' | 'paragraph' | 'multipleChoice' | 'checkboxes' | 'dropdown'
  | 'linearScale' | 'rating' | 'multipleChoiceGrid' | 'checkboxGrid' | 'date' | 'time';

export interface FormBranch { value: string; targetSectionId: string; }
export interface FormQuestion {
  id: string;
  type: FormQuestionType;
  label: string;
  description?: string | null;
  required: boolean;
  options: string[];
  rows: string[];
  columns: string[];
  min?: number | null;
  max?: number | null;
  minLabel?: string | null;
  maxLabel?: string | null;
  validationPattern?: string | null;
  validationMessage?: string | null;
  branches: FormBranch[];
}
export interface FormSection { id: string; title: string; description?: string | null; questions: FormQuestion[]; }
export interface FormDefinition { sections: FormSection[]; }
export interface CustomFormSummary {
  id: number; publicId: string; title: string; description?: string | null;
  allowMultipleSubmissions: boolean; opensAt?: string | null; closesAt?: string | null;
  responseLimit?: number | null; responseCount: number; createdByName: string; createdAt: string; updatedAt?: string | null;
}
export interface CustomFormDetail extends CustomFormSummary {
  confirmationMessage?: string | null; definition: FormDefinition;
}
export type SaveCustomForm = Omit<CustomFormDetail, 'id' | 'publicId' | 'responseCount' | 'createdByName' | 'createdAt' | 'updatedAt'>;
export interface PublicForm {
  publicId: string; title: string; description?: string | null; confirmationMessage?: string | null;
  allowMultipleSubmissions: boolean; opensAt?: string | null; closesAt?: string | null;
  responseLimit?: number | null; definition: FormDefinition;
}
export interface VerifiedStudent {
  accessToken: string; expiresAt: string; studentProfileId: number; studentId: string; studentName: string;
  gradeLevel?: string | null; homeroom?: string | null; form: PublicForm;
}

export interface FormStudentCandidate {
  studentId: string; studentName: string; gradeLevel?: string | null; homeroom?: string | null;
}
export interface FormResponse {
  id: number; studentProfileId: number; studentId: string; studentName: string; gradeLevel?: string | null;
  homeroom?: string | null; respondentName: string; respondentRelationship: string; respondentEmail: string;
  respondentPhone: string; answers: Record<string, unknown>; questionSnapshot: FormDefinition; submittedAt: string;
}
export interface FormResponsePage { items: FormResponse[]; page: number; pageSize: number; totalCount: number; totalPages: number; }

@Injectable({ providedIn: 'root' })
export class CustomFormsService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  list() { return this.http.get<CustomFormSummary[]>(`${this.basePath}/api/custom-forms`); }
  archived() { return this.http.get<CustomFormSummary[]>(`${this.basePath}/api/custom-forms/archived`); }
  detail(id: number) { return this.http.get<CustomFormDetail>(`${this.basePath}/api/custom-forms/${id}`); }
  create(model: SaveCustomForm) { return this.http.post<CustomFormDetail>(`${this.basePath}/api/custom-forms`, model); }
  update(id: number, model: SaveCustomForm) { return this.http.put<CustomFormDetail>(`${this.basePath}/api/custom-forms/${id}`, model); }
  archive(id: number) { return this.http.delete<void>(`${this.basePath}/api/custom-forms/${id}`); }
  restore(id: number) { return this.http.patch<void>(`${this.basePath}/api/custom-forms/${id}/restore`, {}); }
  responses(id: number, page = 1, pageSize = 20) {
    return this.http.get<FormResponsePage>(`${this.basePath}/api/custom-forms/${id}/responses`, {
      params: new HttpParams().set('page', page).set('pageSize', pageSize),
    });
  }
  deleteResponse(id: number, responseId: number) { return this.http.delete<void>(`${this.basePath}/api/custom-forms/${id}/responses/${responseId}`); }
  exportResponses(id: number) { return this.http.get(`${this.basePath}/api/custom-forms/${id}/responses/export`, { observe: 'response', responseType: 'blob' }); }
  publicSummary(publicId: string) { return this.http.get<{ publicId: string; title: string; description?: string | null }>(`${this.basePath}/api/public/forms/${publicId}`); }
  verify(publicId: string, studentId: string) {
    return this.http.post<VerifiedStudent>(`${this.basePath}/api/public/forms/${publicId}/verify`, { studentId });
  }
  lookupStudents(publicId: string, email: string, phone: string) {
    return this.http.post<FormStudentCandidate[]>(`${this.basePath}/api/public/forms/${publicId}/students/lookup`, { email, phone });
  }
  submit(publicId: string, payload: { accessToken: string; respondentName: string; respondentRelationship: string; respondentEmail: string; respondentPhone: string; answers: Record<string, unknown> }) {
    return this.http.post<{ message: string }>(`${this.basePath}/api/public/forms/${publicId}/responses`, payload);
  }
}

export function newId(prefix: string): string {
  return `${prefix}_${Date.now().toString(36)}_${Math.random().toString(36).slice(2, 8)}`;
}

export function blankQuestion(type: FormQuestionType = 'shortText'): FormQuestion {
  const options = ['multipleChoice', 'checkboxes', 'dropdown'].includes(type) ? ['Option 1'] : [];
  const grid = ['multipleChoiceGrid', 'checkboxGrid'].includes(type);
  return { id: newId('q'), type, label: 'Untitled question', required: false, options,
    rows: grid ? ['Row 1'] : [], columns: grid ? ['Column 1'] : [], min: 1, max: 5, branches: [] };
}

export function blankSection(): FormSection {
  return { id: newId('section'), title: 'Untitled section', description: '', questions: [blankQuestion()] };
}
