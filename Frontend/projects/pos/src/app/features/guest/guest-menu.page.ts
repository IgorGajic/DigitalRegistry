import { CurrencyPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MenuItemDto, TableTabDto, ThemeService, itemsLabel } from 'shared';

import { GuestSessionService } from './guest-session.service';

/** One line the guest has put together, before it is sent. */
interface BasketLine {
  item: MenuItemDto;
  quantity: number;
}

/**
 * What the QR code on a table leads to.
 *
 * Built for a phone held in one hand: one column, large targets, the basket pinned to the bottom
 * where a thumb reaches it. There is no sign-in and no account — the token in the link is the whole
 * session, and it says only which table this is.
 *
 * The guest never sees a bill to settle: ordering this way still ends with a waiter bringing one,
 * because payment goes through the till. What they get is the menu of the venue their table belongs
 * to, a way to send an order to the bar without waiting to be noticed, and a running list of what
 * the table has had — which no single round can tell them, since each round opens its own order.
 */
@Component({
  selector: 'pos-guest-menu',
  providers: [GuestSessionService],
  imports: [CurrencyPipe, MatButtonModule, MatIconModule, MatProgressBarModule],
  template: `
    @if (loading()) {
      <mat-progress-bar mode="indeterminate" />
    }

    <div class="guest">
      <header class="guest__header">
        <div>
          <h1>{{ venueName() || 'Jelovnik' }}</h1>
          @if (session()?.tableNumber; as number) {
            <span class="guest__table">Sto {{ number }}</span>
          }
        </div>
      </header>

      @if (error(); as message) {
        <div class="guest__notice">
          <mat-icon>error_outline</mat-icon>
          <p>{{ message }}</p>
        </div>
      } @else if (sent()) {
        <!-- Deliberately a whole screen: the one thing a guest wants to know is that it went. -->
        <div class="guest__done">
          <mat-icon class="guest__done-icon">check_circle</mat-icon>
          <h2>Porudžbina je poslata</h2>
          <p class="guest__muted">Konobar je obavešten. Račun se plaća kod konobara, kao i obično.</p>

          <ul class="guest__sent">
            @for (line of sent()!; track line.item.id) {
              <li>
                <span>{{ line.quantity }}× {{ line.item.name }}</span>
                <span class="guest__numeric">
                  {{ line.quantity * line.item.unitPrice | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}
                </span>
              </li>
            }
          </ul>

          @if (tab(); as running) {
            @if (running.rounds.length > 1) {
              <p class="guest__muted guest__running">
                Za ovaj sto do sada: {{ running.itemCount }} {{ itemsLabel(running.itemCount) }} ·
                <strong class="guest__numeric">
                  {{ running.total | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}
                </strong>
              </p>
            }
          }

          <button mat-flat-button class="guest__wide" (click)="orderMore()">Poruči još nešto</button>
        </div>
      } @else {
        @if (categories().length > 1) {
          <nav class="guest__categories">
            <button
              class="guest__chip"
              [class.guest__chip--on]="category() === null"
              (click)="category.set(null)"
            >
              Sve
            </button>
            @for (name of categories(); track name) {
              <button
                class="guest__chip"
                [class.guest__chip--on]="category() === name"
                (click)="category.set(name)"
              >
                {{ name }}
              </button>
            }
          </nav>
        }

        @if (tab(); as running) {
          @if (running.rounds.length > 0) {
            <details class="guest__tab" [open]="tabOpen()" (toggle)="tabOpen.set($any($event.target).open)">
              <summary class="guest__tab-head">
                <span>Već ste poručili ({{ running.itemCount }})</span>
                <span class="guest__numeric">
                  {{ running.total | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}
                </span>
              </summary>

              <ul class="guest__tab-lines">
                @for (round of running.rounds; track round.orderId) {
                  @for (line of round.lines; track $index) {
                    <li>
                      <span>
                        {{ line.quantity }}× {{ line.menuItemName }}
                        @if (!round.placedByGuest) {
                          <span class="guest__muted">· konobar</span>
                        }
                      </span>
                      <span class="guest__numeric">
                        {{ line.lineTotal | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}
                      </span>
                    </li>
                  }
                }
              </ul>

              <p class="guest__muted guest__tab-note">
                Iznos je informativan. Račun se plaća kod konobara.
              </p>
            </details>
          }
        }

        <ul class="guest__list">
          @for (item of visible(); track item.id) {
            <li class="guest__item" [class.guest__item--out]="!item.isAvailable">
              <div class="guest__item-text">
                <span class="guest__item-name">{{ item.name }}</span>
                <span class="guest__muted">
                  {{ item.unitPrice | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}
                  @if (!item.isAvailable) {
                    · trenutno nema
                  }
                </span>
              </div>

              @if (item.isAvailable) {
                @if (quantityOf(item) > 0) {
                  <div class="guest__stepper">
                    <button mat-icon-button (click)="remove(item)" aria-label="Manje">
                      <mat-icon>remove</mat-icon>
                    </button>
                    <span class="guest__count">{{ quantityOf(item) }}</span>
                    <button mat-icon-button (click)="add(item)" aria-label="Više">
                      <mat-icon>add</mat-icon>
                    </button>
                  </div>
                } @else {
                  <button mat-stroked-button (click)="add(item)">Dodaj</button>
                }
              }
            </li>
          }
        </ul>

        @if (visible().length === 0 && !loading()) {
          <p class="guest__muted guest__empty">Jelovnik je trenutno prazan.</p>
        }
      }

      @if (basket().length > 0 && !sent() && !error()) {
        <div class="guest__basket">
          <div class="guest__basket-summary">
            <strong>{{ count() }} {{ itemsLabel(count()) }}</strong>
            <span class="guest__numeric">
              {{ total() | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}
            </span>
          </div>
          <button mat-flat-button class="guest__wide" [disabled]="sending()" (click)="send()">
            Pošalji konobaru
          </button>
        </div>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
      min-height: 100dvh;
      background: var(--mat-sys-surface-container-low);
    }

    .guest {
      max-width: 560px;
      margin: 0 auto;
      /* Room for the basket, which floats above the content. */
      padding: 0 12px calc(148px + env(safe-area-inset-bottom));
    }

    .guest__header {
      position: sticky;
      top: 0;
      z-index: 2;
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 14px 4px;
      background: var(--mat-sys-surface-container-low);
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }

    h1 {
      margin: 0;
      font-size: 1.25rem;
      line-height: 1.2;
    }

    .guest__table {
      color: var(--mat-sys-on-surface-variant);
      font-size: 0.9rem;
    }

    .guest__categories {
      display: flex;
      gap: 8px;
      overflow-x: auto;
      padding: 12px 0;
      /* Chips scroll sideways rather than wrapping: on a phone this stays one thumb-height tall. */
      scrollbar-width: none;
    }

    .guest__categories::-webkit-scrollbar {
      display: none;
    }

    .guest__chip {
      flex: 0 0 auto;
      padding: 8px 16px;
      border-radius: 999px;
      border: 1px solid var(--mat-sys-outline-variant);
      background: var(--mat-sys-surface);
      color: inherit;
      font: inherit;
      cursor: pointer;
    }

    .guest__chip--on {
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
      border-color: transparent;
    }

    .guest__list {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 8px;
    }

    .guest__item {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      /* 56 px keeps every row a comfortable tap target. */
      min-height: 56px;
      padding: 12px 14px;
      border-radius: var(--dr-radius);
      background: var(--mat-sys-surface);
    }

    .guest__item--out {
      opacity: 0.55;
    }

    .guest__item-text {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .guest__item-name {
      font-weight: 500;
    }

    .guest__muted {
      color: var(--mat-sys-on-surface-variant);
      font-size: 0.85rem;
    }

    .guest__stepper {
      display: flex;
      align-items: center;
      gap: 4px;
    }

    .guest__count {
      min-width: 24px;
      text-align: center;
      font-family: var(--dr-font-mono);
      font-variant-numeric: tabular-nums;
      font-weight: 600;
    }

    .guest__basket {
      position: fixed;
      left: 50%;
      bottom: 0;
      transform: translateX(-50%);
      width: min(560px, 100%);
      display: flex;
      flex-direction: column;
      gap: 10px;
      padding: 14px 16px calc(14px + env(safe-area-inset-bottom));
      background: var(--mat-sys-surface);
      border-top: 1px solid var(--mat-sys-outline-variant);
      box-shadow: 0 -6px 18px rgb(0 0 0 / 8%);
    }

    .guest__basket-summary {
      display: flex;
      justify-content: space-between;
      align-items: baseline;
      font-size: 1.05rem;
    }

    .guest__numeric {
      font-family: var(--dr-font-mono);
      font-variant-numeric: tabular-nums;
      font-weight: 600;
    }

    .guest__wide {
      width: 100%;
      height: 48px;
    }

    .guest__notice,
    .guest__done {
      display: flex;
      flex-direction: column;
      align-items: center;
      text-align: center;
      gap: 8px;
      padding: 48px 16px;
    }

    .guest__done-icon {
      font-size: 48px;
      width: 48px;
      height: 48px;
      color: var(--dr-free);
    }

    .guest__sent {
      list-style: none;
      margin: 12px 0 20px;
      padding: 0;
      width: 100%;
      display: flex;
      flex-direction: column;
      gap: 6px;
    }

    .guest__sent li {
      display: flex;
      justify-content: space-between;
      gap: 12px;
    }

    .guest__empty {
      padding: 40px 0;
      text-align: center;
    }

    /* Collapsed by default once there is a lot on it: the menu is what the guest came for. */
    .guest__tab {
      margin: 12px 0;
      padding: 10px 14px;
      border-radius: var(--dr-radius);
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
    }

    .guest__tab-head {
      display: flex;
      justify-content: space-between;
      gap: 12px;
      font-weight: 500;
      cursor: pointer;
    }

    .guest__tab-lines {
      list-style: none;
      margin: 10px 0 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 4px;
      font-size: 0.9rem;
    }

    .guest__tab-lines li {
      display: flex;
      justify-content: space-between;
      gap: 12px;
    }

    .guest__tab-note {
      margin: 10px 0 0;
    }

    .guest__running {
      margin: 4px 0 16px;
    }
  `,
})
export class GuestMenuPage implements OnInit {
  /** The table's QR token, bound from the route by `withComponentInputBinding`. */
  readonly token = input.required<string>();

