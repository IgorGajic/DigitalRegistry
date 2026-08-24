/**
 * The API's response shapes.
 *
 * Hand-written rather than generated, and kept in the shared library so the till and the master
 * application cannot drift apart on what a licence or a restaurant looks like.
 */
import {
  LicensePlan,
  LicenseStatus,
  OrderStatus,
  PaymentMethod,
  ReservationStatus,
  StockMovementType,
  TableShape,
  TableStatus,
  UnitOfMeasure,
  UserRole,
  VoidType,
  WeekDays,
} from './enums';

// ---------------------------------------------------------------------------------- authentication

export interface AuthenticationResult {
  accessToken: string;
  expiresAtUtc: string;
  userId: string | null;
  email: string | null;
  fullName: string | null;
  role: UserRole;
  restaurantId: string | null;
  restaurantSlug: string | null;
  tableId: string | null;
  /** Set only for a scanned table session, so the guest's screen can name the table. */
  tableNumber: number | null;
}

// ---------------------------------------------------------------------------------------- licence

export interface LicenseStatusDto {
  restaurantName: string;
  isValid: boolean;
  status: LicenseStatus;
  expiresAtUtc: string | null;
  daysRemaining: number;
  plan: LicensePlan | null;
  isExpiringSoon: boolean;
}

// ------------------------------------------------------------------------------------- floor plan

export interface FloorPlanTableDto {
  id: string;
  tableNumber: number;
  capacity: number;
  status: TableStatus;
  shape: TableShape;
  positionX: number;
  positionY: number;
  width: number;
  height: number;
  rotation: number;
  isActive: boolean;
  /** The tabs running on the table, oldest first — how the till reopens a bill it did not open. */
  openOrderIds: string[];
  openOrderTotal: number;
  oldestOpenOrderAtUtc: string | null;
}

export interface RoomDto {
  id: string;
  name: string;
  displayOrder: number;
  canvasWidth: number;
  canvasHeight: number;
  tables: FloorPlanTableDto[];
}

export interface FloorPlanDto {
  rooms: RoomDto[];
  unplacedTables: FloorPlanTableDto[];
}

export interface TableLayoutRequest {
  tableId: string;
  positionX: number;
  positionY: number;
  width: number;
  height: number;
  shape: TableShape;
  rotation: number;
}

export interface TableDto {
  id: string;
  tableNumber: number;
  capacity: number;
  qrCodeToken: string;
  isActive: boolean;
}

// ------------------------------------------------------------------------------------------- menu

export interface MenuItemDto {
  id: string;
  name: string;
  category: string;
  unitPrice: number;
  isAvailable: boolean;
}

export interface RecipeLineDto {
  ingredientId: string;
  ingredientName: string;
  quantityRequired: number;
  unit: UnitOfMeasure;
  stockQuantity: number;
  lineCost: number;
}

export interface MenuItemDetailDto {
  id: string;
  name: string;
  category: string;
  unitPrice: number;
  isAvailable: boolean;
  costPrice: number;
  marginPercent: number | null;
  recipe: RecipeLineDto[];
}

// ----------------------------------------------------------------------------------------- orders

export interface OrderItemDto {
  id: string;
  menuItemId: string;
  menuItemName: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  notes: string | null;
}

export interface OrderDto {
  id: string;
  tableId: string;
  tableNumber: number;
  waiterId: string | null;
  status: OrderStatus;
  total: number;
  createdAt: string;
  items: OrderItemDto[];
}

export interface TransactionDto {
  id: string;
  orderId: string;
  processedByWaiterId: string;
  amount: number;
  paymentMethod: PaymentMethod;
  transactionDate: string;
}

export interface VoidResultDto {
  voidRecordId: string;
  orderId: string;
  type: VoidType;
  itemName: string | null;
  quantity: number;
  amount: number;
  remainingTotal: number;
  orderStatus: OrderStatus;
  reason: string;
  voidedAtUtc: string;
}

export interface ReceiptLineDto {
  name: string;
  quantity: number;
  unitPrice: number;
  lineTotal: number;
  notes: string | null;
}

