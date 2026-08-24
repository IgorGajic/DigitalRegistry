import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, inject, input, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import {
  LicenseDto,
  LicensePaymentDto,
  LicensePlan,
  LicenseStatus,
  PaymentMethod,
  PlatformApiService,
  RestaurantSummaryDto,
  licensePlanLabels,
  licenseStatusLabels,
  paymentMethodLabels,
} from 'shared';

/**
 * One venue: its licence, its payments, and the owner account.
 *
 * Everything that decides whether the restaurant can trade is on this one screen, because that is
 * the question an administrator opens it to answer.
 */
@Component({
  selector: 'mstr-restaurant-detail',
  imports: [
    CurrencyPipe,
    DatePipe,
    FormsModule,
    ReactiveFormsModule,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
  ],
  template: `
    <div class="dr-page">
      @if (restaurant(); as venue) {
        <header class="det__header">
          <a mat-icon-button routerLink="/restorani" aria-label="Nazad">
            <mat-icon>arrow_back</mat-icon>
          </a>
          <div>
            <h1>{{ venue.name }}</h1>
            <code class="dr-muted">{{ venue.slug }}</code>
          </div>
          <span class="dr-toolbar-spacer"></span>

          <button mat-stroked-button (click)="toggleEdit(venue)">
            <mat-icon>edit</mat-icon>
            {{ editing() ? 'Odustani' : 'Izmeni podatke' }}
          </button>

          @if (venue.isActive) {
            <button mat-stroked-button color="warn" (click)="setActive(false)">
              <mat-icon>block</mat-icon>
              Ugasi restoran
            </button>
          } @else {
            <button mat-flat-button (click)="setActive(true)">
              <mat-icon>power_settings_new</mat-icon>
              Uključi restoran
            </button>
          }
        </header>

        @if (editing()) {
          <mat-card class="det__edit">
            <mat-card-header>
              <mat-card-title>Podaci restorana</mat-card-title>
              <mat-card-subtitle>
                Šifra (slug) se ne menja — osoblje je kuca pri svakoj prijavi, a promena bi
                zaključala sve naloge restorana napolju.
              </mat-card-subtitle>
            </mat-card-header>
            <mat-card-content>
              <form [formGroup]="form" (ngSubmit)="save()">
                <mat-form-field appearance="outline">
                  <mat-label>Naziv</mat-label>
                  <input matInput formControlName="name" />
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

                <mat-form-field appearance="outline">
                  <mat-label>Valuta</mat-label>
                  <input matInput formControlName="currencyCode" maxlength="3" />
                  <mat-hint>Tri slova, npr. RSD</mat-hint>
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Vremenska zona</mat-label>
                  <input matInput formControlName="timeZoneId" />
                  <mat-hint>Po njoj se smene iz šablona pretvaraju u UTC</mat-hint>
                </mat-form-field>

                <div class="det__edit-actions">
                  <button mat-flat-button type="submit" [disabled]="form.invalid">Sačuvaj</button>
                  <button mat-button type="button" (click)="editing.set(false)">Odustani</button>
                </div>
              </form>
            </mat-card-content>
          </mat-card>
        }

        <div class="det__grid">
          <mat-card>
            <mat-card-header>
              <mat-card-title>Licenca</mat-card-title>
            </mat-card-header>
            <mat-card-content>
              @if (license(); as current) {
                <div class="det__license">
                  <mat-chip-set>
                    <mat-chip>{{ statusLabel(current.status) }}</mat-chip>
                    <mat-chip>{{ planLabel(current.plan) }}</mat-chip>
                  </mat-chip-set>

                  <dl>
                    <dt>Ističe</dt>
                    <dd>
                      {{ current.expiresAtUtc | date: 'dd.MM.yyyy.' }}
                      <span class="dr-muted">({{ current.daysRemaining }} dana)</span>
                    </dd>
                    <dt>Cena</dt>
                    <dd>{{ current.price | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}</dd>
                    <dt>Uplaćeno</dt>
                    <dd
                      [class.det__unpaid]="current.amountPaid < current.price"
                    >
                      {{ current.amountPaid | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}
                    </dd>
                    @if (current.notes) {
                      <dt>Napomena</dt>
                      <dd>{{ current.notes }}</dd>
                    }
                  </dl>

                  <div class="det__license-actions">
                    <mat-form-field appearance="outline">
                      <mat-label>Trajanje</mat-label>
                      <mat-select [(ngModel)]="plan">
                        @for (option of plans; track option) {
                          <mat-option [value]="option">{{ planLabel(option) }}</mat-option>
                        }
                      </mat-select>
                    </mat-form-field>

                    <mat-form-field appearance="outline">
                      <mat-label>Cena</mat-label>
                      <input matInput type="number" min="0" [(ngModel)]="price" />
                      <span matTextSuffix>RSD</span>
                    </mat-form-field>

                    <button mat-flat-button (click)="renew(current)">Produži</button>
                  </div>

                  <p class="dr-muted det__note">
                    Produžetak pre isteka se dodaje na postojeći rok — plaćanje ranije ne košta ništa.
                  </p>

                  <div class="det__license-state">
                    @if (current.status === LicenseStatus.Active) {
                      <button mat-stroked-button color="warn" (click)="suspend(current)">
                        Suspenduj
                      </button>
                    }
                    @if (current.status === LicenseStatus.Suspended) {
                      <button mat-stroked-button (click)="reactivate(current)">Vrati u rad</button>
                    }
                    @if (current.status !== LicenseStatus.Cancelled) {
                      <button mat-button color="warn" (click)="cancel(current)">Otkaži</button>
                    }
                  </div>
                </div>
              } @else {
                <p class="dr-muted">
                  Restoran nema licencu — kasa odgovara sa 402 na svaki poziv dok se ne izda.
                </p>

                <div class="det__license-actions">
                  <mat-form-field appearance="outline">
                    <mat-label>Trajanje</mat-label>
                    <mat-select [(ngModel)]="plan">
                      @for (option of plans; track option) {
                        <mat-option [value]="option">{{ planLabel(option) }}</mat-option>
                      }
                    </mat-select>
                  </mat-form-field>

                  <mat-form-field appearance="outline">
                    <mat-label>Cena</mat-label>
                    <input matInput type="number" min="0" [(ngModel)]="price" />
                    <span matTextSuffix>RSD</span>
                  </mat-form-field>

                  <button mat-flat-button (click)="issue()">Izdaj licencu</button>
                </div>
              }
            </mat-card-content>
          </mat-card>

          <mat-card>
            <mat-card-header>
              <mat-card-title>Vlasnički nalog</mat-card-title>
              <mat-card-subtitle>
                Jedini nalog koji platforma pravi. Konobare i menadžere dalje dodaje vlasnik iz kase.
              </mat-card-subtitle>
            </mat-card-header>
            <mat-card-content>
              @if (venue.staffCount > 0) {
                <p>
                  <mat-icon class="det__ok" inline>check_circle</mat-icon>
                  Restoran ima {{ venue.staffCount }} nalog(a).
                </p>
              } @else {
                <div class="det__owner">
                  <mat-form-field appearance="outline">
                    <mat-label>Ime</mat-label>
                    <input matInput [(ngModel)]="ownerFirst" />
                  </mat-form-field>
                  <mat-form-field appearance="outline">
                    <mat-label>Prezime</mat-label>
                    <input matInput [(ngModel)]="ownerLast" />
                  </mat-form-field>
                  <mat-form-field appearance="outline">
                    <mat-label>Email</mat-label>
                    <input matInput type="email" [(ngModel)]="ownerEmail" />
                  </mat-form-field>
                  <mat-form-field appearance="outline">
                    <mat-label>Lozinka</mat-label>
                    <input matInput [(ngModel)]="ownerPassword" />
                  </mat-form-field>
                  <button
                    mat-flat-button
                    [disabled]="!ownerEmail.trim() || ownerPassword.length < 8"
                    (click)="createOwner()"
                  >
                    Napravi vlasnika
                  </button>
                </div>
              }
            </mat-card-content>
          </mat-card>
        </div>

        @if (license(); as current) {
          <mat-card class="det__payments">
            <mat-card-header>
              <mat-card-title>Uplate</mat-card-title>
            </mat-card-header>
            <mat-card-content>
              <div class="det__payment-form">
                <mat-form-field appearance="outline">
                  <mat-label>Iznos</mat-label>
                  <input matInput type="number" min="0" [(ngModel)]="paymentAmount" />
                  <span matTextSuffix>RSD</span>
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Način</mat-label>
                  <mat-select [(ngModel)]="paymentMethod">
                    @for (option of methods; track option) {
                      <mat-option [value]="option">{{ methodLabel(option) }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>

                <mat-form-field appearance="outline">
                  <mat-label>Poziv na broj</mat-label>
                  <input matInput [(ngModel)]="paymentReference" />
                </mat-form-field>

                <button mat-flat-button [disabled]="paymentAmount <= 0" (click)="recordPayment(current)">
                  Evidentiraj
                </button>
              </div>

              <table mat-table [dataSource]="payments()">
                <ng-container matColumnDef="date">
                  <th mat-header-cell *matHeaderCellDef>Datum</th>
                  <td mat-cell *matCellDef="let row">{{ row.paidAtUtc | date: 'dd.MM.yyyy.' }}</td>
                </ng-container>

                <ng-container matColumnDef="amount">
                  <th mat-header-cell *matHeaderCellDef class="dr-numeric">Iznos</th>
                  <td mat-cell *matCellDef="let row" class="dr-numeric">
                    {{ row.amount | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}
                  </td>
                </ng-container>

                <ng-container matColumnDef="method">
                  <th mat-header-cell *matHeaderCellDef>Način</th>
                  <td mat-cell *matCellDef="let row">{{ methodLabel(row.paymentMethod) }}</td>
                </ng-container>

                <ng-container matColumnDef="reference">
                  <th mat-header-cell *matHeaderCellDef>Poziv na broj</th>
                  <td mat-cell *matCellDef="let row" class="dr-muted">{{ row.referenceNumber }}</td>
                </ng-container>

                <tr mat-header-row *matHeaderRowDef="paymentColumns"></tr>
                <tr mat-row *matRowDef="let row; columns: paymentColumns"></tr>
              </table>

              @if (payments().length === 0) {
                <p class="dr-empty">Nema evidentiranih uplata.</p>
              }
            </mat-card-content>
          </mat-card>
        }
      }
    </div>
  `,
  styles: `
    .det__header {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-bottom: 16px;
    }

    h1 {
      margin: 0;
      font-size: 1.5rem;
    }

    .det__grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: var(--dr-gap);
      align-items: start;
    }

    .det__edit {
      margin-bottom: var(--dr-gap);
    }

    .det__edit form {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 8px 16px;
    }

    .det__edit-actions {
      grid-column: 1 / -1;
      display: flex;
      gap: 8px;
    }

    dl {
      display: grid;
      grid-template-columns: auto 1fr;
      gap: 4px 16px;
      margin: 12px 0;
    }

    dt {
      color: var(--mat-sys-on-surface-variant);
    }

    dd {
      margin: 0;
      font-weight: 500;
    }

    .det__unpaid {
      color: var(--dr-expiring);
    }

    .det__license-actions,
    .det__payment-form {
      display: flex;
      gap: 8px;
      align-items: flex-start;
      flex-wrap: wrap;
    }

    .det__license-actions mat-form-field,
    .det__payment-form mat-form-field {
      width: 150px;
    }

    .det__license-state {
      display: flex;
      gap: 8px;
      margin-top: 8px;
    }

    .det__note {
      margin: 4px 0 8px;
      font-size: 0.85rem;
    }

    .det__owner {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 8px;
    }

    .det__owner button {
      grid-column: 1 / -1;
    }

    .det__ok {
      color: var(--dr-valid);
      vertical-align: middle;
    }

    .det__payments {
      margin-top: var(--dr-gap);
    }

    table {
      width: 100%;
      margin-top: 12px;
    }

    @media (max-width: 1000px) {
      .det__grid {
        grid-template-columns: 1fr;
      }
    }
  `,
})
export class RestaurantDetailPage {
  readonly id = input.required<string>();

