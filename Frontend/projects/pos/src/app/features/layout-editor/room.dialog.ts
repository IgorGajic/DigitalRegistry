import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { RoomDto } from 'shared';

export interface RoomDialogData {
  room: RoomDto;
}

export interface RoomDialogResult {
  name: string;
  displayOrder: number;
  canvasWidth: number;
  canvasHeight: number;
}

/**
 * A room's name and how large its floor is.
 *
 * The canvas is the coordinate space every table in the room is positioned in, so shrinking it can
 * strand a table outside the visible area — the API refuses that, naming the table. This form works
 * out the same thing first and says which table is in the way, because being told after the fact
 * that "table 7 would fall outside" gives no clue how much room is actually needed.
 */
@Component({
  selector: 'pos-room-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
  ],
  template: `
    <h2 mat-dialog-title>Prostorija: {{ data.room.name }}</h2>

    <mat-dialog-content>
      <mat-form-field appearance="outline" class="room__wide">
        <mat-label>Naziv</mat-label>
        <input
          matInput
          cdkFocusInitial
          [ngModel]="name()"
          (ngModelChange)="name.set($event)"
          name="name"
        />
        <mat-hint>Ime pod kojim stoji u tabovima sale.</mat-hint>
      </mat-form-field>

      <div class="room__fields">
        <mat-form-field appearance="outline">
          <mat-label>Širina platna</mat-label>
          <input
            matInput
            type="number"
            min="200"
            max="5000"
            step="50"
            [ngModel]="width()"
            (ngModelChange)="width.set(+$event)"
            name="width"
          />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Visina platna</mat-label>
          <input
            matInput
            type="number"
            min="200"
            max="5000"
            step="50"
            [ngModel]="height()"
            (ngModelChange)="height.set(+$event)"
            name="height"
          />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Redosled</mat-label>
          <input
            matInput
            type="number"
            min="0"
            [ngModel]="order()"
            (ngModelChange)="order.set(+$event)"
            name="order"
          />
          <mat-hint>Redosled tabova</mat-hint>
        </mat-form-field>
      </div>

      <p class="room__hint dr-muted">
        Platno je samo koordinatni prostor — odnos širine i visine određuje oblik sale na ekranu,
        a stolovi se skaliraju uz njega.
      </p>

      @if (problem(); as message) {
        <p class="room__problem">
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
    .room__wide {
      width: 100%;
      min-width: min(420px, 76vw);
    }

    .room__fields {
      display: flex;
      gap: 8px;
    }

    .room__fields mat-form-field {
      flex: 1;
    }

    .room__hint {
      margin: 12px 0 0;
      font-size: 0.85rem;
      max-width: 46ch;
    }

    .room__problem {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 8px 0 0;
      color: var(--mat-sys-error);
    }
  `,
})
export class RoomDialog {
  protected readonly data = inject<RoomDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<RoomDialog, RoomDialogResult>);

  protected readonly name = signal(this.data.room.name);
  protected readonly width = signal(this.data.room.canvasWidth);
  protected readonly height = signal(this.data.room.canvasHeight);
  protected readonly order = signal(this.data.room.displayOrder);

  protected readonly problem = computed<string | null>(() => {
    if (this.name().trim().length < 2) {
      return 'Naziv prostorije je prekratak.';
    }

    const width = this.width();
    const height = this.height();

    if (!Number.isFinite(width) || width < 200 || width > 5000) {
      return 'Širina platna mora biti između 200 i 5000.';
    }

    if (!Number.isFinite(height) || height < 200 || height > 5000) {
      return 'Visina platna mora biti između 200 i 5000.';
    }

    // The same check the API makes, made early and with the offending table named.
    const stranded = this.data.room.tables.find(
      (table) => table.positionX + table.width > width || table.positionY + table.height > height,
    );

    if (stranded) {
      return `Sto ${stranded.tableNumber} bi ostao izvan platna. Prvo ga pomerite, ili zadržite veće platno.`;
    }

    return null;
  });

  protected confirm(): void {
    if (this.problem() !== null) {
      return;
    }

    this.ref.close({
      name: this.name().trim(),
      displayOrder: this.order(),
      canvasWidth: this.width(),
      canvasHeight: this.height(),
    });
  }
}
