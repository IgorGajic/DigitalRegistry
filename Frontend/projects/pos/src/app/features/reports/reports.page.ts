import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  LoadingState,
  TillApiService,
  TopSellingItemDto,
  TurnoverReportDto,
  VoidReportDto,
  WaiterPerformanceReportDto,
  addDays,
  endOfDayUtc,
  saveBlobResponse,
  startOfDayUtc,
  toDateOnly,
  voidTypeLabels,
} from 'shared';

import { TurnoverChart } from './turnover-chart';

/**
 * What the owner reads.
 *
 * Every figure here is netted of cancellations: turnover has reversals deducted, and the top-selling
 * list counts only what was actually paid for. That is deliberate — the alternative flatters, and a
 * report that flatters is worse than none.
 */
@Component({
  selector: 'pos-reports',
  providers: [provideNativeDateAdapter()],
  imports: [
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatProgressBarModule,
    MatTableModule,
    MatTabsModule,
    MatTooltipModule,
    TurnoverChart,
  ],
  template: `
    @if (loading.active()) {
      <mat-progress-bar mode="indeterminate" />
    }

    <div class="dr-page">
      <header class="rep__header">
        <h1>Izveštaji</h1>
        <span class="dr-toolbar-spacer"></span>

        <mat-form-field appearance="outline" class="rep__date">
          <mat-label>Od</mat-label>
          <input matInput [matDatepicker]="fromPicker" [(ngModel)]="from" (dateChange)="load()" />
          <mat-datepicker-toggle matIconSuffix [for]="fromPicker" />
          <mat-datepicker #fromPicker />
        </mat-form-field>

        <mat-form-field appearance="outline" class="rep__date">
          <mat-label>Do</mat-label>
          <input matInput [matDatepicker]="toPicker" [(ngModel)]="to" (dateChange)="load()" />
          <mat-datepicker-toggle matIconSuffix [for]="toPicker" />
          <mat-datepicker #toPicker />
        </mat-form-field>

        <button mat-button (click)="setRange(0)">Danas</button>
        <button mat-button (click)="setRange(6)">7 dana</button>
        <button mat-button (click)="setRange(29)">30 dana</button>
      </header>

      @if (turnover(); as report) {
        <div class="rep__summary">
          <mat-card>
            <mat-card-content>
              <span class="dr-muted">Promet</span>
              <strong>{{ report.turnover | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}</strong>
            </mat-card-content>
          </mat-card>
          <mat-card>
            <mat-card-content>
              <span class="dr-muted">Računa</span>
              <strong>{{ report.billCount }}</strong>
            </mat-card-content>
          </mat-card>
          <mat-card>
            <mat-card-content>
              <span class="dr-muted">Prosečan račun</span>
              <strong>{{ report.averageBill | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}</strong>
            </mat-card-content>
          </mat-card>
          <mat-card>
            <mat-card-content>
              <span class="dr-muted">Gotovina / kartica</span>
              <strong>
                {{ report.cash | number: '1.0-0' }} / {{ report.card | number: '1.0-0' }}
              </strong>
            </mat-card-content>
          </mat-card>
        </div>
      }

      @if (turnover(); as report) {
        @if (report.days.length > 1) {
          <mat-card class="rep__chart">
            <pos-turnover-chart
              [days]="report.days"
              [cash]="report.cash"
              [card]="report.card"
              [wallet]="report.digitalWallet"
            />
          </mat-card>
        }
      }

      <mat-tab-group animationDuration="120ms">
        <mat-tab label="Pazar po danima">
          <mat-card class="rep__panel">
            <table mat-table [dataSource]="days()">
              <ng-container matColumnDef="date">
                <th mat-header-cell *matHeaderCellDef>Dan</th>
                <td mat-cell *matCellDef="let row">{{ row.date | date: 'EEE dd.MM.' }}</td>
              </ng-container>

              <ng-container matColumnDef="turnover">
                <th mat-header-cell *matHeaderCellDef class="dr-numeric">Promet</th>
                <td mat-cell *matCellDef="let row" class="dr-numeric">
                  {{ row.turnover | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}
                </td>
              </ng-container>

              <ng-container matColumnDef="cash">
                <th mat-header-cell *matHeaderCellDef class="dr-numeric">Gotovina</th>
                <td mat-cell *matCellDef="let row" class="dr-numeric">
                  {{ row.cash | number: '1.0-0' }}
                </td>
              </ng-container>

              <ng-container matColumnDef="card">
                <th mat-header-cell *matHeaderCellDef class="dr-numeric">Kartica</th>
                <td mat-cell *matCellDef="let row" class="dr-numeric">
                  {{ row.card | number: '1.0-0' }}
                </td>
              </ng-container>

              <ng-container matColumnDef="bills">
                <th mat-header-cell *matHeaderCellDef class="dr-numeric">Računa</th>
                <td mat-cell *matCellDef="let row" class="dr-numeric">{{ row.billCount }}</td>
              </ng-container>

              <ng-container matColumnDef="average">
                <th mat-header-cell *matHeaderCellDef class="dr-numeric">Prosek</th>
                <td mat-cell *matCellDef="let row" class="dr-numeric">
                  {{ row.averageBill | number: '1.0-0' }}
                </td>
              </ng-container>

              <ng-container matColumnDef="reversed">
                <th mat-header-cell *matHeaderCellDef class="dr-numeric">Stornirano</th>
                <td mat-cell *matCellDef="let row" class="dr-numeric">
                  @if (row.reversalCount) {
                    <span
                      class="rep__reversed"
                      [matTooltip]="row.reversalCount + ' storniranih računa — već odbijeno od prometa'"
                    >
                      −{{ row.reversedAmount | number: '1.0-0' }}
                    </span>
                  } @else {
                    <span class="dr-muted">—</span>
                  }
                </td>
              </ng-container>

              <tr mat-header-row *matHeaderRowDef="turnoverColumns"></tr>
              <tr mat-row *matRowDef="let row; columns: turnoverColumns"></tr>
            </table>
          </mat-card>
        </mat-tab>

        <mat-tab label="Najprodavanije">
          <mat-card class="rep__panel">
            <table mat-table [dataSource]="topItems()">
              <ng-container matColumnDef="name">
                <th mat-header-cell *matHeaderCellDef>Artikal</th>
                <td mat-cell *matCellDef="let row">{{ row.name }}</td>
              </ng-container>

              <ng-container matColumnDef="category">
                <th mat-header-cell *matHeaderCellDef>Kategorija</th>
                <td mat-cell *matCellDef="let row">{{ row.category }}</td>
              </ng-container>

              <ng-container matColumnDef="quantity">
                <th mat-header-cell *matHeaderCellDef class="dr-numeric">Prodato</th>
                <td mat-cell *matCellDef="let row" class="dr-numeric">{{ row.quantitySold }}</td>
              </ng-container>

              <ng-container matColumnDef="revenue">
                <th mat-header-cell *matHeaderCellDef class="dr-numeric">Prihod</th>
                <td mat-cell *matCellDef="let row" class="dr-numeric">
                  {{ row.revenue | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}
                </td>
              </ng-container>

              <ng-container matColumnDef="cost">
                <th mat-header-cell *matHeaderCellDef class="dr-numeric">Nabavna</th>
                <td mat-cell *matCellDef="let row" class="dr-numeric">
                  @if (row.estimatedCost !== null) {
                    {{ row.estimatedCost | number: '1.0-0' }}
                  } @else {
                    <span class="dr-muted" matTooltip="Nema normativa ili nabavne cene">—</span>
                  }
                </td>
              </ng-container>

              <ng-container matColumnDef="margin">
                <th mat-header-cell *matHeaderCellDef class="dr-numeric">Marža</th>
                <td mat-cell *matCellDef="let row" class="dr-numeric">
                  @if (row.estimatedMargin !== null) {
                    {{ row.estimatedMargin | number: '1.0-0' }}
                  } @else {
                    <span class="dr-muted">—</span>
                  }
                </td>
              </ng-container>

              <tr mat-header-row *matHeaderRowDef="topColumns"></tr>
              <tr mat-row *matRowDef="let row; columns: topColumns"></tr>
            </table>

            @if (topItems().length === 0) {
              <p class="dr-empty">Nema prodaje u izabranom periodu.</p>
            }
          </mat-card>
        </mat-tab>

        <mat-tab label="Konobari">
          <mat-card class="rep__panel">
            <mat-card-header>
              <mat-card-title>Učinak po konobaru</mat-card-title>
              <mat-card-subtitle>
                Runde se pripisuju onome ko ih je izneo, a ako toga nema — onome ko ih je primio.
              </mat-card-subtitle>
              <span class="dr-toolbar-spacer"></span>
              <button
                mat-stroked-button
                class="rep__export"
                [disabled]="exporting() || waiterRows().length === 0"
                (click)="exportWaiters()"
              >
                <mat-icon>download</mat-icon>
                Izvezi u Excel
              </button>
            </mat-card-header>

            <mat-card-content>
              <table mat-table [dataSource]="waiterRows()">
                <ng-container matColumnDef="name">
                  <th mat-header-cell *matHeaderCellDef>Konobar</th>
                  <td mat-cell *matCellDef="let row">{{ row.name }}</td>
                </ng-container>

                <ng-container matColumnDef="orders">
                  <th mat-header-cell *matHeaderCellDef class="dr-numeric">Porudžbina</th>
                  <td mat-cell *matCellDef="let row" class="dr-numeric">{{ row.orderCount }}</td>
                </ng-container>

                <ng-container matColumnDef="value">
                  <th mat-header-cell *matHeaderCellDef class="dr-numeric">Vrednost</th>
                  <td mat-cell *matCellDef="let row" class="dr-numeric">
                    {{ row.totalValue | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}
                  </td>
                </ng-container>

                <ng-container matColumnDef="service">
                  <th mat-header-cell *matHeaderCellDef class="dr-numeric">Prosečno čekanje</th>
                  <td mat-cell *matCellDef="let row" class="dr-numeric">
                    @if (row.averageServiceMinutes !== null) {
                      <span
                        [matTooltip]="
                          'Mereno na ' + row.timedOrderCount + ' rundi poručenih preko QR koda'
                        "
                      >
                        {{ row.averageServiceMinutes | number: '1.1-1' }} min
                      </span>
                    } @else {
                      <span class="dr-muted" matTooltip="Nijedna runda ovog konobara nije merena">
                        —
                      </span>
                    }
                  </td>
                </ng-container>

                <ng-container matColumnDef="hours">
                  <th mat-header-cell *matHeaderCellDef class="dr-numeric">Sati rada</th>
                  <td mat-cell *matCellDef="let row" class="dr-numeric">
                    {{ row.hoursWorked | number: '1.0-2' }}
                  </td>
                </ng-container>

                <ng-container matColumnDef="perHour">
                  <th mat-header-cell *matHeaderCellDef class="dr-numeric">Po satu</th>
                  <td mat-cell *matCellDef="let row" class="dr-numeric">
                    @if (row.hoursWorked > 0) {
                      {{ row.valuePerHour | number: '1.0-0' }}
                    } @else {
                      <span class="dr-muted">—</span>
                    }
                  </td>
                </ng-container>

                <tr mat-header-row *matHeaderRowDef="waiterColumns"></tr>
                <tr mat-row *matRowDef="let row; columns: waiterColumns"></tr>
              </table>

              @if (waiterRows().length === 0) {
                <p class="dr-empty">Nema porudžbina ni smena u izabranom periodu.</p>
              }

              <p class="rep__note dr-muted">
                Sati rada su sati iz rasporeda smena, a ne evidencija dolaska — kasa nema
                čitač radnog vremena. Vreme usluge se meri samo za runde poručene preko QR koda,
                od porudžbine do trenutka kada je konobar potvrdio da ju je izneo.
              </p>
            </mat-card-content>
          </mat-card>
        </mat-tab>

        <mat-tab label="Storno">
          @if (voids(); as report) {
            <mat-card class="rep__panel">
              <mat-card-header>
                <mat-card-title>
                  {{ report.totalVoids }} storna,
                  {{ report.totalAmount | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}
                </mat-card-title>
                <mat-card-subtitle>
                  Ko koliko stornira. Konobar koji odudara od kolega vidi se samo ovde.
                </mat-card-subtitle>
              </mat-card-header>

              <mat-card-content>
                <table mat-table [dataSource]="report.byStaff" class="rep__staff">
                  <ng-container matColumnDef="name">
                    <th mat-header-cell *matHeaderCellDef>Zaposleni</th>
                    <td mat-cell *matCellDef="let row">{{ row.name }}</td>
                  </ng-container>

                  <ng-container matColumnDef="count">
                    <th mat-header-cell *matHeaderCellDef class="dr-numeric">Storna</th>
                    <td mat-cell *matCellDef="let row" class="dr-numeric">{{ row.voidCount }}</td>
                  </ng-container>

                  <ng-container matColumnDef="amount">
                    <th mat-header-cell *matHeaderCellDef class="dr-numeric">Iznos</th>
                    <td mat-cell *matCellDef="let row" class="dr-numeric">
                      {{ row.totalAmount | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}
                    </td>
                  </ng-container>

                  <ng-container matColumnDef="breakdown">
                    <th mat-header-cell *matHeaderCellDef>Po tipu</th>
                    <td mat-cell *matCellDef="let row" class="dr-muted">
                      stavke {{ row.itemVoids }}, otvoreni {{ row.openOrderVoids }}, plaćeni
                      {{ row.paidOrderVoids }}
                    </td>
                  </ng-container>

                  <tr mat-header-row *matHeaderRowDef="staffColumns"></tr>
                  <tr mat-row *matRowDef="let row; columns: staffColumns"></tr>
                </table>

                <h3>Pojedinačno</h3>
                <ul class="rep__voids">
                  @for (entry of report.entries; track entry.id) {
                    <li>
                      <span class="dr-muted">{{ entry.voidedAtUtc | date: 'dd.MM. HH:mm' }}</span>
                      <span>
                        @if (entry.tableNumber) {
                          sto {{ entry.tableNumber }},
                        }
                        {{ voidTypeLabels[entry.type] }}
                        @if (entry.itemName) {
                          — {{ entry.itemName }} ×{{ entry.quantity }}
                        }
                      </span>
                      <strong class="dr-numeric">
                        {{ entry.amount | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}
                      </strong>
                      <em>„{{ entry.reason }}“</em>
                      <span class="dr-muted">{{ entry.performedBy }}</span>
                    </li>
                  }
                </ul>

                @if (report.entries.length === 0) {
                  <p class="dr-empty">Nema storna u izabranom periodu.</p>
                }
              </mat-card-content>
            </mat-card>
          }
        </mat-tab>
      </mat-tab-group>
    </div>
  `,
  styles: `
    @use 'responsive-table' as rt;

    .rep__header {
      display: flex;
      align-items: center;
      gap: 8px;
      flex-wrap: wrap;
      margin-bottom: 12px;
    }

    h1 {
      margin: 0;
      font-size: 1.5rem;
    }

    .rep__date {
      width: 160px;
    }

    .rep__summary {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: 12px;
      margin-bottom: 16px;
    }

    .rep__summary mat-card-content {
      display: flex;
      flex-direction: column;
      gap: 2px;
      padding: 12px 16px;
    }

    .rep__summary strong {
      font-size: 1.4rem;
      font-family: var(--dr-font-mono);
      font-variant-numeric: tabular-nums;
    }

    .rep__panel {
      margin-top: 12px;
    }

    .rep__chart {
      margin-bottom: 16px;
    }

    table {
      width: 100%;
    }

    .rep__staff {
      margin-bottom: 20px;
    }

    .rep__reversed {
      color: var(--dr-occupied);
    }

    /* The card header lays its title block out as a grid; the button has to be told to sit on the
       same row rather than under it. */
    .rep__export {
      align-self: center;
      white-space: nowrap;
    }

    .rep__note {
      margin: 16px 0 0;
      font-size: 0.8rem;
      max-width: 70ch;
    }

    h3 {
      margin: 8px 0;
      font-size: 1rem;
    }

    .rep__voids {
      list-style: none;
      margin: 0;
      padding: 0;
    }

    .rep__voids li {
      display: grid;
      grid-template-columns: 110px 1fr 100px 1.4fr 130px;
      gap: 12px;
      align-items: baseline;
      padding: 6px 0;
      border-bottom: 1px solid var(--mat-sys-outline-variant);
      font-size: 0.875rem;
    }

    /* All three tables on this screen share a stack; the labels are the union of their columns,
       and a column absent from a given table simply never matches. */
    @include rt.labels((
      date: 'Dan',
      turnover: 'Promet',
      cash: 'Gotovina',
      card: 'Kartica',
      bills: 'Računa',
      average: 'Prosek',
      reversed: 'Stornirano',
      name: 'Artikal',
      category: 'Kategorija',
      quantity: 'Prodato',
      revenue: 'Prihod',
      cost: 'Nabavna',
      margin: 'Marža',
      count: 'Storna',
      amount: 'Iznos',
      breakdown: 'Po tipu',
      orders: 'Porudžbina',
      value: 'Vrednost',
      service: 'Prosečno čekanje',
      hours: 'Sati rada',
      perHour: 'Po satu',
    ));

    @media (max-width: 900px) {
      .rep__date {
        width: 100%;
      }

      /* The individual voids are a five-column grid of their own, not a mat-table. */
      .rep__voids li {
        grid-template-columns: 1fr;
        gap: 2px;
        padding: 10px 0;
      }
    }
  `,
})
export class ReportsPage {
  private readonly api = inject(TillApiService);
  private readonly snackBar = inject(MatSnackBar);

