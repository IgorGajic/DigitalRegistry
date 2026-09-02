import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';

export interface PromptDialogData {
  title: string;
  label: string;
  /** Shown under the field: what the value is for, or what the API will accept. */
  hint?: string;
  /** Warning above the field, for a step that takes something away. */
  warning?: string;
  placeholder?: string;
  initialValue?: string;
  /** Enforced here as well as by the API, so the person finds out before the request. */
  minLength?: number;
  multiline?: boolean;
  confirmText?: string;
}

/**
 * Asks for one piece of text.
 *
 * Replaces `window.prompt`, which blocks the browser, cannot be styled, cannot say what the value is
 * for, and looks nothing like the rest of the application. Shared because both applications need the
 * same thing: a room's name here, a reason for suspending a licence there.
 */
@Component({
  selector: 'dr-prompt-dialog',
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
      @if (data.warning) {
        <p class="prompt__warning">
          <mat-icon inline>warning</mat-icon>
          {{ data.warning }}
        </p>
      }

      <mat-form-field appearance="outline" class="prompt__field">
        <mat-label>{{ data.label }}</mat-label>

        @if (data.multiline) {
          <textarea
            matInput
            rows="3"
            cdkFocusInitial
            [placeholder]="data.placeholder ?? ''"
            [ngModel]="value()"
            (ngModelChange)="value.set($event)"
          ></textarea>
        } @else {
          <input
            matInput
            cdkFocusInitial
            [placeholder]="data.placeholder ?? ''"
            [ngModel]="value()"
            (ngModelChange)="value.set($event)"
            (keyup.enter)="confirm()"
          />
        }

        @if (data.hint) {
          <mat-hint>{{ data.hint }}</mat-hint>
        }
      </mat-form-field>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Odustani</button>
      <button mat-flat-button [disabled]="!valid()" (click)="confirm()">
        {{ data.confirmText ?? 'Potvrdi' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .prompt__field {
      width: 100%;
      min-width: 320px;
    }

    .prompt__warning {
      display: flex;
      align-items: center;
      gap: 6px;
      margin: 0 0 12px;
      color: var(--mat-sys-error);
    }
  `,
})
export class PromptDialog {
  protected readonly data = inject<PromptDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<PromptDialog, string>);

  protected readonly value = signal(this.data.initialValue ?? '');

  protected readonly valid = computed(
    () => this.value().trim().length >= (this.data.minLength ?? 1),
  );

  protected confirm(): void {
    if (this.valid()) {
      this.ref.close(this.value().trim());
    }
  }
}
