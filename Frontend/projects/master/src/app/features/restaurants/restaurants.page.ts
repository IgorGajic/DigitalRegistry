import { DatePipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Router } from '@angular/router';
import { LicenseStatus, PlatformApiService, RestaurantSummaryDto, licenseStatusLabels } from 'shared';

/**
 * Every venue on the platform, ordered by whatever is closest to lapsing.
 *
 * That ordering is the point of the screen: an administrator opens it to find who needs chasing, not
 * to browse alphabetically.
 */
@Component({
  selector: 'mstr-restaurants',
  imports: [
    DatePipe,
    FormsModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatTableModule,
    MatTooltipModule,
  ],
  template: `
    <div class="dr-page">
      <header class="rest__header">
        <h1>Restorani</h1>
        <span class="dr-toolbar-spacer"></span>

        <mat-form-field appearance="outline" class="rest__search">
          <mat-label>Pretraga</mat-label>
          <input matInput [(ngModel)]="search" (ngModelChange)="load()" />
          <mat-icon matSuffix>search</mat-icon>
        </mat-form-field>

        <button mat-flat-button (click)="showForm.set(!showForm())">
          <mat-icon>add_business</mat-icon>
          Nov restoran
        </button>
      </header>

      @if (showForm()) {
        <mat-card class="rest__form">
          <mat-card-header>
            <mat-card-title>Novi restoran</mat-card-title>
            <mat-card-subtitle>
              Restoran se pravi bez licence i bez vlasnika — oba su zasebni koraci, pa poluzavršena
              registracija ne može slučajno da pusti kasu u rad.
            </mat-card-subtitle>
          </mat-card-header>
          <mat-card-content>
            <form [formGroup]="form" (ngSubmit)="create()">
              <mat-form-field appearance="outline">
                <mat-label>Naziv</mat-label>
                <input matInput formControlName="name" />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Šifra (slug)</mat-label>
                <input matInput formControlName="slug" placeholder="kafana-x" />
                <mat-hint>
                  Mala slova, cifre i crtice. Osoblje je kuca pri svakoj prijavi i ne menja se kasnije.
                </mat-hint>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Adresa</mat-label>
                <input matInput formControlName="address" />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Kontakt email</mat-label>
                <input matInput type="email" formControlName="contactEmail" />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Telefon</mat-label>
                <input matInput formControlName="phoneNumber" />
              </mat-form-field>

              <div class="rest__form-actions">
                <button mat-flat-button type="submit" [disabled]="form.invalid">Sačuvaj</button>
                <button mat-button type="button" (click)="showForm.set(false)">Odustani</button>
              </div>
            </form>
          </mat-card-content>
        </mat-card>
      }

      <mat-card>
        <table mat-table [dataSource]="restaurants()">
          <ng-container matColumnDef="name">
            <th mat-header-cell *matHeaderCellDef>Restoran</th>
            <td mat-cell *matCellDef="let row">
              <strong>{{ row.name }}</strong>
              <br />
              <code class="dr-muted">{{ row.slug }}</code>
            </td>
          </ng-container>

          <ng-container matColumnDef="license">
            <th mat-header-cell *matHeaderCellDef>Licenca</th>
            <td mat-cell *matCellDef="let row">
              <span class="rest__badge" [style.background]="badgeBackground(row)" [style.color]="badgeColour(row)">
                {{ statusLabel(row.licenseStatus) }}
              </span>
            </td>
          </ng-container>

          <ng-container matColumnDef="expires">
            <th mat-header-cell *matHeaderCellDef>Ističe</th>
            <td mat-cell *matCellDef="let row">
              @if (row.licenseExpiresAtUtc) {
                {{ row.licenseExpiresAtUtc | date: 'dd.MM.yyyy.' }}
                <span class="dr-muted">({{ row.daysRemaining }} dana)</span>
              } @else {
                <span class="dr-muted">nema licencu</span>
              }
            </td>
          </ng-container>

          <ng-container matColumnDef="staff">
            <th mat-header-cell *matHeaderCellDef class="dr-numeric">Osoblje</th>
            <td mat-cell *matCellDef="let row" class="dr-numeric">{{ row.staffCount }}</td>
          </ng-container>

          <ng-container matColumnDef="active">
            <th mat-header-cell *matHeaderCellDef>Stanje</th>
            <td mat-cell *matCellDef="let row">
              @if (row.isActive) {
                <mat-icon class="rest__on" matTooltip="Restoran je uključen">check_circle</mat-icon>
              } @else {
                <mat-icon class="rest__off" matTooltip="Restoran je ugašen — osoblje ne može da se prijavi">
                  cancel
                </mat-icon>
              }
            </td>
          </ng-container>

          <ng-container matColumnDef="actions">
            <th mat-header-cell *matHeaderCellDef></th>
            <td mat-cell *matCellDef="let row" class="rest__actions">
              <button mat-stroked-button (click)="open(row)">Detalji</button>
            </td>
          </ng-container>

          <tr mat-header-row *matHeaderRowDef="columns"></tr>
          <tr mat-row *matRowDef="let row; columns: columns"></tr>
        </table>

        @if (restaurants().length === 0) {
          <p class="dr-empty">Nema restorana.</p>
        }
      </mat-card>
    </div>
  `,
  styles: `
    .rest__header {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-bottom: 16px;
      flex-wrap: wrap;
    }

    h1 {
      margin: 0;
      font-size: 1.5rem;
    }

    .rest__search {
      width: 240px;
    }

    .rest__form {
      margin-bottom: 16px;
    }

    .rest__form form {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: 8px;
      padding-top: 8px;
    }

    .rest__form-actions {
      grid-column: 1 / -1;
      display: flex;
      gap: 8px;
    }

    table {
      width: 100%;
    }

    .rest__badge {
      display: inline-block;
      padding: 2px 10px;
      border-radius: 999px;
      font-size: 0.75rem;
      font-weight: 600;
    }

    .rest__on {
      color: var(--dr-valid);
    }

    .rest__off {
      color: var(--dr-expired);
    }

    .rest__actions {
      text-align: right;
    }

    code {
      font-size: 0.8rem;
    }
  `,
})
export class RestaurantsPage {
  private readonly api = inject(PlatformApiService);
  private readonly router = inject(Router);

