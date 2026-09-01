import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { FloorPlanTableDto, seatsLabel } from 'shared';

export interface TableDialogData {
  table: FloorPlanTableDto;
  /** Every number in use, so a clash is caught before the request rather than as a 409. */
  takenNumbers: number[];
}

export interface TableDialogResult {
  tableNumber: number;
  capacity: number;
  isActive: boolean;
}

/**
 * A table's own properties, as distinct from where it sits.
 *
 * Position, size and shape are edited on the canvas and saved with the whole room; these three are
 * the table itself and go to their own endpoint. Until this existed a table's number and capacity
 * were fixed at the moment it was created, and taking one out of service could not be done from the
 * till at all.
 */
@Component({
  selector: 'pos-table-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSlideToggleModule,
  ],
  template: `
    <h2 mat-dialog-title>Sto {{ data.table.tableNumber }}</h2>

    <mat-dialog-content>
      <div class="table__fields">
        <mat-form-field appearance="outline">
          <mat-label>Broj stola</mat-label>
          <input
            matInput
            type="number"
            min="1"
            cdkFocusInitial
            [ngModel]="number()"
            (ngModelChange)="number.set(+$event)"
            name="number"
          />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Mesta</mat-label>
          <input
            matInput
            type="number"
            min="1"
            max="50"
            [ngModel]="capacity()"
            (ngModelChange)="capacity.set(+$event)"
            name="capacity"
          />
          <span matTextSuffix>{{ seatsLabel(capacity()) }}</span>
        </mat-form-field>
      </div>

      <mat-slide-toggle [ngModel]="active()" (ngModelChange)="active.set($event)" name="active">
        U upotrebi
      </mat-slide-toggle>

      <p class="table__hint dr-muted">
        Sto van upotrebe nestaje sa ekrana sale i ne prima ni račune ni rezervacije. Ostaje na svemu
        što je do sada radio, i vraća se u upotrebu istim prekidačem.
      </p>

      @if (problem(); as message) {
        <p class="table__problem">
          <mat-icon inline>error_outline</mat-icon>
          {{ message }}
        </p>
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Odustani</button>
      <button mat-flat-button [disabled]="problem() !== null" (click)="confirm()">Sačuvaj</button>
    </mat-dialog-actions>
  `,
  styles: `
    .table__fields {
      display: flex;
      gap: 8px;
      min-width: min(380px, 76vw);
    }

    .table__fields mat-form-field {
      flex: 1;
    }

    .table__hint {
      margin: 12px 0 0;
      font-size: 0.85rem;
      max-width: 44ch;
    }

    .table__problem {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 8px 0 0;
      color: var(--mat-sys-error);
    }
  `,
})
export class TableDialog {
  protected readonly data = inject<TableDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<TableDialog, TableDialogResult>);

  protected readonly seatsLabel = seatsLabel;

  protected readonly number = signal(this.data.table.tableNumber);
  protected readonly capacity = signal(this.data.table.capacity);
  protected readonly active = signal(this.data.table.isActive);

  protected readonly problem = computed<string | null>(() => {
    const chosen = this.number();

    if (!Number.isInteger(chosen) || chosen < 1) {
      return 'Broj stola mora biti ceo broj veći od nule.';
    }

    if (chosen !== this.data.table.tableNumber && this.data.takenNumbers.includes(chosen)) {
      return `Sto broj ${chosen} već postoji.`;
    }

    const seats = this.capacity();

    if (!Number.isInteger(seats) || seats < 1 || seats > 50) {
      return 'Broj mesta mora biti između 1 i 50.';
    }

    return null;
  });

  protected confirm(): void {
    if (this.problem() !== null) {
      return;
    }

    this.ref.close({
      tableNumber: this.number(),
      capacity: this.capacity(),
      isActive: this.active(),
    });
  }
}
