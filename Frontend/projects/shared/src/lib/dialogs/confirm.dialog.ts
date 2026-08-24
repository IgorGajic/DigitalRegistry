import { Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';

export interface ConfirmDialogData {
  title: string;
  message: string;
  confirmText?: string;
  cancelText?: string;
  /** Colours the confirming button as a warning, for something that removes or discards. */
  destructive?: boolean;
}

/**
 * Asks yes or no.
 *
 * The counterpart to {@link PromptDialog}, and there for the same reason: `window.confirm` blocks
 * the page, ignores the theme, and gives no room to say what will actually happen — which is the
 * only part of a confirmation worth reading.
 */
@Component({
  selector: 'dr-confirm-dialog',
  imports: [MatButtonModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>

    <mat-dialog-content>
      <p class="confirm__message">{{ data.message }}</p>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button [mat-dialog-close]="false">{{ data.cancelText ?? 'Odustani' }}</button>
      <button
        mat-flat-button
        cdkFocusInitial
        [color]="data.destructive ? 'warn' : 'primary'"
        [mat-dialog-close]="true"
      >
        {{ data.confirmText ?? 'Potvrdi' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .confirm__message {
      margin: 0;
      max-width: 44ch;
    }
  `,
})
export class ConfirmDialog {
  protected readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);
}
