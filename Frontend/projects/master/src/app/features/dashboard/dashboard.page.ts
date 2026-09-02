import { CurrencyPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { RouterLink } from '@angular/router';
import {
  LoadingState,
  PlatformApiService,
  PlatformDashboardDto,
  daysLabel,
} from 'shared';
import {
  BarChart,
  BarPoint,
} from 'shared/ui';

/**
 * The platform at a glance.
 *
 * The revenue chart is the shared {@link BarChart}, the same component the till draws its takings
 * with. It used to be a row of divs whose height was a percentage of the largest month — the same
 * question answered a second time, and differently: no zero line, no scale, and a tooltip that
 * behaved unlike the other one. Two charts in a product that should have one is a thing a reader
 * has to learn twice.
 *
 * Still no charting library. One series of twelve values does not justify a dependency that would
 * be most of the bundle; it justifies one component, written once and used in both hosts.
 */
@Component({
  selector: 'mstr-dashboard',
  imports: [
    CurrencyPipe,
    RouterLink,
    MatCardModule,
    MatIconModule,
    MatProgressBarModule,
    BarChart,
  ],
  template: `
    @if (loading.active()) {
      <mat-progress-bar mode="indeterminate" />
    }

    <div class="dr-page">
      <h1>Pregled</h1>

      @if (data(); as d) {
        <div class="dash__tiles">
          <mat-card>
            <mat-card-content>
              <span class="dr-muted">Restorana</span>
              <strong>{{ d.totalRestaurants }}</strong>
              <span class="dr-muted">{{ d.activeRestaurants }} aktivnih</span>
            </mat-card-content>
          </mat-card>

          <mat-card>
            <mat-card-content>
              <span class="dr-muted">Aktivne licence</span>
              <strong class="dash__ok">{{ d.activeLicenses }}</strong>
            </mat-card-content>
          </mat-card>

          <mat-card [class.dash__alert]="d.expiredLicenses > 0">
            <mat-card-content>
              <span class="dr-muted">Istekle</span>
              <strong class="dash__bad">{{ d.expiredLicenses }}</strong>
            </mat-card-content>
          </mat-card>

          <mat-card>
            <mat-card-content>
              <span class="dr-muted">Suspendovane</span>
              <strong class="dash__suspended">{{ d.suspendedLicenses }}</strong>
            </mat-card-content>
          </mat-card>

          <mat-card [class.dash__warn]="d.expiringSoon > 0">
            <mat-card-content>
              <span class="dr-muted">Ističu u 30 dana</span>
              <strong class="dash__warn-text">{{ d.expiringSoon }}</strong>
            </mat-card-content>
          </mat-card>

          <mat-card>
            <mat-card-content>
              <span class="dr-muted">Prihod od licenci</span>
              <strong>
                {{ d.totalLicenseRevenue | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}
              </strong>
            </mat-card-content>
          </mat-card>
        </div>

        <div class="dash__panels">
          <mat-card>
            <mat-card-content>
              @if (d.monthlyLicenseRevenue.length) {
                <dr-bar-chart
                  [points]="months()"
                  eyebrow="Prihod po mesecima"
                  peakLabel="najjači mesec"
                  [formatValue]="money"
                  summary="Prihod od licenci po mesecima."
                />
              } @else {
                <p class="dr-empty">Nema evidentiranih uplata.</p>
              }
            </mat-card-content>
          </mat-card>

          <mat-card>
            <mat-card-header>
              <mat-card-title>Uskoro ističu</mat-card-title>
              <mat-card-subtitle>Restorani kojima licenca ističe u narednih 30 dana</mat-card-subtitle>
            </mat-card-header>
            <mat-card-content>
              @if (d.expiringRestaurants.length) {
                <ul class="dash__expiring">
                  @for (restaurant of d.expiringRestaurants; track restaurant.id) {
                    <li>
                      <a [routerLink]="['/restorani', restaurant.id]">{{ restaurant.name }}</a>
                      <span class="dr-muted">{{ restaurant.slug }}</span>
                      <strong [class.dash__warn-text]="restaurant.daysRemaining <= 7">
                        {{ restaurant.daysRemaining }} {{ daysLabel(restaurant.daysRemaining) }}
                      </strong>
                    </li>
                  }
                </ul>
              } @else {
                <p class="dr-empty">Nijedna licenca ne ističe uskoro.</p>
              }
            </mat-card-content>
          </mat-card>
        </div>
      } @else if (!loading.active()) {
        <p class="dr-empty">Pregled trenutno nije dostupan. Osvežite stranicu.</p>
      }
    </div>
  `,
  styles: `
    h1 {
      margin: 0 0 16px;
      font-size: 1.5rem;
    }

    .dash__tiles {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
      gap: 12px;
      margin-bottom: 16px;
    }

    .dash__tiles mat-card-content {
      display: flex;
      flex-direction: column;
      gap: 2px;
      padding: 12px 16px;
    }

    .dash__tiles strong {
      font-size: 1.6rem;
      font-family: var(--dr-font-mono);
      font-variant-numeric: tabular-nums;
    }

    .dash__ok {
      color: var(--dr-valid);
    }

    .dash__bad {
      color: var(--dr-expired);
    }

    .dash__suspended {
      color: var(--dr-suspended);
    }

    .dash__warn-text {
      color: var(--dr-expiring);
    }

    .dash__alert {
      border: 1px solid var(--dr-expired);
    }

    .dash__warn {
      border: 1px solid var(--dr-expiring);
    }

    .dash__panels {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: var(--dr-gap);
      align-items: start;
    }

    .dash__expiring {
      list-style: none;
      margin: 0;
      padding: 0;
    }

    .dash__expiring li {
      display: grid;
      grid-template-columns: 1fr auto auto;
      gap: 12px;
      align-items: baseline;
      padding: 8px 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }

    @media (max-width: 1000px) {
      .dash__panels {
        grid-template-columns: 1fr;
      }
    }
  `,
})
export class DashboardPage {
  private readonly api = inject(PlatformApiService);

  protected readonly loading = new LoadingState();
  protected readonly daysLabel = daysLabel;
  protected readonly data = signal<PlatformDashboardDto | null>(null);

  constructor() {
    this.loading.track(this.api.dashboard()).subscribe((dashboard) => this.data.set(dashboard));
  }

  /**
   * The months, as the shared chart wants them.
   *
   * These were a row of divs whose height was a percentage of the largest month — the same question
   * the till's takings chart answers, answered differently, with no zero line and no scale. The API
   * fills in the empty months of the requested window, because a month with no payments is a fact
   * and not an absence of one, and the chart draws those as the hairline it draws any zero.
   */
  protected readonly months = computed<BarPoint[]>(() =>
    (this.data()?.monthlyLicenseRevenue ?? []).map((month) => ({
      label: `${month.month}.`,
      value: month.amount,
      title: `${monthNames[month.month - 1]} ${month.year}.`,
      notes: [`${month.paymentCount} ${paymentsLabel(month.paymentCount)}`],
    })),
  );

  /** Passed into the chart, so it is an arrow rather than a method: it travels without `this`. */
  protected readonly money = (value: number): string =>
    `${Math.round(value).toLocaleString('sr-Latn-RS')} RSD`;
}

const monthNames = [
  'Januar', 'Februar', 'Mart', 'April', 'Maj', 'Jun',
  'Jul', 'Avgust', 'Septembar', 'Oktobar', 'Novembar', 'Decembar',
];

/** "uplata" / "uplate" / "uplata" — feminine, so the plural returns to the singular's stem. */
function paymentsLabel(count: number): string {
  const last = Math.abs(count) % 10;
  const lastTwo = Math.abs(count) % 100;

  if (last === 1 && lastTwo !== 11) {
    return 'uplata';
  }

  if (last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14)) {
    return 'uplate';
  }

  return 'uplata';
}
