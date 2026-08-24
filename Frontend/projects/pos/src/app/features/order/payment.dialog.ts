import { CurrencyPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { PaymentMethod, paymentMethodLabels } from 'shared';

export interface PaymentDialogData {
  total: number;
}

export interface PaymentDialogResult {
  method: PaymentMethod;
}

/**
 * Takes payment.
 *
 * For cash it also works out the change. The amount tendered is never sent to the API — the venue
 * takes the bill total either way — but a waiter counting notes at a table needs the arithmetic done
 * for them, and getting it wrong is the most common way a till loses money to nobody in particular.
 */
@Component({
  selector: 'pos-payment-dialog',
  imports: [
    CurrencyPipe,
    FormsModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
  ],
  template: `
    <h2 mat-dialog-title>Naplata</h2>

    <mat-dialog-content>
      <div class="pay__total">
        <span>Za naplatu</span>
        <strong>{{ data.total | currency: 'RSD' : 'symbol-narrow' : '1.0-2' }}</strong>
      </div>

      <mat-button-toggle-group
        [value]="method()"
        (change)="method.set($any($event).value)"
        class="pay__methods"
      >
        @for (option of methods; track option) {
          <mat-button-toggle [value]="option">
            <mat-icon>{{ icon(option) }}</mat-icon>
            {{ paymentMethodLabels[option] }}
          </mat-button-toggle>
        }
      </mat-button-toggle-group>

      @if (method() === PaymentMethod.Cash) {
        <mat-form-field appearance="outline" class="pay__tendered">
          <mat-label>Primljeno</mat-label>
          <input
            matInput
            type="number"
            min="0"
            step="10"
            [ngModel]="tendered()"
            (ngModelChange)="tendered.set(+$event)"
            name="tendered"
          />
          <span matTextSuffix>RSD</span>
        </mat-form-field>

        <div class="pay__quick">
          @for (note of suggestions(); track note) {
            <button mat-stroked-button type="button" (click)="tendered.set(note)">
              {{ note }}
            </button>
          }
        </div>

        <div class="pay__change" [class.pay__change--short]="change() < 0">
          <span>{{ change() < 0 ? 'Nedostaje' : 'Kusur' }}</span>
          <strong>{{ absChange() | currency: 'RSD' : 'symbol-narrow' : '1.0-2' }}</strong>
        </div>
      }
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Odustani</button>
      <button mat-flat-button (click)="confirm()">Naplati</button>
    </mat-dialog-actions>
  `,
  styles: `
    .pay__total {
      display: flex;
      justify-content: space-between;
      align-items: baseline;
      margin-bottom: 16px;
      font-size: 1.1rem;
    }

    .pay__total strong {
      font-size: 1.8rem;
      font-variant-numeric: tabular-nums;
    }

    .pay__methods {
      width: 100%;
      margin-bottom: 16px;
    }

    .pay__methods mat-button-toggle {
      flex: 1;
    }

    .pay__tendered {
      width: 100%;
    }

    .pay__quick {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
      margin-bottom: 12px;
    }

    .pay__change {
      display: flex;
      justify-content: space-between;
      align-items: baseline;
      padding: 12px;
      border-radius: var(--dr-radius);
      background: var(--dr-free-bg);
      color: var(--dr-free);
    }

    .pay__change--short {
      background: var(--dr-occupied-bg);
      color: var(--dr-occupied);
    }

    .pay__change strong {
      font-size: 1.4rem;
      font-variant-numeric: tabular-nums;
    }
  `,
})
export class PaymentDialog {
  protected readonly data = inject<PaymentDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<PaymentDialog, PaymentDialogResult>);

  protected readonly PaymentMethod = PaymentMethod;
  protected readonly paymentMethodLabels = paymentMethodLabels;
  protected readonly methods = [PaymentMethod.Cash, PaymentMethod.Card, PaymentMethod.DigitalWallet];

  protected readonly method = signal(PaymentMethod.Cash);
  protected readonly tendered = signal(this.data.total);

  protected readonly change = computed(() => this.tendered() - this.data.total);
  protected readonly absChange = computed(() => Math.abs(this.change()));

  /**
   * The notes a guest is most likely to hand over: the exact amount, and the next few round figures
   * above it. Saves typing on a touchscreen, which is where this is used.
   */
  protected readonly suggestions = computed(() => {
    const total = this.data.total;
    const rounded = [500, 1000, 2000, 5000]
      .map((step) => Math.ceil(total / step) * step)
      .filter((value) => value > total);

    return [...new Set([Math.ceil(total), ...rounded])].slice(0, 4);
  });

  protected icon(method: PaymentMethod): string {
    switch (method) {
      case PaymentMethod.Card:
        return 'credit_card';
      case PaymentMethod.DigitalWallet:
        return 'account_balance_wallet';
      default:
        return 'payments';
    }
  }

  protected confirm(): void {
    this.ref.close({ method: this.method() });
  }
}
