export type ImportModule = {
  id: string;
  titleKey: string;
  descriptionKey: string;
  headers: string[];
  requiredHeaders: string[];
};

export type ImportRecord = {
  id: string;
  moduleId: string;
  moduleTitleKey: string;
  fileName: string;
  importedAt: string;
  importedBy: string;
  importedByName: string;
  status: 'Valid' | 'Rejected';
  validRows: number;
  invalidRows: number;
};

export type ValidationResult = {
  status: 'Valid' | 'Rejected';
  errors: string[];
  validRows: number;
  invalidRows: number;
};

export const importModules: ImportModule[] = [
  {
    id: 'student-behaviours',
    titleKey: 'imports.modules.behaviours.title',
    descriptionKey: 'imports.modules.behaviours.description',
    headers: ['student_id', 'student_name', 'incident_date', 'behaviour_type', 'notes'],
    requiredHeaders: ['student_id', 'student_name', 'incident_date', 'behaviour_type'],
  },
  {
    id: 'student-projects',
    titleKey: 'imports.modules.projects.title',
    descriptionKey: 'imports.modules.projects.description',
    headers: ['student_id', 'student_name', 'project_name', 'subject', 'status', 'teacher_notes'],
    requiredHeaders: ['student_id', 'student_name', 'project_name', 'subject', 'status'],
  },
  {
    id: 'student-parents',
    titleKey: 'imports.modules.parents.title',
    descriptionKey: 'imports.modules.parents.description',
    headers: ['student_id', 'student_name', 'parent_name', 'relationship', 'phone', 'email'],
    requiredHeaders: ['student_id', 'student_name', 'parent_name', 'relationship'],
  },
];
