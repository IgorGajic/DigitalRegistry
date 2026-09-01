import { ComponentRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import { TurnoverChart } from './turnover-chart';
import { DailyTurnoverDto } from 'shared';

function day(date: string, turnover: number, extra: Partial<DailyTurnoverDto> = {}): DailyTurnoverDto {
  return {
    date,
    turnover,
    cash: turnover,
    card: 0,
    digitalWallet: 0,
    billCount: 4,
    averageBill: turnover / 4,
    reversedAmount: 0,
    reversalCount: 0,
    ...extra,
  };
}

function chartOf(days: DailyTurnoverDto[], cash = 0, card = 0, wallet = 0) {
  const fixture = TestBed.createComponent(TurnoverChart);
  const ref = fixture.componentRef as ComponentRef<TurnoverChart>;

  ref.setInput('days', days);
  ref.setInput('cash', cash);
  ref.setInput('card', card);
  ref.setInput('wallet', wallet);
  fixture.detectChanges();

  return fixture.componentInstance;
}

/**
 * The geometry is tested where it lives, in `BarChart`. What is left here is what this screen adds
 * to it: turning a day into a point, and the rule that decides whether the composition meter can
 * honestly be drawn at all.
 */
describe('TurnoverChart', () => {
  beforeEach(() => TestBed.configureTestingModule({ imports: [TurnoverChart] }));

  it('turns each day into a point the chart can draw', () => {
    const chart = chartOf([day('2026-09-01', 4800, { billCount: 3, averageBill: 1600 })]);
    const [first] = chart.points();

    expect(first.value).toBe(4800);
    expect(first.label).toBe('01.09');
    expect(first.notes?.[0]).toContain('3 računa');
  });

  it('says on the tooltip when a day carried a reversal', () => {
    const plain = chartOf([day('2026-09-01', 1000)]).points()[0];
    const reversed = chartOf([
      day('2026-09-01', 1000, { reversalCount: 1, reversedAmount: 250 }),
    ]).points()[0];

    expect(plain.notes).toHaveLength(1);
    expect(reversed.notes).toHaveLength(2);
    expect(reversed.notes?.[1]).toContain('250');
  });

  it('splits the period into shares that add up', () => {
    const chart = chartOf([day('2026-09-01', 1000)], 500, 300, 200);

    expect(chart.slices()!.map((slice) => slice.share)).toEqual([0.5, 0.3, 0.2]);
  });

  /**
   * A reversal is booked against the method it reverses, so one bucket can end a quiet period below
   * zero — and a share of a whole cannot be negative. The meter is dropped rather than drawn wrongly.
   */
  it('drops the composition meter when a method is negative', () => {
    expect(chartOf([day('2026-09-01', 100)], -50, 150, 0).slices()).toBeNull();
  });

  it('drops the composition meter when nothing was taken', () => {
    expect(chartOf([day('2026-09-01', 0)], 0, 0, 0).slices()).toBeNull();
  });
});
