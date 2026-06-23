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
        path: 'admin/teacher-student-assignments',
        loadComponent: () =>
          import('./admin/teacher-student-assignment.component').then(
            (m) => m.TeacherStudentAssignmentComponent,
          ),
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
