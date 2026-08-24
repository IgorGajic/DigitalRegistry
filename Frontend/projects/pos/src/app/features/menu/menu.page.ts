import { CurrencyPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatListModule } from '@angular/material/list';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import {
  InventoryValuationLineDto,
  MenuItemDetailDto,
  MenuItemDto,
  TillApiService,
  endOfDayUtc,
  startOfDayUtc,
  unitLabels,
} from 'shared';

import { RecipeDialog, RecipeDialogResult } from './recipe.dialog';

/**
 * The price list, and what each item is made of.
 *
 * The recipe is what ties a sale to the store: without one an item sells but consumes nothing, and
 * the consumption report will not account for it. The margin column makes that visible — an item
 * with no recipe shows no cost, which is the signal that its normative is missing.
 */
@Component({
  selector: 'pos-menu',
  imports: [
    CurrencyPipe,
    ReactiveFormsModule,
    MatButtonModule,
    MatCardModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatListModule,
    MatSlideToggleModule,
    MatTableModule,
    MatTooltipModule,
  ],
  template: `
    <div class="dr-page">
      <header class="menu__header">
        <h1>Jelovnik</h1>
        <span class="dr-toolbar-spacer"></span>
        <button mat-flat-button (click)="startNew()">
          <mat-icon>add</mat-icon>
          Nov artikal
        </button>
      </header>

      <div class="menu__layout">
        <mat-card class="menu__list">
          <table mat-table [dataSource]="items()">
            <ng-container matColumnDef="name">
              <th mat-header-cell *matHeaderCellDef>Artikal</th>
              <td mat-cell *matCellDef="let item">
                {{ item.name }}
                @if (!item.isAvailable) {
                  <mat-icon
                    class="menu__off"
                    matTooltip="Trenutno nedostupno — nema zaliha ili je isključeno"
                  >
                    block
                  </mat-icon>
                }
              </td>
            </ng-container>

            <ng-container matColumnDef="category">
              <th mat-header-cell *matHeaderCellDef>Kategorija</th>
              <td mat-cell *matCellDef="let item">{{ item.category }}</td>
            </ng-container>

            <ng-container matColumnDef="price">
              <th mat-header-cell *matHeaderCellDef class="dr-numeric">Cena</th>
              <td mat-cell *matCellDef="let item" class="dr-numeric">
                {{ item.unitPrice | currency: 'RSD' : 'symbol-narrow' : '1.0-2' }}
              </td>
            </ng-container>

            <ng-container matColumnDef="actions">
              <th mat-header-cell *matHeaderCellDef></th>
              <td mat-cell *matCellDef="let item" class="menu__actions">
                <button mat-icon-button (click)="select(item)" matTooltip="Izmeni">
                  <mat-icon>edit</mat-icon>
                </button>
                <button mat-icon-button (click)="editRecipe(item)" matTooltip="Normativ">
                  <mat-icon>science</mat-icon>
                </button>
                <button
                  mat-icon-button
                  color="warn"
                  (click)="remove(item)"
                  matTooltip="Obriši"
                >
                  <mat-icon>delete</mat-icon>
                </button>
              </td>
            </ng-container>

            <tr mat-header-row *matHeaderRowDef="columns"></tr>
            <tr mat-row *matRowDef="let row; columns: columns"></tr>
          </table>

          @if (items().length === 0) {
            <p class="dr-empty">Jelovnik je prazan.</p>
          }
        </mat-card>

        <mat-card class="menu__editor">
          <mat-card-header>
            <mat-card-title>{{ editing() ? 'Izmena artikla' : 'Nov artikal' }}</mat-card-title>
          </mat-card-header>

          <mat-card-content>
            <form [formGroup]="form" (ngSubmit)="save()">
              <mat-form-field appearance="outline">
                <mat-label>Naziv</mat-label>
                <input matInput formControlName="name" />
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Kategorija</mat-label>
                <input matInput formControlName="category" list="categories" />
                <datalist id="categories">
                  @for (name of categories(); track name) {
                    <option [value]="name"></option>
                  }
                </datalist>
              </mat-form-field>

              <mat-form-field appearance="outline">
                <mat-label>Cena</mat-label>
                <input matInput type="number" min="0" step="10" formControlName="unitPrice" />
                <span matTextSuffix>RSD</span>
              </mat-form-field>

              <mat-slide-toggle formControlName="isAvailable">U ponudi</mat-slide-toggle>

              <div class="menu__editor-actions">
                <button mat-flat-button type="submit" [disabled]="form.invalid">Sačuvaj</button>
                @if (editing()) {
                  <button mat-button type="button" (click)="startNew()">Odustani</button>
                }
              </div>
            </form>

            @if (detail(); as item) {
              <div class="menu__cost">
                <h3>Kalkulacija</h3>
                <div class="menu__cost-row">
                  <span>Nabavna cena</span>
                  <strong>{{ item.costPrice | currency: 'RSD' : 'symbol-narrow' : '1.0-2' }}</strong>
                </div>
                <div class="menu__cost-row">
                  <span>Marža</span>
                  <strong>
                    @if (item.marginPercent !== null) {
                      {{ item.marginPercent }} %
                    } @else {
                      <span class="dr-muted" matTooltip="Normativ nije postavljen, ili sastojci nemaju nabavnu cenu">
                        nepoznata
                      </span>
                    }
                  </strong>
                </div>

                @if (item.recipe.length) {
                  <mat-list dense>
                    @for (line of item.recipe; track line.ingredientId) {
                      <mat-list-item>
                        <span matListItemTitle>{{ line.ingredientName }}</span>
                        <span matListItemLine class="dr-muted">
                          {{ line.quantityRequired }} {{ unitLabels[line.unit] }} po porciji
                        </span>
                      </mat-list-item>
                    }
                  </mat-list>
                } @else {
                  <p class="dr-muted">
                    Nema normativa — prodaja ovog artikla ne razdužuje magacin.
                  </p>
                }
              </div>
            }
          </mat-card-content>
        </mat-card>
      </div>
    </div>
  `,
  styles: `
    .menu__header {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-bottom: 16px;
    }

    h1 {
      margin: 0;
      font-size: 1.5rem;
    }

    .menu__layout {
      display: grid;
      grid-template-columns: 1fr minmax(300px, 380px);
      gap: var(--dr-gap);
      align-items: start;
    }

    table {
      width: 100%;
    }

    .menu__actions {
      white-space: nowrap;
      text-align: right;
    }

    .menu__off {
      font-size: 16px;
      width: 16px;
      height: 16px;
      vertical-align: middle;
      color: var(--dr-occupied);
    }

    form {
      display: flex;
      flex-direction: column;
      gap: 4px;
      padding-top: 8px;
    }

    mat-form-field {
      width: 100%;
    }

    .menu__editor-actions {
      display: flex;
      gap: 8px;
      margin-top: 12px;
    }

    .menu__cost {
      margin-top: 20px;
      padding-top: 12px;
      border-top: 1px solid var(--mat-sys-outline-variant);
    }

    .menu__cost h3 {
      margin: 0 0 8px;
      font-size: 1rem;
    }

    .menu__cost-row {
      display: flex;
      justify-content: space-between;
      padding: 2px 0;
    }

    @media (max-width: 1000px) {
      .menu__layout {
        grid-template-columns: 1fr;
      }
    }
  `,
})
export class MenuPage {
  private readonly api = inject(TillApiService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  protected readonly unitLabels = unitLabels;
  protected readonly columns = ['name', 'category', 'price', 'actions'];

  protected readonly items = signal<MenuItemDto[]>([]);
  protected readonly categories = signal<string[]>([]);
  protected readonly editing = signal<string | null>(null);
  protected readonly detail = signal<MenuItemDetailDto | null>(null);
  protected readonly ingredients = signal<InventoryValuationLineDto[]>([]);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    name: ['', [Validators.required]],
    category: ['', [Validators.required]],
    unitPrice: [0, [Validators.required, Validators.min(0)]],
    isAvailable: [true],
  });

  constructor() {
    this.load();
    this.loadIngredients();
  }

  protected load(): void {
    this.api.menu().subscribe((items) => {
      this.items.set(items);
      this.categories.set([...new Set(items.map((item) => item.category))].sort());
    });
  }

  protected startNew(): void {
    this.editing.set(null);
    this.detail.set(null);
    this.form.reset({ name: '', category: '', unitPrice: 0, isAvailable: true });
  }

  protected select(item: MenuItemDto): void {
    this.editing.set(item.id);
    this.form.setValue({
      name: item.name,
      category: item.category,
      unitPrice: item.unitPrice,
      isAvailable: item.isAvailable,
    });

    this.api.menuItem(item.id).subscribe((detail) => this.detail.set(detail));
  }

  protected save(): void {
    if (this.form.invalid) {
      return;
    }

    this.api.saveMenuItem({ id: this.editing(), ...this.form.getRawValue() }).subscribe((saved) => {
      this.detail.set(saved);
      this.editing.set(saved.id);
      this.load();
    });
  }

  protected editRecipe(item: MenuItemDto): void {
    this.api.menuItem(item.id).subscribe((detail) => {
      this.dialog
        .open(RecipeDialog, {
          data: { item: detail, ingredients: this.ingredients() },
          width: '560px',
        })
        .afterClosed()
        .subscribe((result: RecipeDialogResult | undefined) => {
          if (!result) {
            return;
          }

          this.api.setRecipe(item.id, result.lines).subscribe((saved) => {
            this.detail.set(saved);
            this.editing.set(saved.id);
            this.load();
          });
        });
    });
  }

  protected remove(item: MenuItemDto): void {
    this.api.deleteMenuItem(item.id).subscribe({
      next: () => {
        this.startNew();
        this.load();
      },
      // A 409 means the item is on past orders. The interceptor has already said so; this only
      // keeps the list honest about what is still there.
      error: () => this.load(),
    });
  }

  /**
   * Ingredients come from the valuation report rather than a dedicated endpoint.
   *
   * It already returns every ingredient with its unit and stock, which is exactly what the recipe
   * editor needs — and adding a second listing would be one more thing to keep in step.
   */
  private loadIngredients(): void {
    const today = new Date();

    this.api
      .inventoryValuation(startOfDayUtc(today), endOfDayUtc(today))
      .subscribe((report) => this.ingredients.set(report.lines));
  }
}
