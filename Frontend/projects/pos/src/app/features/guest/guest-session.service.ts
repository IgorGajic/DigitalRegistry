import { HttpClient, HttpContext, HttpHeaders } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import {
  API_BASE_URL,
  AuthenticationResult,
  LicenseStatusDto,
  MenuItemDto,
  OrderDto,
  TABLE_SESSION_REQUEST,
  TableTabDto,
} from 'shared';

/** The part of a table session the guest's screen needs after the token itself. */
export interface TableSession {
  accessToken: string;
  expiresAtUtc: string;
  tableId: string;
  tableNumber: number | null;
}

const STORAGE_KEY = 'digitalregistry.table.session';

/**
 * The session a guest gets by scanning the QR code on their table.
 *
 * Deliberately separate from {@link AuthService}: this is not a sign-in. Nobody proves who they are;
 * the token proves only which table the phone is sitting at, and it says nothing about the guest.
 * Keeping it apart means a guest scanning a code on a member of staff's phone cannot displace the
 * till's session, and a lapsed table session cannot sign anybody out.
 *
 * It lives in `sessionStorage`, so it disappears when the tab is closed — a guest who leaves does
 * not want to still be ordering for table 5 tomorrow.
 */
@Injectable()
export class GuestSessionService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  readonly session = signal<TableSession | null>(this.restore());

  /** Trades the token from the QR code for a session pinned to that table. */
  open(qrCodeToken: string): Observable<AuthenticationResult> {
    return this.http
      .post<AuthenticationResult>(`${this.baseUrl}/api/tables/sessions`, { qrCodeToken })
      .pipe(tap((result) => this.store(result)));
  }

  menu(): Observable<MenuItemDto[]> {
    return this.http.get<MenuItemDto[]>(`${this.baseUrl}/api/menu`, this.asTable());
  }

  /** The venue's name, and whether it can trade at all — the licence screen's data, read as a guest. */
  venue(): Observable<LicenseStatusDto> {
    return this.http.get<LicenseStatusDto>(`${this.baseUrl}/api/license/status`, this.asTable());
  }

  placeOrder(items: { menuItemId: string; quantity: number }[]): Observable<OrderDto> {
    return this.http.post<OrderDto>(`${this.baseUrl}/api/orders/qr`, { items }, this.asTable());
  }

  /**
   * What the table has had so far, across every round still running.
   *
   * Each round opens its own order, so no single response the guest has already seen adds up to the
   * table's running total; the API sums them from the table on the session token.
   */
  tab(): Observable<TableTabDto> {
    return this.http.get<TableTabDto>(`${this.baseUrl}/api/orders/mine`, this.asTable());
  }

  clear(): void {
    sessionStorage.removeItem(STORAGE_KEY);
    this.session.set(null);
  }

  /**
   * The table's own bearer token, plus the flag that tells the interceptors to leave it alone.
   */
  private asTable(): { headers: HttpHeaders; context: HttpContext } {
    return {
      headers: new HttpHeaders({
        Authorization: `Bearer ${this.session()?.accessToken ?? ''}`,
      }),
      context: new HttpContext().set(TABLE_SESSION_REQUEST, true),
    };
  }

  private store(result: AuthenticationResult): void {
    const session: TableSession = {
      accessToken: result.accessToken,
      expiresAtUtc: result.expiresAtUtc,
      tableId: result.tableId!,
      tableNumber: result.tableNumber,
    };

    sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    this.session.set(session);
  }

  private restore(): TableSession | null {
    const raw = sessionStorage.getItem(STORAGE_KEY);

    if (!raw) {
      return null;
    }

    try {
      const session = JSON.parse(raw) as TableSession;

      // Three hours is the whole life of a table session; an expired one would only produce 401s.
      if (new Date(session.expiresAtUtc) <= new Date()) {
        sessionStorage.removeItem(STORAGE_KEY);
        return null;
      }

      return session;
    } catch {
      sessionStorage.removeItem(STORAGE_KEY);
      return null;
    }
  }
}
