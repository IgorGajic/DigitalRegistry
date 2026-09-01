import { HttpContextToken, HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { catchError, throwError } from 'rxjs';

import { AuthService } from '../auth/auth.service';
import { API_BASE_URL, LICENSE_ROUTE } from '../config/tokens';
import { toSerbian } from './messages';

/**
 * Marks a request as belonging to a scanned table session rather than to the signed-in member of
 * staff.
 *
 * A guest's phone and a waiter's tablet run the same application, and a table session is not the
 * till's session: it carries its own token, it must not overwrite the staff one, and when it lapses
 * it must not sign the waiter out. The flag keeps both interceptors from treating it as ours.
 */
export const TABLE_SESSION_REQUEST = new HttpContextToken<boolean>(() => false);

/** Attaches the bearer token to calls going to our own API, and to nothing else. */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const baseUrl = inject(API_BASE_URL);
  const token = auth.token();

  // A table session brings its own Authorization header; overwriting it would send the guest's
  // order under whichever staff account last used this browser.
  if (request.context.get(TABLE_SESSION_REQUEST)) {
    return next(request);
  }

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
        // The licence screen is a staff screen: it names the venue and tells whoever is reading to
        // call the platform administrator. A guest at a table gets told, by their own screen, that
        // the venue is not taking orders — not sent somewhere they can do nothing about.
        if (licenseRoute && !request.context.get(TABLE_SESSION_REQUEST)) {
          void router.navigate([licenseRoute]);
        }

        return throwError(() => error);
      }

      if (error.status === 401) {
        // A lapsed table session is the guest's problem, not the till's; the guest screen says so
        // itself. Signing the venue's staff out because a QR session timed out would be absurd.
        if (request.context.get(TABLE_SESSION_REQUEST)) {
          return throwError(() => error);
        }

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
 * Reads an RFC 7807 problem response, in Serbian.
 *
 * The API returns validation failures as `errors`, keyed by field, and everything else as `detail`.
 * Falling back through both means a message never comes out as "[object Object]".
 *
 * The API answers in English, so everything that comes out of here goes through {@link toSerbian}.
 * That is the single point where it can be done: this function is what the snackbar shows and what
 * both sign-in forms print, so nothing reaches a screen around it.
 */
export function describe(error: HttpErrorResponse): string {
  const problem = error.error;

  if (typeof problem === 'string' && problem.trim()) {
    return toSerbian(problem);
  }

  if (problem?.errors && typeof problem.errors === 'object') {
    const messages = Object.values(problem.errors as Record<string, string[]>).flat();

    if (messages.length) {
      // Each field's complaint is translated on its own: they are separate sentences from separate
      // validators, and a joined string would match nothing.
      return messages.map(toSerbian).join(' ');
    }
  }

  const detail = problem?.detail || problem?.title;

  return detail ? toSerbian(detail) : 'Došlo je do greške.';
}

function notify(snackBar: MatSnackBar, message: string): void {
  snackBar.open(message, 'U redu', { duration: 6000 });
}
