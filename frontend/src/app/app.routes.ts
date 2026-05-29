import { Routes } from '@angular/router';
import { LoginComponent } from './auth/login/login.component';
import { DashboardComponent } from './dashboard/dashboard.component';
import { MainLayoutComponent } from './main-layout/main-layout.component';
import { authGuard } from './core/auth/auth.guard';
import { ReportsComponent } from './reports/reports.component';
import { AcademicOverviewReportComponent } from './reports/academic-overview-report.component';
import { AttendanceEngagementReportComponent } from './reports/attendance-engagement-report.component';
import { StudentWellbeingReportComponent } from './reports/student-wellbeing-report.component';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  {
    path: '',
    component: MainLayoutComponent,
    canActivate: [authGuard],
    children: [
      { path: 'dashboard', component: DashboardComponent },
      { path: 'reports', component: ReportsComponent },
      { path: 'reports/academic-overview', component: AcademicOverviewReportComponent },
      { path: 'reports/attendance-engagement', component: AttendanceEngagementReportComponent },
      { path: 'reports/student-wellbeing', component: StudentWellbeingReportComponent },
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
    ],
  },
  { path: '**', redirectTo: 'dashboard' },
];
