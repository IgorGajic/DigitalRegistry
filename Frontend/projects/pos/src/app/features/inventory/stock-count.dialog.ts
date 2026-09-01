import { DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { InventoryValuationLineDto, unitLabels } from 'shared';

export interface StockCountDialogData {
  line: InventoryValuationLineDto;
}

export interface StockCountDialogResult {
  counted: number;
  reason: string;
}

/** Shortest reason the API will take for a stocktake correction. */
const MIN_REASON = 3;

/**
 * A stocktake.
 *
 * What is asked for is the **counted** quantity, not the difference — that is what somebody holding
 * a stocktake sheet actually has. The difference is worked out and shown, so the correction can be
 * checked before it is filed rather than after.
 *
 * The reason is required because this is the only movement not driven by a sale or a delivery: it
 * is the one place stock can change because a person said so, which is exactly the entry an owner
 * later wants explained.
 */
@Component({
  selector: 'pos-stock-count-dialog',
  imports: [
    DecimalPipe,
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
  ],
  template: `
    <h2 mat-dialog-title>Popis: {{ line.name }}</h2>

    <mat-dialog-content>
      <p class="count__book dr-muted">
        Knjigovodstveno stanje: {{ line.stockQuantity | number: '1.0-3' }} {{ unit }}
      </p>

      <mat-form-field appearance="outline" class="count__field">
        <mat-label>Prebrojano</mat-label>
        <input
          matInput
          type="number"
          min="0"
          step="0.1"
          cdkFocusInitial
          [ngModel]="counted()"
          (ngModelChange)="counted.set(+$event)"
          name="counted"
        />
        <span matTextSuffix>{{ unit }}</span>
        <mat-hint>Unesite koliko ste stvarno izbrojali, ne razliku.</mat-hint>
      </mat-form-field>

      <div class="count__delta" [class.count__delta--short]="difference() < 0">
        <span>{{ difference() < 0 ? 'Manjak' : difference() > 0 ? 'Višak' : 'Bez razlike' }}</span>
        <strong>
          {{ difference() > 0 ? '+' : '' }}{{ difference() | number: '1.0-3' }} {{ unit }}
        </strong>
      </div>

      <mat-form-field appearance="outline" class="count__field">
        <mat-label>Razlog</mat-label>
        <textarea
          matInput
          rows="2"
          [ngModel]="reason()"
          (ngModelChange)="reason.set($event)"
          name="reason"
          placeholder="npr. lom, kalo, greška u prijemu"
        ></textarea>
        <mat-hint>Najmanje {{ minReason }} znaka. Ostaje zabeleženo uz vaše ime.</mat-hint>
      </mat-form-field>

      @if (problem(); as message) {
        <p class="count__problem">
          <mat-icon inline>error_outline</mat-icon>
          {{ message }}
        </p>
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Odustani</button>
      <button mat-flat-button [disabled]="problem() !== null" (click)="confirm()">
        Sačuvaj popis
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .count__book {
      margin: 0 0 12px;
    }

    .count__field {
      width: 100%;
      min-width: min(420px, 76vw);
    }

    .count__delta {
      display: flex;
      justify-content: space-between;
      align-items: baseline;
      padding: 10px 12px;
      margin-bottom: 12px;
      border-radius: var(--dr-radius);
      background: var(--dr-free-bg);
      color: var(--dr-free);
    }

    .count__delta--short {
      background: var(--dr-occupied-bg);
      color: var(--dr-occupied);
    }

    .count__delta strong {
      font-size: 1.2rem;
      font-family: var(--dr-font-mono);
      font-variant-numeric: tabular-nums;
    }

    .count__problem {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 0;
      color: var(--mat-sys-error);
    }
  `,
})
export class StockCountDialog {
  private readonly data = inject<StockCountDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<StockCountDialog, StockCountDialogResult>);

  protected readonly line = this.data.line;
  protected readonly unit = unitLabels[this.data.line.unit];
  protected readonly minReason = MIN_REASON;

  protected readonly counted = signal(this.data.line.stockQuantity);
  protected readonly reason = signal('');

  protected readonly difference = computed(() => this.counted() - this.data.line.stockQuantity);

  protected readonly problem = computed<string | null>(() => {
    if (!Number.isFinite(this.counted()) || this.counted() < 0) {
      return 'Prebrojana količina ne može biti negativna.';
    }

    if (this.reason().trim().length < MIN_REASON) {
      return `Razlog mora imati bar ${MIN_REASON} znaka.`;
    }

    return null;
  });

  protected confirm(): void {
    if (this.problem() !== null) {
      return;
    }

    this.ref.close({ counted: this.counted(), reason: this.reason().trim() });
  }
}
