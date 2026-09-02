import { daysLabel } from './labels';

/**
 * Date helpers for talking to an API that thinks in UTC instants and local business days.
 *
 * The distinction matters. Reports over *days* take a `DateOnly` — the venue's local business day,
 * which the backend converts using the restaurant's own time zone. Reports over *periods* take a UTC
 * instant. Sending one where the other is expected is off by hours and looks like a rounding error.
 */

/** `yyyy-MM-dd` in the browser's local calendar, for the API's `DateOnly` parameters. */
export function toDateOnly(date: Date): string {
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');

  return `${year}-${month}-${day}`;
}

/** An ISO instant, for the API's `DateTime` parameters. */
export function toUtc(date: Date): string {
  return date.toISOString();
}

/** Midnight at the start of a local day, as a UTC instant. */
export function startOfDayUtc(date: Date): string {
  const start = new Date(date);
  start.setHours(0, 0, 0, 0);

  return start.toISOString();
}

/** Midnight at the start of the following day, so a period ends exclusively. */
export function endOfDayUtc(date: Date): string {
  const end = new Date(date);
  end.setHours(0, 0, 0, 0);
  end.setDate(end.getDate() + 1);

  return end.toISOString();
}

export function addDays(date: Date, days: number): Date {
  const result = new Date(date);
  result.setDate(result.getDate() + days);

  return result;
}

/** The Monday of the week a date falls in, which is how a rota is read here. */
export function startOfWeek(date: Date): Date {
  const result = new Date(date);
  const offset = (result.getDay() + 6) % 7;

  result.setDate(result.getDate() - offset);
  result.setHours(0, 0, 0, 0);

  return result;
}

/**
 * How long a tab has been open, as "1 h 20 min" — what a waiter glancing at the floor wants.
 *
 * Rolls over into days past twenty-four hours. It reads as an edge case and is not: a tab left open
 * overnight is exactly what a waiter needs to notice, and the unrolled form buried it in arithmetic
 * — a bill open since last Monday said "192 h 24 min". Beyond a day the minutes stop being the
 * question, so they are dropped and the hours kept.
 */
export function elapsedSince(isoUtc: string, now = new Date()): string {
  const minutes = Math.max(0, Math.floor((now.getTime() - new Date(isoUtc).getTime()) / 60000));

  if (minutes < 60) {
    return `${minutes} min`;
  }

  const hours = Math.floor(minutes / 60);

  if (hours < 24) {
    const rest = minutes % 60;

    return rest ? `${hours} h ${rest} min` : `${hours} h`;
  }

  const days = Math.floor(hours / 24);
  const restHours = hours % 24;
  const dayPart = `${days} ${daysLabel(days)}`;

  return restHours ? `${dayPart} ${restHours} h` : dayPart;
}

/** `HH:mm` from an ISO instant, in the browser's local time. */
export function timeOfDay(isoUtc: string): string {
  return new Date(isoUtc).toLocaleTimeString('sr-RS', { hour: '2-digit', minute: '2-digit' });
}

/**
 * `HH:mm` from a .NET `TimeOnly`, which arrives as `HH:mm:ss`.
 *
 * Not a date at all — a shift template's hours are a wall clock, so they must not be run through
 * anything that would apply a time zone to them.
 */
export function shortTime(timeOnly: string): string {
  return timeOnly.slice(0, 5);
}