export interface ReceiptDto {
  orderId: string;
  number: string;
  restaurantName: string;
  restaurantAddress: string | null;
  restaurantPhone: string | null;
  currencyCode: string;
  tableNumber: number;
  servedBy: string | null;
  openedAtUtc: string;
  paidAtUtc: string | null;
  paymentMethod: PaymentMethod | null;
  status: OrderStatus;
  isReversed: boolean;
  total: number;
  lines: ReceiptLineDto[];
}

// -------------------------------------------------------------------------------------- inventory

export interface IngredientStockDto {
  id: string;
  name: string;
  stockQuantity: number;
  unit: UnitOfMeasure;
  lowStockThreshold: number;
  isLowOnStock: boolean;
}

export interface LowStockReportEntryDto {
  id: string;
  name: string;
  stockQuantity: number;
  unit: UnitOfMeasure;
  lowStockThreshold: number;
  blockedMenuItems: string[];
}

export interface StockEntryDto {
  id: string;
  ingredientId: string;
  ingredientName: string;
  quantity: number;
  unit: UnitOfMeasure;
  purchaseUnitPrice: number;
  totalCost: number;
  supplier: string | null;
  referenceNumber: string | null;
  note: string | null;
  recordedBy: string;
  entryDateUtc: string;
  stockAfter: number;
  averagePurchasePriceAfter: number;
}

export interface StockMovementDto {
  id: string;
  ingredientId: string;
  ingredientName: string;
  type: StockMovementType;
  quantity: number;
  balanceAfter: number;
  unit: UnitOfMeasure;
  orderId: string | null;
  note: string | null;
  occurredAtUtc: string;
}

export interface StockAdjustmentResultDto {
  ingredientId: string;
  ingredientName: string;
  previousQuantity: number;
  countedQuantity: number;
  difference: number;
  unit: UnitOfMeasure;
  isLowOnStock: boolean;
}

// ----------------------------------------------------------------------------------------- shifts

export interface ShiftTemplateDto {
  id: string;
  name: string;
  startTime: string;
  endTime: string;
  crossesMidnight: boolean;
  durationHours: number;
  isActive: boolean;
  assignmentCount: number;
}

export interface ShiftAssignmentDto {
  id: string;
  waiterId: string;
  waiterName: string;
  shiftTemplateId: string;
  shiftTemplateName: string;
  startTime: string;
  endTime: string;
  days: WeekDays;
  validFrom: string;
  validTo: string | null;
}

export interface ScheduledShiftDto {
  id: string;
  date: string;
  startUtc: string;
  endUtc: string;
  hours: number;
  shiftTemplateName: string | null;
  isGenerated: boolean;
}

export interface WaiterWeekDto {
  waiterId: string;
  waiterName: string;
  totalHours: number;
  shifts: ScheduledShiftDto[];
}

export interface WeeklyScheduleDto {
  weekStart: string;
  days: string[];
  waiters: WaiterWeekDto[];
}

export interface ScheduleConflictDto {
  date: string;
  waiterId: string;
  waiterName: string;
  shiftTemplateName: string;
  startUtc: string;
  endUtc: string;
  reason: string;
}

export interface GenerateScheduleResultDto {
  fromDate: string;
  toDate: string;
  created: number;
  alreadyPresent: number;
  conflicts: ScheduleConflictDto[];
}

// ----------------------------------------------------------------------------------- reservations

export interface ReservationDto {
  id: string;
  tableId: string;
  tableNumber: number;
  startTime: string;
  endTime: string;
  partySize: number;
  status: ReservationStatus;
}

/**
 * A booking on the day's service sheet.
 *
 * Carries the guest's name, which the guest-facing {@link ReservationDto} does not: the sheet is
 * staff-only for exactly that reason.
 */
export interface ReservationScheduleEntryDto extends ReservationDto {
  guestId: string;
  guestName: string;
}

// ------------------------------------------------------------------------------------------ staff