  /** Three calls go out together here, so the bar has to outlast the first one to come back. */
  protected readonly loading = new LoadingState();

  protected readonly voidTypeLabels = voidTypeLabels;
  protected readonly turnoverColumns = [
    'date',
    'turnover',
    'cash',
    'card',
    'bills',
    'average',
    'reversed',
  ];
  protected readonly topColumns = ['name', 'category', 'quantity', 'revenue', 'cost', 'margin'];
  protected readonly staffColumns = ['name', 'count', 'amount', 'breakdown'];
  protected readonly waiterColumns = ['name', 'orders', 'value', 'service', 'hours', 'perHour'];

  protected readonly turnover = signal<TurnoverReportDto | null>(null);
  protected readonly topItems = signal<TopSellingItemDto[]>([]);
  protected readonly voids = signal<VoidReportDto | null>(null);
  protected readonly waiters = signal<WaiterPerformanceReportDto | null>(null);

  /** Kept apart from {@link loading}: the export is one button, not the whole screen. */
  protected readonly exporting = signal(false);

  protected from = addDays(new Date(), -6);
  protected to = new Date();

  protected readonly days = computed(() => this.turnover()?.days ?? []);
  protected readonly waiterRows = computed(() => this.waiters()?.waiters ?? []);

  constructor() {
    this.load();
  }