  private readonly guests = inject(GuestSessionService);
  private readonly theme = inject(ThemeService);

  protected readonly session = this.guests.session;
  protected readonly menu = signal<MenuItemDto[]>([]);
  protected readonly basket = signal<BasketLine[]>([]);
  protected readonly sent = signal<BasketLine[] | null>(null);
  protected readonly tab = signal<TableTabDto | null>(null);
  protected readonly tabOpen = signal(false);
  protected readonly itemsLabel = itemsLabel;
  protected readonly category = signal<string | null>(null);
  protected readonly venueName = signal('');
  protected readonly loading = signal(true);
  protected readonly sending = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly categories = computed(() =>
    [...new Set(this.menu().map((item) => item.category))].sort(),
  );

  protected readonly visible = computed(() => {
    const chosen = this.category();

    return chosen ? this.menu().filter((item) => item.category === chosen) : this.menu();
  });

  protected readonly count = computed(() =>
    this.basket().reduce((sum, line) => sum + line.quantity, 0),
  );

  protected readonly total = computed(() =>
    this.basket().reduce((sum, line) => sum + line.quantity * line.item.unitPrice, 0),
  );

  /** Not the constructor: a required route input has no value until Angular has set it. */
  ngOnInit(): void {
    // A fresh session on every scan: the code on the table is the only credential, and trading it
    // again costs one request. It also means a code rotated by the manager stops working at once.
    this.guests.open(this.token()).subscribe({
      next: () => this.load(),
      error: () => {
        this.loading.set(false);
        this.error.set('Ovaj QR kod ne važi. Zamolite osoblje za pomoć.');
      },
    });
  }

