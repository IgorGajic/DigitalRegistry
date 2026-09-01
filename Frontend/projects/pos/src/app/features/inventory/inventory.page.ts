import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  InventoryValuationDto,
  InventoryValuationLineDto,
  LoadingState,
  StockMovementDto,
  TillApiService,
  addDays,
  endOfDayUtc,
  startOfDayUtc,
  stockMovementLabels,
  unitLabels,
} from 'shared';

import { StockCountDialog, StockCountDialogResult } from './stock-count.dialog';
import { StockEntryDialog, StockEntryDialogResult } from './stock-entry.dialog';

/**
 * The store: what is on the shelf, what it cost, and what moved.
 *
 * Goods in are recorded with a purchase price, which is what makes the store worth anything on a
 * report and what lets an owner see a margin at all. A stocktake is separate and asks for a reason,
 * because it is the only movement not driven by a sale or a delivery.
 */
@Component({
  selector: 'pos-inventory',
  imports: [
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    MatButtonModule,
    MatCardModule,
    MatDialogModule,
    MatIconModule,
    MatProgressBarModule,
    MatTableModule,
    MatTabsModule,
    MatTooltipModule,
  ],
  template: `
    @if (loading.active()) {
      <mat-progress-bar mode="indeterminate" />
    }

    <div class="dr-page">
      <h1>Magacin</h1>

      @if (valuation(); as report) {
        <div class="inv__summary">
          <mat-card>
            <mat-card-content>
              <span class="dr-muted">Vrednost zaliha</span>
              <strong>{{ report.totalStockValue | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}</strong>
            </mat-card-content>
          </mat-card>
          <mat-card>
            <mat-card-content>
              <span class="dr-muted">Utrošeno (30 dana)</span>
              <strong>{{ report.totalConsumedValue | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}</strong>
            </mat-card-content>
          </mat-card>
          <mat-card>
            <mat-card-content>
              <span class="dr-muted">Nabavljeno (30 dana)</span>
              <strong>{{ report.totalPurchasedValue | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}</strong>
            </mat-card-content>
          </mat-card>
          <mat-card [class.inv__alert]="report.lowStockCount > 0">
            <mat-card-content>
              <span class="dr-muted">Ispod minimuma</span>
              <strong>{{ report.lowStockCount }}</strong>
            </mat-card-content>
          </mat-card>
        </div>
      }

      <mat-tab-group animationDuration="120ms">
        <mat-tab label="Zalihe">
          <mat-card class="inv__panel">
            <table mat-table [dataSource]="lines()">
              <ng-container matColumnDef="name">
                <th mat-header-cell *matHeaderCellDef>Artikal</th>
                <td mat-cell *matCellDef="let line">
                  {{ line.name }}
                  @if (line.isLowOnStock) {
                    <mat-icon class="inv__low" matTooltip="Ispod minimuma">warning</mat-icon>
                  }
                </td>
              </ng-container>

              <ng-container matColumnDef="stock">
                <th mat-header-cell *matHeaderCellDef class="dr-numeric">Stanje</th>
                <td mat-cell *matCellDef="let line" class="dr-numeric">
                  {{ line.stockQuantity | number: '1.0-3' }} {{ unit(line) }}
                </td>
              </ng-container>

              <ng-container matColumnDef="price">
                <th mat-header-cell *matHeaderCellDef class="dr-numeric">Prosečna nabavna</th>
                <td mat-cell *matCellDef="let line" class="dr-numeric">
                  {{ line.averagePurchasePrice | number: '1.2-4' }}
                </td>
              </ng-container>

              <ng-container matColumnDef="value">
                <th mat-header-cell *matHeaderCellDef class="dr-numeric">Vrednost</th>
                <td mat-cell *matCellDef="let line" class="dr-numeric">
                  {{ line.stockValue | currency: 'RSD' : 'symbol-narrow' : '1.0-0' }}
                </td>
              </ng-container>

              <ng-container matColumnDef="consumed">
                <th mat-header-cell *matHeaderCellDef class="dr-numeric">Utrošeno</th>
                <td mat-cell *matCellDef="let line" class="dr-numeric">
                  {{ line.consumedQuantity | number: '1.0-3' }}
                </td>
              </ng-container>

              <ng-container matColumnDef="actions">
                <th mat-header-cell *matHeaderCellDef></th>
                <td mat-cell *matCellDef="let line" class="inv__actions">
                  <button mat-stroked-button (click)="startEntry(line)">Ulaz robe</button>
                  <button mat-button (click)="startCount(line)">Popis</button>
                </td>
              </ng-container>

              <tr mat-header-row *matHeaderRowDef="stockColumns"></tr>
              <tr mat-row *matRowDef="let row; columns: stockColumns"></tr>
            </table>

            @if (lines().length === 0 && !loading.active()) {
              <p class="dr-empty">
                Nema nijednog sastojka. Dok ih nema, prodaja ne razdužuje magacin i marža na
                jelovniku ostaje nepoznata.
              </p>
            }
          </mat-card>
        </mat-tab>

        <mat-tab label="Knjiga prometa">
          <mat-card class="inv__panel">
            <table mat-table [dataSource]="movements()">
              <ng-container matColumnDef="when">
                <th mat-header-cell *matHeaderCellDef>Kada</th>
                <td mat-cell *matCellDef="let row">
                  {{ row.occurredAtUtc | date: 'dd.MM. HH:mm' }}
                </td>
              </ng-container>

              <ng-container matColumnDef="ingredient">
                <th mat-header-cell *matHeaderCellDef>Artikal</th>
                <td mat-cell *matCellDef="let row">{{ row.ingredientName }}</td>
              </ng-container>

              <ng-container matColumnDef="type">
                <th mat-header-cell *matHeaderCellDef>Tip</th>
                <td mat-cell *matCellDef="let row">{{ movementLabel(row.type) }}</td>
              </ng-container>

              <ng-container matColumnDef="quantity">
                <th mat-header-cell *matHeaderCellDef class="dr-numeric">Količina</th>
                <td
                  mat-cell
                  *matCellDef="let row"
                  class="dr-numeric"
                  [class.inv__out]="row.quantity < 0"
                  [class.inv__in]="row.quantity > 0"
                >
                  {{ row.quantity > 0 ? '+' : '' }}{{ row.quantity | number: '1.0-3' }}
                </td>
              </ng-container>

              <ng-container matColumnDef="balance">
                <th mat-header-cell *matHeaderCellDef class="dr-numeric">Stanje posle</th>
                <td mat-cell *matCellDef="let row" class="dr-numeric">
                  {{ row.balanceAfter | number: '1.0-3' }}
                </td>
              </ng-container>

              <ng-container matColumnDef="note">
                <th mat-header-cell *matHeaderCellDef>Napomena</th>
                <td mat-cell *matCellDef="let row" class="dr-muted">{{ row.note }}</td>
              </ng-container>

              <tr mat-header-row *matHeaderRowDef="movementColumns"></tr>
              <tr mat-row *matRowDef="let row; columns: movementColumns"></tr>
            </table>

            @if (movements().length === 0 && !loading.active()) {
              <p class="dr-empty">Nema kretanja u poslednjih 30 dana.</p>
            }
          </mat-card>
        </mat-tab>
      </mat-tab-group>

    </div>
  `,
  styles: `
    @use 'responsive-table' as rt;

    h1 {
      margin: 0 0 16px;
      font-size: 1.5rem;
    }

    .inv__summary {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(180px, 1fr));
      gap: 12px;
      margin-bottom: 16px;
    }

    .inv__summary mat-card-content {
      display: flex;
      flex-direction: column;
      gap: 2px;
      padding: 12px 16px;
    }

    .inv__summary strong {
      font-size: 1.4rem;
      font-family: var(--dr-font-mono);
      font-variant-numeric: tabular-nums;
    }

    .inv__alert {
      border: 1px solid var(--dr-reserved);
    }

    .inv__panel {
      margin-top: 12px;
    }

    table {
      width: 100%;
    }

    .inv__actions {
      white-space: nowrap;
      text-align: right;
    }

    .inv__low {
      font-size: 16px;
      width: 16px;
      height: 16px;
      vertical-align: middle;
      color: var(--dr-reserved);
    }

    .inv__in {
      color: var(--dr-free);
    }

    .inv__out {
      color: var(--dr-occupied);
    }

    @include rt.labels((
      name: 'Artikal',
      stock: 'Stanje',
      price: 'Prosečna nabavna',
      value: 'Vrednost',
      consumed: 'Utrošeno',
      when: 'Kada',
      ingredient: 'Artikal',
      type: 'Tip',
      quantity: 'Količina',
      balance: 'Stanje posle',
      note: 'Napomena',
    ));
  `,
})
export class InventoryPage {
  private readonly api = inject(TillApiService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  protected readonly loading = new LoadingState();
  protected readonly stockColumns = ['name', 'stock', 'price', 'value', 'consumed', 'actions'];
  protected readonly movementColumns = ['when', 'ingredient', 'type', 'quantity', 'balance', 'note'];

  protected readonly valuation = signal<InventoryValuationDto | null>(null);
  protected readonly movements = signal<StockMovementDto[]>([]);
  protected readonly lines = computed(() => this.valuation()?.lines ?? []);

  constructor() {
    this.load();
  }

  protected unit(line: InventoryValuationLineDto): string {
    return unitLabels[line.unit];
  }

  protected movementLabel(type: StockMovementDto['type']): string {
    return stockMovementLabels[type];
  }

  /**
   * Books a delivery in.
   *
   * A dialog rather than a panel under the table: the panel was rendered after the tab group, so on
   * a store with more than a handful of ingredients the button was on screen and the form it opened
   * was not, and pressing it looked like pressing nothing.
   */
  protected startEntry(line: InventoryValuationLineDto): void {
    this.dialog
      .open(StockEntryDialog, { data: { line } })
      .afterClosed()
      .subscribe((result: StockEntryDialogResult | undefined) => {
        if (!result) {
          return;
        }

        this.loading
          .track(this.api.recordStockEntry({ ingredientId: line.ingredientId, ...result }))
          .subscribe((entry) => {
            this.snackBar.open(
              `Zaduženo. Novo stanje ${entry.stockAfter}, prosečna nabavna `
                + `${entry.averagePurchasePriceAfter}.`,
              'U redu',
              { duration: 6000 },
            );
            this.load();
          });
      });
  }

  /** Files a stocktake. The counted quantity is what is asked for; the difference is derived. */
  protected startCount(line: InventoryValuationLineDto): void {
    this.dialog
      .open(StockCountDialog, { data: { line } })
      .afterClosed()
      .subscribe((result: StockCountDialogResult | undefined) => {
        if (!result) {
          return;
        }

        this.loading
          .track(this.api.adjustStock(line.ingredientId, result.counted, result.reason))
          .subscribe((outcome) => {
            this.snackBar.open(`Popis sačuvan. Razlika ${outcome.difference}.`, 'U redu', {
              duration: 6000,
            });
            this.load();
          });
      });
  }

  private load(): void {
    const now = new Date();
    const from = startOfDayUtc(addDays(now, -30));
    const to = endOfDayUtc(now);

    this.loading
      .track(this.api.inventoryValuation(from, to))
      .subscribe((report) => this.valuation.set(report));

    this.loading
      .track(this.api.stockMovements(from, to))
      .subscribe((rows) => this.movements.set(rows));
  }
}
