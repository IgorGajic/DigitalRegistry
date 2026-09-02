import {
  FixtureKind,
  FixtureShape,
  FixtureTone,
  LicensePlan,
  LicenseStatus,
  OrderStatus,
  PaymentMethod,
  ReservationStatus,
  StockMovementType,
  TableStatus,
  UnitOfMeasure,
  UserRole,
  VoidType,
  WeekDays,
} from '../models/enums';

/**
 * Serbian labels for the API's enums.
 *
 * Kept here rather than scattered through templates so a term is worded the same on every screen —
 * a waiter should not meet "storniran" in one place and "poništen" in another for the same state.
 */

export const userRoleLabels: Record<UserRole, string> = {
  [UserRole.Guest]: 'Gost',
  [UserRole.Waiter]: 'Konobar',
  [UserRole.Manager]: 'Menadžer',
  [UserRole.Owner]: 'Vlasnik',
  [UserRole.PlatformAdmin]: 'Administrator platforme',
};

export const tableStatusLabels: Record<TableStatus, string> = {
  [TableStatus.Available]: 'Slobodan',
  [TableStatus.Reserved]: 'Rezervisan',
  [TableStatus.Occupied]: 'Zauzet',
  [TableStatus.OutOfService]: 'Van upotrebe',
};

export const orderStatusLabels: Record<OrderStatus, string> = {
  [OrderStatus.Open]: 'Otvoren',
  [OrderStatus.InPreparation]: 'U pripremi',
  [OrderStatus.Served]: 'Servirano',
  [OrderStatus.Paid]: 'Plaćen',
  [OrderStatus.Cancelled]: 'Otkazan',
  [OrderStatus.Voided]: 'Storniran',
};

export const paymentMethodLabels: Record<PaymentMethod, string> = {
  [PaymentMethod.Cash]: 'Gotovina',
  [PaymentMethod.Card]: 'Kartica',
  [PaymentMethod.DigitalWallet]: 'Digitalni novčanik',
};

export const reservationStatusLabels: Record<ReservationStatus, string> = {
  [ReservationStatus.Pending]: 'Na čekanju',
  [ReservationStatus.Confirmed]: 'Potvrđena',
  [ReservationStatus.Cancelled]: 'Otkazana',
  [ReservationStatus.Completed]: 'Gost stigao',
};

export const voidTypeLabels: Record<VoidType, string> = {
  [VoidType.Item]: 'Stavka',
  [VoidType.OpenOrder]: 'Otvoren račun',
  [VoidType.PaidOrder]: 'Plaćen račun',
};

export const fixtureKindLabels: Record<FixtureKind, string> = {
  [FixtureKind.Bar]: 'Šank',
  [FixtureKind.Restroom]: 'Toalet',
  [FixtureKind.Entrance]: 'Ulaz',
  [FixtureKind.Kitchen]: 'Kuhinja',
  [FixtureKind.Stairs]: 'Stepenice',
  [FixtureKind.Partition]: 'Zid',
  [FixtureKind.Other]: 'Ostalo',
};

export const fixtureShapeLabels: Record<FixtureShape, string> = {
  [FixtureShape.Rectangle]: 'Pravougaonik',
  [FixtureShape.Ellipse]: 'Krug',
};

export const fixtureToneLabels: Record<FixtureTone, string> = {
  [FixtureTone.Wood]: 'Drvo',
  [FixtureTone.Slate]: 'Tamno siva',
  [FixtureTone.Stone]: 'Svetlo siva',
  [FixtureTone.Glass]: 'Staklo',
};

/**
 * What a newly drawn fixture starts as.
 *
 * The kind seeds a label, a shape, a tone and a size, so placing a bar takes one click rather than
 * five. Every one of them is then editable — a venue with two restrooms needs "WC M" and "WC Ž",
 * and no list of kinds can know that.
 */
export const fixtureDefaults: Record<
  FixtureKind,
  { label: string; shape: FixtureShape; tone: FixtureTone; width: number; height: number }
> = {
  [FixtureKind.Bar]: {
    label: 'Šank',
    shape: FixtureShape.Rectangle,
    tone: FixtureTone.Wood,
    width: 400,
    height: 70,
  },
  [FixtureKind.Restroom]: {
    label: 'Toalet',
    shape: FixtureShape.Rectangle,
    tone: FixtureTone.Slate,
    width: 140,
    height: 120,
  },
  [FixtureKind.Entrance]: {
    label: 'Ulaz',
    shape: FixtureShape.Rectangle,
    tone: FixtureTone.Glass,
    width: 120,
    height: 30,
  },
  [FixtureKind.Kitchen]: {
    label: 'Kuhinja',
    shape: FixtureShape.Rectangle,
    tone: FixtureTone.Slate,
    width: 260,
    height: 160,
  },
  [FixtureKind.Stairs]: {
    label: 'Stepenice',
    shape: FixtureShape.Rectangle,
    tone: FixtureTone.Stone,
    width: 120,
    height: 200,
  },
  [FixtureKind.Partition]: {
    label: 'Zid',
    shape: FixtureShape.Rectangle,
    tone: FixtureTone.Stone,
    width: 300,
    height: 24,
  },
  [FixtureKind.Other]: {
    label: 'Element',
    shape: FixtureShape.Rectangle,
    tone: FixtureTone.Stone,
    width: 160,
    height: 100,
  },
};

