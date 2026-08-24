import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { AuthService } from '../auth/auth.service';
import { API_BASE_URL, LICENSE_ROUTE } from '../config/tokens';

/** Attaches the bearer token to calls going to our own API, and to nothing else. */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const baseUrl = inject(API_BASE_URL);
  const token = auth.token();

  if (!token || !request.url.startsWith(baseUrl)) {
    return next(request);
  }

  return next(
    request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }),
  );
};

/**
 * Turns the API's failures into something the person in front of the screen can act on.
 *
 * The three cases that are not simply "an error" get their own handling:
 *
 * - **402** means the venue has not paid. The caller is who they say they are and would be allowed
 *   to do this, so it is not an access failure and must not read as one — it goes to the renewal
 *   screen.
 * - **401** means the session is over. Signing out locally stops a stale token producing a wall of
 *   further 401s.
 * - **403** means the role is wrong. Worth saying plainly, because the alternative is a screen that
 *   silently does nothing.
 */
export const errorInterceptor: HttpInterceptorFn = (request, next) => {
  const router = inject(Router);
  const snackBar = inject(MatSnackBar);
  const auth = inject(AuthService);
  const licenseRoute = inject(LICENSE_ROUTE, { optional: true });

  return next(request).pipe(
    catchError((error: HttpErrorResponse) => {
      if (error.status === 402) {
        if (licenseRoute) {
          void router.navigate([licenseRoute]);
        }

        return throwError(() => error);
      }

      if (error.status === 401) {
        // Sign-in failures are the login form's business; anything else means the session lapsed.
        if (!request.url.includes('/auth/login')) {
          auth.logout();
          notify(snackBar, 'Sesija je istekla. Prijavite se ponovo.');
        }

        return throwError(() => error);
      }

      if (error.status === 403) {
        notify(snackBar, 'Nemate ovlašćenje za ovu radnju.');
        return throwError(() => error);
      }

      if (error.status === 0) {
        notify(snackBar, 'Server nije dostupan. Proverite vezu.');
        return throwError(() => error);
      }

      notify(snackBar, describe(error));

      return throwError(() => error);
    }),
  );
};

/**
 * Reads an RFC 7807 problem response.
 *
 * The API returns validation failures as `errors`, keyed by field, and everything else as `detail`.
 * Falling back through both means a message never comes out as "[object Object]".
 */
export function describe(error: HttpErrorResponse): string {
  const problem = error.error;

  if (typeof problem === 'string' && problem.trim()) {
    return problem;
  }

  if (problem?.errors && typeof problem.errors === 'object') {
    const messages = Object.values(problem.errors as Record<string, string[]>).flat();

    if (messages.length) {
      return messages.join(' ');
    }
  }

  return problem?.detail || problem?.title || 'Došlo je do greške.';
}

function notify(snackBar: MatSnackBar, message: string): void {
  snackBar.open(message, 'U redu', { duration: 6000 });
}
