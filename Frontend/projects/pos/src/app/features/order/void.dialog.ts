import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';

export interface VoidDialogData {
  title: string;
  /** Present for an item void, where part of a line may be cancelled. */
  maxQuantity?: number;
  minReasonLength: number;
  hint?: string;
}

export interface VoidDialogResult {
  reason: string;
  quantity?: number;
}

/**
 * Asks why.
 *
 * The reason is not a formality: it is the whole content of the void report, which is how an owner
 * spots a waiter cancelling far more than their colleagues. The minimum length is enforced here as
 * well as by the API, so the person finds out before the request rather than after it.
 */
@Component({
  selector: 'pos-void-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>

    <mat-dialog-content>
      @if (data.hint) {
        <p class="void__hint">
          <mat-icon inline>warning</mat-icon>
          {{ data.hint }}
        </p>
      }

      @if (data.maxQuantity && data.maxQuantity > 1) {
        <mat-form-field appearance="outline">
          <mat-label>Koliko komada</mat-label>
          <input
            matInput
            type="number"
            min="1"
            [max]="data.maxQuantity"
            [(ngModel)]="quantity"
            name="quantity"
          />
          <mat-hint>Na računu ih ima {{ data.maxQuantity }}.</mat-hint>
        </mat-form-field>
      }

      <mat-form-field appearance="outline">
        <mat-label>Razlog</mat-label>
        <textarea
          matInput
          rows="3"
          [ngModel]="reason()"
          (ngModelChange)="reason.set($event)"
          name="reason"
          placeholder="npr. gost se predomislio, lom, greška pri kucanju"
        ></textarea>
        <mat-hint>Najmanje {{ data.minReasonLength }} znaka. Ostaje zabeleženo uz vaše ime.</mat-hint>
      </mat-form-field>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Odustani</button>
      <button mat-flat-button color="warn" [disabled]="!valid()" (click)="confirm()">
        Storniraj
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    mat-form-field {
      width: 100%;
    }

    .void__hint {
      display: flex;
      gap: 6px;
      align-items: flex-start;
      margin: 0 0 12px;
      color: var(--dr-reserved);
    }
  `,
})
export class VoidDialog {
  protected readonly data = inject<VoidDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<VoidDialog, VoidDialogResult>);

  protected readonly reason = signal('');
  protected quantity = 1;

  protected readonly valid = computed(() => this.reason().trim().length >= this.data.minReasonLength);

  protected confirm(): void {
    if (!this.valid()) {
      return;
    }

    this.ref.close({
      reason: this.reason().trim(),
      // Omitted when the whole line goes, which is what the API takes as "all of it".
      quantity: this.data.maxQuantity && this.data.maxQuantity > 1 ? this.quantity : undefined,
    });
  }
}
