import { Routes } from '@angular/router';
import { LoginComponent } from './auth/login/login.component';
import { DashboardComponent } from './dashboard/dashboard.component';
import { MainLayoutComponent } from './main-layout/main-layout.component';
import { authGuard } from './core/auth/auth.guard';
import { ReportsComponent } from './reports/reports.component';
import { WorkspaceComponent } from './workspace/workspace.component';
import { WorkspaceDetailComponent } from './workspace/workspace-detail.component';
import { AcademicOverviewReportComponent } from './reports/academic-overview-report.component';
import { AttendanceEngagementReportComponent } from './reports/attendance-engagement-report.component';
import { StudentWellbeingReportComponent } from './reports/student-wellbeing-report.component';
import { ManualExcelImportComponent } from './imports/manual-excel-import.component';
import { ManualExcelImportDetailComponent } from './imports/manual-excel-import-detail.component';
import { AccountManagementComponent } from './admin/account-management.component';
import { SyncHistoryComponent } from './admin/sync-history.component';
import { adminGuard } from './core/auth/auth.guard';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: 'dashboard', component: DashboardComponent },
      { path: 'reports', component: ReportsComponent },
      { path: 'workspace', component: WorkspaceComponent },
      { path: 'workspace/:workspaceId', component: WorkspaceDetailComponent },
      { path: 'reports/academic-overview', component: AcademicOverviewReportComponent },
      { path: 'reports/attendance-engagement', component: AttendanceEngagementReportComponent },
      { path: 'reports/student-wellbeing', component: StudentWellbeingReportComponent },
      { path: 'imports', component: ManualExcelImportComponent },
      { path: 'imports/:moduleId', component: ManualExcelImportDetailComponent },
      { path: 'admin/accounts', component: AccountManagementComponent, canActivate: [adminGuard] },
      { path: 'admin/sync-history', component: SyncHistoryComponent, canActivate: [adminGuard] },
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
    ],
  },
  { path: '**', redirectTo: 'dashboard' },
];
