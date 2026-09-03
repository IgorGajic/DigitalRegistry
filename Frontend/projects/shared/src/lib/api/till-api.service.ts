import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { API_BASE_URL } from '../config/tokens';
import {
  FixtureLayoutRequest,
  FloorPlanDto,
  GenerateScheduleResultDto,
  InventoryValuationDto,
  LicenseStatusDto,
  LowStockReportEntryDto,
  MenuItemDetailDto,
  MenuItemDto,
  OrderDto,
  OrderSummaryDto,
  ReceiptDto,
  ReservationDto,
  ReservationScheduleEntryDto,
  RoomDto,
  ServiceQueueDto,
  ServiceTicketDto,
  ShiftAssignmentDto,
  ShiftTemplateDto,
  StaffMemberDto,
  StockAdjustmentResultDto,
  StockEntryDto,
  StockMovementDto,
  TableDto,
  TableLayoutRequest,
  TableQrCodeSheetEntryDto,
  TopSellingItemDto,
  TransactionDto,
  TurnoverReportDto,
  VoidReportDto,
  VoidResultDto,
  WeeklyScheduleDto,
} from '../models/dtos';
import { OrderStatus, PaymentMethod, UserRole, WeekDays } from '../models/enums';

/** How a line is changed on an open tab. Only ever adds; taking off is a void. */
export enum OrderItemChange {
  Add = 1,
  IncreaseQuantity = 2,
  ChangeNotes = 3,
}

/**
 * Every call the till makes.
 *
 * One service rather than one per feature: the surface is small enough to read in a sitting, and
 * having it in a single place makes it obvious when a screen is reaching for something the API does
 * not offer.
 */
