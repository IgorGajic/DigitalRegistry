import { describe, expect, it } from 'vitest';

import { elapsedSince, startOfWeek } from './dates';

/**
 * How long a tab has been open is read at a glance, off a floor plan, by someone carrying plates.
 * The unit it comes out in is the whole point.
 */
describe('elapsedSince', () => {
  const at = (iso: string) => new Date(iso);

  it('stays in minutes for the first hour, which is where most tabs live', () => {
    expect(elapsedSince('2026-09-02T10:00:00Z', at('2026-09-02T10:00:00Z'))).toBe('0 min');
    expect(elapsedSince('2026-09-02T10:00:00Z', at('2026-09-02T10:35:00Z'))).toBe('35 min');
    expect(elapsedSince('2026-09-02T10:00:00Z', at('2026-09-02T10:59:00Z'))).toBe('59 min');
  });

  it('drops the minutes when there are none, rather than writing "2 h 0 min"', () => {
    expect(elapsedSince('2026-09-02T10:00:00Z', at('2026-09-02T12:00:00Z'))).toBe('2 h');
    expect(elapsedSince('2026-09-02T10:00:00Z', at('2026-09-02T12:20:00Z'))).toBe('2 h 20 min');
  });

  it('rolls over into days, instead of counting to "192 h 24 min"', () => {
    expect(elapsedSince('2026-08-25T10:00:00Z', at('2026-09-02T10:24:00Z'))).toBe('8 dana');
    expect(elapsedSince('2026-09-01T10:00:00Z', at('2026-09-02T13:00:00Z'))).toBe('1 dan 3 h');
  });

  it('declines the day in Serbian, where 1, 2 and 5 differ', () => {
    expect(elapsedSince('2026-09-01T10:00:00Z', at('2026-09-02T10:00:00Z'))).toBe('1 dan');
    expect(elapsedSince('2026-08-31T10:00:00Z', at('2026-09-02T10:00:00Z'))).toBe('2 dana');
    expect(elapsedSince('2026-08-28T10:00:00Z', at('2026-09-02T10:00:00Z'))).toBe('5 dana');
  });

  it('never counts backwards from a clock that is a little ahead', () => {
    expect(elapsedSince('2026-09-02T10:05:00Z', at('2026-09-02T10:00:00Z'))).toBe('0 min');
  });
});

describe('startOfWeek', () => {
  it('returns Monday, because that is how a rota is read here', () => {
    // 2026-09-02 is a Wednesday.
    expect(startOfWeek(new Date(2026, 8, 2)).getDate()).toBe(31);

    // A Sunday belongs to the week that began six days earlier, not to the one starting tomorrow.
    expect(startOfWeek(new Date(2026, 8, 6)).getDate()).toBe(31);
  });
});