  protected readonly columns = ['name', 'license', 'expires', 'staff', 'active', 'actions'];
  protected readonly restaurants = signal<RestaurantSummaryDto[]>([]);
  protected readonly showForm = signal(false);
  protected search = '';

  protected readonly form = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required]],
    // Mirrors the API rule: lower-case letters, digits and hyphens. Anything ambiguous is a support
    // call, since staff read this code aloud.
    slug: ['', [Validators.required, Validators.pattern(/^[a-z0-9]+(-[a-z0-9]+)*$/)]],
    address: [''],
    contactEmail: ['', [Validators.email]],
    phoneNumber: [''],
  });

  constructor() {
    this.load();
  }

  protected load(): void {
    this.api
      .restaurants({ search: this.search.trim() || undefined })
      .subscribe((rows) => this.restaurants.set(rows));
  }

  protected create(): void {
    if (this.form.invalid) {
      return;
    }

    const value = this.form.getRawValue();

    this.api
      .createRestaurant({
        name: value.name.trim(),
        slug: value.slug.trim(),
        address: value.address.trim() || null,
        contactEmail: value.contactEmail.trim() || null,
        phoneNumber: value.phoneNumber.trim() || null,
      })
      .subscribe((created) => {
        this.showForm.set(false);
        this.form.reset();
        // Straight into the detail screen, because a venue is useless until it has an owner and a
        // licence — both of which live there.
        void this.router.navigate(['/restorani', created.id]);
      });
  }

  protected open(restaurant: RestaurantSummaryDto): void {
    void this.router.navigate(['/restorani', restaurant.id]);
  }

  protected statusLabel(status: LicenseStatus): string {
    return licenseStatusLabels[status];
  }

  protected badgeColour(row: RestaurantSummaryDto): string {
    switch (row.licenseStatus) {
      case LicenseStatus.Active:
        return row.daysRemaining <= 30 ? '#fff' : '#fff';
      case LicenseStatus.Suspended:
        return '#fff';
      default:
        return '#fff';
    }
  }

  protected badgeBackground(row: RestaurantSummaryDto): string {
    switch (row.licenseStatus) {
      case LicenseStatus.Active:
        return row.daysRemaining <= 30 ? 'var(--dr-expiring)' : 'var(--dr-valid)';
      case LicenseStatus.Suspended:
        return 'var(--dr-suspended)';
      default:
        return 'var(--dr-expired)';
    }
  }
}