@Injectable({ providedIn: 'root' })
export class TillApiService {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);

  // ------------------------------------------------------------------------------------- licence

  licenseStatus(): Observable<LicenseStatusDto> {
    return this.http.get<LicenseStatusDto>(`${this.base}/api/license/status`);
  }

  // ---------------------------------------------------------------------------------- floor plan

  floorPlan(includeInactive = false): Observable<FloorPlanDto> {
    return this.http.get<FloorPlanDto>(`${this.base}/api/floor-plan`, {
      params: new HttpParams().set('includeInactive', includeInactive),
    });
  }

  createRoom(body: {
    name: string;
    displayOrder?: number | null;
    canvasWidth?: number | null;
    canvasHeight?: number | null;
  }): Observable<RoomDto> {
    return this.http.post<RoomDto>(`${this.base}/api/floor-plan/rooms`, body);
  }

  updateRoom(
    id: string,
    body: { name: string; displayOrder: number; canvasWidth: number; canvasHeight: number },
  ): Observable<RoomDto> {
    return this.http.put<RoomDto>(`${this.base}/api/floor-plan/rooms/${id}`, { id, ...body });
  }

  deleteRoom(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/api/floor-plan/rooms/${id}`);
  }

  /**
   * What guests have ordered from their phones and nobody has carried out yet, oldest first.
   */
  serviceQueue(): Observable<ServiceQueueDto> {
    return this.http.get<ServiceQueueDto>(`${this.base}/api/orders/service-queue`);
  }

  /**
   * Takes a round off the queue.
   *
   * Does not close the tab: the table stays occupied and still owes what it owes. This records that
   * the drinks reached it, nothing more.
   */
  markOrderServed(orderId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/api/orders/${orderId}/served`, {});
  }

  /** Puts a round back on the queue after it was ticked off by mistake. */
  reopenOrderForService(orderId: string): Observable<void> {
    return this.http.post<void>(`${this.base}/api/orders/${orderId}/unserved`, {});
  }

  /**
   * Saves a room's whole arrangement.
   *
   * Absence means different things for the two lists, which is the API's rule and not this method's:
   * a table left out is taken out of the room, a fixture left out is deleted. A table outlives its
   * room because it carries order history; a landmark drawn nowhere is nothing at all.
   */
  saveRoomLayout(
    roomId: string,
    tables: TableLayoutRequest[],
    fixtures: FixtureLayoutRequest[],
  ): Observable<RoomDto> {
    return this.http.put<RoomDto>(`${this.base}/api/floor-plan/rooms/${roomId}/layout`, {
      roomId,
      tables,
      fixtures,
    });
  }

  // ------------------------------------------------------------------------------------- tables

  table(id: string): Observable<TableDto> {
    return this.http.get<TableDto>(`${this.base}/api/tables/${id}`);
  }

  createTable(body: { tableNumber: number; capacity: number }): Observable<TableDto> {
    return this.http.post<TableDto>(`${this.base}/api/tables`, body);
  }

  updateTable(
    id: string,
    body: { tableNumber: number; capacity: number; isActive: boolean },
  ): Observable<void> {
    return this.http.put<void>(`${this.base}/api/tables/${id}`, { id, ...body });
  }

  deleteTable(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/api/tables/${id}`);
  }

  /**
   * Every table's QR token, for printing the codes that go on the tables.
   *
   * Manager and owner only, because the token is a credential rather than a label.
   */
  tableQrCodes(roomId?: string, includeInactive = false): Observable<TableQrCodeSheetEntryDto[]> {
    let params = new HttpParams().set('includeInactive', includeInactive);

    if (roomId) {
      params = params.set('roomId', roomId);
    }

    return this.http.get<TableQrCodeSheetEntryDto[]>(`${this.base}/api/tables/qr-codes`, { params });
  }

  rotateQrCode(id: string): Observable<{ tableId: string; tableNumber: number; qrCodeToken: string }> {
    return this.http.post<{ tableId: string; tableNumber: number; qrCodeToken: string }>(
      `${this.base}/api/tables/${id}/qr-code`,
      {},
    );
  }

  // --------------------------------------------------------------------------------------- menu

  menu(category?: string): Observable<MenuItemDto[]> {
    let params = new HttpParams();

    if (category) {
      params = params.set('category', category);
    }

    return this.http.get<MenuItemDto[]>(`${this.base}/api/menu`, { params });
  }

  menuItem(id: string): Observable<MenuItemDetailDto> {
    return this.http.get<MenuItemDetailDto>(`${this.base}/api/menu/items/${id}`);
  }

  saveMenuItem(body: {
    id?: string | null;
    name: string;
    category: string;
    unitPrice: number;
    isAvailable: boolean;
  }): Observable<MenuItemDetailDto> {
    return this.http.post<MenuItemDetailDto>(`${this.base}/api/menu/items`, body);
  }

  setRecipe(
    menuItemId: string,
    lines: { ingredientId: string; quantityRequired: number }[],
  ): Observable<MenuItemDetailDto> {
    return this.http.put<MenuItemDetailDto>(`${this.base}/api/menu/items/${menuItemId}/recipe`, {
      menuItemId,
      lines,
    });
  }

  deleteMenuItem(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/api/menu/items/${id}`);
  }

  // ------------------------------------------------------------------------------------- orders

  order(id: string): Observable<OrderDto> {
    return this.http.get<OrderDto>(`${this.base}/api/orders/${id}`);
  }

  /**
   * The bills opened in a period, newest first.
   *
   * How a settled bill is found again once its receipt has been closed — to reprint it, or to
   * reverse it. Omit the window and the API answers for the current day.
   */
  orders(options: {
    fromUtc?: string;
    toUtc?: string;
    status?: OrderStatus | null;
    tableId?: string | null;
    take?: number;
  } = {}): Observable<OrderSummaryDto[]> {
    let params = new HttpParams();

    if (options.fromUtc) {
      params = params.set('from', options.fromUtc);
    }

    if (options.toUtc) {
      params = params.set('to', options.toUtc);
    }

    if (options.status) {
      params = params.set('status', options.status);
    }

    if (options.tableId) {
      params = params.set('tableId', options.tableId);
    }

    if (options.take) {
      params = params.set('take', options.take);
    }

    return this.http.get<OrderSummaryDto[]>(`${this.base}/api/orders`, { params });
  }

  openOrder(
    tableId: string,
    items: { menuItemId: string; quantity: number; notes?: string | null }[],
  ): Observable<OrderDto> {
    return this.http.post<OrderDto>(`${this.base}/api/orders`, { tableId, items });
  }

  addLine(
    orderId: string,
    menuItemId: string,
    quantity: number,
    notes?: string | null,
  ): Observable<OrderDto> {
    return this.http.patch<OrderDto>(`${this.base}/api/orders/${orderId}/items`, {
      orderId,
      change: OrderItemChange.Add,
      menuItemId,
      quantity,
      notes,
    });
  }

  /** Only upwards; reducing a line is a void, which needs a reason. */
  increaseLine(orderId: string, orderItemId: string, quantity: number): Observable<OrderDto> {
    return this.http.patch<OrderDto>(`${this.base}/api/orders/${orderId}/items`, {
      orderId,
      change: OrderItemChange.IncreaseQuantity,
      orderItemId,
      quantity,
    });
  }

  changeNotes(orderId: string, orderItemId: string, notes: string | null): Observable<OrderDto> {
    return this.http.patch<OrderDto>(`${this.base}/api/orders/${orderId}/items`, {
      orderId,
      change: OrderItemChange.ChangeNotes,
      orderItemId,
      notes,
    });
  }

  pay(orderId: string, paymentMethod: PaymentMethod): Observable<TransactionDto> {
    return this.http.post<TransactionDto>(`${this.base}/api/orders/${orderId}/payment`, {
      orderId,
      paymentMethod,
    });
  }

  receipt(orderId: string): Observable<ReceiptDto> {
    return this.http.get<ReceiptDto>(`${this.base}/api/orders/${orderId}/receipt`);
  }

  voidItem(
    orderId: string,
    itemId: string,
    reason: string,
    quantity?: number | null,
  ): Observable<VoidResultDto> {
    return this.http.post<VoidResultDto>(
      `${this.base}/api/orders/${orderId}/items/${itemId}/void`,
      { reason, quantity },
    );
  }

  voidOrder(orderId: string, reason: string): Observable<VoidResultDto> {
    return this.http.post<VoidResultDto>(`${this.base}/api/orders/${orderId}/void`, { reason });
  }

  reverseOrder(orderId: string, reason: string): Observable<VoidResultDto> {
    return this.http.post<VoidResultDto>(`${this.base}/api/orders/${orderId}/reverse`, { reason });
  }

  // ---------------------------------------------------------------------------------- inventory

  lowStock(): Observable<LowStockReportEntryDto[]> {
    return this.http.get<LowStockReportEntryDto[]>(`${this.base}/api/inventory/low-stock`);
  }

  recordStockEntry(body: {
    ingredientId: string;
    quantity: number;
    purchaseUnitPrice: number;
    totalCost?: number | null;
    supplier?: string | null;
    referenceNumber?: string | null;
    note?: string | null;
  }): Observable<StockEntryDto> {
    return this.http.post<StockEntryDto>(`${this.base}/api/inventory/entries`, body);
  }

  stockEntries(fromUtc: string, toUtc: string, ingredientId?: string): Observable<StockEntryDto[]> {
    let params = new HttpParams().set('from', fromUtc).set('to', toUtc);

    if (ingredientId) {
      params = params.set('ingredientId', ingredientId);
    }

    return this.http.get<StockEntryDto[]>(`${this.base}/api/inventory/entries`, { params });
  }

  stockMovements(
    fromUtc: string,
    toUtc: string,
    ingredientId?: string,
  ): Observable<StockMovementDto[]> {
    let params = new HttpParams().set('from', fromUtc).set('to', toUtc);

    if (ingredientId) {
      params = params.set('ingredientId', ingredientId);
    }

    return this.http.get<StockMovementDto[]>(`${this.base}/api/inventory/movements`, { params });
  }

  adjustStock(
    ingredientId: string,
    countedQuantity: number,
    reason: string,
  ): Observable<StockAdjustmentResultDto> {
    return this.http.post<StockAdjustmentResultDto>(
      `${this.base}/api/inventory/ingredients/${ingredientId}/adjust`,
      { ingredientId, countedQuantity, reason },
    );
  }

  // ------------------------------------------------------------------------------------- shifts

  shiftTemplates(includeRetired = false): Observable<ShiftTemplateDto[]> {
    return this.http.get<ShiftTemplateDto[]>(`${this.base}/api/shifts/templates`, {
      params: new HttpParams().set('includeRetired', includeRetired),
    });
  }

  saveShiftTemplate(body: {
    id?: string | null;
    name: string;
    startTime: string;
    endTime: string;
    isActive: boolean;
  }): Observable<ShiftTemplateDto> {
    return this.http.post<ShiftTemplateDto>(`${this.base}/api/shifts/templates`, body);
  }

  shiftAssignments(waiterId?: string, onDate?: string): Observable<ShiftAssignmentDto[]> {
    let params = new HttpParams();

    if (waiterId) {
      params = params.set('waiterId', waiterId);
    }

    if (onDate) {
      params = params.set('onDate', onDate);
    }

    return this.http.get<ShiftAssignmentDto[]>(`${this.base}/api/shifts/assignments`, { params });
  }

  saveShiftAssignment(body: {
    id?: string | null;
    waiterId: string;
    shiftTemplateId: string;
    days: WeekDays;
    validFrom: string;
    validTo: string | null;
  }): Observable<ShiftAssignmentDto> {
    return this.http.post<ShiftAssignmentDto>(`${this.base}/api/shifts/assignments`, body);
  }

  deleteShiftAssignment(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/api/shifts/assignments/${id}`);
  }

  generateSchedule(
    fromDate: string,
    toDate: string,
    waiterId?: string | null,
  ): Observable<GenerateScheduleResultDto> {
    return this.http.post<GenerateScheduleResultDto>(`${this.base}/api/shifts/generate`, {
      fromDate,
      toDate,
      waiterId,
    });
  }

  weeklySchedule(date: string): Observable<WeeklyScheduleDto> {
    return this.http.get<WeeklyScheduleDto>(`${this.base}/api/shifts/week`, {
      params: new HttpParams().set('date', date),
    });
  }

  deleteShift(id: string): Observable<void> {
    return this.http.delete<void>(`${this.base}/api/shifts/${id}`);
  }

  // -------------------------------------------------------------------------------------- staff

  staff(includeDisabled = false): Observable<StaffMemberDto[]> {
    return this.http.get<StaffMemberDto[]>(`${this.base}/api/staff`, {
      params: new HttpParams().set('includeDisabled', includeDisabled),
    });
  }

  createStaff(body: {
    email: string;
    password: string;
    firstName: string;
    lastName: string;
    role: UserRole;
  }): Observable<StaffMemberDto> {
    return this.http.post<StaffMemberDto>(`${this.base}/api/staff`, body);
  }

  updateStaff(
    id: string,
    body: { firstName: string; lastName: string; role: UserRole },
  ): Observable<StaffMemberDto> {
    return this.http.put<StaffMemberDto>(`${this.base}/api/staff/${id}`, { id, ...body });
  }

  setStaffEnabled(id: string, enabled: boolean): Observable<void> {
    return this.http.post<void>(
      `${this.base}/api/staff/${id}/${enabled ? 'enable' : 'disable'}`,
      {},
    );
  }

  resetStaffPassword(id: string, newPassword: string): Observable<void> {
    return this.http.post<void>(`${this.base}/api/staff/${id}/password`, { newPassword });
  }

  // ------------------------------------------------------------------------------- reservations

  /**
   * The day's service sheet.
   *
   * @param date The day, as `yyyy-MM-dd`; the API reads it in UTC and defaults to today.
   */
  reservationSchedule(date?: string, tableId?: string): Observable<ReservationScheduleEntryDto[]> {
    let params = new HttpParams();

    if (date) {
      params = params.set('date', date);
    }

    if (tableId) {
      params = params.set('tableId', tableId);
    }

    return this.http.get<ReservationScheduleEntryDto[]>(`${this.base}/api/reservations/schedule`, {
      params,
    });
  }

  /**
   * Books a table for a named guest.
   *
   * Staff must give a name: without one the API would have to file the booking under whoever is
   * signed in, which is the thing this endpoint exists to avoid.
   */
  createReservation(body: {
    tableId: string;
    startTime: string;
    endTime: string;
    partySize: number;
    contactName: string;
    contactPhone?: string | null;
  }): Observable<ReservationDto> {
    return this.http.post<ReservationDto>(`${this.base}/api/reservations`, body);
  }

  checkInReservation(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/api/reservations/${id}/check-in`, {});
  }

  /** Manager and owner only — a waiter may seat a booking but not cancel one. */
  cancelReservation(id: string): Observable<void> {
    return this.http.post<void>(`${this.base}/api/reservations/${id}/cancel`, {});
  }

  // ------------------------------------------------------------------------------------ reports

  turnover(fromDate: string, toDate: string): Observable<TurnoverReportDto> {
    return this.http.get<TurnoverReportDto>(`${this.base}/api/reports/turnover`, {
      params: new HttpParams().set('from', fromDate).set('to', toDate),
    });
  }

  topItems(
    fromUtc: string,
    toUtc: string,
    category?: string,
    top = 20,
  ): Observable<TopSellingItemDto[]> {
    let params = new HttpParams().set('from', fromUtc).set('to', toUtc).set('top', top);

    if (category) {
      params = params.set('category', category);
    }

    return this.http.get<TopSellingItemDto[]>(`${this.base}/api/reports/top-items`, { params });
  }

  inventoryValuation(
    fromUtc: string,
    toUtc: string,
    lowStockOnly = false,
  ): Observable<InventoryValuationDto> {
    return this.http.get<InventoryValuationDto>(`${this.base}/api/reports/inventory`, {
      params: new HttpParams()
        .set('from', fromUtc)
        .set('to', toUtc)
        .set('lowStockOnly', lowStockOnly),
    });
  }

  voidReport(fromUtc: string, toUtc: string, performedByUserId?: string): Observable<VoidReportDto> {
    let params = new HttpParams().set('from', fromUtc).set('to', toUtc);

    if (performedByUserId) {
      params = params.set('performedByUserId', performedByUserId);
    }

    return this.http.get<VoidReportDto>(`${this.base}/api/reports/voids`, { params });
  }
}
