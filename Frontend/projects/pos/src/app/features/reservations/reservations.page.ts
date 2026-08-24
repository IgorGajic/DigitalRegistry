import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import {
  AuthService,
  FloorPlanTableDto,
  RealtimeService,
  ReservationScheduleEntryDto,
  ReservationStatus,
  TillApiService,
  UserRole,
  addDays,
  reservationStatusLabels,
  timeOfDay,
  toDateOnly,
} from 'shared';

/**
 * The day's bookings, as the front of house works them.
 *
 * Two jobs: knowing what is coming, and marking a party in when it arrives. Checking in is what
 * turns a booking into a seated table — it raises the floor alert the rest of the staff see — so it
 * is the one action here a waiter may take. Cancelling belongs to a manager, matching the API, which
 * remains the authority; the button is hidden rather than left to answer 403.
 *
 * Taking a booking is not offered. The API books for the caller, so a booking entered by a waiter
 * would be filed under the waiter's own name; a desk that books on a guest's behalf needs the API to
 * accept a guest first.
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
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
    MatTooltipModule,
  ],
  template: `
    <div class="dr-page">
      <header class="res__header">
        <h1>Rezervacije</h1>
        <span class="dr-toolbar-spacer"></span>

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
            <td mat-cell *matCellDef="let row">{{ row.guestName }}</td>
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

        @if (entries().length === 0) {
          <p class="dr-empty">Za izabrani dan nema rezervacija.</p>
        }
      </mat-card>
    </div>
  `,
  styles: `
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
      font-variant-numeric: tabular-nums;
    }

    table {
      width: 100%;
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
  `,
})
export class ReservationsPage {
  private readonly api = inject(TillApiService);
  private readonly auth = inject(AuthService);
  private readonly realtime = inject(RealtimeService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);

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
    this.api
      .reservationSchedule(toDateOnly(this.date), this.tableId ?? undefined)
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
    if (!confirm(`Otkazati rezervaciju za ${entry.guestName}, sto ${entry.tableNumber}?`)) {
      return;
    }

    this.api.cancelReservation(entry.id).subscribe(() => {
      this.snackBar.open('Rezervacija je otkazana.', 'U redu', { duration: 4000 });
      this.load();
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
