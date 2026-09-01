import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { FloorPlanTableDto, seatsLabel } from 'shared';

export interface ReservationDialogData {
  tables: FloorPlanTableDto[];
  /** The day the sheet is showing, so a booking taken while looking at Friday lands on Friday. */
  date: Date;
  /** Pre-selected when the desk started from a particular table. */
  tableId?: string | null;
}

export interface ReservationDialogResult {
  tableId: string;
  startTime: string;
  endTime: string;
  partySize: number;
  contactName: string;
  contactPhone: string | null;
}

/** How long a sitting is held for unless the desk says otherwise. */
const DEFAULT_DURATION_HOURS = 2;

/**
 * Taking a booking at the desk.
 *
 * The name is required and is the point of the form: the API books for the caller unless a name is
 * given, so a booking entered without one would go down under the waiter who answered the telephone.
 * Everything else has a sensible default, because this is filled in with a phone against one ear.
 *
 * Capacity and the period are checked here as well as by the API — offering a table for four to a
 * party of six and only then being refused wastes the caller's time while they are on the line.
 */
@Component({
  selector: 'pos-reservation-dialog',
  providers: [provideNativeDateAdapter()],
  imports: [
    FormsModule,
    MatButtonModule,
    MatDatepickerModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
  ],
  template: `
    <h2 mat-dialog-title>Nova rezervacija</h2>

    <mat-dialog-content class="res-form">
      <mat-form-field appearance="outline" class="res-form__wide">
        <mat-label>Ime gosta</mat-label>
        <input
          matInput
          [ngModel]="name()"
          (ngModelChange)="name.set($event)"
          name="name"
          autocomplete="off"
          required
        />
        <mat-hint>Rezervacija se vodi na ovo ime, ne na vaše.</mat-hint>
      </mat-form-field>

      <mat-form-field appearance="outline" class="res-form__wide">
        <mat-label>Telefon (nije obavezno)</mat-label>
        <input
          matInput
          [ngModel]="phone()"
          (ngModelChange)="phone.set($event)"
          name="phone"
          autocomplete="off"
        />
      </mat-form-field>

      <mat-form-field appearance="outline">
        <mat-label>Dan</mat-label>
        <input
          matInput
          [matDatepicker]="picker"
          [ngModel]="day()"
          (ngModelChange)="day.set($event)"
          name="day"
        />
        <mat-datepicker-toggle matIconSuffix [for]="picker" />
        <mat-datepicker #picker />
      </mat-form-field>

      <mat-form-field appearance="outline">
        <mat-label>Od</mat-label>
        <input
          matInput
          type="time"
          [ngModel]="startTime()"
          (ngModelChange)="startTime.set($event)"
          name="startTime"
        />
      </mat-form-field>

      <mat-form-field appearance="outline">
        <mat-label>Do</mat-label>
        <input
          matInput
          type="time"
          [ngModel]="endTime()"
          (ngModelChange)="endTime.set($event)"
          name="endTime"
        />
      </mat-form-field>

      <mat-form-field appearance="outline">
        <mat-label>Osoba</mat-label>
        <input
          matInput
          type="number"
          min="1"
          max="50"
          [ngModel]="partySize()"
          (ngModelChange)="partySize.set(+$event)"
          name="partySize"
        />
      </mat-form-field>

      <mat-form-field appearance="outline" class="res-form__wide">
        <mat-label>Sto</mat-label>
        <mat-select [ngModel]="tableId()" (ngModelChange)="tableId.set($event)" name="tableId">
          @for (table of seatable(); track table.id) {
            <mat-option [value]="table.id">
              Sto {{ table.tableNumber }} — {{ table.capacity }} {{ seatsLabel(table.capacity) }}
            </mat-option>
          }
        </mat-select>
        @if (seatable().length === 0) {
          <mat-hint class="res-form__warn">Nijedan sto ne prima toliko gostiju.</mat-hint>
        }
      </mat-form-field>

      @if (problem(); as message) {
        <p class="res-form__problem">
          <mat-icon inline>error_outline</mat-icon>
          {{ message }}
        </p>
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Odustani</button>
      <button mat-flat-button [disabled]="problem() !== null" (click)="confirm()">Rezerviši</button>
    </mat-dialog-actions>
  `,
  styles: `
    .res-form {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 8px;
      padding-top: 8px;
      min-width: min(520px, 80vw);
    }

    .res-form__wide {
      grid-column: 1 / -1;
    }

    .res-form__warn {
      color: var(--mat-sys-error);
    }

    .res-form__problem {
      grid-column: 1 / -1;
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 0;
      color: var(--mat-sys-error);
    }
  `,
})
export class ReservationDialog {
  private readonly data = inject<ReservationDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<ReservationDialog, ReservationDialogResult>);

  protected readonly seatsLabel = seatsLabel;

  protected readonly name = signal('');
  protected readonly phone = signal('');
  protected readonly day = signal(new Date(this.data.date));
  protected readonly partySize = signal(2);
  protected readonly tableId = signal<string | null>(this.data.tableId ?? null);
  protected readonly startTime = signal(openingSlot(this.data.date));
  protected readonly endTime = signal(addHours(openingSlot(this.data.date), DEFAULT_DURATION_HOURS));

  /** Only tables that can physically take the party, which is the API's rule stated early. */
  protected readonly seatable = computed(() =>
    this.data.tables.filter(
      (table) => table.isActive && table.capacity >= Math.max(1, this.partySize()),
    ),
  );

  /** Whatever stops this booking being sent, in the order the desk would notice it. */
  protected readonly problem = computed<string | null>(() => {
    if (this.name().trim().length < 2) {
      return 'Unesite ime na koje se vodi rezervacija.';
    }

    const party = this.partySize();

    if (!Number.isFinite(party) || party < 1 || party > 50) {
      return 'Broj gostiju mora biti između 1 i 50.';
    }

    const chosen = this.tableId();

    if (!chosen || !this.seatable().some((table) => table.id === chosen)) {
      return 'Izaberite sto koji prima toliko gostiju.';
    }

    const { start } = this.window();

    if (start.getTime() <= Date.now()) {
      return 'Rezervacija mora biti u budućnosti.';
    }

    return null;
  });

  protected confirm(): void {
    if (this.problem() !== null) {
      return;
    }

    const { start, end } = this.window();

    this.ref.close({
      tableId: this.tableId()!,
      startTime: start.toISOString(),
      endTime: end.toISOString(),
      partySize: this.partySize(),
      contactName: this.name().trim(),
      contactPhone: this.phone().trim() || null,
    });
  }

  /**
   * The chosen day and the two wall-clock times, as instants.
   *
   * An end at or before the start means a sitting that runs past midnight, which is a late table
   * rather than a mistake — so it lands on the following day instead of being refused.
   */
  private window(): { start: Date; end: Date } {
    const start = at(this.day(), this.startTime());
    const end = at(this.day(), this.endTime());

    if (end <= start) {
      end.setDate(end.getDate() + 1);
    }

    return { start, end };
  }
}

/** A `Date` on `day` at the `HH:mm` the time input produced. Never mutates `day`. */
function at(day: Date, time: string): Date {
  const [hours, minutes] = time.split(':').map(Number);
  const result = new Date(day);

  result.setHours(hours || 0, minutes || 0, 0, 0);

  return result;
}

/**
 * The time the form opens on: the next half hour when booking for today, otherwise seven o'clock.
 *
 * A booking taken now is almost never for this minute, and one taken for a future day is almost
 * always for the evening.
 */
function openingSlot(day: Date): string {
  const now = new Date();

  if (day.toDateString() !== now.toDateString()) {
    return '19:00';
  }

  const slot = new Date(now.getTime() + 30 * 60000);
  slot.setMinutes(slot.getMinutes() > 30 ? 60 : 30, 0, 0);

  return format(slot.getHours(), slot.getMinutes());
}

function addHours(time: string, hours: number): string {
  const [h, m] = time.split(':').map(Number);

  return format((h + hours) % 24, m);
}

function format(hours: number, minutes: number): string {
  return `${`${hours}`.padStart(2, '0')}:${`${minutes}`.padStart(2, '0')}`;
}
