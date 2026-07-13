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
        path: 'students',
        loadComponent: () =>
          import('./students/students.component').then((m) => m.StudentsComponent),
      },
      {
        path: 'at-risk',
        loadComponent: () =>
          import('./students/at-risk.component').then((m) => m.AtRiskComponent),
      },
      {
        path: 'commendations',
        loadComponent: () =>
          import('./students/commendations.component').then((m) => m.CommendationsComponent),
      },
      {
        path: 'cohorts',
        loadComponent: () =>
          import('./students/cohorts.component').then((m) => m.CohortsComponent),
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