  protected quantityOf(item: MenuItemDto): number {
    return this.basket().find((line) => line.item.id === item.id)?.quantity ?? 0;
  }

  protected add(item: MenuItemDto): void {
    this.basket.update((lines) =>
      lines.some((line) => line.item.id === item.id)
        ? lines.map((line) =>
            line.item.id === item.id ? { ...line, quantity: line.quantity + 1 } : line,
          )
        : [...lines, { item, quantity: 1 }],
    );
  }

  protected remove(item: MenuItemDto): void {
    this.basket.update((lines) =>
      lines
        .map((line) => (line.item.id === item.id ? { ...line, quantity: line.quantity - 1 } : line))
        .filter((line) => line.quantity > 0),
    );
  }

  protected send(): void {
    const lines = this.basket();

    if (lines.length === 0) {
      return;
    }

    this.sending.set(true);

    this.guests
      .placeOrder(lines.map((line) => ({ menuItemId: line.item.id, quantity: line.quantity })))
      .subscribe({
        next: () => {
          this.sending.set(false);
          this.sent.set(lines);
          this.basket.set([]);
          this.loadTab();
        },
        // The error interceptor has already said what went wrong; the basket is kept so the guest
        // can simply press send again rather than rebuild it.
        error: () => this.sending.set(false),
      });
  }

