export type ImportModule = {
  id: string;
  category: ImportModuleCategoryId;
  titleKey: string;
  descriptionKey: string;
  headers: string[];
  requiredHeaders: string[];
  fileKind?: 'tabular-text' | 'sis-roster-xlsx';
  supportsManualEntry?: boolean;
  supportsCustomFields?: boolean;
};

export type ImportModuleCategoryId = 'student-records' | 'student-development' | 'health-wellbeing';

export type ImportModuleCategory = {
  id: ImportModuleCategoryId;
  titleKey: string;
  descriptionKey: string;
};

export const importModuleCategories: ImportModuleCategory[] = [
  {
    id: 'student-records',
    titleKey: 'imports.categories.studentRecords.title',
    descriptionKey: 'imports.categories.studentRecords.description',
  },
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
    id: 'sis-students',
    category: 'student-records',
    titleKey: 'imports.modules.sisStudents.title',
    descriptionKey: 'imports.modules.sisStudents.description',
    headers: [
      'code', 'fullname', 'foreign_name', 'gender', 'date_of_birth',
      'homeroom_class', 'code_homeroom_class', 'email',
      'fa_fullname', 'fa_username', 'fa_phone', 'fa_email', 'father_job', 'NAM_SINH_CHA',
      'mo_fullname', 'mo_username', 'mo_phone', 'mo_email', 'mother_job', 'NAM_SINH_ME',
      'tutor_fullname', 'tutor_job', 'NAM_SINH_NGUOI_DO_DAU', 'tutor_phone', 'phone', 'status',
    ],
    requiredHeaders: ['code', 'fullname', 'date_of_birth', 'homeroom_class', 'code_homeroom_class', 'email'],
    fileKind: 'sis-roster-xlsx',
    supportsManualEntry: false,
    supportsCustomFields: false,
  },
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
    id: 'student-bus-attendance',
    category: 'student-development',
    titleKey: 'imports.modules.busAttendance.title',
    descriptionKey: 'imports.modules.busAttendance.description',
    headers: [
      'student_id', 'student_name', 'attendance_date', 'trip_direction', 'violation_type',
      'bus_code', 'route_name', 'notes',
    ],
    requiredHeaders: [
      'student_id', 'student_name', 'attendance_date', 'trip_direction', 'violation_type',
    ],
  },
  {
    id: 'student-mental-health',
    category: 'health-wellbeing',
    titleKey: 'imports.modules.mentalHealth.title',
    descriptionKey: 'imports.modules.mentalHealth.description',
    headers: [
      'student_id', 'student_name', 'record_date', 'academic_year', 'concern_type',
      'risk_level', 'notes',
    ],
    requiredHeaders: [
      'student_id', 'student_name', 'record_date', 'academic_year', 'concern_type', 'risk_level',
    ],
  },
];
