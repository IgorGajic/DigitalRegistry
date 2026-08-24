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
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  TillApiService,
  TopSellingItemDto,
  TurnoverReportDto,
  VoidReportDto,
  addDays,
  endOfDayUtc,
  startOfDayUtc,
  toDateOnly,
  voidTypeLabels,
} from 'shared';

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
    MatTableModule,
    MatTabsModule,
    MatTooltipModule,
  ],
  template: `
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
      font-variant-numeric: tabular-nums;
    }

    .rep__panel {
      margin-top: 12px;
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
  `,
})
export class ReportsPage {
  private readonly api = inject(TillApiService);

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

  protected readonly turnover = signal<TurnoverReportDto | null>(null);
  protected readonly topItems = signal<TopSellingItemDto[]>([]);
  protected readonly voids = signal<VoidReportDto | null>(null);

  protected from = addDays(new Date(), -6);
  protected to = new Date();

  protected readonly days = computed(() => this.turnover()?.days ?? []);

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
    this.api
      .turnover(toDateOnly(this.from), toDateOnly(this.to))
      .subscribe((report) => this.turnover.set(report));

    const fromUtc = startOfDayUtc(this.from);
    const toUtc = endOfDayUtc(this.to);

    this.api.topItems(fromUtc, toUtc, undefined, 20).subscribe((rows) => this.topItems.set(rows));
    this.api.voidReport(fromUtc, toUtc).subscribe((report) => this.voids.set(report));
  }
}
