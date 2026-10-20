import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { retry, throwError, timer } from 'rxjs';
import { AuthenticationService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthenticationService);
  const token = authService.getToken();

  return next(
    req.clone({
      withCredentials: true,
      ...(token ? { setHeaders: { Authorization: `Bearer ${token}` } } : {}),
    }),
  ).pipe(retry({
    count: 2,
    delay: (error: HttpErrorResponse, attempt) =>
      ['GET', 'HEAD'].includes(req.method) && [0, 502, 503, 504].includes(error.status)
        ? timer(500 * attempt)
        : throwError(() => error),
  }));
};
