import { Component, computed, input } from '@angular/core';
import {
  BarChart,
  BarPoint,
  DailyTurnoverDto,
  PaymentMethod,
  billsLabel,
  paymentMethodLabels,
} from 'shared';

/** A slice of the period's takings, for the composition meter. */
interface Slice {
  readonly method: PaymentMethod;
  readonly label: string;
  readonly amount: number;
  readonly share: number;
  readonly colour: string;
}

/**
 * The takings, by day and by method.
 *
 * Two questions, two marks, because they are genuinely different questions. *How did the period go
 * and which days carry it* is a shape over time, and gets bars on a zero baseline — drawn by the
 * shared {@link BarChart}, the same one the platform's licence revenue uses. *What were we paid in*
 * is a part-to-whole, and gets one composition meter for the period.
 *
 * Putting the second question inside the first — a stacked bar per day — was the obvious move and is
 * wrong here: a reversal is booked as a negative amount into whichever method it reverses, so a
 * day's cash can go below zero, and a stacked segment cannot honestly be negative.
 *
 * The exact figures are in the table underneath; this is here for the shape.
 */
@Component({
  selector: 'pos-turnover-chart',
  imports: [BarChart],
  template: `
    <div class="turnover">
      <dr-bar-chart
        [points]="points()"
        eyebrow="Promet po danima"
        peakLabel="najbolji dan"
        [formatValue]="money"
        [summary]="summary()"
      />

      @if (slices(); as parts) {
        <div class="turnover__split">
          <span class="dr-eyebrow">Naplaćeno kroz</span>

          <div class="turnover__meter" role="presentation">
            @for (slice of parts; track slice.method) {
              @if (slice.share > 0) {
                <span
                  class="turnover__slice"
                  [style.flex-basis.%]="slice.share * 100"
                  [style.background]="slice.colour"
                ></span>
              }
            }
          </div>

          <!-- Every slice is named and valued, so identity never rests on the colour alone. -->
          <ul class="turnover__legend">
            @for (slice of parts; track slice.method) {
              <li>
                <i class="turnover__swatch" [style.background]="slice.colour"></i>
                {{ slice.label }}
                <span class="dr-figure">{{ money(slice.amount) }}</span>
                <span class="dr-muted">{{ percent(slice.share) }}</span>
              </li>
            }
          </ul>
        </div>
      }
    </div>
  `,
  styles: `
    .turnover {
      padding: 16px;
    }

    .turnover__split {
      margin-top: 20px;
      padding-top: 16px;
      border-top: 1px solid var(--mat-sys-outline-variant);
    }

    .turnover__meter {
      display: flex;
      /* The 2 px surface gap keeps two segments from reading as one. */
      gap: 2px;
      height: 14px;
      margin: 8px 0 10px;
    }

    .turnover__slice {
      border-radius: var(--dr-radius-sm);
      min-width: 2px;
    }

    .turnover__legend {
      display: flex;
      flex-wrap: wrap;
      gap: 6px 20px;
      list-style: none;
      margin: 0;
      padding: 0;
      font-size: 0.85rem;
    }

    .turnover__legend li {
      display: flex;
      align-items: center;
      gap: 6px;
    }

    .turnover__swatch {
      width: 10px;
      height: 10px;
      border-radius: var(--dr-radius-sm);
      display: inline-block;
    }
  `,
})
export class TurnoverChart {
  readonly days = input.required<DailyTurnoverDto[]>();
  readonly cash = input.required<number>();
  readonly card = input.required<number>();
  readonly wallet = input.required<number>();

  readonly points = computed<BarPoint[]>(() =>
    this.days().map((day) => ({
      label: shortDay(day.date),
      value: day.turnover,
      title: fullDay(day.date),
      notes: [
        `${day.billCount} ${billsLabel(day.billCount)} · prosek ${this.money(day.averageBill)}`,
        ...(day.reversalCount ? [`stornirano −${this.money(day.reversedAmount)}`] : []),
      ],
    })),
  );

  /**
   * The period's takings by method, as shares.
   *
   * Rendered only when every method is non-negative. A reversal is booked against the method it
   * reverses, so a quiet period with a large reversal can leave one of these below zero — and a
   * share of a whole cannot be negative. In that case the meter is dropped rather than drawn
   * wrongly; the table below still carries the figures.
   */
  readonly slices = computed<Slice[] | null>(() => {
    const parts: { method: PaymentMethod; amount: number; colour: string }[] = [
      // A fixed order, assigned once. Validated against the light surface for lightness, chroma,
      // colour-vision separation and contrast.
      { method: PaymentMethod.Cash, amount: this.cash(), colour: '#C07C2E' },
      { method: PaymentMethod.Card, amount: this.card(), colour: '#00949C' },
      { method: PaymentMethod.DigitalWallet, amount: this.wallet(), colour: '#7B52A8' },
    ];

    if (parts.some((part) => part.amount < 0)) {
      return null;
    }

    const total = parts.reduce((sum, part) => sum + part.amount, 0);

    if (total <= 0) {
      return null;
    }

    return parts.map((part) => ({
      method: part.method,
      label: paymentMethodLabels[part.method],
      amount: part.amount,
      share: part.amount / total,
      colour: part.colour,
    }));
  });

  protected readonly summary = computed(() => {
    const days = this.days();

    if (days.length === 0) {
      return 'Nema prometa u izabranom periodu.';
    }

    const total = days.reduce((sum, day) => sum + day.turnover, 0);

    return `Promet po danima, ${days.length} dana, ukupno ${this.money(total)}.`;
  });

  /** Passed into the chart, so it is an arrow rather than a method: it travels without `this`. */
  protected readonly money = (value: number): string =>
    `${Math.round(value).toLocaleString('sr-Latn-RS')} RSD`;

  protected percent(share: number): string {
    return `${Math.round(share * 100)}%`;
  }
}

/** `dd.MM` — the axis has room for the date and nothing more. */
function shortDay(isoDate: string): string {
  const date = new Date(isoDate);

  return `${`${date.getDate()}`.padStart(2, '0')}.${`${date.getMonth() + 1}`.padStart(2, '0')}`;
}

function fullDay(isoDate: string): string {
  return new Date(isoDate).toLocaleDateString('sr-Latn-RS', {
    weekday: 'long',
    day: '2-digit',
    month: '2-digit',
  });
}
