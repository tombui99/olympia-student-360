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

export const accessManagerGuard: CanActivateFn = () => {
  const authService = inject(AuthenticationService);
  const router = inject(Router);
  return authService.ensureCurrentUser().pipe(
    map((authenticated) => authenticated && ['Admin', 'SchoolAdministration', 'Principal']
      .includes(authService.getUserRole() || '')
      ? true : router.createUrlTree(authenticated ? ['/dashboard'] : ['/login'])),
  );
};

export const scopeGuard: CanActivateFn = (route) => {
  const authService = inject(AuthenticationService);
  const router = inject(Router);
  return authService.ensureCurrentUser().pipe(
    map((authenticated) => {
      if (!authenticated) return router.createUrlTree(['/login']);
      const capability = route.data['access'] as Parameters<AuthenticationService['canAccess']>[0];
      return !capability || authService.canAccess(capability)
        ? true : router.createUrlTree(['/dashboard']);
    }),
  );
};

export const formManagerGuard: CanActivateFn = () => {
  const authService = inject(AuthenticationService);
  const router = inject(Router);
  return authService.ensureCurrentUser().pipe(
    map((authenticated) => authenticated && authService.canManageForms()
      ? true : router.createUrlTree(authenticated ? ['/dashboard'] : ['/login'])),
  );
};