export const unitLabels: Record<UnitOfMeasure, string> = {
  [UnitOfMeasure.Milliliters]: 'ml',
  [UnitOfMeasure.Grams]: 'g',
  [UnitOfMeasure.Units]: 'kom',
};

export const stockMovementLabels: Record<StockMovementType, string> = {
  [StockMovementType.Purchase]: 'Nabavka',
  [StockMovementType.Sale]: 'Prodaja',
  [StockMovementType.Return]: 'Povraćaj',
  [StockMovementType.Adjustment]: 'Korekcija',
};

export const licenseStatusLabels: Record<LicenseStatus, string> = {
  [LicenseStatus.Active]: 'Aktivna',
  [LicenseStatus.Expired]: 'Istekla',
  [LicenseStatus.Suspended]: 'Suspendovana',
  [LicenseStatus.Cancelled]: 'Otkazana',
};

export const licensePlanLabels: Record<LicensePlan, string> = {
  [LicensePlan.Monthly]: 'Mesečna',
  [LicensePlan.Quarterly]: 'Tromesečna',
  [LicensePlan.SemiAnnual]: 'Šestomesečna',
  [LicensePlan.Annual]: 'Godišnja',
};

/** Monday first, as a rota is read here — not Sunday first as the bit positions run. */
export const weekDayOrder: { flag: WeekDays; short: string; long: string }[] = [
  { flag: WeekDays.Monday, short: 'Pon', long: 'Ponedeljak' },
  { flag: WeekDays.Tuesday, short: 'Uto', long: 'Utorak' },
  { flag: WeekDays.Wednesday, short: 'Sre', long: 'Sreda' },
  { flag: WeekDays.Thursday, short: 'Čet', long: 'Četvrtak' },
  { flag: WeekDays.Friday, short: 'Pet', long: 'Petak' },
  { flag: WeekDays.Saturday, short: 'Sub', long: 'Subota' },
  { flag: WeekDays.Sunday, short: 'Ned', long: 'Nedelja' },
];

/** Renders a day set the way a manager would say it: "Pon–Pet", "Vikend", or a list. */
export function describeDays(days: WeekDays): string {
  if (days === WeekDays.All) {
    return 'Svaki dan';
  }

  if (days === WeekDays.Weekdays) {
    return 'Pon–Pet';
  }

  if (days === WeekDays.Weekend) {
    return 'Vikend';
  }

  const named = weekDayOrder.filter((day) => (days & day.flag) !== 0).map((day) => day.short);

  return named.length ? named.join(', ') : '—';
}

/**
 * Picks the Serbian plural form for a count.
 *
 * Serbian has three, not two: 1 stavka, 2–4 stavke, 5+ stavki — and the teens break the pattern, so
 * 21 takes the singular while 11 does not. A two-way `count === 1 ? … : …` gets the common cases
 * wrong, and "3 stavki" reads as broken to anybody using the till.
 *
 * @param one Form for 1, 21, 31 … — anything ending in 1 that is not 11.
 * @param few Form for 2–4, 22–24 … — anything ending in 2–4 that is not 12–14.
 * @param many Form for everything else, including 0 and the teens.
 */
export function plural(count: number, one: string, few: string, many: string): string {
  const last = Math.abs(count) % 10;
  const lastTwo = Math.abs(count) % 100;

  if (last === 1 && lastTwo !== 11) {
    return one;
  }

  if (last >= 2 && last <= 4 && (lastTwo < 12 || lastTwo > 14)) {
    return few;
  }

  return many;
}

/** "stavka" / "stavke" / "stavki", the count this application says most often. */
export function itemsLabel(count: number): string {
  return plural(count, 'stavka', 'stavke', 'stavki');
}

/** "dan" / "dana" / "dana" — the middle and plural forms coincide here. */
export function daysLabel(count: number): string {
  return plural(count, 'dan', 'dana', 'dana');
}

/** "sto" / "stola" / "stolova" — the one noun this application counts on every screen. */
export function tablesLabel(count: number): string {
  return plural(count, 'sto', 'stola', 'stolova');
}

/** "mesto" / "mesta" / "mesta", for a table's capacity. */
export function seatsLabel(count: number): string {
  return plural(count, 'mesto', 'mesta', 'mesta');
}

/** "nalog" / "naloga" / "naloga", for staff accounts. */
export function accountsLabel(count: number): string {
  return plural(count, 'nalog', 'naloga', 'naloga');
}

/** "račun" / "računa" / "računa", for a count of bills. */
export function billsLabel(count: number): string {
  return plural(count, 'račun', 'računa', 'računa');
}

/** "dodela" / "dodele" / "dodela" — a feminine noun, so the forms differ from the masculine ones. */
export function assignmentsLabel(count: number): string {
  return plural(count, 'dodela', 'dodele', 'dodela');
}
