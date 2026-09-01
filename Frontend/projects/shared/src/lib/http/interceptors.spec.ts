import { HttpContext, HttpErrorResponse, HttpRequest } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Router } from '@angular/router';
import { Observable, firstValueFrom, of, throwError } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AuthService } from '../auth/auth.service';
import { API_BASE_URL, LICENSE_ROUTE } from '../config/tokens';
import { TABLE_SESSION_REQUEST, authInterceptor, describe as describeError, errorInterceptor } from './interceptors';

/**
 * The interceptors decide things no screen can see, and get them wrong in ways nobody notices until
 * a shift goes badly: a lapsed table session signing the venue's staff out, or an unpaid licence
 * reading as an access failure. They are pure functions over a request and a response, which is
 * exactly what a test can pin down.
 */

const BASE = 'http://localhost:5275';

function request(url = `${BASE}/api/menu`, context = new HttpContext()): HttpRequest<unknown> {
  return new HttpRequest('GET', url, { context });
}

/** A request marked as belonging to a scanned table session rather than to the till. */
function tableRequest(url = `${BASE}/api/menu`): HttpRequest<unknown> {
  return request(url, new HttpContext().set(TABLE_SESSION_REQUEST, true));
}

function failWith(status: number) {
  return () =>
    throwError(() => new HttpErrorResponse({ status, url: BASE, error: { detail: 'nope' } }));
}

describe('errorInterceptor', () => {
  let navigate: ReturnType<typeof vi.fn>;
  let logout: ReturnType<typeof vi.fn>;
  let open: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    navigate = vi.fn().mockResolvedValue(true);
    logout = vi.fn();
    open = vi.fn();

    TestBed.configureTestingModule({
      providers: [
        { provide: Router, useValue: { navigate } },
        { provide: MatSnackBar, useValue: { open } },
        { provide: AuthService, useValue: { logout, token: () => 'staff-token' } },
        { provide: API_BASE_URL, useValue: BASE },
        { provide: LICENSE_ROUTE, useValue: '/licenca' },
      ],
    });
  });

  /** Runs the interceptor and swallows the rethrow, which every branch performs. */
  async function intercept(
    req: HttpRequest<unknown>,
    next: () => Observable<never>,
  ): Promise<HttpErrorResponse> {
    return TestBed.runInInjectionContext(
      () =>
        firstValueFrom(errorInterceptor(req, next as never)).catch(
          (error: HttpErrorResponse) => error,
        ) as Promise<HttpErrorResponse>,
    );
  }

  it('sends staff to the licence screen on 402, because they are who they say they are', async () => {
    await intercept(request(), failWith(402));

    expect(navigate).toHaveBeenCalledWith(['/licenca']);
    expect(logout).not.toHaveBeenCalled();
  });

  it('leaves a guest at a table where they are on 402: the licence screen is for staff', async () => {
    await intercept(tableRequest(), failWith(402));

    expect(navigate).not.toHaveBeenCalled();
  });

  it('signs the till out on 401, so a stale token stops producing a wall of failures', async () => {
    await intercept(request(), failWith(401));

    expect(logout).toHaveBeenCalledTimes(1);
    expect(open).toHaveBeenCalled();
  });

  it('never signs staff out because a table session lapsed', async () => {
    await intercept(tableRequest(), failWith(401));

    expect(logout).not.toHaveBeenCalled();
    expect(open).not.toHaveBeenCalled();
  });

  it('leaves a rejected sign-in to the login form rather than calling it a lapsed session', async () => {
    await intercept(request(`${BASE}/api/auth/login`), failWith(401));

    expect(logout).not.toHaveBeenCalled();
  });

  it('says plainly that the role is wrong on 403, rather than doing nothing', async () => {
    await intercept(request(), failWith(403));

    expect(open).toHaveBeenCalledWith(
      'Nemate ovlašćenje za ovu radnju.',
      'U redu',
      expect.anything(),
    );
    expect(logout).not.toHaveBeenCalled();
  });

  it('names the connection as the problem on 0, not the request', async () => {
    await intercept(request(), failWith(0));

    expect(open).toHaveBeenCalledWith(
      'Server nije dostupan. Proverite vezu.',
      'U redu',
      expect.anything(),
    );
  });

  it('rethrows so the calling screen still gets its error', async () => {
    const error = await intercept(request(), failWith(409));

    expect(error.status).toBe(409);
  });
});

describe('authInterceptor', () => {
  function attach(req: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { token: () => token } },
        { provide: API_BASE_URL, useValue: BASE },
      ],
    });

    let seen: HttpRequest<unknown> | null = null;

    TestBed.runInInjectionContext(() =>
      authInterceptor(req, (passed) => {
        seen = passed;
        return of();
      }).subscribe(),
    );

    return seen!;
  }

  it('attaches the bearer token to our own API', () => {
    expect(attach(request(), 'staff-token').headers.get('Authorization')).toBe('Bearer staff-token');
  });

  it('leaves a table session alone: overwriting it would order under the last staff account', () => {
    expect(attach(tableRequest(), 'staff-token').headers.has('Authorization')).toBe(false);
  });

  it('sends the token nowhere but our own API', () => {
    expect(attach(request('https://example.com/x'), 'staff-token').headers.has('Authorization')).toBe(
      false,
    );
  });
});

describe('describe', () => {
  it('reads a validation problem out of its per-field lists', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { errors: { Name: ['Ime je obavezno.'], Party: ['Previše gostiju.'] } },
    });

    expect(describeError(error)).toBe('Ime je obavezno. Previše gostiju.');
  });

  it('falls back through detail and title rather than printing an object', () => {
    expect(describeError(new HttpErrorResponse({ status: 409, error: { detail: 'Zauzeto.' } }))).toBe(
      'Zauzeto.',
    );
    expect(describeError(new HttpErrorResponse({ status: 409, error: { title: 'Sukob.' } }))).toBe(
      'Sukob.',
    );
    expect(describeError(new HttpErrorResponse({ status: 500, error: null }))).toBe(
      'Došlo je do greške.',
    );
  });
});
