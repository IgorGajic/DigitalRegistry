import { DestroyRef, Injectable, inject, signal } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';

import { API_BASE_URL, AuthService } from 'shared';

/** What the kitchen and floor hubs push. Payloads are loose because the screens only need a nudge. */
export type RealtimeEvent =
  | { kind: 'orderCreated'; payload: unknown }
  | { kind: 'orderItemUpdated'; payload: unknown }
  | { kind: 'orderPaid'; payload: unknown }
  | { kind: 'menuItemAvailabilityChanged'; payload: unknown }
  | { kind: 'guestQrOrderPlaced'; payload: unknown }
  | { kind: 'reservationArrivalAlert'; payload: unknown };

/**
 * Keeps the floor screen in step with what other staff are doing.
 *
 * The events carry enough to know *that* something changed, and the screen re-reads the floor plan
 * rather than trying to patch its own state from the payload. A till has a handful of tables and one
 * request is cheap; reconstructing state from a stream of deltas is how two waiters end up seeing
 * different totals for the same table.
 */
@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private readonly auth = inject(AuthService);
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly destroyRef = inject(DestroyRef);

  private connections: HubConnection[] = [];

  /** Bumped on every event, so a screen can simply react to it changing. */
  readonly lastEvent = signal<RealtimeEvent | null>(null);
  readonly connected = signal(false);

  constructor() {
    this.destroyRef.onDestroy(() => void this.stop());
  }

  /**
   * Opens the kitchen and floor hubs.
   *
   * The browser cannot set an Authorization header on a WebSocket handshake, so the token goes in
   * the query string — which the API accepts for hub paths only.
   */
  async start(): Promise<void> {
    if (!this.auth.token()) {
      return;
    }

    // A handle that has fallen over is not a connection. Checking the count alone would leave the
    // service sitting on dead sockets forever: while a licence is lapsed the API refuses the
    // handshake, and nothing would reopen the hubs once it is paid — the floor would keep saying
    // "no live connection" until someone reloaded the page.
    const alive = this.connections.some(
      (connection) => connection.state !== HubConnectionState.Disconnected,
    );

    if (alive) {
      return;
    }

    await this.stop();

    const hubs: { path: string; events: RealtimeEvent['kind'][] }[] = [
      {
        path: '/hubs/kitchen',
        events: ['orderCreated', 'orderItemUpdated', 'menuItemAvailabilityChanged'],
      },
      {
        path: '/hubs/order',
        events: ['guestQrOrderPlaced', 'reservationArrivalAlert', 'orderPaid'],
      },
    ];

    this.connections = hubs.map(({ path, events }) => {
      const connection = new HubConnectionBuilder()
        .withUrl(`${this.baseUrl}${path}`, { accessTokenFactory: () => this.auth.token() ?? '' })
        .withAutomaticReconnect()
        .configureLogging(LogLevel.Warning)
        .build();

      for (const kind of events) {
        // Hub methods are named in PascalCase on the server.
        const method = kind.charAt(0).toUpperCase() + kind.slice(1);
        connection.on(method, (payload: unknown) => this.lastEvent.set({ kind, payload }));
      }

      connection.onreconnected(() => this.connected.set(true));
      connection.onclose(() => this.connected.set(false));

      return connection;
    });

    await Promise.all(
      this.connections.map((connection) =>
        connection
          .start()
          .then(() => this.connected.set(true))
          // A till that cannot reach the hub still has to work; it simply stops updating by itself.
          .catch(() => this.connected.set(false)),
      ),
    );
  }

  async stop(): Promise<void> {
    const open = this.connections.filter(
      (connection) => connection.state !== HubConnectionState.Disconnected,
    );

    this.connections = [];
    this.connected.set(false);

    await Promise.all(open.map((connection) => connection.stop().catch(() => undefined)));
  }
}
