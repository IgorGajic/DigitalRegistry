import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';

import { AuthService } from './auth.service';
import { UserRole } from '../models/enums';
import { LOGIN_ROUTE } from '../config/tokens';

/** Keeps unauthenticated people out of everything except the sign-in screen. */
export const authGuard: CanActivateFn = () => {
  const auth = inject(AuthService);
  const router = inject(Router);
  const loginRoute = inject(LOGIN_ROUTE);

  return auth.isSignedIn() ? true : router.createUrlTree([loginRoute]);
};

/**
 * Restricts a route to given roles.
 *
 * Convenience only — the API enforces the same matrix and is the authority. This exists so a waiter
 * is not shown a reports screen that would answer every request with 403.
 */
export function roleGuard(...roles: UserRole[]): CanActivateFn {
  return () => {
    const auth = inject(AuthService);
    const router = inject(Router);
    const loginRoute = inject(LOGIN_ROUTE);

    if (!auth.isSignedIn()) {
      return router.createUrlTree([loginRoute]);
    }

    // Sent back to the application root rather than to sign-in: they are signed in perfectly well,
    // just not as somebody who may see this.
    return auth.hasAnyRole(...roles) ? true : router.createUrlTree(['/']);
  };
}
