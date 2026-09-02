/**
 * The backend's enums, mirrored.
 *
 * Numeric because that is what the API sends and accepts: `System.Text.Json` serialises enums as
 * their underlying value by default, and the values themselves are part of the contract — see the
 * C# definitions, several of which carry meaning in the number (a licence plan *is* its term in
 * months).
 */

export enum UserRole {
  Guest = 1,
  Waiter = 2,
  Manager = 3,
  Owner = 4,
  PlatformAdmin = 5,
}

export enum TableStatus {
  Available = 1,
  Reserved = 2,
  Occupied = 3,
  OutOfService = 4,
}

export enum TableShape {
  Round = 1,
  Rectangle = 2,
  Square = 3,
}

/**
 * The palette a venue's till is painted in.
 *
 * A closed list, not a colour picked freely: on the floor screen colour means status, and each
 * theme has had those four hues checked against its own surface. Mirrors the API's enum.
 */
export enum AppTheme {
  Petrol = 1,
  Charcoal = 2,
  Forest = 3,
  Sand = 4,
}

/**
 * What a room fixture is — the things drawn on a floor plan that are not tables.
 *
 * The kind carries no rule. It seeds the label, size and tone the editor starts a new fixture with,
 * for the handful of landmarks every venue draws.
 */
export enum FixtureKind {
  Bar = 1,
  Restroom = 2,
  Entrance = 3,
  Kitchen = 4,
  Stairs = 5,
  Partition = 6,
  Other = 7,
}

/** A rectangle with equal sides is a square; an ellipse with equal sides is a circle. */
export enum FixtureShape {
  Rectangle = 1,
  Ellipse = 2,
}

/**
 * The colour a fixture is drawn in, named rather than given as a value.
 *
 * On the floor screen colour means status, so the set a fixture may take is closed — a free picker
 * would sooner or later produce a bar in the shade of an occupied table. Naming it also lets the
 * venue's chosen theme resolve it, which a stored colour value could not follow.
 */
export enum FixtureTone {
  Wood = 1,
  Slate = 2,
  Stone = 3,
  Glass = 4,
}

export enum OrderStatus {
  Open = 1,
  InPreparation = 2,
  Served = 3,
  Paid = 4,
  Cancelled = 5,
  Voided = 6,
}

export enum PaymentMethod {
  Cash = 1,
  Card = 2,
  DigitalWallet = 3,
}

export enum VoidType {
  Item = 1,
  OpenOrder = 2,
  PaidOrder = 3,
}

export enum UnitOfMeasure {
  Milliliters = 1,
  Grams = 2,
  Units = 3,
}

export enum StockMovementType {
  Purchase = 1,
  Sale = 2,
  Return = 3,
  Adjustment = 4,
}

export enum ReservationStatus {
  Pending = 1,
  Confirmed = 2,
  Cancelled = 3,
  Completed = 4,
}

/** The value is the term in months, so a renewal is simply "add this many". */
export enum LicensePlan {
  Monthly = 1,
  Quarterly = 3,
  SemiAnnual = 6,
  Annual = 12,
}

export enum LicenseStatus {
  Active = 1,
  Expired = 2,
  Suspended = 3,
  Cancelled = 4,
}

/**
 * Days of the week as a bit field, matching the backend's `[Flags]` enum.
 *
 * The bit positions follow JavaScript's `Date.getDay()` and .NET's `DayOfWeek`, both of which start
 * at Sunday, so converting is a shift rather than a lookup.
 */
export enum WeekDays {
  None = 0,
  Sunday = 1 << 0,
  Monday = 1 << 1,
  Tuesday = 1 << 2,
  Wednesday = 1 << 3,
  Thursday = 1 << 4,
  Friday = 1 << 5,
  Saturday = 1 << 6,
  Weekdays = Monday | Tuesday | Wednesday | Thursday | Friday,
  Weekend = Saturday | Sunday,
  All = Weekdays | Weekend,
}
