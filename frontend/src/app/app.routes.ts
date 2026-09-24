import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { adminGuard } from './core/auth/auth.guard';
import { formManagerGuard } from './core/auth/auth.guard';
import { accessManagerGuard, scopeGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./auth/login/login.component').then((m) => m.LoginComponent),
  },
  {
    path: 'f/:publicId',
    loadComponent: () => import('./custom-forms/public-form.component').then((m) => m.PublicFormComponent),
  },
  {
    path: '',
    loadComponent: () =>
      import('./main-layout/main-layout.component').then((m) => m.MainLayoutComponent),
    canActivate: [authGuard],
    children: [
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./dashboard/dashboard.component').then((m) => m.DashboardComponent),
      },
      {
        path: 'students',
        loadComponent: () =>
          import('./students/students.component').then((m) => m.StudentsComponent),
      },
      {
        path: 'at-risk',
        loadComponent: () =>
          import('./students/at-risk.component').then((m) => m.AtRiskComponent),
        canActivate: [scopeGuard],
        data: { access: 'atRisk' },
      },
      {
        path: 'commendations',
        loadComponent: () =>
          import('./students/commendations.component').then((m) => m.CommendationsComponent),
        canActivate: [scopeGuard],
        data: { access: 'commendations' },
      },
      {
        path: 'transcripts',
        loadComponent: () =>
          import('./transcripts/transcripts-layout.component').then((m) => m.TranscriptsLayoutComponent),
        canActivate: [scopeGuard],
        data: { access: 'academic' },
        children: [
          {
            path: '',
            loadComponent: () =>
              import('./transcripts/transcripts.component').then((m) => m.TranscriptsComponent),
          },
          {
            path: 'student',
            loadComponent: () =>
              import('./transcripts/student-transcript.component').then((m) => m.StudentTranscriptComponent),
          },
          {
            path: 'student/:studentId',
            loadComponent: () =>
              import('./transcripts/student-transcript.component').then((m) => m.StudentTranscriptComponent),
          },
          {
            path: ':academicYear/grades/:gradeLevel',
            loadComponent: () =>
              import('./transcripts/transcript-grade-detail.component').then((m) => m.TranscriptGradeDetailComponent),
          },
        ],
      },
      {
        path: 'health-visits',
        loadComponent: () => import('./module-records/module-records.component').then((m) => m.ModuleRecordsComponent),
        canActivate: [scopeGuard],
        data: { moduleId: 'student-health-visits', access: 'health' },
      },
      {
        path: 'behaviours',
        loadComponent: () => import('./module-records/module-records.component').then((m) => m.ModuleRecordsComponent),
        data: { moduleId: 'student-behaviours', access: 'academic' },
        canActivate: [scopeGuard],
      },
      {
        path: 'projects',
        loadComponent: () => import('./module-records/module-records.component').then((m) => m.ModuleRecordsComponent),
        data: { moduleId: 'student-projects', access: 'academic' },
        canActivate: [scopeGuard],
      },
      {
        path: 'bus-attendance',
        loadComponent: () => import('./module-records/module-records.component').then((m) => m.ModuleRecordsComponent),
        data: { moduleId: 'student-bus-attendance', access: 'bus' },
        canActivate: [scopeGuard],
      },
      {
        path: 'mental-health',
        loadComponent: () => import('./module-records/module-records.component').then((m) => m.ModuleRecordsComponent),
        data: { moduleId: 'student-mental-health', access: 'health' },
        canActivate: [scopeGuard],
      },
      {
        path: 'students/:studentId',
        loadComponent: () =>
          import('./students/student-profile.component').then((m) => m.StudentProfileComponent),
      },
      {
        path: 'students/:studentId/digest',
        loadComponent: () =>
          import('./students/student-digest.component').then((m) => m.StudentDigestComponent),
        canActivate: [scopeGuard],
        data: { access: 'academic' },
      },
      {
        path: 'workspace',
        loadComponent: () =>
          import('./workspace/workspace.component').then((m) => m.WorkspaceComponent),
        canActivate: [scopeGuard],
        data: { access: 'workspace' },
      },
      {
        path: 'workspace/:workspaceId',
        loadComponent: () =>
          import('./workspace/workspace-detail.component').then((m) => m.WorkspaceDetailComponent),
        canActivate: [scopeGuard],
        data: { access: 'workspace' },
      },
      {
        path: 'reports',
        loadComponent: () =>
          import('./reports/static-reports.component').then((m) => m.StaticReportsComponent),
      },
      {
        path: 'imports',
        loadComponent: () =>
          import('./imports/manual-excel-import.component').then(
            (m) => m.ManualExcelImportComponent,
          ),
      },
      {
        path: 'forms/new',
        loadComponent: () => import('./custom-forms/form-builder.component').then((m) => m.FormBuilderComponent),
        canActivate: [formManagerGuard],
      },
      {
        path: 'forms/:id/responses',
        loadComponent: () => import('./custom-forms/form-responses.component').then((m) => m.FormResponsesComponent),
        canActivate: [formManagerGuard],
      },
      {
        path: 'forms/:id',
        loadComponent: () => import('./custom-forms/form-builder.component').then((m) => m.FormBuilderComponent),
        canActivate: [formManagerGuard],
      },
      {
        path: 'forms',
        loadComponent: () => import('./custom-forms/custom-forms.component').then((m) => m.CustomFormsComponent),
        canActivate: [scopeGuard],
        data: { access: 'academic' },
      },
      {
        path: 'admin/accounts',
        loadComponent: () =>
          import('./admin/account-management.component').then((m) => m.AccountManagementComponent),
        canActivate: [adminGuard],
      },
      {
        path: 'admin/sync-history',
        loadComponent: () =>
          import('./admin/sync-history.component').then((m) => m.SyncHistoryComponent),
        canActivate: [adminGuard],
      },
      {
        path: 'admin/user-access',
        loadComponent: () => import('./admin/user-access-management.component')
          .then((m) => m.UserAccessManagementComponent),
        canActivate: [accessManagerGuard],
      },
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
    ],
  },
  { path: '**', redirectTo: 'dashboard' },
];