  protected orderMore(): void {
    this.sent.set(null);
    // Availability may have changed while they were eating; each round starts from a fresh menu.
    this.load();
  }

  private load(): void {
    this.loading.set(true);

    // Whose room this is, and what colour it is painted. Both from one call, because they are one
    // fact: the guest's screen belongs to this venue for the length of a meal.
    //
    // The palette is worn, not adopted — never written to the phone's storage, so it lasts exactly
    // as long as the session does. It also arrives late on purpose: painting before the token is
    // traded would mean guessing, and a screen that changes colour a beat after it opens reads as a
    // fault, whereas one that does so while the menu is still loading reads as the page arriving.
    this.guests.settings().subscribe({
      next: (settings) => {
        this.venueName.set(settings.restaurantName);
        this.theme.wearVenueTheme(settings.theme);
      },
      // A guest who cannot be told whose room this is still gets a menu. The heading falls back to
      // "Jelovnik", which is true of every venue and wrong about none.
      error: () => undefined,
    });

    this.guests.menu().subscribe({
      next: (items) => {
        this.menu.set(items);
        this.loading.set(false);
      },
      error: (failure: HttpErrorResponse) => {
        this.loading.set(false);

        // 402 is the venue not having paid, and it is the one failure with a different cause worth
        // telling a guest apart: nothing is broken and calling somebody over will not fix it.
        this.error.set(
          failure.status === 402
            ? 'Restoran trenutno ne prima porudžbine preko koda.'
            : 'Jelovnik trenutno nije dostupan. Pozovite konobara.',
        );
      },
    });

    this.loadTab();
  }

  /**
   * Reads what the table has had so far.
   *
   * Failure is swallowed on purpose: this is context, not the reason the guest opened the page, and
   * a menu that refuses to appear because a summary could not be fetched would be worse than one
   * without the summary.
   */
  private loadTab(): void {
    this.guests.tab().subscribe({
      next: (running) => this.tab.set(running),
      error: () => this.tab.set(null),
    });
  }
}
