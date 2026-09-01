import { CurrencyPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  AuthService,
  FloorPlanTableDto,
  OrderStatus,
  OrderSummaryDto,
  TillApiService,
  UserRole,
  addDays,
  endOfDayUtc,
  orderStatusLabels,
  paymentMethodLabels,
  startOfDayUtc,
  timeOfDay,
} from 'shared';

import { ReceiptDialog, ReceiptDialogResult } from '../order/receipt.dialog';
import { VoidDialog, VoidDialogData, VoidDialogResult } from '../order/void.dialog';

/**
 * The day's bills, and the way back into one that has been closed.
 *
 * Until this screen existed a settled bill could only be reached while its receipt was still on the
 * screen that took the payment: close that, and the bill could be neither reprinted nor reversed,
 * because nothing listed orders and nobody writes an id down. A guest coming back an hour later to
 * query a charge had no answer.
 *
 * Reversing is offered here to a manager or owner, as it is on the receipt itself — the one void a
 * waiter may not perform. A waiter still gets the screen, because fetching a copy of a bill they
 * closed themselves should not need somebody senior.
 */
@Component({
  selector: 'pos-bills',
  providers: [provideNativeDateAdapter()],
  imports: [
    CurrencyPipe,
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatDatepickerModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatSelectModule,
    MatTableModule,
    MatTooltipModule,
  ],
  template: `
    <div class="dr-page">
      @if (busy()) {
        <mat-progress-bar mode="indeterminate" class="dr-no-print" />
      }

      <header class="bills__header">
        <h1>Poslednji računi</h1>
        <span class="dr-toolbar-spacer"></span>

        <button mat-icon-button (click)="shift(-1)" matTooltip="Prethodni dan">
          <mat-icon>chevron_left</mat-icon>
        </button>

        <mat-form-field appearance="outline" class="bills__date">
          <mat-label>Dan</mat-label>
          <input matInput [matDatepicker]="picker" [(ngModel)]="date" (dateChange)="load()" />
          <mat-datepicker-toggle matIconSuffix [for]="picker" />
          <mat-datepicker #picker />
        </mat-form-field>

        <button mat-icon-button (click)="shift(1)" matTooltip="Sledeći dan">
          <mat-icon>chevron_right</mat-icon>
        </button>

        <button mat-button (click)="today()">Danas</button>

        <mat-form-field appearance="outline" class="bills__filter">
          <mat-label>Status</mat-label>
          <mat-select [(ngModel)]="status" (selectionChange)="load()">
            <mat-option [value]="null">Svi</mat-option>
            <mat-option [value]="OrderStatus.Paid">Plaćen</mat-option>
            <mat-option [value]="OrderStatus.Open">Otvoren</mat-option>
            <mat-option [value]="OrderStatus.Voided">Storniran</mat-option>
            <mat-option [value]="OrderStatus.Cancelled">Otkazan</mat-option>
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="bills__filter">
          <mat-label>Sto</mat-label>
          <mat-select [(ngModel)]="tableId" (selectionChange)="load()">
            <mat-option [value]="null">Svi stolovi</mat-option>
            @for (table of tables(); track table.id) {
              <mat-option [value]="table.id">Sto {{ table.tableNumber }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="bills__search">
          <mat-label>Broj računa</mat-label>
          <input matInput [(ngModel)]="search" placeholder="npr. 3F2504E0" />
          <mat-icon matIconSuffix>search</mat-icon>
        </mat-form-field>
      </header>

      <div class="bills__summary">
        <mat-card>
          <mat-card-content>
            <span class="dr-muted">Naplaćeno</span>
            <strong>{{ settled() | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}</strong>
          </mat-card-content>
        </mat-card>
        <mat-card>
          <mat-card-content>
            <span class="dr-muted">Računa</span>
            <strong>{{ settledCount() }}</strong>
          </mat-card-content>
        </mat-card>
        <mat-card>
          <mat-card-content>
            <span class="dr-muted">Stornirano</span>
            <strong>{{ reversed() | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}</strong>
          </mat-card-content>
        </mat-card>
      </div>

      <mat-card>
        <table mat-table [dataSource]="visible()">
          <ng-container matColumnDef="number">
            <th mat-header-cell *matHeaderCellDef>Račun</th>
            <td mat-cell *matCellDef="let row">
              <span class="bills__number">{{ row.number }}</span>
            </td>
          </ng-container>

          <ng-container matColumnDef="time">
            <th mat-header-cell *matHeaderCellDef>Vreme</th>
            <td mat-cell *matCellDef="let row">
              <strong>{{ time(row.paidAtUtc ?? row.createdAt) }}</strong>
              @if (row.paidAtUtc) {
                <span class="dr-muted"> · otvoren {{ time(row.createdAt) }}</span>
              }
            </td>
          </ng-container>

          <ng-container matColumnDef="table">
            <th mat-header-cell *matHeaderCellDef>Sto</th>
            <td mat-cell *matCellDef="let row">
              Sto {{ row.tableNumber }}
              @if (row.placedByGuest) {
                <mat-icon class="bills__qr" matTooltip="Gost poručio preko QR koda">
                  qr_code_2
                </mat-icon>
              }
            </td>
          </ng-container>

          <ng-container matColumnDef="servedBy">
            <th mat-header-cell *matHeaderCellDef>Konobar</th>
            <td mat-cell *matCellDef="let row">{{ row.servedBy ?? '—' }}</td>
          </ng-container>

          <ng-container matColumnDef="items">
            <th mat-header-cell *matHeaderCellDef>Stavki</th>
            <td mat-cell *matCellDef="let row" class="dr-numeric">{{ row.itemCount }}</td>
          </ng-container>

          <ng-container matColumnDef="total">
            <th mat-header-cell *matHeaderCellDef class="dr-numeric">Iznos</th>
            <td mat-cell *matCellDef="let row" class="dr-numeric">
              {{ row.total | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}
            </td>
          </ng-container>

          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let row">
              <mat-chip [style.background]="statusBackground(row.status)">
                {{ statusLabel(row.status) }}
              </mat-chip>
              @if (row.paymentMethod !== null) {
                <span class="dr-muted"> {{ methodLabel(row.paymentMethod) }}</span>
              }
            </td>
          </ng-container>

          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let row" class="bills__actions">
              <button mat-stroked-button (click)="openReceipt(row)">
                <mat-icon>receipt_long</mat-icon>
                Otisak
              </button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr
            mat-row
            *matRowDef="let row; columns: columns"
            [class.bills__row--void]="row.isReversed || row.status === OrderStatus.Cancelled"
            [class.bills__row--open]="row.status !== OrderStatus.Paid && !row.isReversed"
          ></tr>
        </table>

        @if (visible().length === 0 && !busy()) {
          <p class="dr-empty">Za izabrani dan nema računa koji odgovaraju filteru.</p>
        }
      </mat-card>
    </div>
  `,
  styles: `
    @use 'responsive-table' as rt;

    .bills__header {
      display: flex;
      align-items: center;
      gap: 8px;
      margin-bottom: 16px;
      flex-wrap: wrap;
    }

    h1 {
      margin: 0;
      font-size: 1.5rem;
    }

    .bills__date {
      width: 170px;
    }

    .bills__filter {
      width: 150px;
    }

    .bills__search {
      width: 180px;
    }

    .bills__date,
    .bills__filter,
    .bills__search {
      margin-bottom: -1.25em;
    }

    .bills__summary {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
      gap: var(--dr-gap);
      margin-bottom: var(--dr-gap);
    }

    .bills__summary mat-card-content {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }

    .bills__summary strong {
      font-size: 1.6rem;
      font-family: var(--dr-font-mono);
      font-variant-numeric: tabular-nums;
    }

    table {
      width: 100%;
    }

    /* The number is what a guest quotes over the telephone, so it reads as a code, not as prose. */
    .bills__number {
      font-family: var(--dr-font-mono);
      letter-spacing: 0.04em;
    }

    .bills__qr {
      font-size: 16px;
      width: 16px;
      height: 16px;
      vertical-align: middle;
      color: var(--mat-sys-on-surface-variant);
    }

    .bills__actions {
      white-space: nowrap;
      text-align: right;
    }

    /* Reversed and cancelled bills stay in the list: what happened to them is the point. */
    .bills__row--void {
      opacity: 0.55;
      text-decoration: line-through;
    }

    /* The row is struck through; the button on it is not. A struck-out label reads as unavailable,
       and reopening the bill is exactly what somebody looking at a reversed row wants to do. */
    .bills__row--void .bills__actions button {
      text-decoration: none;
    }

    .bills__row--open {
      background: var(--dr-reserved-bg);
    }

    @include rt.labels((
      number: 'Račun',
      time: 'Vreme',
      table: 'Sto',
      servedBy: 'Konobar',
      items: 'Stavki',
      total: 'Iznos',
      status: 'Status',
    ));

    @media (max-width: 900px) {
      /* The filters are a row of six controls; below the breakpoint they wrap to full width. */
      .bills__date,
      .bills__filter,
      .bills__search {
        width: 100%;
        margin-bottom: 0;
      }

      /* A struck-through card would strike the labels too, which reads as broken rather than void. */
      .bills__row--void {
        text-decoration: none;
        border-color: var(--dr-out-of-service);
      }
    }
  `,
})
export class BillsPage {
  private readonly api = inject(TillApiService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly OrderStatus = OrderStatus;

  protected readonly columns = [
    'number',
    'time',
    'table',
    'servedBy',
    'items',
    'total',
    'status',
    'actions',
  ];

  protected readonly bills = signal<OrderSummaryDto[]>([]);
  protected readonly tables = signal<FloorPlanTableDto[]>([]);
  protected readonly busy = signal(false);

  protected date = new Date();
  protected status: OrderStatus | null = null;
  protected tableId: string | null = null;
  protected search = '';

  /**
   * The number filter is applied here rather than by the API.
   *
   * The day's bills are already in hand, and a number is quoted when somebody is standing at the
   * till — filtering as they type beats a request per keystroke.
   */
  protected readonly visible = computed(() => {
    const needle = this.search.trim().toUpperCase();

    return needle ? this.bills().filter((bill) => bill.number.includes(needle)) : this.bills();
  });

  /** Reversed bills took money and gave it back, so they are not part of what was settled. */
  protected readonly settled = computed(() =>
    this.bills()
      .filter((bill) => bill.status === OrderStatus.Paid)
      .reduce((sum, bill) => sum + bill.total, 0),
  );

  protected readonly settledCount = computed(
    () => this.bills().filter((bill) => bill.status === OrderStatus.Paid).length,
  );

  protected readonly reversed = computed(() =>
    this.bills()
      .filter((bill) => bill.isReversed)
      .reduce((sum, bill) => sum + bill.total, 0),
  );

  protected readonly canReverse = computed(() =>
    this.auth.hasAnyRole(UserRole.Manager, UserRole.Owner),
  );

  constructor() {
    this.loadTables();
    this.load();
  }

  protected load(): void {
    this.busy.set(true);

    this.api
      .orders({
        fromUtc: startOfDayUtc(this.date),
        toUtc: endOfDayUtc(this.date),
        status: this.status,
        tableId: this.tableId,
      })
      .subscribe({
        next: (rows) => {
          this.bills.set(rows);
          this.busy.set(false);
        },
        error: () => this.busy.set(false),
      });
  }

  protected shift(days: number): void {
    this.date = addDays(this.date, days);
    this.load();
  }

  protected today(): void {
    this.date = new Date();
    this.load();
  }

  protected time(isoUtc: string): string {
    return timeOfDay(isoUtc);
  }

  /** Rows arrive untyped from the Material table, so lookups happen here and not in the template. */
  protected statusLabel(status: OrderStatus): string {
    return orderStatusLabels[status];
  }

  protected methodLabel(method: number): string {
    return paymentMethodLabels[method as keyof typeof paymentMethodLabels] ?? '';
  }

  protected statusBackground(status: OrderStatus): string {
    switch (status) {
      case OrderStatus.Paid:
        return 'var(--dr-free-bg)';
      case OrderStatus.Voided:
      case OrderStatus.Cancelled:
        return 'var(--dr-out-of-service-bg)';
      default:
        return 'var(--dr-reserved-bg)';
    }
  }

  /** Reopens the bill. Reversing is offered from the receipt, exactly as it is after payment. */
  protected openReceipt(bill: OrderSummaryDto): void {
    this.busy.set(true);

    this.api.receipt(bill.id).subscribe({
      next: (receipt) => {
        this.busy.set(false);

        this.dialog
          .open(ReceiptDialog, {
            data: {
              receipt,
              // Only a settled bill can be reversed; an open tab is voided from its own screen.
              canReverse: this.canReverse() && bill.status === OrderStatus.Paid,
            },
            width: '420px',
          })
          .afterClosed()
          .subscribe((result: ReceiptDialogResult | undefined) => {
            if (result?.reverse) {
              this.reverse(bill);
            }
          });
      },
      error: () => this.busy.set(false),
    });
  }

  private reverse(bill: OrderSummaryDto): void {
    const data: VoidDialogData = {
      title: `Storno plaćenog računa ${bill.number}`,
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

        this.api.reverseOrder(bill.id, result.reason).subscribe(() => {
          this.snackBar.open('Plaćen račun je storniran.', 'U redu', { duration: 4000 });
          this.load();
        });
      });
  }

  private loadTables(): void {
    this.api.floorPlan(true).subscribe((plan) => {
      const all = [...plan.rooms.flatMap((room) => room.tables), ...plan.unplacedTables];

      this.tables.set([...all].sort((left, right) => left.tableNumber - right.tableNumber));
    });
  }
}
