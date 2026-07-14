export type ImportModule = {
  id: string;
  category: ImportModuleCategoryId;
  titleKey: string;
  descriptionKey: string;
  headers: string[];
  requiredHeaders: string[];
};

export type ImportModuleCategoryId = 'student-development' | 'health-wellbeing';

export type ImportModuleCategory = {
  id: ImportModuleCategoryId;
  titleKey: string;
  descriptionKey: string;
};

export const importModuleCategories: ImportModuleCategory[] = [
  {
    id: 'student-development',
    titleKey: 'imports.categories.studentDevelopment.title',
    descriptionKey: 'imports.categories.studentDevelopment.description',
  },
  {
    id: 'health-wellbeing',
    titleKey: 'imports.categories.healthWellbeing.title',
    descriptionKey: 'imports.categories.healthWellbeing.description',
  },
];

export type ImportRecord = {
  id: number;
  moduleId: string;
  moduleTitleKey: string;
  source: 'Manual' | 'Excel';
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
    category: 'student-development',
    titleKey: 'imports.modules.behaviours.title',
    descriptionKey: 'imports.modules.behaviours.description',
    headers: ['student_id', 'student_name', 'incident_date', 'behaviour_type', 'notes'],
    requiredHeaders: ['student_id', 'student_name', 'incident_date', 'behaviour_type'],
  },
  {
    id: 'student-projects',
    category: 'student-development',
    titleKey: 'imports.modules.projects.title',
    descriptionKey: 'imports.modules.projects.description',
    headers: ['student_id', 'student_name', 'project_name', 'subject', 'status', 'teacher_notes'],
    requiredHeaders: ['student_id', 'student_name', 'project_name', 'subject', 'status'],
  },
  {
    id: 'student-health-incidents',
    category: 'health-wellbeing',
    titleKey: 'imports.modules.health.title',
    descriptionKey: 'imports.modules.health.description',
    headers: [
      'student_id', 'student_name', 'incident_date', 'academic_year', 'incident_type',
      'severity_level', 'body_area', 'location', 'cause', 'action_taken',
    ],
    requiredHeaders: [
      'student_id', 'student_name', 'incident_date', 'academic_year', 'incident_type',
      'severity_level', 'cause',
    ],
  },
];
