import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { OrderStatus, ReceiptDto, paymentMethodLabels } from 'shared';

export interface ReceiptDialogData {
  receipt: ReceiptDto;

  /**
   * Whether to offer reversing this bill.
   *
   * True for a manager or owner looking at a bill that has just been settled. This is the one place
   * the till can offer it: every other screen works from open tabs, and a bill stops being one the
   * moment it is paid.
   */
  canReverse: boolean;
}

/** What the caller should do next. */
export interface ReceiptDialogResult {
  reverse: boolean;
}

/**
 * The bill, as it is handed to the guest.
 *
 * A simulation, not a fiscal receipt: no ESIR, no tax authority, no signature. What it is instead is
 * the venue's own record of a settled tab, and the print stylesheet reduces the page to just this
 * card so `window.print()` produces the bill rather than the till around it.
 *
 * A reversed bill says so across the top. A voided tab that printed like a valid one would be a way
 * to walk out with goods and paper that agree with each other.
 */
@Component({
  selector: 'pos-receipt-dialog',
  imports: [CurrencyPipe, DatePipe, MatButtonModule, MatDialogModule, MatIconModule],
  template: `
    <div class="receipt dr-printable" id="dr-receipt">
      @if (receipt.isReversed || receipt.status === OrderStatus.Voided) {
        <p class="receipt__void">STORNIRANO — NEVAŽEĆI RAČUN</p>
      }

      <header class="receipt__head">
        <strong>{{ receipt.restaurantName }}</strong>
        @if (receipt.restaurantAddress) {
          <span>{{ receipt.restaurantAddress }}</span>
        }
        @if (receipt.restaurantPhone) {
          <span>{{ receipt.restaurantPhone }}</span>
        }
      </header>

      <dl class="receipt__meta">
        <dt>Račun</dt>
        <dd>{{ receipt.number }}</dd>
        <dt>Sto</dt>
        <dd>{{ receipt.tableNumber }}</dd>
        @if (receipt.servedBy) {
          <dt>Konobar</dt>
          <dd>{{ receipt.servedBy }}</dd>
        }
        <dt>Otvoren</dt>
        <dd>{{ receipt.openedAtUtc | date: 'dd.MM.yyyy. HH:mm' }}</dd>
        @if (receipt.paidAtUtc) {
          <dt>Plaćen</dt>
          <dd>{{ receipt.paidAtUtc | date: 'dd.MM.yyyy. HH:mm' }}</dd>
        }
        @if (receipt.paymentMethod !== null) {
          <dt>Način plaćanja</dt>
          <dd>{{ paymentMethodLabels[receipt.paymentMethod] }}</dd>
        }
      </dl>

      <table class="receipt__lines">
        @for (line of receipt.lines; track $index) {
          <tr>
            <td class="receipt__name">
              {{ line.name }}
              @if (line.notes) {
                <span class="receipt__note">{{ line.notes }}</span>
              }
            </td>
            <td class="receipt__qty">{{ line.quantity }}×</td>
            <td class="receipt__price">
              {{ line.unitPrice | currency: receipt.currencyCode : 'symbol-narrow' : '1.0-2' }}
            </td>
            <td class="receipt__sum">
              {{ line.lineTotal | currency: receipt.currencyCode : 'symbol-narrow' : '1.0-2' }}
            </td>
          </tr>
        }
      </table>

      <p class="receipt__total">
        <span>UKUPNO</span>
        <strong>{{ receipt.total | currency: receipt.currencyCode : 'symbol-narrow' : '1.0-2' }}</strong>
      </p>

      <p class="receipt__footer">Hvala na poseti!</p>
      <p class="receipt__sim">Simulacija računa — nije fiskalni isečak.</p>
    </div>

    <mat-dialog-actions align="end" class="dr-no-print">
      @if (data.canReverse && !receipt.isReversed) {
        <button mat-button color="warn" (click)="reverse()">Storno plaćenog</button>
      }
      <span class="dr-toolbar-spacer"></span>
      <button mat-button mat-dialog-close>Zatvori</button>
      <button mat-flat-button (click)="print()">
        <mat-icon>print</mat-icon>
        Štampaj
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .receipt {
      /* A thermal roll is 80 mm wide; matching it on screen means what is seen is what prints. */
      width: 80mm;
      max-width: 100%;
      margin: 0 auto;
      padding: 8px 0;
      /* The face the rest of the application borrows its figures from. It was 'Roboto Mono',
         which was never loaded — every bill printed in whatever the platform happened to have. */
      font-family: var(--dr-font-mono);
      font-size: 0.78rem;
      color: #000;
      background: #fff;
    }

    .receipt__void {
      margin: 0 0 8px;
      padding: 4px;
      text-align: center;
      font-weight: 700;
      letter-spacing: 0.05em;
      border: 2px solid #000;
    }

    .receipt__head {
      display: flex;
      flex-direction: column;
      align-items: center;
      text-align: center;
      gap: 2px;
      padding-bottom: 8px;
      border-bottom: 1px dashed #000;
    }

    .receipt__head strong {
      font-size: 1rem;
    }

    .receipt__meta {
      display: grid;
      grid-template-columns: auto 1fr;
      gap: 2px 12px;
      margin: 8px 0;
      padding-bottom: 8px;
      border-bottom: 1px dashed #000;
    }

    .receipt__meta dt {
      color: #444;
    }

    .receipt__meta dd {
      margin: 0;
      text-align: right;
    }

    .receipt__lines {
      width: 100%;
      border-collapse: collapse;
    }

    .receipt__lines td {
      padding: 2px 0;
      vertical-align: top;
    }

    .receipt__note {
      display: block;
      font-size: 0.7rem;
      color: #444;
    }

    .receipt__qty,
    .receipt__price,
    .receipt__sum {
      text-align: right;
      white-space: nowrap;
      padding-left: 6px !important;
      font-family: var(--dr-font-mono);
      font-variant-numeric: tabular-nums;
    }

    .receipt__total {
      display: flex;
      justify-content: space-between;
      align-items: baseline;
      margin: 8px 0 0;
      padding-top: 8px;
      border-top: 1px dashed #000;
      font-size: 1rem;
    }

    .receipt__total strong {
      font-family: var(--dr-font-mono);
      font-variant-numeric: tabular-nums;
    }

    .receipt__footer {
      margin: 12px 0 0;
      text-align: center;
    }

    .receipt__sim {
      margin: 4px 0 0;
      text-align: center;
      font-size: 0.68rem;
      color: #444;
    }
  `,
})
export class ReceiptDialog {
  protected readonly data = inject<ReceiptDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<ReceiptDialog, ReceiptDialogResult>);

  protected readonly receipt = this.data.receipt;
  protected readonly OrderStatus = OrderStatus;
  protected readonly paymentMethodLabels = paymentMethodLabels;

  protected print(): void {
    window.print();
  }

  /** Hands the decision back: the reason and the call belong to the screen that took the payment. */
  protected reverse(): void {
    this.ref.close({ reverse: true });
  }
}
