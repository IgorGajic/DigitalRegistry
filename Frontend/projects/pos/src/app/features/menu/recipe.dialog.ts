import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { InventoryValuationLineDto, MenuItemDetailDto, unitLabels } from 'shared';

export interface RecipeDialogData {
  item: MenuItemDetailDto;
  ingredients: InventoryValuationLineDto[];
}

export interface RecipeDialogResult {
  lines: { ingredientId: string; quantityRequired: number }[];
}

interface EditableLine {
  ingredientId: string;
  quantityRequired: number;
}

/**
 * What one serving consumes.
 *
 * The whole recipe is submitted, so a line removed here is removed from the recipe — which is why
 * the editor works on a local copy and only sends it on save.
 */
@Component({
  selector: 'pos-recipe-dialog',
  imports: [
    FormsModule,
    MatButtonModule,
    MatDialogModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatSelectModule,
  ],
  template: `
    <h2 mat-dialog-title>Normativ: {{ data.item.name }}</h2>

    <mat-dialog-content>
      <p class="dr-muted">
        Koliko se čega troši na jednu porciju. Flaširano piće koje se prodaje kakvo jeste je jedna
        stavka sa količinom 1.
      </p>

      @for (line of lines(); track $index) {
        <div class="recipe__line">
          <mat-form-field appearance="outline" class="recipe__ingredient">
            <mat-label>Sastojak</mat-label>
            <mat-select
              [ngModel]="line.ingredientId"
              (ngModelChange)="setIngredient($index, $event)"
              [name]="'ingredient' + $index"
            >
              @for (ingredient of data.ingredients; track ingredient.ingredientId) {
                <mat-option [value]="ingredient.ingredientId">
                  {{ ingredient.name }} ({{ unitLabels[ingredient.unit] }})
                </mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline" class="recipe__quantity">
            <mat-label>Količina</mat-label>
            <input
              matInput
              type="number"
              min="0"
              step="0.1"
              [ngModel]="line.quantityRequired"
              (ngModelChange)="setQuantity($index, +$event)"
              [name]="'quantity' + $index"
            />
            <span matTextSuffix>{{ unitFor(line.ingredientId) }}</span>
          </mat-form-field>

          <button mat-icon-button color="warn" (click)="removeLine($index)" aria-label="Ukloni">
            <mat-icon>close</mat-icon>
          </button>
        </div>
      }

      @if (lines().length === 0) {
        <p class="dr-empty">
          Nema stavki. Bez normativa se prodaja ovog artikla ne razdužuje iz magacina.
        </p>
      }

      <button mat-stroked-button (click)="addLine()" [disabled]="!data.ingredients.length">
        <mat-icon>add</mat-icon>
        Dodaj sastojak
      </button>
    </mat-dialog-content>

    <mat-dialog-actions align="end">
      <button mat-button mat-dialog-close>Odustani</button>
      <button mat-flat-button [disabled]="!valid()" (click)="confirm()">Sačuvaj normativ</button>
    </mat-dialog-actions>
  `,
  styles: `
    .recipe__line {
      display: flex;
      gap: 8px;
      align-items: center;
    }

    .recipe__ingredient {
      flex: 1;
    }

    .recipe__quantity {
      width: 140px;
    }
  `,
})
export class RecipeDialog {
  protected readonly data = inject<RecipeDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<RecipeDialog, RecipeDialogResult>);

  protected readonly unitLabels = unitLabels;

  protected readonly lines = signal<EditableLine[]>(
    this.data.item.recipe.map((line) => ({
      ingredientId: line.ingredientId,
      quantityRequired: line.quantityRequired,
    })),
  );

  protected addLine(): void {
    const used = new Set(this.lines().map((line) => line.ingredientId));
    const next = this.data.ingredients.find(
      (ingredient) => !used.has(ingredient.ingredientId),
    );

    if (next) {
      this.lines.update((lines) => [
        ...lines,
        { ingredientId: next.ingredientId, quantityRequired: 1 },
      ]);
    }
  }

  protected removeLine(index: number): void {
    this.lines.update((lines) => lines.filter((_, position) => position !== index));
  }

  protected setIngredient(index: number, ingredientId: string): void {
    this.lines.update((lines) =>
      lines.map((line, position) => (position === index ? { ...line, ingredientId } : line)),
    );
  }

  protected setQuantity(index: number, quantityRequired: number): void {
    this.lines.update((lines) =>
      lines.map((line, position) => (position === index ? { ...line, quantityRequired } : line)),
    );
  }

  protected unitFor(ingredientId: string): string {
    const ingredient = this.data.ingredients.find(
      (candidate) => candidate.ingredientId === ingredientId,
    );

    return ingredient ? unitLabels[ingredient.unit] : '';
  }

  /** The API rejects a duplicate ingredient, so it is caught here rather than after the request. */
  protected valid(): boolean {
    const lines = this.lines();
    const ids = new Set(lines.map((line) => line.ingredientId));

    return (
      ids.size === lines.length && lines.every((line) => line.quantityRequired > 0)
    );
  }

  protected confirm(): void {
    if (this.valid()) {
      this.ref.close({ lines: this.lines() });
    }
  }
}
