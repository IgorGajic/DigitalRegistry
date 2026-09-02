import { ComponentRef } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { beforeEach, describe, expect, it } from 'vitest';

import { BarChart, BarPoint } from './bar-chart';

function point(label: string, value: number): BarPoint {
  return { label, value, title: label };
}

function chartOf(points: BarPoint[]) {
  const fixture = TestBed.createComponent(BarChart);
  const ref = fixture.componentRef as ComponentRef<BarChart>;

  ref.setInput('points', points);
  ref.setInput('eyebrow', 'Promet');
  ref.setInput('formatValue', (value: number) => `${value}`);
  fixture.detectChanges();

  return fixture.componentInstance;
}

/**
 * The geometry both applications now share. It was written for the till's takings and then reused
 * for the platform's licence revenue, so these are the checks that keep one from quietly breaking
 * the other.
 */
describe('BarChart geometry', () => {
  beforeEach(() => TestBed.configureTestingModule({ imports: [BarChart] }));

  it('draws one bar per point, inside the plot area', () => {
    const chart = chartOf([point('01', 100), point('02', 200), point('03', 50)]);

    expect(chart.bars()).toHaveLength(3);

    for (const bar of chart.bars()) {
      expect(bar.x).toBeGreaterThanOrEqual(64);
      expect(bar.x + bar.width).toBeLessThanOrEqual(960 - 12);
      expect(bar.width).toBeGreaterThan(0);
    }
  });

  it('scales against the largest value, from zero', () => {
    const chart = chartOf([point('01', 100), point('02', 200)]);
    const [small, large] = chart.bars();

    // Half the value, half the height — the check that catches a scale anchored anywhere but zero.
    expect(small.height).toBeCloseTo(large.height / 2, 1);
  });

  /**
   * A day whose reversals outweigh its takings is genuinely negative. Clamping it to zero would draw
   * a flat day where the venue actually gave money back, which is the one thing a takings chart must
   * never do.
   */
  it('draws a negative value below the baseline', () => {
    const chart = chartOf([point('01', 400), point('02', -120)]);
    const [good, bad] = chart.bars();

    expect(bad.height).toBeGreaterThan(0);
    expect(bad.y).toBeCloseTo(good.y + good.height, 1);
  });

  it('gives a zero value a visible hairline rather than nothing', () => {
    const chart = chartOf([point('01', 500), point('02', 0)]);

    expect(chart.barPath(chart.bars()[1])).toContain('v 0.75');
  });

  it('thins the labels as the series grows, keeping both ends', () => {
    const chart = chartOf(
      Array.from({ length: 30 }, (_, i) => point(`${i + 1}`, 100 + i)),
    );
    const labelled = chart.bars().filter((bar) => chart.showLabel(bar));

    expect(labelled.length).toBeLessThan(12);
    expect(chart.showLabel({ index: 0 })).toBe(true);
    expect(chart.showLabel({ index: 29 })).toBe(true);
  });

  it('labels every slot when the series is short enough to hold them', () => {
    const chart = chartOf(Array.from({ length: 7 }, (_, i) => point(`${i + 1}`, 100)));

    expect(chart.bars().every((bar) => chart.showLabel(bar))).toBe(true);
  });

  it('names the strongest point', () => {
    const chart = chartOf([point('01', 100), point('02', 900), point('03', 400)]);

    expect(chart.peak()?.point.value).toBe(900);
  });

  /**
   * A series with one small negative value puts the floor a few pixels under zero, and the two tick
   * labels printed on top of each other.
   */
  it('drops a tick that would collide, and never drops zero', () => {
    const chart = chartOf([point('01', 40000), point('02', -60)]);
    const ticks = chart.gridLines();

    expect(ticks.some((tick) => tick.value === 0)).toBe(true);

    for (let i = 0; i < ticks.length; i += 1) {
      for (let j = i + 1; j < ticks.length; j += 1) {
        expect(Math.abs(ticks[i].y - ticks[j].y)).toBeGreaterThanOrEqual(16);
      }
    }
  });

  it('keeps all four ticks when the negative extent is wide enough to hold them', () => {
    const chart = chartOf([point('01', 40000), point('02', -18000)]);

    expect(chart.gridLines()).toHaveLength(4);
  });

  it('has nothing to draw for an empty series', () => {
    const chart = chartOf([]);

    expect(chart.bars()).toEqual([]);
    expect(chart.peak()).toBeNull();
  });
});
