import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  InventoryValuationDto,
  InventoryValuationLineDto,
  StockMovementDto,
  TillApiService,
  addDays,
  endOfDayUtc,
  startOfDayUtc,
  stockMovementLabels,
  unitLabels,
} from 'shared';

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
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
    MatTableModule,
    MatTabsModule,
    MatTooltipModule,
  ],
  template: `
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

            @if (movements().length === 0) {
              <p class="dr-empty">Nema kretanja u poslednjih 30 dana.</p>
            }
          </mat-card>
        </mat-tab>
      </mat-tab-group>

      @if (entryFor(); as line) {
        <mat-card class="inv__form">
          <mat-card-header>
            <mat-card-title>Ulaz robe: {{ line.name }}</mat-card-title>
          </mat-card-header>
          <mat-card-content>
            <div class="inv__fields">
              <mat-form-field appearance="outline">
                <mat-label>Količina</mat-label>
                <input matInput type="number" min="0" step="1" [(ngModel)]="quantity" />
                <span matTextSuffix>{{ unit(line) }}</span>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Nabavna cena po jedinici</mat-label>
                <input matInput type="number" min="0" step="0.01" [(ngModel)]="unitPrice" />
                <span matTextSuffix>RSD</span>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Dobavljač</mat-label>
                <input matInput [(ngModel)]="supplier" />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Broj otpremnice</mat-label>
                <input matInput [(ngModel)]="reference" />
              </mat-form-field>
            </div>

            <p class="dr-muted">
              Ukupno: {{ quantity * unitPrice | currency: 'RSD' : 'symbol-narrow' : '1.0-2' }}.
              Nabavna cena ulazi u klizeći prosek, iz kojeg se računa marža na jelovniku.
            </p>

            <div class="inv__form-actions">
              <button mat-flat-button [disabled]="quantity <= 0" (click)="saveEntry(line)">
                Zaduži magacin
              </button>
              <button mat-button (click)="entryFor.set(null)">Odustani</button>
            </div>
          </mat-card-content>
        </mat-card>
      }

      @if (countFor(); as line) {
        <mat-card class="inv__form">
          <mat-card-header>
            <mat-card-title>Popis: {{ line.name }}</mat-card-title>
            <mat-card-subtitle>
              Knjigovodstveno stanje: {{ line.stockQuantity | number: '1.0-3' }} {{ unit(line) }}
            </mat-card-subtitle>
          </mat-card-header>
          <mat-card-content>
            <div class="inv__fields">
              <mat-form-field appearance="outline">
                <mat-label>Prebrojano</mat-label>
                <input matInput type="number" min="0" step="0.1" [(ngModel)]="counted" />
                <span matTextSuffix>{{ unit(line) }}</span>
              </mat-form-field>

              <mat-form-field appearance="outline" class="inv__reason">
                <mat-label>Razlog</mat-label>
                <input matInput [(ngModel)]="reason" placeholder="npr. lom, kalo, greška u prijemu" />
              </mat-form-field>
            </div>

            <p class="dr-muted">
              Razlika: {{ counted - line.stockQuantity | number: '1.0-3' }} {{ unit(line) }}.
              Korekcija ostaje zabeležena uz vaše ime.
            </p>

            <div class="inv__form-actions">
              <button mat-flat-button [disabled]="reason.trim().length < 3" (click)="saveCount(line)">
                Sačuvaj popis
              </button>
              <button mat-button (click)="countFor.set(null)">Odustani</button>
            </div>
          </mat-card-content>
        </mat-card>
      }
    </div>
  `,
  styles: `
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

    .inv__form {
      margin-top: 16px;
    }

    .inv__fields {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 8px;
    }

    .inv__reason {
      grid-column: span 2;
    }

    .inv__form-actions {
      display: flex;
      gap: 8px;
      margin-top: 8px;
    }
  `,
})
export class InventoryPage {
  private readonly api = inject(TillApiService);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly stockColumns = ['name', 'stock', 'price', 'value', 'consumed', 'actions'];
  protected readonly movementColumns = ['when', 'ingredient', 'type', 'quantity', 'balance', 'note'];

  protected readonly valuation = signal<InventoryValuationDto | null>(null);
  protected readonly movements = signal<StockMovementDto[]>([]);
  protected readonly lines = computed(() => this.valuation()?.lines ?? []);

  protected readonly entryFor = signal<InventoryValuationLineDto | null>(null);
  protected readonly countFor = signal<InventoryValuationLineDto | null>(null);

  protected quantity = 0;
  protected unitPrice = 0;
  protected supplier = '';
  protected reference = '';
  protected counted = 0;
  protected reason = '';

  constructor() {
    this.load();
  }

  protected unit(line: InventoryValuationLineDto): string {
    return unitLabels[line.unit];
  }

  protected movementLabel(type: StockMovementDto['type']): string {
    return stockMovementLabels[type];
  }

  protected startEntry(line: InventoryValuationLineDto): void {
    this.countFor.set(null);
    this.entryFor.set(line);
    this.quantity = 0;
    // Pre-filled with what the last deliveries averaged, which is usually close enough to correct.
    this.unitPrice = line.averagePurchasePrice;
    this.supplier = '';
    this.reference = '';
  }

  protected startCount(line: InventoryValuationLineDto): void {
    this.entryFor.set(null);
    this.countFor.set(line);
    this.counted = line.stockQuantity;
    this.reason = '';
  }

  protected saveEntry(line: InventoryValuationLineDto): void {
    this.api
      .recordStockEntry({
        ingredientId: line.ingredientId,
        quantity: this.quantity,
        purchaseUnitPrice: this.unitPrice,
        supplier: this.supplier.trim() || null,
        referenceNumber: this.reference.trim() || null,
      })
      .subscribe((entry) => {
        this.snackBar.open(
          `Zaduženo. Novo stanje ${entry.stockAfter}, prosečna nabavna ${entry.averagePurchasePriceAfter}.`,
          'U redu',
          { duration: 6000 },
        );
        this.entryFor.set(null);
        this.load();
      });
  }

  protected saveCount(line: InventoryValuationLineDto): void {
    this.api
      .adjustStock(line.ingredientId, this.counted, this.reason.trim())
      .subscribe((result) => {
        this.snackBar.open(
          `Popis sačuvan. Razlika ${result.difference}.`,
          'U redu',
          { duration: 6000 },
        );
        this.countFor.set(null);
        this.load();
      });
  }

  private load(): void {
    const now = new Date();
    const from = startOfDayUtc(addDays(now, -30));
    const to = endOfDayUtc(now);

    this.api.inventoryValuation(from, to).subscribe((report) => this.valuation.set(report));
    this.api.stockMovements(from, to).subscribe((rows) => this.movements.set(rows));
  }
}
