import { CurrencyPipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { InventoryValuationLineDto, unitLabels } from 'shared';

export interface StockEntryDialogData {
  line: InventoryValuationLineDto;
}

export interface StockEntryDialogResult {
  quantity: number;
  purchaseUnitPrice: number;
  supplier: string | null;
  referenceNumber: string | null;
}

/**
 * Goods in, with what they cost.
 *
 * The purchase price is the point of the form rather than a detail on it: it feeds the rolling
 * average, and the average is where every margin figure on the menu and in the reports comes from.
 * A delivery booked without it leaves the item looking like pure profit.
 *
 * It was a panel at the bottom of the page before. On a store with more than a handful of
 * ingredients the button that opened it was above the fold and the form it opened was not, so the
 * screen appeared to do nothing.
 */
@Component({
  selector: 'pos-stock-entry-dialog',
  imports: [
    CurrencyPipe,
    DecimalPipe,
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
  ],
  template: `
    <h2 mat-dialog-title>Ulaz robe: {{ line.name }}</h2>

    <mat-dialog-content>
      <p class="entry__current dr-muted">
        Trenutno stanje {{ line.stockQuantity | number: '1.0-3' }} {{ unit }} ·
        prosečna nabavna {{ line.averagePurchasePrice | number: '1.2-4' }}
      </p>

      <div class="entry__fields">
        <mat-form-field appearance="outline">
          <mat-label>Količina</mat-label>
          <input
            matInput
            type="number"
            min="0"
            step="1"
            cdkFocusInitial
            [ngModel]="quantity()"
            (ngModelChange)="quantity.set(+$event)"
            name="quantity"
          />
          <span matTextSuffix>{{ unit }}</span>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Nabavna cena po jedinici</mat-label>
          <input
            matInput
            type="number"
            min="0"
            step="0.01"
            [ngModel]="unitPrice()"
            (ngModelChange)="unitPrice.set(+$event)"
            name="unitPrice"
          />
          <span matTextSuffix>RSD</span>
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Dobavljač</mat-label>
          <input matInput [ngModel]="supplier()" (ngModelChange)="supplier.set($event)" name="supplier" />
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>Broj otpremnice</mat-label>
          <input
            matInput
            [ngModel]="reference()"
            (ngModelChange)="reference.set($event)"
            name="reference"
          />
        </mat-form-field>
      </div>

      <div class="entry__total">
        <span>Ukupno</span>
        <strong>{{ total() | currency: 'RSD' : 'symbol-narrow' : '1.0-2' }}</strong>
      </div>

      <p class="entry__after">
        Novo stanje: <strong>{{ stockAfter() | number: '1.0-3' }} {{ unit }}</strong>
      </p>

      @if (problem(); as message) {
        <p class="entry__problem">
          <mat-icon inline>error_outline</mat-icon>
          {{ message }}
        </p>
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Odustani</button>
      <button mat-flat-button [disabled]="problem() !== null" (click)="confirm()">
        Zaduži magacin
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .entry__current {
      margin: 0 0 12px;
    }

    .entry__fields {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 8px;
      min-width: min(460px, 76vw);
    }

    .entry__total {
      display: flex;
      justify-content: space-between;
      align-items: baseline;
      margin-top: 8px;
      padding-top: 8px;
      border-top: 1px solid var(--mat-sys-outline-variant);
      font-size: 1.05rem;
    }

    .entry__total strong {
      font-size: 1.35rem;
      font-family: var(--dr-font-mono);
      font-variant-numeric: tabular-nums;
    }

    .entry__after {
      margin: 6px 0 0;
      color: var(--mat-sys-on-surface-variant);
      font-size: 0.9rem;
    }

    .entry__problem {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 8px 0 0;
      color: var(--mat-sys-error);
    }

    @media (max-width: 560px) {
      .entry__fields {
        grid-template-columns: 1fr;
      }
    }
  `,
})
export class StockEntryDialog {
  private readonly data = inject<StockEntryDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<StockEntryDialog, StockEntryDialogResult>);

  protected readonly line = this.data.line;
  protected readonly unit = unitLabels[this.data.line.unit];

  protected readonly quantity = signal(0);

  /** Pre-filled with what the last deliveries averaged, which is usually close enough to correct. */
  protected readonly unitPrice = signal(this.data.line.averagePurchasePrice);
  protected readonly supplier = signal('');
  protected readonly reference = signal('');

  protected readonly total = computed(() => this.quantity() * this.unitPrice());
  protected readonly stockAfter = computed(() => this.data.line.stockQuantity + this.quantity());

  protected readonly problem = computed<string | null>(() => {
    if (!Number.isFinite(this.quantity()) || this.quantity() <= 0) {
      return 'Unesite količinu veću od nule.';
    }

    if (!Number.isFinite(this.unitPrice()) || this.unitPrice() < 0) {
      return 'Nabavna cena ne može biti negativna.';
    }

    return null;
  });

  protected confirm(): void {
    if (this.problem() !== null) {
      return;
    }

    this.ref.close({
      quantity: this.quantity(),
      purchaseUnitPrice: this.unitPrice(),
      supplier: this.supplier().trim() || null,
      referenceNumber: this.reference().trim() || null,
    });
  }
}
