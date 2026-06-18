import { Routes } from '@angular/router';
import { authGuard } from './core/auth/auth.guard';
import { adminGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  {
    path: 'login',
    loadComponent: () => import('./auth/login/login.component').then((m) => m.LoginComponent),
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
        path: 'reports',
        loadComponent: () => import('./reports/reports.component').then((m) => m.ReportsComponent),
      },
      {
        path: 'workspace',
        loadComponent: () =>
          import('./workspace/workspace.component').then((m) => m.WorkspaceComponent),
      },
      {
        path: 'workspace/:workspaceId',
        loadComponent: () =>
          import('./workspace/workspace-detail.component').then((m) => m.WorkspaceDetailComponent),
      },
      {
        path: 'reports/academic-overview',
        loadComponent: () =>
          import('./reports/academic-overview-report.component').then(
            (m) => m.AcademicOverviewReportComponent,
          ),
      },
      {
        path: 'reports/attendance-engagement',
        loadComponent: () =>
          import('./reports/attendance-engagement-report.component').then(
            (m) => m.AttendanceEngagementReportComponent,
          ),
      },
      {
        path: 'reports/student-wellbeing',
        loadComponent: () =>
          import('./reports/student-wellbeing-report.component').then(
            (m) => m.StudentWellbeingReportComponent,
          ),
      },
      {
        path: 'imports',
        loadComponent: () =>
          import('./imports/manual-excel-import.component').then(
            (m) => m.ManualExcelImportComponent,
          ),
      },
      {
        path: 'imports/:moduleId',
        loadComponent: () =>
          import('./imports/manual-excel-import-detail.component').then(
            (m) => m.ManualExcelImportDetailComponent,
          ),
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
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
    ],
  },
  { path: '**', redirectTo: 'dashboard' },
];
