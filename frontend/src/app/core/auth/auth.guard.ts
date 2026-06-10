import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { map } from 'rxjs';
import { AuthenticationService } from './auth.service';

export const authGuard: CanActivateFn = () => {
  const authService = inject(AuthenticationService);
  const router = inject(Router);

  return authService.ensureCurrentUser().pipe(
    map((authenticated) => (authenticated ? true : router.createUrlTree(['/login']))),
  );
};

export const adminGuard: CanActivateFn = () => {
  const authService = inject(AuthenticationService);
  const router = inject(Router);

  return authService.ensureCurrentUser().pipe(
    map((authenticated) => {
      if (!authenticated) {
        return router.createUrlTree(['/login']);
      }

      return authService.getUserRole() === 'Admin' ? true : router.createUrlTree(['/dashboard']);
    }),
  );
};
