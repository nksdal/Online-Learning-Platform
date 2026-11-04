import { CanActivateFn, Router } from '@angular/router';
import { inject } from '@angular/core';
import { AuthService } from './auth.service';

export const adminGuard: CanActivateFn = (_route, state) => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) {
    return router.createUrlTree(['/login'], { queryParams: { returnUrl: state.url } });
  }
  if (auth.mustChangePassword()) return router.createUrlTree(['/change-password']);
  return auth.user()?.roles.includes('Admin') ? true : router.createUrlTree(['/']);
};

export const changePasswordGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  if (!auth.isAuthenticated()) return router.createUrlTree(['/login']);
  if (auth.mustChangePassword()) return true;
  const roles = auth.user()?.roles ?? [];
  return router.createUrlTree([roles.includes('Admin') ? '/admin' : roles.includes('Teacher') ? '/teacher' : '/my-learning']);
};
