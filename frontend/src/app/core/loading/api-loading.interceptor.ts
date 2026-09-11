import { HttpContextToken, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { finalize } from 'rxjs';
import { BASE_PATH } from '../../api/variables';
import { ApiLoadingService } from './api-loading.service';

export const SKIP_GLOBAL_API_LOADING = new HttpContextToken<boolean>(() => false);

export const apiLoadingInterceptor: HttpInterceptorFn = (request, next) => {
  const basePath = inject(BASE_PATH);
  const apiPrefix = `${basePath.replace(/\/$/, '')}/api/`;

  // Page data is loaded through API GET requests. Mutations keep their local button/progress
  // states and should not cover the entire page with the navigation loader.
  if (
    request.method !== 'GET'
    || !request.url.startsWith(apiPrefix)
    || request.context.get(SKIP_GLOBAL_API_LOADING)
  ) {
    return next(request);
  }

  const loading = inject(ApiLoadingService);
  loading.begin();

  return next(request).pipe(finalize(() => loading.end()));
};