export interface StaffMemberDto {
  id: string;
  fullName: string;
  email: string;
  userName: string;
  role: UserRole;
  isEnabled: boolean;
  created: string;
}

// ---------------------------------------------------------------------------------------- reports

export interface DailyTurnoverDto {
  date: string;
  turnover: number;
  cash: number;
  card: number;
  digitalWallet: number;
  billCount: number;
  averageBill: number;
  reversedAmount: number;
  reversalCount: number;
}

export interface TurnoverReportDto {
  fromDate: string;
  toDate: string;
  turnover: number;
  cash: number;
  card: number;
  digitalWallet: number;
  billCount: number;
  averageBill: number;
  days: DailyTurnoverDto[];
}

export interface TopSellingItemDto {
  menuItemId: string;
  name: string;
  category: string;
  quantitySold: number;
  revenue: number;
  estimatedCost: number | null;
  estimatedMargin: number | null;
}

export interface InventoryValuationLineDto {
  ingredientId: string;
  name: string;
  unit: UnitOfMeasure;
  stockQuantity: number;
  averagePurchasePrice: number;
  stockValue: number;
  lowStockThreshold: number;
  isLowOnStock: boolean;
  consumedQuantity: number;
  consumedValue: number;
  purchasedQuantity: number;
  purchasedValue: number;
  adjustedQuantity: number;
}

export interface InventoryValuationDto {
  fromUtc: string;
  toUtc: string;
  totalStockValue: number;
  totalConsumedValue: number;
  totalPurchasedValue: number;
  lowStockCount: number;
  lines: InventoryValuationLineDto[];
}

export interface VoidReportEntryDto {
  id: string;
  voidedAtUtc: string;
  type: VoidType;
  orderId: string;
  tableNumber: number | null;
  itemName: string | null;
  quantity: number;
  amount: number;
  reason: string;
  performedBy: string;
  approvedBy: string | null;
}

export interface VoidsByStaffDto {
  userId: string;
  name: string;
  voidCount: number;
  totalAmount: number;
  itemVoids: number;
  openOrderVoids: number;
  paidOrderVoids: number;
}

export interface VoidReportDto {
  fromUtc: string;
  toUtc: string;
  totalVoids: number;
  totalAmount: number;
  byStaff: VoidsByStaffDto[];
  entries: VoidReportEntryDto[];
}

// --------------------------------------------------------------------------------------- platform

export interface RestaurantSummaryDto {
  id: string;
  name: string;
  slug: string;
  contactEmail: string | null;
  phoneNumber: string | null;
  address: string | null;
  currencyCode: string;
  /** Shown so it can be corrected: shift generation reads it, and it is otherwise set once and unseen. */
  timeZoneId: string;
  isActive: boolean;
  created: string;
  licenseStatus: LicenseStatus;
  licenseExpiresAtUtc: string | null;
  daysRemaining: number;
  plan: LicensePlan | null;
  staffCount: number;
}

export interface LicenseDto {
  id: string;
  restaurantId: string;
  restaurantName: string;
  plan: LicensePlan;
  termMonths: number;
  startsAtUtc: string;
  expiresAtUtc: string;
  status: LicenseStatus;
  daysRemaining: number;
  price: number;
  amountPaid: number;
  notes: string | null;
}

export interface LicensePaymentDto {
  id: string;
  licenseId: string;
  amount: number;
  paidAtUtc: string;
  paymentMethod: PaymentMethod;
  referenceNumber: string | null;
  notes: string | null;
}

export interface CreatedUserDto {
  id: string;
  email: string;
  userName: string;
  fullName: string;
  role: UserRole;
}

export interface MonthlyRevenueDto {
  year: number;
  month: number;
  amount: number;
  paymentCount: number;
}

export interface PlatformDashboardDto {
  totalRestaurants: number;
  activeRestaurants: number;
  activeLicenses: number;
  expiredLicenses: number;
  suspendedLicenses: number;
  expiringSoon: number;
  totalLicenseRevenue: number;
  monthlyLicenseRevenue: MonthlyRevenueDto[];
  expiringRestaurants: RestaurantSummaryDto[];
}
