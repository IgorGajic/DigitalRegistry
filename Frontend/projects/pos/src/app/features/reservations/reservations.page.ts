import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import {
  AuthService,
  FloorPlanTableDto,
  LoadingState,
  ReservationScheduleEntryDto,
  ReservationStatus,
  TillApiService,
  UserRole,
  addDays,
  reservationStatusLabels,
  timeOfDay,
  toDateOnly,
} from 'shared';
import { RealtimeService } from 'shared/realtime';
import {
  ConfirmDialog,
  ConfirmDialogData,
} from 'shared/ui';

import {
  ReservationDialog,
  ReservationDialogData,
  ReservationDialogResult,
} from './reservation.dialog';

/**
 * The day's bookings, as the front of house works them.
 *
 * Three jobs: taking a booking, knowing what is coming, and marking a party in when it arrives.
 * Checking in is what turns a booking into a seated table — it raises the floor alert the rest of
 * the staff see — so it, and taking a booking, are what a waiter may do here. Cancelling belongs to
 * a manager, matching the API, which remains the authority; the button is hidden rather than left to
 * answer 403.
 *
 * A booking taken here is filed under the guest's name, not the name of whoever answered the
 * telephone. That is why the form insists on a name: the API books for the caller unless it is given
 * one, and the desk's own name on somebody else's table is worse than no booking at all.
 */