  private readonly api = inject(PlatformApiService);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly LicenseStatus = LicenseStatus;
  protected readonly plans = [
    LicensePlan.Monthly,
    LicensePlan.Quarterly,
    LicensePlan.SemiAnnual,
    LicensePlan.Annual,
  ];
  protected readonly methods = [PaymentMethod.Cash, PaymentMethod.Card, PaymentMethod.DigitalWallet];
  protected readonly paymentColumns = ['date', 'amount', 'method', 'reference'];

  private readonly formBuilder = inject(FormBuilder);

  protected readonly editing = signal(false);

  /** Only what the API accepts on `PUT`: the slug is fixed once the venue exists. */
  protected readonly form = this.formBuilder.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    address: [''],
    contactEmail: ['', Validators.email],
    phoneNumber: [''],
    currencyCode: ['', [Validators.required, Validators.pattern(/^[A-Za-z]{3}$/)]],
    timeZoneId: ['', Validators.required],
  });

  protected readonly restaurant = signal<RestaurantSummaryDto | null>(null);
  protected readonly licenses = signal<LicenseDto[]>([]);
  protected readonly payments = signal<LicensePaymentDto[]>([]);

  /** The licence that governs is the one running latest — the same one the till enforces. */
  protected readonly license = computed<LicenseDto | null>(() => this.licenses()[0] ?? null);

  protected plan = LicensePlan.Monthly;
  protected price = 15000;

  protected ownerFirst = '';
  protected ownerLast = '';
  protected ownerEmail = '';
  protected ownerPassword = '';

  protected paymentAmount = 0;
  protected paymentMethod = PaymentMethod.Card;
  protected paymentReference = '';

  constructor() {
    queueMicrotask(() => this.load());
  }

  protected planLabel(plan: LicensePlan): string {
    return licensePlanLabels[plan];
  }

  protected statusLabel(status: LicenseStatus): string {
    return licenseStatusLabels[status];
  }

  protected methodLabel(method: PaymentMethod): string {
    return paymentMethodLabels[method];
  }

  protected toggleEdit(venue: RestaurantSummaryDto): void {
    if (this.editing()) {
      this.editing.set(false);
      return;
    }

    // Filled from what is on screen rather than re-fetched, so the form always opens on the values
    // the administrator is looking at.
    this.form.setValue({
      name: venue.name,
      address: venue.address ?? '',
      contactEmail: venue.contactEmail ?? '',
      phoneNumber: venue.phoneNumber ?? '',
      currencyCode: venue.currencyCode,
      timeZoneId: venue.timeZoneId,
    });

    this.editing.set(true);
  }

  protected save(): void {
    if (this.form.invalid) {
      return;
    }

    const value = this.form.getRawValue();

    this.api
      .updateRestaurant(this.id(), {
        name: value.name.trim(),
        // Empty means "no value" here, not "leave as is" — the API clears these when they are null.
        address: value.address.trim() || null,
        contactEmail: value.contactEmail.trim() || null,
        phoneNumber: value.phoneNumber.trim() || null,
        currencyCode: value.currencyCode.trim(),
        timeZoneId: value.timeZoneId.trim(),
      })
      .subscribe((venue) => {
        this.restaurant.set(venue);
        this.editing.set(false);
        this.snackBar.open('Podaci su sačuvani.', 'U redu', { duration: 4000 });
      });
  }

  protected setActive(active: boolean): void {
    this.api.setRestaurantActive(this.id(), active).subscribe((venue) => {
      this.restaurant.set(venue);
      this.snackBar.open(active ? 'Restoran je uključen.' : 'Restoran je ugašen.', 'U redu', {
        duration: 4000,
      });
    });
  }

  protected issue(): void {
    this.api
      .issueLicense({ restaurantId: this.id(), plan: this.plan, price: this.price })
      .subscribe(() => {
        this.snackBar.open('Licenca je izdata. Kasa radi odmah.', 'U redu', { duration: 5000 });
        this.load();
      });
  }

  protected renew(license: LicenseDto): void {
    this.api.renewLicense(license.id, { plan: this.plan, price: this.price }).subscribe((renewed) => {
      this.snackBar.open(
        `Produženo do ${new Date(renewed.expiresAtUtc).toLocaleDateString('sr-RS')}.`,
        'U redu',
        { duration: 5000 },
      );
      this.load();
    });
  }

  protected suspend(license: LicenseDto): void {
    const reason = prompt('Razlog suspenzije');

    if (!reason?.trim()) {
      return;
    }

    this.api.suspendLicense(license.id, reason.trim()).subscribe(() => {
      this.snackBar.open('Licenca je suspendovana. Kasa staje odmah.', 'U redu', { duration: 5000 });
      this.load();
    });
  }

  protected reactivate(license: LicenseDto): void {
    this.api.reactivateLicense(license.id).subscribe(() => this.load());
  }

  protected cancel(license: LicenseDto): void {
    const reason = prompt('Razlog otkazivanja');

    if (!reason?.trim()) {
      return;
    }

    this.api.cancelLicense(license.id, reason.trim()).subscribe(() => this.load());
  }

  protected createOwner(): void {
    this.api
      .createOwner(this.id(), {
        email: this.ownerEmail.trim(),
        password: this.ownerPassword,
        firstName: this.ownerFirst.trim(),
        lastName: this.ownerLast.trim(),
      })
      .subscribe((owner) => {
        // Shown at length: the administrator has to pass the sign-in on, and the user name is not
        // simply the email.
        this.snackBar.open(`Vlasnik napravljen. Prijava: ${owner.userName}`, 'U redu', {
          duration: 15000,
        });
        this.ownerPassword = '';
        this.load();
      });
  }

  protected recordPayment(license: LicenseDto): void {
    this.api
      .recordPayment(license.id, {
        amount: this.paymentAmount,
        paymentMethod: this.paymentMethod,
        referenceNumber: this.paymentReference.trim() || null,
      })
      .subscribe(() => {
        this.paymentAmount = 0;
        this.paymentReference = '';
        this.load();
      });
  }

  private load(): void {
    this.api.restaurant(this.id()).subscribe((venue) => {
      this.restaurant.set(venue);
      this.price = venue.plan ? this.price : 15000;
    });

    this.api.licenses(this.id()).subscribe((rows) => {
      this.licenses.set(rows);

      const current = rows[0];

      if (current) {
        this.plan = current.plan;
        this.price = current.price;
        this.api.licensePayments(current.id).subscribe((paid) => this.payments.set(paid));
      } else {
        this.payments.set([]);
      }
    });
  }
}
