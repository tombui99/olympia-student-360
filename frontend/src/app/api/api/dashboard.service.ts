import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { BASE_PATH } from '../variables';
import { DashboardSummaryDto } from '../model/models';

@Injectable({ providedIn: 'root' })
export class DashboardService {
  private readonly http = inject(HttpClient);
  private readonly basePath = inject(BASE_PATH);

  apiDashboardSummaryGet(): Observable<DashboardSummaryDto> {
    return this.http.get<DashboardSummaryDto>(`${this.basePath}/api/Dashboard/summary`);
  }
}