  protected setRange(daysBack: number): void {
    this.from = addDays(new Date(), -daysBack);
    this.to = new Date();
    this.load();
  }

  protected load(): void {
    // Turnover groups by the venue's local business day, so it takes dates; the other two take UTC
    // instants. Sending one shape where the other is expected is off by hours and reads as a
    // rounding error.
    this.loading
      .track(this.api.turnover(toDateOnly(this.from), toDateOnly(this.to)))
      .subscribe((report) => this.turnover.set(report));

    const fromUtc = startOfDayUtc(this.from);
    const toUtc = endOfDayUtc(this.to);

    this.loading
      .track(this.api.topItems(fromUtc, toUtc, undefined, 20))
      .subscribe((rows) => this.topItems.set(rows));

    this.loading
      .track(this.api.voidReport(fromUtc, toUtc))
      .subscribe((report) => this.voids.set(report));

    // Business days again, like the turnover: a waiter's shift is a night, not a UTC date.
    this.loading
      .track(this.api.waiterPerformance(toDateOnly(this.from), toDateOnly(this.to)))
      .subscribe((report) => this.waiters.set(report));
  }

  /**
   * Downloads the per-waiter report as a workbook.
   *
   * The server builds it from the same query the table is drawn from, so the file cannot disagree
   * with what is on screen the way a sheet serialised out of the browser could.
   */
  protected exportWaiters(): void {
    this.exporting.set(true);

    this.api.waiterPerformanceExport(toDateOnly(this.from), toDateOnly(this.to)).subscribe({
      next: (response) => {
        saveBlobResponse(response, 'konobari.xlsx');
        this.exporting.set(false);
      },
      // The error interceptor reads a JSON problem document and this response is a blob, so it has
      // nothing to say here; the message has to be given locally.
      error: () => {
        this.exporting.set(false);
        this.snackBar.open('Izveštaj nije mogao da se preuzme.', 'U redu', { duration: 5000 });
      },
    });
  }
}
