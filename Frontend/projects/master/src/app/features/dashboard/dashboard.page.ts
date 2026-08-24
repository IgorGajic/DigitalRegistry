import { CurrencyPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { MonthlyRevenueDto, PlatformApiService, PlatformDashboardDto } from 'shared';

/**
 * The platform at a glance.
 *
 * The revenue chart is drawn as plain bars rather than pulled in from a charting library: it is one
 * series of twelve values, and a dependency the size of a chart library would be most of the bundle.
 */
@Component({
  selector: 'mstr-dashboard',
  imports: [CurrencyPipe, RouterLink, MatButtonModule, MatCardModule, MatIconModule, MatTooltipModule],
  template: `
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
            <mat-card-header>
              <mat-card-title>Prihod po mesecima</mat-card-title>
            </mat-card-header>
            <mat-card-content>
              @if (d.monthlyLicenseRevenue.length) {
                <div class="dash__chart">
                  @for (month of d.monthlyLicenseRevenue; track month.year + '-' + month.month) {
                    <div class="dash__bar-wrap">
                      <div
                        class="dash__bar"
                        [style.height.%]="height(month)"
                        [matTooltip]="tooltip(month)"
                      ></div>
                      <span class="dash__bar-label">{{ month.month }}.</span>
                    </div>
                  }
                </div>
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
                        {{ restaurant.daysRemaining }} dana
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

    .dash__chart {
      display: flex;
      align-items: flex-end;
      gap: 6px;
      height: 180px;
      padding-top: 8px;
    }

    .dash__bar-wrap {
      flex: 1;
      display: flex;
      flex-direction: column;
      justify-content: flex-end;
      align-items: center;
      height: 100%;
      gap: 4px;
    }

    .dash__bar {
      width: 100%;
      min-height: 2px;
      background: var(--mat-sys-primary);
      border-radius: 4px 4px 0 0;
    }

    .dash__bar-label {
      font-size: 0.7rem;
      color: var(--mat-sys-on-surface-variant);
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

  protected readonly data = signal<PlatformDashboardDto | null>(null);

  private readonly peak = computed(() =>
    Math.max(1, ...(this.data()?.monthlyLicenseRevenue ?? []).map((month) => month.amount)),
  );

  constructor() {
    this.api.dashboard().subscribe((dashboard) => this.data.set(dashboard));
  }

  protected height(month: MonthlyRevenueDto): number {
    return (month.amount / this.peak()) * 100;
  }

  protected tooltip(month: MonthlyRevenueDto): string {
    return `${month.month}/${month.year}: ${month.amount.toLocaleString('sr-RS')} RSD (${month.paymentCount} uplata)`;
  }
}
