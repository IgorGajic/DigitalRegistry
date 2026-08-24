import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';

import { AuthenticationResult } from '../models/dtos';
import { UserRole } from '../models/enums';
import { API_BASE_URL, LOGIN_ROUTE, STORAGE_KEY } from '../config/tokens';

/** What the till and the master application both keep about the signed-in person. */
export interface Session {
  accessToken: string;
  expiresAtUtc: string;
  userId: string | null;
  fullName: string | null;
  email: string | null;
  role: UserRole;
  restaurantId: string | null;
  restaurantSlug: string | null;
}

/**
 * Holds the session and issues the calls that create one.
 *
 * The token is kept in `localStorage` so a refresh, or a browser closed at the end of a shift, does
 * not sign the waiter out mid-service. That does expose it to any script running on the page, which
 * is the accepted trade for a till: the alternative is re-authenticating every reload, which staff
 * would work around by never closing the tab.
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly storageKey = inject(STORAGE_KEY);
  private readonly loginRoute = inject(LOGIN_ROUTE);

  private readonly session = signal<Session | null>(this.restore());

  readonly current = this.session.asReadonly();
  readonly isSignedIn = computed(() => this.session() !== null);
  readonly role = computed(() => this.session()?.role ?? null);
  readonly displayName = computed(() => this.session()?.fullName ?? '');
  readonly restaurantSlug = computed(() => this.session()?.restaurantSlug ?? '');

  /** Signs in to a restaurant. The venue code is what selects the tenant. */
  login(restaurantSlug: string, email: string, password: string): Observable<AuthenticationResult> {
    return this.http
      .post<AuthenticationResult>(`${this.baseUrl}/api/auth/login`, {
        restaurantSlug,
        email,
        password,
      })
      .pipe(tap((result) => this.store(result)));
  }

  /** Signs a platform administrator in to the master application. No venue code: they have none. */
  loginPlatformAdmin(email: string, password: string): Observable<AuthenticationResult> {
    return this.http
      .post<AuthenticationResult>(`${this.baseUrl}/api/platform/auth/login`, { email, password })
      .pipe(tap((result) => this.store(result)));
  }

  logout(redirect = true): void {
    localStorage.removeItem(this.storageKey);
    this.session.set(null);

    if (redirect) {
      void this.router.navigate([this.loginRoute]);
    }
  }

  token(): string | null {
    return this.session()?.accessToken ?? null;
  }

  hasAnyRole(...roles: UserRole[]): boolean {
    const role = this.session()?.role;
    return role !== undefined && roles.includes(role);
  }

  private store(result: AuthenticationResult): void {
    const session: Session = {
      accessToken: result.accessToken,
      expiresAtUtc: result.expiresAtUtc,
      userId: result.userId,
      fullName: result.fullName,
      email: result.email,
      role: result.role,
      restaurantId: result.restaurantId,
      restaurantSlug: result.restaurantSlug,
    };

    localStorage.setItem(this.storageKey, JSON.stringify(session));
    this.session.set(session);
  }

  private restore(): Session | null {
    const raw = localStorage.getItem(this.storageKey);

    if (!raw) {
      return null;
    }

    try {
      const session = JSON.parse(raw) as Session;

      // An expired token would be rejected by every call anyway; dropping it here means the person
      // sees the sign-in screen rather than a wall of 401s.
      if (new Date(session.expiresAtUtc) <= new Date()) {
        localStorage.removeItem(this.storageKey);
        return null;
      }

      return session;
    } catch {
      // Corrupt or from an older shape of this object. Better to sign in again than to guess.
      localStorage.removeItem(this.storageKey);
      return null;
    }
  }
}
