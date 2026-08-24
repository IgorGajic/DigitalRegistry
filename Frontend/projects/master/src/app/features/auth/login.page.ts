import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { Router } from '@angular/router';
import { AuthService, describe } from 'shared';

/**
 * Sign-in for platform administrators.
 *
 * No restaurant code, unlike the till: these accounts belong to no venue. A restaurant owner is
 * refused here even with correct credentials, and the API says so without revealing which it was.
 */
@Component({
  selector: 'mstr-login',
  imports: [
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
  ],
  template: `
    <div class="login">
      <mat-card class="login__card">
        @if (busy()) {
          <mat-progress-bar mode="indeterminate" />
        }

        <mat-card-header>
          <mat-card-title>DigitalRegistry</mat-card-title>
          <mat-card-subtitle>Administracija platforme</mat-card-subtitle>
        </mat-card-header>

        <mat-card-content>
          <form [formGroup]="form" (ngSubmit)="submit()">
            <mat-form-field appearance="outline">
              <mat-label>Email</mat-label>
              <input matInput type="email" formControlName="email" autocomplete="username" />
              <mat-icon matSuffix>admin_panel_settings</mat-icon>
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Lozinka</mat-label>
              <input
                matInput
                type="password"
                formControlName="password"
                autocomplete="current-password"
              />
            </mat-form-field>

            @if (error()) {
              <p class="login__error" role="alert">{{ error() }}</p>
            }

            <button mat-flat-button type="submit" [disabled]="busy() || form.invalid">
              Prijavi se
            </button>
          </form>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: `
    .login {
      display: grid;
      place-items: center;
      min-height: 100vh;
      padding: 24px;
    }

    .login__card {
      width: min(400px, 100%);
      overflow: hidden;
    }

    form {
      display: flex;
      flex-direction: column;
      gap: 4px;
      margin-top: 8px;
    }

    mat-form-field {
      width: 100%;
    }

    button[type='submit'] {
      height: 48px;
      margin-top: 8px;
    }

    .login__error {
      margin: 0 0 8px;
      color: var(--mat-sys-error);
    }
  `,
})
export class LoginPage {
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  protected readonly busy = signal(false);
  protected readonly error = signal('');

  protected readonly form = inject(FormBuilder).nonNullable.group({
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  protected submit(): void {
    if (this.form.invalid || this.busy()) {
      return;
    }

    const { email, password } = this.form.getRawValue();

    this.busy.set(true);
    this.error.set('');

    this.auth.loginPlatformAdmin(email.trim(), password).subscribe({
      next: () => {
        this.busy.set(false);
        void this.router.navigate(['/']);
      },
      error: (failure) => {
        this.busy.set(false);
        this.error.set(describe(failure));
      },
    });
  }
}
