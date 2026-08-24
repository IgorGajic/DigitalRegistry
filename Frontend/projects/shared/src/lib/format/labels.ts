import {
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
