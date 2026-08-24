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

@Component({
  selector: 'pos-login',
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
          <mat-card-subtitle>Prijava na kasu</mat-card-subtitle>
        </mat-card-header>

        <mat-card-content>
          <form [formGroup]="form" (ngSubmit)="submit()">
            <mat-form-field appearance="outline">
              <mat-label>Šifra restorana</mat-label>
              <input matInput formControlName="restaurantSlug" autocomplete="organization" />
              <mat-icon matSuffix>storefront</mat-icon>
              <mat-hint>Kod koji ste dobili uz nalog, npr. „demo“</mat-hint>
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Email</mat-label>
              <input matInput type="email" formControlName="email" autocomplete="username" />
              <mat-icon matSuffix>mail</mat-icon>
            </mat-form-field>

            <mat-form-field appearance="outline">
              <mat-label>Lozinka</mat-label>
              <input
                matInput
                [type]="showPassword() ? 'text' : 'password'"
                formControlName="password"
                autocomplete="current-password"
              />
              <button
                mat-icon-button
                matSuffix
                type="button"
                (click)="showPassword.set(!showPassword())"
                [attr.aria-label]="showPassword() ? 'Sakrij lozinku' : 'Prikaži lozinku'"
              >
                <mat-icon>{{ showPassword() ? 'visibility_off' : 'visibility' }}</mat-icon>
              </button>
            </mat-form-field>

            @if (error()) {
              <p class="login__error" role="alert">{{ error() }}</p>
            }

            <button
              mat-flat-button
              type="submit"
              class="login__submit"
              [disabled]="busy() || form.invalid"
            >
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
      width: min(420px, 100%);
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

    .login__submit {
      margin-top: 8px;
      height: 48px;
      font-size: 1rem;
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
  protected readonly showPassword = signal(false);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    // The venue code is what selects the tenant: the same email may exist at more than one
    // restaurant, so it alone does not identify an account.
    restaurantSlug: ['', [Validators.required]],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required]],
  });

  protected submit(): void {
    if (this.form.invalid || this.busy()) {
      return;
    }

    const { restaurantSlug, email, password } = this.form.getRawValue();

    this.busy.set(true);
    this.error.set('');

    this.auth.login(restaurantSlug.trim(), email.trim(), password).subscribe({
      next: () => {
        this.busy.set(false);
        void this.router.navigate(['/']);
      },
      error: (failure) => {
        this.busy.set(false);
        // Shown on the form rather than as a snackbar: the person is looking at these three fields
        // and one of them is wrong.
        this.error.set(describe(failure));
      },
    });
  }
}
