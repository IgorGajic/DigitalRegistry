import { CurrencyPipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import {
  AuthService,
  FloorPlanTableDto,
  MenuItemDto,
  OrderDto,
  OrderItemDto,
  TillApiService,
  UserRole,
  seatsLabel,
} from 'shared';
import {
  PromptDialog,
  PromptDialogData,
} from 'shared/ui';

import { PaymentDialog, PaymentDialogResult } from './payment.dialog';
import { ReceiptDialog, ReceiptDialogResult } from './receipt.dialog';
import { VoidDialog, VoidDialogData, VoidDialogResult } from './void.dialog';

/**
 * One table's tab: what is on it, and everything a waiter does to it.
 *
 * The tab stays open across as many rounds as the guests order. Each addition is its own call, and
 * the bill is only closed by taking payment — which is what lets a table run all evening.
 */
@Component({
  selector: 'pos-order',
  imports: [
    CurrencyPipe,
    MatButtonModule,
    MatButtonToggleModule,
    MatChipsModule,
    MatDialogModule,
    MatDividerModule,
    MatIconModule,
    MatProgressBarModule,
    MatTooltipModule,
  ],
  template: `
    @if (busy()) {
      <mat-progress-bar mode="indeterminate" />
    }

    <div class="order">
      <!-- The running bill. Left-hand side, because it is what the guest asks about. -->
      <section class="order__bill">
        <header class="order__header">
          <button mat-icon-button (click)="back()" aria-label="Nazad na salu">
            <mat-icon>arrow_back</mat-icon>
          </button>
          <div>
            <h1>Sto {{ table()?.tableNumber ?? '—' }}</h1>
            <span class="dr-muted">{{ table()?.capacity }} {{ seatsLabel(table()?.capacity ?? 0) }}</span>
          </div>
        </header>

        <!-- A table may run more than one tab: separate parties, or rounds kept apart. -->
        @if (openTabIds().length > 1) {
          <mat-button-toggle-group
            class="order__tabs"
            [value]="order()?.id"
            (change)="switchTab($any($event).value)"
          >
            @for (id of openTabIds(); track id; let i = $index) {
              <mat-button-toggle [value]="id">{{ i + 1 }}. račun</mat-button-toggle>
            }
          </mat-button-toggle-group>
        }

        <mat-divider />

        @if (order(); as current) {
          @if (current.items.length === 0) {
            <p class="dr-empty">Račun je prazan. Dodajte stavke sa desne strane.</p>
          } @else {
            <ul class="order__lines">
              @for (line of current.items; track line.id) {
                <li class="order__line">
                  <div class="order__line-main">
                    <span class="order__line-name">{{ line.menuItemName }}</span>
                    @if (line.notes) {
                      <span class="order__line-note">{{ line.notes }}</span>
                    }
                  </div>

                  <span class="order__qty">{{ line.quantity }}×</span>
                  <span class="dr-numeric order__line-total">
                    {{ line.lineTotal | currency: 'RSD' : 'symbol-narrow' : '1.0-2' }}
                  </span>

                  <button
                    mat-icon-button
                    (click)="addOne(line)"
                    matTooltip="Još jedno"
                    aria-label="Dodaj još jedno"
                  >
                    <mat-icon>add</mat-icon>
                  </button>

                  <!-- Taking anything off the bill is a void, never a quiet decrement: the API
                       refuses a reduction through the ordinary edit, and requires a reason. -->
                  <button
                    mat-icon-button
                    (click)="editNote(line)"
                    [matTooltip]="line.notes ? 'Izmeni napomenu' : 'Dodaj napomenu'"
                    [attr.aria-label]="line.notes ? 'Izmeni napomenu' : 'Dodaj napomenu'"
                  >
                    <mat-icon [class.order__note-on]="line.notes">sticky_note_2</mat-icon>
                  </button>

                  <button
                    mat-icon-button
                    color="warn"
                    (click)="voidLine(line)"
                    matTooltip="Storno stavke"
                    aria-label="Storno stavke"
                  >
                    <mat-icon>remove_circle_outline</mat-icon>
                  </button>
                </li>
              }
            </ul>
          }

          <footer class="order__footer">
            <div class="order__total">
              <span>Ukupno</span>
              <strong>{{ current.total | currency: 'RSD' : 'symbol-narrow' : '1.0-2' }}</strong>
            </div>

            <div class="order__actions">
              <button
                mat-flat-button
                class="order__pay"
                [disabled]="current.items.length === 0 || busy()"
                (click)="pay()"
              >
                <mat-icon>payments</mat-icon>
                Plaćanje
              </button>

              <button mat-stroked-button color="warn" [disabled]="busy()" (click)="voidWhole()">
                Storno računa
              </button>

            </div>
          </footer>
        } @else {
          <p class="dr-empty">
            Sto je slobodan. Izaberite artikal sa desne strane da otvorite račun.
          </p>
        }
      </section>

      <!-- The menu, as a grid of buttons. Big targets: this is used on a touchscreen, standing. -->
      <section class="order__menu">
        <div class="order__categories">
          <mat-chip-listbox [value]="category()" (change)="category.set($any($event).value)">
            <mat-chip-option [value]="null" selected>Sve</mat-chip-option>
            @for (name of categories(); track name) {
              <mat-chip-option [value]="name">{{ name }}</mat-chip-option>
            }
          </mat-chip-listbox>
        </div>

        <div class="order__grid">
          @for (item of visibleMenu(); track item.id) {
            <button
              type="button"
              class="order__item"
              [disabled]="!item.isAvailable || busy()"
              (click)="add(item)"
            >
              <span class="order__item-name">{{ item.name }}</span>
              <span class="order__item-price">
                {{ item.unitPrice | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}
              </span>
              @if (!item.isAvailable) {
                <span class="order__item-out">nema</span>
              }
            </button>
          }
        </div>
      </section>
    </div>
  `,
  styles: `
    .order {
      display: grid;
      grid-template-columns: minmax(320px, 420px) 1fr;
      gap: var(--dr-gap);
      padding: var(--dr-gap);
      align-items: start;
      min-height: calc(100vh - 64px);
    }

    .order__bill {
      background: var(--mat-sys-surface);
      border-radius: var(--dr-radius);
      border: 1px solid var(--mat-sys-outline-variant);
      display: flex;
      flex-direction: column;
      position: sticky;
      top: 80px;
      max-height: calc(100vh - 96px);
    }

    .order__header {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 12px;
    }

    .order__header h1 {
      margin: 0;
      font-size: 1.25rem;
    }

    .order__lines {
      list-style: none;
      margin: 0;
      padding: 0;
      overflow-y: auto;
      flex: 1;
    }

    .order__line {
      display: grid;
      grid-template-columns: 1fr auto auto auto auto auto;
      align-items: center;
      gap: 6px;
      padding: 6px 8px 6px 12px;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }

    .order__line-main {
      display: flex;
      flex-direction: column;
      min-width: 0;
    }

    .order__line-name {
      font-weight: 500;
    }

    .order__line-note {
      font-size: 0.75rem;
      color: var(--mat-sys-on-surface-variant);
    }

    /* A line that carries a note says so on the icon, so it reads without opening anything. */
    .order__note-on {
      color: var(--dr-reserved);
    }

    .order__qty {
      font-family: var(--dr-font-mono);
      font-variant-numeric: tabular-nums;
      color: var(--mat-sys-on-surface-variant);
    }

    .order__line-total {
      min-width: 84px;
    }

    .order__footer {
      padding: 12px;
      border-top: 1px solid var(--mat-sys-outline-variant);
    }

    .order__total {
      display: flex;
      justify-content: space-between;
      align-items: baseline;
      font-size: 1.25rem;
      margin-bottom: 12px;
    }

    .order__total strong {
      font-size: 1.6rem;
      font-family: var(--dr-font-mono);
      font-variant-numeric: tabular-nums;
    }

    .order__actions {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }

    .order__tabs {
      width: 100%;
      margin: 8px 0;
    }

    .order__pay {
      height: 52px;
      font-size: 1.05rem;
    }

    .order__categories {
      margin-bottom: 12px;
    }

    .order__grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(150px, 1fr));
      gap: 10px;
    }

    .order__item {
      display: flex;
      flex-direction: column;
      align-items: flex-start;
      gap: 4px;
      min-height: 84px;
      padding: 12px;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: var(--dr-radius);
      background: var(--mat-sys-surface);
      color: inherit;
      font: inherit;
      text-align: left;
      cursor: pointer;
    }

    .order__item:hover:not(:disabled) {
      background: var(--mat-sys-secondary-container);
    }

    .order__item:disabled {
      opacity: 0.45;
      cursor: not-allowed;
    }

    .order__item-name {
      font-weight: 500;
      line-height: 1.2;
    }

    .order__item-price {
      font-family: var(--dr-font-mono);
      font-variant-numeric: tabular-nums;
      color: var(--mat-sys-on-surface-variant);
    }

    .order__item-out {
      font-size: 0.7rem;
      color: var(--dr-occupied);
      text-transform: uppercase;
    }

    @media (max-width: 900px) {
      .order {
        grid-template-columns: 1fr;
      }

      .order__bill {
        position: static;
        max-height: none;
      }
    }
  `,
})
export class OrderPage {
  /** Bound from the route by `withComponentInputBinding`. */
  readonly tableId = input.required<string>();

  private readonly api = inject(TillApiService);
  private readonly router = inject(Router);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly auth = inject(AuthService);

  protected readonly seatsLabel = seatsLabel;

  protected readonly order = signal<OrderDto | null>(null);

  /** Every tab the floor plan reports on this table, oldest first. Usually one. */
  protected readonly openTabIds = signal<string[]>([]);
  protected readonly table = signal<FloorPlanTableDto | null>(null);
  protected readonly menu = signal<MenuItemDto[]>([]);
  protected readonly category = signal<string | null>(null);
  protected readonly busy = signal(false);

  protected readonly categories = computed(() =>
    [...new Set(this.menu().map((item) => item.category))].sort(),
  );

  protected readonly visibleMenu = computed(() => {
    const chosen = this.category();

    return chosen ? this.menu().filter((item) => item.category === chosen) : this.menu();
  });

  /**
   * Reversing a settled bill is the one void a waiter cannot perform.
   *
   * Offered on the receipt rather than on the bill panel: the panel only ever holds an *open* tab,
   * and the API rightly refuses to reverse one of those. A mischarge is caught with the guest still
   * standing there, which is exactly when the receipt is on screen.
   */
  protected readonly canReverse = computed(() =>
    this.auth.hasAnyRole(UserRole.Manager, UserRole.Owner),
  );

  constructor() {
    this.api.menu().subscribe((items) => this.menu.set(items));
    this.loadTable();
  }

  protected back(): void {
    void this.router.navigate(['/sala']);
  }

  /** Adds an item, opening the tab first if the table has none. */
  protected add(item: MenuItemDto): void {
    const current = this.order();

    this.run(
      current
        ? this.api.addLine(current.id, item.id, 1)
        : this.api.openOrder(this.tableId(), [{ menuItemId: item.id, quantity: 1 }]),
      (updated) => {
        this.order.set(updated);

        // A tab opened here is not in the list the floor plan handed over on entry.
        this.openTabIds.update((ids) => (ids.includes(updated.id) ? ids : [...ids, updated.id]));
      },
    );
  }

  protected addOne(line: OrderItemDto): void {
    const current = this.order();

    if (!current) {
      return;
    }

    this.run(this.api.increaseLine(current.id, line.id, line.quantity + 1), (updated) =>
      this.order.set(updated),
    );
  }

  /**
   * Adds, changes or clears the note on a line.
   *
   * The note already printed on the bill and on the receipt; there was simply nowhere to type it,
   * so "bez leda" had to be carried to the bar in somebody's head. Clearing is distinguished from
   * cancelling by the empty string: the dialog closes with `undefined` when it is dismissed.
   */
  protected editNote(line: OrderItemDto): void {
    const current = this.order();

    if (!current) {
      return;
    }

    const data: PromptDialogData = {
      title: `Napomena: ${line.menuItemName}`,
      label: 'Napomena za šank ili kuhinju',
      placeholder: 'npr. bez leda, dobro pečeno, odvojeno',
      hint: 'Ide na račun i na otisak. Ostavite prazno da je uklonite.',
      initialValue: line.notes ?? '',
      multiline: true,
      // Zero, so an existing note can be cleared. Cancelling still returns undefined.
      minLength: 0,
      confirmText: 'Sačuvaj',
    };

    this.dialog
      .open(PromptDialog, { data })
      .afterClosed()
      .subscribe((notes: string | undefined) => {
        if (notes === undefined) {
          return;
        }

        this.run(this.api.changeNotes(current.id, line.id, notes.trim() || null), (updated) =>
          this.order.set(updated),
        );
      });
  }

  protected voidLine(line: OrderItemDto): void {
    const current = this.order();

    if (!current) {
      return;
    }

    const data: VoidDialogData = {
      title: `Storno: ${line.menuItemName}`,
      maxQuantity: line.quantity,
      minReasonLength: 3,
    };

    this.dialog
      .open(VoidDialog, { data, width: '420px' })
      .afterClosed()
      .subscribe((result: VoidDialogResult | undefined) => {
        if (!result) {
          return;
        }

        this.run(
          this.api.voidItem(current.id, line.id, result.reason, result.quantity),
          (outcome) => {
            this.snackBar.open(
              `Stornirano ${outcome.amount.toFixed(0)} RSD.`,
              'U redu',
              { duration: 4000 },
            );
            this.loadOrder(current.id);
          },
        );
      });
  }

  protected voidWhole(): void {
    const current = this.order();

    if (!current) {
      return;
    }

    const data: VoidDialogData = {
      title: 'Storno celog računa',
      minReasonLength: 3,
    };

    this.dialog
      .open(VoidDialog, { data, width: '420px' })
      .afterClosed()
      .subscribe((result: VoidDialogResult | undefined) => {
        if (!result) {
          return;
        }

        this.run(this.api.voidOrder(current.id, result.reason), () => {
          this.snackBar.open('Račun je storniran.', 'U redu', { duration: 4000 });
          this.back();
        });
      });
  }

  private reverse(orderId: string): void {
    const data: VoidDialogData = {
      title: 'Storno plaćenog računa',
      // The API holds a reversal to a longer explanation than an ordinary void: it takes money back
      // out of the day's takings.
      minReasonLength: 10,
      hint: 'Ovaj postupak izdaje protivstavku i umanjuje dnevni pazar.',
    };

    this.dialog
      .open(VoidDialog, { data, width: '460px' })
      .afterClosed()
      .subscribe((result: VoidDialogResult | undefined) => {
        if (!result) {
          return;
        }

        this.run(this.api.reverseOrder(orderId, result.reason), () => {
          this.snackBar.open('Plaćen račun je storniran.', 'U redu', { duration: 4000 });
          this.back();
        });
      });
  }

  protected pay(): void {
    const current = this.order();

    if (!current) {
      return;
    }

    this.dialog
      .open(PaymentDialog, { data: { total: current.total }, width: '460px' })
      .afterClosed()
      .subscribe((result: PaymentDialogResult | undefined) => {
        if (!result) {
          return;
        }

        this.run(this.api.pay(current.id, result.method), () => {
          this.snackBar.open('Račun je naplaćen.', 'U redu', { duration: 4000 });
          this.showReceipt(current.id);
        });
      });
  }

  /**
   * Offers the settled bill for printing, then returns to the floor.
   *
   * The screen leaves for the floor whether or not the receipt could be fetched: the money is
   * already taken, and a waiter must not be stranded on a paid tab because a print preview failed.
   */
  private showReceipt(orderId: string): void {
    this.api.receipt(orderId).subscribe({
      next: (receipt) =>
        this.dialog
          .open(ReceiptDialog, {
            data: { receipt, canReverse: this.canReverse() },
            width: '420px',
          })
          .afterClosed()
          .subscribe((result: ReceiptDialogResult | undefined) => {
            if (result?.reverse) {
              this.reverse(orderId);
              return;
            }

            this.back();
          }),
      error: () => this.back(),
    });
  }

  /**
   * Loads the table and whatever tab is already running on it.
   *
   * The floor plan is the source of both: it is the one call that says which tabs a table is
   * carrying, so a waiter coming back to an occupied table — or simply reloading the page — picks
   * the bill up where it was instead of being shown an empty one and opening a second tab beside it.
   */
  private loadTable(): void {
    this.busy.set(true);

    this.api.floorPlan().subscribe({
      next: (plan) => {
        const all = [...plan.rooms.flatMap((room) => room.tables), ...plan.unplacedTables];
        const found = all.find((candidate) => candidate.id === this.tableId()) ?? null;

        this.table.set(found);
        this.openTabIds.set(found?.openOrderIds ?? []);
        this.busy.set(false);

        const running = found?.openOrderIds[0];

        if (running) {
          this.loadOrder(running);
        } else {
          this.order.set(null);
        }
      },
      error: () => this.busy.set(false),
    });
  }

  protected switchTab(orderId: string): void {
    if (orderId !== this.order()?.id) {
      this.loadOrder(orderId);
    }
  }

  private loadOrder(orderId: string): void {
    this.run(this.api.order(orderId), (loaded) => this.order.set(loaded));
  }

  private run<T>(call: import('rxjs').Observable<T>, done: (value: T) => void): void {
    this.busy.set(true);

    call.subscribe({
      next: (value) => {
        this.busy.set(false);
        done(value);
      },
      // The error interceptor has already told the person what went wrong; this only releases the UI.
      error: () => this.busy.set(false),
    });
  }
}
