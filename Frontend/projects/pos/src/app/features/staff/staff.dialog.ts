import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { StaffMemberDto, UserRole, userRoleLabels } from 'shared';

export interface StaffDialogData {
  member: StaffMemberDto | null;
  passwordOnly?: boolean;
}

export interface StaffDialogResult {
  email: string;
  password?: string;
  firstName: string;
  lastName: string;
  role: UserRole;
}

/**
 * One form for taking somebody on, correcting their details, and resetting a password.
 *
 * The email is fixed once the account exists: it is half of the Identity user name, so changing it
 * would alter how the person signs in — a rename that silently locks somebody out.
 */
@Component({
  selector: 'pos-staff-dialog',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ title }}</h2>

    <mat-dialog-content>
      <form [formGroup]="form">
        @if (!data.passwordOnly) {
          <mat-form-field appearance="outline">
            <mat-label>Ime</mat-label>
            <input matInput formControlName="firstName" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Prezime</mat-label>
            <input matInput formControlName="lastName" />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Email</mat-label>
            <input matInput type="email" formControlName="email" />
            @if (data.member) {
              <mat-hint>Email se ne menja — deo je korisničkog imena za prijavu.</mat-hint>
            }
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>Uloga</mat-label>
            <mat-select formControlName="role">
              <mat-option [value]="UserRole.Waiter">{{ userRoleLabels[UserRole.Waiter] }}</mat-option>
              <mat-option [value]="UserRole.Manager">
                {{ userRoleLabels[UserRole.Manager] }}
              </mat-option>
            </mat-select>
          </mat-form-field>
        }

        @if (needsPassword) {
          <mat-form-field appearance="outline">
            <mat-label>{{ data.passwordOnly ? 'Nova lozinka' : 'Lozinka' }}</mat-label>
            <input matInput type="text" formControlName="password" />
            <mat-hint>
              Najmanje 8 znakova, sa velikim i malim slovom i cifrom. Zapišite je — prikazuje se
              samo sada.
            </mat-hint>
          </mat-form-field>
        }
      </form>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Odustani</button>
      <button mat-flat-button [disabled]="form.invalid" (click)="confirm()">Sačuvaj</button>
    </mat-dialog-actions>
  `,
  styles: `
    form {
      display: flex;
      flex-direction: column;
      gap: 4px;
      padding-top: 8px;
    }

    mat-form-field {
      width: 100%;
    }
  `,
})
export class StaffDialog {
  protected readonly data = inject<StaffDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<StaffDialog, StaffDialogResult>);

  protected readonly UserRole = UserRole;
  protected readonly userRoleLabels = userRoleLabels;

  protected readonly needsPassword = !this.data.member || !!this.data.passwordOnly;

  protected readonly title = this.data.passwordOnly
    ? 'Nova lozinka'
    : this.data.member
      ? 'Izmena zaposlenog'
      : 'Novi zaposleni';

  // Mirrors the API's password rules, so a weak one is caught before the request rather than after.
  private readonly passwordRules = [
    Validators.required,
    Validators.minLength(8),
    Validators.pattern(/(?=.*[a-z])(?=.*[A-Z])(?=.*\d).+/),
  ];

  protected readonly form = inject(FormBuilder).nonNullable.group({
    firstName: [
      this.data.member?.fullName.split(' ')[0] ?? '',
      this.data.passwordOnly ? [] : [Validators.required],
    ],
    lastName: [
      this.data.member?.fullName.split(' ').slice(1).join(' ') ?? '',
      this.data.passwordOnly ? [] : [Validators.required],
    ],
    email: [
      { value: this.data.member?.email ?? '', disabled: !!this.data.member },
      this.data.passwordOnly ? [] : [Validators.required, Validators.email],
    ],
    role: [this.data.member?.role ?? UserRole.Waiter],
    password: ['', this.needsPassword ? this.passwordRules : []],
  });

  protected confirm(): void {
    if (this.form.invalid) {
      return;
    }

    const value = this.form.getRawValue();

    this.ref.close({
      email: value.email.trim(),
      password: this.needsPassword ? value.password : undefined,
      firstName: value.firstName.trim(),
      lastName: value.lastName.trim(),
      role: value.role,
    });
  }
}
