import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { AuthenticationService } from './auth.service';

export const authInterceptor: HttpInterceptorFn = (req, next) => {
  const authService = inject(AuthenticationService);
  const token = authService.getToken();

  if (!token) {
    return next(req.clone({ withCredentials: true }));
  }

  return next(
    req.clone({
      withCredentials: true,
      setHeaders: { Authorization: `Bearer ${token}` },
    }),
  );
};
