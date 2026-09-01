import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL, AuthenticationResult, TABLE_SESSION_REQUEST, UserRole } from 'shared';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { GuestSessionService } from './guest-session.service';

/**
 * The table session is the one credential in the till that is not a sign-in, and the rules that keep
 * it apart from one are invisible on screen: it lives in `sessionStorage`, it carries its own header,
 * and it marks every request so the interceptors leave the staff session alone. Getting any of that
 * wrong means a guest scanning a code on a waiter's phone displaces the till's session — which is
 * precisely the failure nobody would reproduce by hand.
 */

const BASE = 'http://localhost:5275';
const KEY = 'digitalregistry.table.session';

function tokenFor(expiresAtUtc: string): AuthenticationResult {
  return {
    accessToken: 'table-token',
    expiresAtUtc,
    userId: null,
    email: null,
    fullName: null,
    role: UserRole.Guest,
    restaurantId: 'r1',
    restaurantSlug: 'demo',
    tableId: 't1',
    tableNumber: 5,
  };
}

function inHours(hours: number): string {
  return new Date(Date.now() + hours * 3600_000).toISOString();
}

describe('GuestSessionService', () => {
  let service: GuestSessionService;
  let http: HttpTestingController;

  function create(): void {
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: BASE },
        GuestSessionService,
      ],
    });

    service = TestBed.inject(GuestSessionService);
    http = TestBed.inject(HttpTestingController);
  }

  beforeEach(() => {
    sessionStorage.clear();
    create();
  });

  afterEach(() => {
    http.verify();
    sessionStorage.clear();
  });

  it('starts with no session when nothing has been scanned', () => {
    expect(service.session()).toBeNull();
  });

  it('keeps the table, not the guest: a scan says which table, and nothing about who', () => {
    service.open('qr-token-1').subscribe();

    const request = http.expectOne(`${BASE}/api/tables/sessions`);
    expect(request.request.body).toEqual({ qrCodeToken: 'qr-token-1' });
    request.flush(tokenFor(inHours(3)));

    expect(service.session()).toEqual({
      accessToken: 'table-token',
      expiresAtUtc: expect.any(String),
      tableId: 't1',
      tableNumber: 5,
    });
  });

  it('lives in sessionStorage, so a guest who leaves is not still ordering for table 5 tomorrow', () => {
    service.open('qr-token-1').subscribe();
    http.expectOne(`${BASE}/api/tables/sessions`).flush(tokenFor(inHours(3)));

    expect(sessionStorage.getItem(KEY)).toContain('table-token');
    expect(localStorage.getItem(KEY)).toBeNull();
  });

  it('restores a session that is still good, so a reload does not need a second scan', () => {
    sessionStorage.setItem(
      KEY,
      JSON.stringify({
        accessToken: 'table-token',
        expiresAtUtc: inHours(1),
        tableId: 't1',
        tableNumber: 5,
      }),
    );

    create();

    expect(service.session()?.tableNumber).toBe(5);
  });

  it('drops an expired session rather than letting it produce a wall of 401s', () => {
    sessionStorage.setItem(
      KEY,
      JSON.stringify({
        accessToken: 'stale',
        expiresAtUtc: inHours(-1),
        tableId: 't1',
        tableNumber: 5,
      }),
    );

    create();

    expect(service.session()).toBeNull();
    expect(sessionStorage.getItem(KEY)).toBeNull();
  });

  it('drops a corrupt session rather than guessing what it meant', () => {
    sessionStorage.setItem(KEY, 'not json');

    create();

    expect(service.session()).toBeNull();
    expect(sessionStorage.getItem(KEY)).toBeNull();
  });

  it("sends the table its own bearer token and marks the request as not the till's", () => {
    service.open('qr-token-1').subscribe();
    http.expectOne(`${BASE}/api/tables/sessions`).flush(tokenFor(inHours(3)));

    service.menu().subscribe();

    const request = http.expectOne(`${BASE}/api/menu`);
    expect(request.request.headers.get('Authorization')).toBe('Bearer table-token');
    expect(request.request.context.get(TABLE_SESSION_REQUEST)).toBe(true);
    request.flush([]);
  });

  it("asks the API for the table's running tab, not for one round", () => {
    service.open('qr-token-1').subscribe();
    http.expectOne(`${BASE}/api/tables/sessions`).flush(tokenFor(inHours(3)));

    service.tab().subscribe();

    const request = http.expectOne(`${BASE}/api/orders/mine`);
    expect(request.request.context.get(TABLE_SESSION_REQUEST)).toBe(true);
    request.flush({ tableId: 't1', tableNumber: 5, itemCount: 0, total: 0, rounds: [] });
  });

  it('sends an order without naming a table: the token already says which one', () => {
    service.open('qr-token-1').subscribe();
    http.expectOne(`${BASE}/api/tables/sessions`).flush(tokenFor(inHours(3)));

    service.placeOrder([{ menuItemId: 'm1', quantity: 2 }]).subscribe();

    const request = http.expectOne(`${BASE}/api/orders/qr`);
    expect(request.request.body).toEqual({ items: [{ menuItemId: 'm1', quantity: 2 }] });
    request.flush({});
  });

  it('clears both the signal and the stored copy', () => {
    service.open('qr-token-1').subscribe();
    http.expectOne(`${BASE}/api/tables/sessions`).flush(tokenFor(inHours(3)));

    service.clear();

    expect(service.session()).toBeNull();
    expect(sessionStorage.getItem(KEY)).toBeNull();
  });
});
