export type ImportModule = {
  id: string;
  category: ImportModuleCategoryId;
  titleKey: string;
  descriptionKey: string;
  headers: string[];
  requiredHeaders: string[];
  supportsManualEntry?: boolean;
  supportsCustomFields?: boolean;
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
    headers: [
      'student_id', 'student_name', 'incident_date', 'incident_time', 'behaviour_type', 'notes',
      'subject', 'academic_year', 'term', 'reporter_name', 'recorded_by_email', 'recorded_by_title', 'decision',
      'conduct_deduction_level', 'occurrence_count', 'behaviour_points_deducted',
    ],
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
  {
    id: 'student-health-visits',
    category: 'health-wellbeing',
    titleKey: 'imports.modules.healthVisits.title',
    descriptionKey: 'imports.modules.healthVisits.description',
    headers: [
      'student_id', 'student_name', 'visit_date', 'classification', 'diagnosis',
      'symptoms', 'treatment', 'medication', 'notes', 'notification_sent',
    ],
    requiredHeaders: ['student_id', 'student_name', 'visit_date', 'symptoms'],
  },
];
