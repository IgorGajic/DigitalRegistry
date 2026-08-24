import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { Router } from '@angular/router';
import { AuthService, LicenseStatusDto, TillApiService, licenseStatusLabels } from 'shared';

/**
 * Where a venue lands when its licence has lapsed.
 *
 * The API answers **402**, not 403, and this screen exists so that distinction reaches the person:
 * they are signed in perfectly well and would be allowed to do this — the restaurant simply has not
 * paid. Telling them "no access" would send them looking for the wrong fix.
 */
@Component({
  selector: 'pos-license-expired',
  imports: [DatePipe, MatButtonModule, MatCardModule, MatIconModule],
  template: `
    <div class="lapsed">
      <mat-card class="lapsed__card">
        <mat-card-content>
          <mat-icon class="lapsed__icon">receipt_long</mat-icon>

          <h1>Licenca nije važeća</h1>

          @if (status(); as s) {
            <p class="lapsed__lead">
              Kasa za <strong>{{ s.restaurantName }}</strong> je privremeno nedostupna.
            </p>
            <dl class="lapsed__facts">
              <dt>Status</dt>
              <dd>{{ licenseStatusLabels[s.status] }}</dd>
              @if (s.expiresAtUtc) {
                <dt>Važila do</dt>
                <dd>{{ s.expiresAtUtc | date: 'dd.MM.yyyy.' }}</dd>
              }
            </dl>
          } @else {
            <p class="lapsed__lead">Kasa za ovaj restoran je privremeno nedostupna.</p>
          }

          <p class="dr-muted">
            Podaci nisu izgubljeni. Čim licenca bude obnovljena, kasa nastavlja da radi tamo gde je
            stala — obratite se administratoru platforme.
          </p>

          <div class="lapsed__actions">
            <button mat-flat-button (click)="recheck()" [disabled]="busy()">
              <mat-icon>refresh</mat-icon>
              Proveri ponovo
            </button>
            <button mat-button (click)="auth.logout()">Odjavi se</button>
          </div>
        </mat-card-content>
      </mat-card>
    </div>
  `,
  styles: `
    .lapsed {
      display: grid;
      place-items: center;
      min-height: 100vh;
      padding: 24px;
    }

    .lapsed__card {
      width: min(520px, 100%);
      text-align: center;
    }

    .lapsed__icon {
      font-size: 56px;
      width: 56px;
      height: 56px;
      color: var(--dr-occupied);
      margin-top: 8px;
    }

    h1 {
      margin: 8px 0 4px;
      font-size: 1.5rem;
    }

    .lapsed__lead {
      margin: 0 0 16px;
    }

    .lapsed__facts {
      display: grid;
      grid-template-columns: auto auto;
      gap: 4px 16px;
      justify-content: center;
      margin: 0 0 16px;
    }

    .lapsed__facts dt {
      color: var(--mat-sys-on-surface-variant);
      text-align: right;
    }

    .lapsed__facts dd {
      margin: 0;
      font-weight: 500;
      text-align: left;
    }

    .lapsed__actions {
      display: flex;
      gap: 8px;
      justify-content: center;
      margin-top: 20px;
    }
  `,
})
export class LicenseExpiredPage {
  protected readonly auth = inject(AuthService);
  private readonly api = inject(TillApiService);
  private readonly router = inject(Router);

  protected readonly licenseStatusLabels = licenseStatusLabels;
  protected readonly status = signal<LicenseStatusDto | null>(null);
  protected readonly busy = signal(false);

  constructor() {
    this.load();
  }

  /**
   * Re-asks the API rather than trusting what this page was told when it opened.
   *
   * The licence check is not cached anywhere, so a renewal recorded in the master application takes
   * effect on the very next request — which makes this button genuinely useful rather than decorative.
   */
  protected recheck(): void {
    this.busy.set(true);
    this.load(() => this.busy.set(false));
  }

  private load(done?: () => void): void {
    this.api.licenseStatus().subscribe({
      next: (status) => {
        this.status.set(status);
        done?.();

        if (status.isValid) {
          void this.router.navigate(['/']);
        }
      },
      error: () => done?.(),
    });
  }
}