@Component({
  selector: 'pos-reservations',
  providers: [provideNativeDateAdapter()],
  imports: [
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
    @if (loading.active()) {
      <mat-progress-bar mode="indeterminate" />
    }

    <div class="dr-page">
      <header class="res__header">
        <h1>Rezervacije</h1>
        <span class="dr-toolbar-spacer"></span>

        <!--
          The day and the three ways of changing it are one control, and are grouped so that they
          stay one control when the header wraps. Left loose, the header's flex-wrap put the two
          chevrons on different lines with the date between them.
        -->
        <div class="res__day">
          <button mat-icon-button (click)="shift(-1)" matTooltip="Prethodni dan">
            <mat-icon>chevron_left</mat-icon>
          </button>

          <mat-form-field appearance="outline" class="res__date">
            <mat-label>Dan</mat-label>
            <input matInput [matDatepicker]="picker" [(ngModel)]="date" (dateChange)="load()" />
            <mat-datepicker-toggle matIconSuffix [for]="picker" />
            <mat-datepicker #picker />
          </mat-form-field>

          <button mat-icon-button (click)="shift(1)" matTooltip="Sledeći dan">
            <mat-icon>chevron_right</mat-icon>
          </button>

          <button mat-button (click)="today()">Danas</button>
        </div>

        <button mat-flat-button (click)="book()">
          <mat-icon>add</mat-icon>
          Nova rezervacija
        </button>

        <mat-form-field appearance="outline" class="res__filter">
          <mat-label>Sto</mat-label>
          <mat-select [(ngModel)]="tableId" (selectionChange)="load()">
            <mat-option [value]="null">Svi stolovi</mat-option>
            @for (table of tables(); track table.id) {
              <mat-option [value]="table.id">Sto {{ table.tableNumber }}</mat-option>
            }
          </mat-select>
        </mat-form-field>
      </header>

      <div class="res__summary">
        <mat-card>
          <mat-card-content>
            <span class="dr-muted">Rezervacija</span>
            <strong>{{ expected().length }}</strong>
          </mat-card-content>
        </mat-card>
        <mat-card>
          <mat-card-content>
            <span class="dr-muted">Gostiju</span>
            <strong>{{ guests() }}</strong>
          </mat-card-content>
        </mat-card>
        <mat-card>
          <mat-card-content>
            <span class="dr-muted">Stiglo</span>
            <strong>{{ arrived() }}</strong>
          </mat-card-content>
        </mat-card>
      </div>

      <mat-card>
        <table mat-table [dataSource]="entries()">
          <ng-container matColumnDef="time">
            <th mat-header-cell *matHeaderCellDef>Vreme</th>
            <td mat-cell *matCellDef="let row">
              <strong>{{ time(row.startTime) }}</strong>
              <span class="dr-muted"> – {{ time(row.endTime) }}</span>
            </td>
          </ng-container>

          <ng-container matColumnDef="table">
            <th mat-header-cell *matHeaderCellDef>Sto</th>
            <td mat-cell *matCellDef="let row">
              <button mat-button (click)="openTable(row.tableId)" matTooltip="Otvori sto">
                Sto {{ row.tableNumber }}
              </button>
            </td>
          </ng-container>

          <ng-container matColumnDef="guest">
            <th mat-header-cell *matHeaderCellDef>Gost</th>
            <td mat-cell *matCellDef="let row">
              <span
                [matTooltip]="row.takenBy ? 'Primio: ' + row.takenBy : 'Gost rezervisao sam'"
              >
                {{ row.guestName }}
              </span>
              @if (row.contactPhone) {
                <a class="res__phone" [href]="'tel:' + row.contactPhone">{{ row.contactPhone }}</a>
              }
            </td>
          </ng-container>

          <ng-container matColumnDef="party">
            <th mat-header-cell *matHeaderCellDef>Osoba</th>
            <td mat-cell *matCellDef="let row" class="dr-numeric">{{ row.partySize }}</td>
          </ng-container>

          <ng-container matColumnDef="status">
            <th mat-header-cell *matHeaderCellDef>Status</th>
            <td mat-cell *matCellDef="let row">
              <mat-chip [style.background]="statusBackground(row.status)">
                {{ statusLabel(row.status) }}
              </mat-chip>
            </td>
          </ng-container>

          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let row" class="res__actions">
              @if (isOpen(row)) {
                <button mat-flat-button (click)="checkIn(row)">
                  <mat-icon>how_to_reg</mat-icon>
                  Stigao
                </button>

                @if (canCancel()) {
                  <button mat-icon-button color="warn" (click)="cancel(row)" matTooltip="Otkaži">
                    <mat-icon>event_busy</mat-icon>
                  </button>
                }
              }
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr
            mat-row
            *matRowDef="let row; columns: columns"
            [class.res__row--done]="!isOpen(row)"
            [class.res__row--now]="isRunning(row)"
          ></tr>
        </table>

        @if (entries().length === 0 && !loading.active()) {
          <p class="dr-empty">Za izabrani dan nema rezervacija.</p>
        }
      </mat-card>
    </div>
  `,
  styles: `
    @use 'responsive-table' as rt;

    .res__header {
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

    .res__day {
      display: flex;
      align-items: center;
      gap: 8px;
    }

    .res__date {
      width: 170px;
    }

    .res__filter {
      width: 160px;
    }

    .res__date,
    .res__filter {
      margin-bottom: -1.25em;
    }

    .res__summary {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
      gap: var(--dr-gap);
      margin-bottom: var(--dr-gap);
    }

    .res__summary mat-card-content {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }

    .res__summary strong {
      font-size: 1.6rem;
      font-family: var(--dr-font-mono);
      font-variant-numeric: tabular-nums;
    }

    table {
      width: 100%;
    }

    /* The number is there to be dialled when a party is late, so it is a link, not decoration. */
    .res__phone {
      display: block;
      font-size: 0.8rem;
      color: var(--mat-sys-on-surface-variant);
    }

    .res__actions {
      white-space: nowrap;
      text-align: right;
    }

    /* Seated and cancelled bookings stay on the sheet: the desk needs to see what already happened. */
    .res__row--done {
      opacity: 0.55;
    }

    .res__row--now {
      background: var(--dr-reserved-bg);
    }

    @include rt.labels((
      time: 'Vreme',
      table: 'Sto',
      guest: 'Gost',
      party: 'Osoba',
      status: 'Status',
    ));

    @media (max-width: 900px) {
      .res__day {
        width: 100%;
      }

      /* Inside the group the date is the part that stretches; the buttons keep their own size. */
      .res__day .res__date {
        flex: 1;
        width: auto;
      }

      .res__date,
      .res__filter {
        width: 100%;
        margin-bottom: 0;
      }

      /* The table cell holds a button, which should not be pushed to the far edge on a card. */
      .cdk-column-table {
        justify-content: flex-start;
        gap: 8px;
      }
    }
  `,
})
export class ReservationsPage {
  private readonly api = inject(TillApiService);
  private readonly auth = inject(AuthService);
  private readonly realtime = inject(RealtimeService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  protected readonly loading = new LoadingState();
  protected readonly columns = ['time', 'table', 'guest', 'party', 'status', 'actions'];

  protected readonly entries = signal<ReservationScheduleEntryDto[]>([]);
  protected readonly tables = signal<FloorPlanTableDto[]>([]);

  protected date = new Date();
  protected tableId: string | null = null;

  /** Cancelled bookings are not covers, and counting them would flatter the evening. */
  protected readonly expected = computed(() =>
    this.entries().filter((entry) => entry.status !== ReservationStatus.Cancelled),
  );

  protected readonly guests = computed(() =>
    this.expected().reduce((sum, entry) => sum + entry.partySize, 0),
  );

  protected readonly arrived = computed(
    () => this.entries().filter((entry) => entry.status === ReservationStatus.Completed).length,
  );

  protected readonly canCancel = computed(() =>
    this.auth.hasAnyRole(UserRole.Manager, UserRole.Owner),
  );

  constructor() {
    this.loadTables();
    this.load();

    // Somebody else seating a party changes this sheet, so it is re-read rather than patched.
    effect(() => {
      if (this.realtime.lastEvent()?.kind === 'reservationArrivalAlert') {
        this.load();
      }
    });
  }

  protected load(): void {
    this.loading
      .track(this.api.reservationSchedule(toDateOnly(this.date), this.tableId ?? undefined))
      .subscribe((rows) => this.entries.set(rows));
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
  protected statusLabel(status: ReservationStatus): string {
    return reservationStatusLabels[status];
  }

  protected statusBackground(status: ReservationStatus): string {
    switch (status) {
      case ReservationStatus.Completed:
        return 'var(--dr-free-bg)';
      case ReservationStatus.Cancelled:
        return 'var(--dr-out-of-service-bg)';
      default:
        return 'var(--dr-reserved-bg)';
    }
  }

  /** Still to happen: everything that has been neither seated nor called off. */
  protected isOpen(entry: ReservationScheduleEntryDto): boolean {
    return (
      entry.status === ReservationStatus.Pending || entry.status === ReservationStatus.Confirmed
    );
  }

  /** Marked out because these are the bookings currently holding a table on the floor screen. */
  protected isRunning(entry: ReservationScheduleEntryDto): boolean {
    const now = Date.now();

    return (
      this.isOpen(entry) &&
      new Date(entry.startTime).getTime() <= now &&
      now < new Date(entry.endTime).getTime()
    );
  }

  protected checkIn(entry: ReservationScheduleEntryDto): void {
    this.api.checkInReservation(entry.id).subscribe(() => {
      this.snackBar.open(`${entry.guestName} — sto ${entry.tableNumber}.`, 'U redu', {
        duration: 4000,
      });
      this.load();
    });
  }

  protected cancel(entry: ReservationScheduleEntryDto): void {
    const data: ConfirmDialogData = {
      title: 'Otkazati rezervaciju?',
      message:
        `${entry.guestName}, sto ${entry.tableNumber}, ${this.time(entry.startTime)}. `
        + 'Sto se odmah oslobađa za druge goste.',
      confirmText: 'Otkaži rezervaciju',
      cancelText: 'Nazad',
      destructive: true,
    };

    this.dialog
      .open(ConfirmDialog, { data })
      .afterClosed()
      .subscribe((confirmed: boolean | undefined) => {
        if (!confirmed) {
          return;
        }

        this.api.cancelReservation(entry.id).subscribe(() => {
          this.snackBar.open('Rezervacija je otkazana.', 'U redu', { duration: 4000 });
          this.load();
        });
      });
  }

  /**
   * Takes a booking for a named guest.
   *
   * The sheet reloads rather than being patched: the API decides the status, and a booking made for
   * another day should not silently appear on the day being looked at.
   */
  protected book(): void {
    const data: ReservationDialogData = {
      tables: this.tables(),
      date: this.date,
      tableId: this.tableId,
    };

    this.dialog
      .open(ReservationDialog, { data })
      .afterClosed()
      .subscribe((result: ReservationDialogResult | undefined) => {
        if (!result) {
          return;
        }

        this.api.createReservation(result).subscribe((created) => {
          this.snackBar.open(
            `Rezervisano: ${result.contactName}, sto ${created.tableNumber}, `
              + `${this.time(created.startTime)}.`,
            'U redu',
            { duration: 5000 },
          );

          // Jump to the day the booking landed on, so it is visible straight away.
          this.date = new Date(created.startTime);
          this.load();
        });
      });
  }

  protected openTable(tableId: string): void {
    void this.router.navigate(['/sala', tableId]);
  }

  private loadTables(): void {
    this.api.floorPlan().subscribe((plan) => {
      const all = [...plan.rooms.flatMap((room) => room.tables), ...plan.unplacedTables];

      this.tables.set([...all].sort((left, right) => left.tableNumber - right.tableNumber));
    });
  }
}
