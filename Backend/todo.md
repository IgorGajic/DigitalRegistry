# DigitalRegistry — Backend Architecture & Implementation TODO

This document serves as the comprehensive specification and implementation tracking list for **DigitalRegistry**, a modern .NET 8 Clean Architecture backend system designed for automated bar and restaurant management.

---

## 1. System Architecture Overview

`DigitalRegistry` follows strict **Clean Architecture (Onion Architecture)** principles to decouple core domain logic from external dependencies, databases, frameworks, and UI components.

```text
       ┌────────────────────────────────────────┐
       │         DigitalRegistry.Api            │
       └───────────────────┬────────────────────┘
                           │
                           ▼
       ┌────────────────────────────────────────┐
       │     DigitalRegistry.Infrastructure     │
       └───────────────────┬────────────────────┘
                           │
                           ▼
       ┌────────────────────────────────────────┐
       │      DigitalRegistry.Application       │
       └───────────────────┬────────────────────┘
                           │
                           ▼
       ┌────────────────────────────────────────┐
       │        DigitalRegistry.Domain          │
       └────────────────────────────────────────┘
```

### Layer Responsibilities

* **`DigitalRegistry.Domain`**: Pure enterprise business logic. Contains domain entities, enums, domain events, value objects, and aggregate roots. Zero external package dependencies.
* **`DigitalRegistry.Application`**: Application business logic. Implements CQRS (Commands/Queries) using MediatR, DTO mappings, FluentValidation rules, pipeline behaviors, and interface definitions for repositories and services.
* **`DigitalRegistry.Infrastructure`**: Implementation of interfaces defined in Application. Handles EF Core persistence, database migrations, ASP.NET Core Identity, JWT authentication, SignalR hub dispatchers, and background processing.
* **`DigitalRegistry.Api`**: Web API host. Exposes RESTful HTTP endpoints, SignalR hubs, Swagger documentation, API versioning, global exception middleware, and authorization policies.

---

## 2. Directory & Folder Structure

```text
DigitalRegistry/
├── src/
│   ├── DigitalRegistry.Domain/
│   │   ├── Common/
│   │   │   ├── BaseEntity.cs
│   │   │   ├── AggregateRoot.cs
│   │   │   └── IDomainEvent.cs
│   │   ├── Entities/
│   │   │   ├── ApplicationUser.cs
│   │   │   ├── Table.cs
│   │   │   ├── Reservation.cs
│   │   │   ├── Shift.cs
│   │   │   ├── MenuItem.cs
│   │   │   ├── Ingredient.cs
│   │   │   ├── RecipeItem.cs
│   │   │   ├── Order.cs
│   │   │   ├── OrderItem.cs
│   │   │   └── Transaction.cs
│   │   ├── Enums/
│   │   │   ├── UserRole.cs
│   │   │   ├── TableStatus.cs
│   │   │   ├── ReservationStatus.cs
│   │   │   ├── OrderStatus.cs
│   │   │   ├── PaymentMethod.cs
│   │   │   └── UnitOfMeasure.cs
│   │   └── ValueObjects/
│   │       ├── Money.cs
│   │       └── ShiftTimeRange.cs
│   │
│   ├── DigitalRegistry.Application/
│   │   ├── Common/
│   │   │   ├── Interfaces/
│   │   │   │   ├── IDigitalRegistryDbContext.cs
│   │   │   │   ├── IIdentityService.cs
│   │   │   │   ├── ICurrentUserService.cs
│   │   │   │   └── INotificationService.cs
│   │   │   └── Models/
│   │   │       └── Result.cs
│   │   ├── Features/
│   │   │   ├── Auth/
│   │   │   │   ├── Commands/Login/
│   │   │   │   └── Commands/RegisterGuest/
│   │   │   ├── Tables/
│   │   │   │   ├── Commands/CreateTable/
│   │   │   │   ├── Commands/DeleteTable/
│   │   │   │   ├── Commands/GenerateQrCode/
│   │   │   │   └── Queries/GetAvailableTables/
│   │   │   ├── Reservations/
│   │   │   │   ├── Commands/CreateReservation/
│   │   │   │   └── Commands/CancelReservation/
│   │   │   ├── Orders/
│   │   │   │   ├── Commands/CreateOrder/
│   │   │   │   ├── Commands/CreateGuestQrOrder/
│   │   │   │   ├── Commands/UpdateOrderItem/
│   │   │   │   └── Commands/ProcessPayment/
│   │   │   ├── Shifts/
│   │   │   │   ├── Commands/AssignShift/
│   │   │   │   └── Queries/GetWaitersSchedule/
│   │   │   └── Inventory/
│   │   │       ├── Commands/RestockIngredient/
│   │   │       └── Queries/GetLowStockReport/
│   │   └── PipelineBehaviors/
│   │       ├── ValidationBehavior.cs
│   │       └── LoggingBehavior.cs
│   │
│   ├── DigitalRegistry.Infrastructure/
│   │   ├── Persistence/
│   │   │   ├── ApplicationDbContext.cs
│   │   │   ├── Configurations/
│   │   │   └── Repositories/
│   │   ├── Identity/
│   │   │   ├── IdentityService.cs
│   │   │   └── JwtTokenGenerator.cs
│   │   ├── RealTime/
│   │   │   ├── SignalRNotificationService.cs
│   │   │   └── Hubs/
│   │   │       ├── KitchenHub.cs
│   │   │       └── OrderHub.cs
│   │   └── Services/
│   │       └── DateTimeService.cs
│   │
│   └── DigitalRegistry.Api/
│       ├── Controllers/
│       │   ├── AuthController.cs
│       │   ├── TablesController.cs
│       │   ├── ReservationsController.cs
│       │   ├── OrdersController.cs
│       │   ├── ShiftsController.cs
│       │   └── InventoryController.cs
│       ├── Middleware/
│       │   └── ExceptionHandlingMiddleware.cs
│       └── Program.cs
└── tests/
    ├── DigitalRegistry.Domain.UnitTests/
    ├── DigitalRegistry.Application.UnitTests/
    └── DigitalRegistry.IntegrationTests/
```

---

## 3. Core Domain Model & Schema Specifications

### Enums
* **`UserRole`**: `Guest` (1), `Waiter` (2), `Manager` (3), `Owner` (4)
* **`OrderStatus`**: `Open` (1), `InPreparation` (2), `Served` (3), `Paid` (4), `Cancelled` (5)
* **`ReservationStatus`**: `Pending` (1), `Confirmed` (2), `Cancelled` (3), `Completed` (4)
* **`PaymentMethod`**: `Cash` (1), `Card` (2), `DigitalWallet` (3)
* **`UnitOfMeasure`**: `Milliliters` (1), `Grams` (2), `Units` (3)

### Domain Entities

```csharp
// ApplicationUser Entity
public class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public ICollection<Shift> Shifts { get; set; } = new List<Shift>();
    public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
}

// Table Entity
public class Table : BaseEntity
{
    public int TableNumber { get; set; }
    public int Capacity { get; set; }
    public Guid QrCodeToken { get; set; } = Guid.NewGuid();
    public bool IsActive { get; set; } = true;
}

// Reservation Entity
public class Reservation : BaseEntity
{
    public Guid GuestId { get; set; }
    public Guid TableId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public int PartySize { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Pending;
}

// Shift Entity
public class Shift : BaseEntity
{
    public Guid WaiterId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public Guid AssignedByManagerId { get; set; }
}

// Ingredient Entity
public class Ingredient : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal StockQuantity { get; set; }
    public UnitOfMeasure Unit { get; set; }
    public decimal LowStockThreshold { get; set; }
}

// MenuItem Entity
public class MenuItem : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public bool IsAvailable { get; set; } = true;
    public ICollection<RecipeItem> Recipe { get; set; } = new List<RecipeItem>();
}

// RecipeItem Entity
public class RecipeItem : BaseEntity
{
    public Guid MenuItemId { get; set; }
    public Guid IngredientId { get; set; }
    public decimal QuantityRequired { get; set; }
}

// Order Entity
public class Order : BaseEntity
{
    public Guid TableId { get; set; }
    public Guid? WaiterId { get; set; } // Null if created via Guest QR scan
    public OrderStatus Status { get; set; } = OrderStatus.Open;
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// OrderItem Entity
public class OrderItem : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid MenuItemId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string? Notes { get; set; }
}

// Transaction Entity
public class Transaction : BaseEntity
{
    public Guid OrderId { get; set; }
    public Guid ProcessedByWaiterId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.UtcNow;
}
```

---

## 4. Role-Based Access Control (RBAC) Matrix

| Endpoint / Feature | Guest (Self / Anon QR) | Waiter | Manager | Owner |
| :--- | :---: | :---: | :---: | :---: |
| Account Registration & Login | ✅ | ✅ | ✅ | ✅ |
| Scan QR Code / Initialize Table Session | ✅ | ✅ | ✅ | ✅ |
| View Menu & Availability | ✅ | ✅ | ✅ | ✅ |
| View Table Availability | ✅ | ✅ | ✅ | ✅ |
| Reserve Table | ✅ | ✅ | ✅ | ✅ |
| Cancel Own Reservation | ✅ | ❌ | ✅ | ✅ |
| Place Order via Table QR Code | ✅ | ❌ | ❌ | ✅ |
| Open Table Order (Staff Direct) | ❌ | ✅ | ❌ | ✅ |
| Modify / Edit Active Order Items | ❌ | ✅ | ❌ | ✅ |
| Process Payment & Finalize Order | ❌ | ✅ | ❌ | ✅ |
| Manage Tables (Add, Modify, Remove) | ❌ | ❌ | ✅ | ✅ |
| Assign Waiter Shifts & Resolve Overlaps | ❌ | ❌ | ✅ | ✅ |
| Manage Menu & Recipe Ingredients | ❌ | ❌ | ✅ | ✅ |
| Manage Inventory Stock Levels | ❌ | ❌ | ✅ | ✅ |
| Financial Reports & System Auditing | ❌ | ❌ | ❌ | ✅ |

---

## 5. Architectural Enhancements & Feature Specifications

### 5.1 Real-Time SignalR Notifications
* **Kitchen / Bar Display (`/hubs/kitchen`)**:
  * Emits `OrderCreated` and `OrderItemUpdated` events in real time.
  * Bar & kitchen monitors receive instant pushes without client polling.
* **Floor & Waiter Alerts (`/hubs/order`)**:
  * Emits `GuestQrOrderPlaced` when a customer places an order via QR.
  * Emits `ReservationArrivalAlert` when a guest checks in.

### 5.2 Automated Inventory Tracking Engine
* **Deduction Rule**: When an `Order` transitions to `InPreparation` or `Open`, the handler looks up `MenuItem -> RecipeItems -> Ingredient` and deducts `QuantityRequired * Quantity` from `Ingredient.StockQuantity`.
* **Stock Exhaustion Guard**: If an ingredient drops below `QuantityRequired`, the background domain listener auto-updates `MenuItem.IsAvailable = false` and broadcasts `MenuItemAvailabilityChanged` via SignalR.

### 5.3 Table QR Code Guest Self-Ordering
* **Session Binding**: Each physical table holds a unique static `QrCodeToken`.
* **Anonymous Guest JWT**: Scanning the QR code issues a scoped JWT containing `TableId` and claims allowing menu view and order submission locked to that table ID.

### 5.4 Shift Overlap & Conflict Prevention Logic
* **Validation Rules for `AssignShiftCommand`**:
  1. `StartTime` must be prior to `EndTime`.
  2. Target user must possess `UserRole.Waiter`.
  3. No overlapping shift exists for the waiter:
     $$	ext{ExistingShift.StartTime} < 	ext{NewShift.EndTime} \quad \land \quad 	ext{NewShift.StartTime} < 	ext{ExistingShift.EndTime}$$

---

## 6. Implementation Roadmap & TODO Checklist

### Phase 1: Solution Setup & Domain Foundations
- [ ] Initialize .NET 8 Solution with projects: `Api`, `Infrastructure`, `Application`, `Domain`, `UnitTests`, `IntegrationTests`.
- [ ] Implement `BaseEntity`, `AggregateRoot`, and `IDomainEvent` interfaces.
- [ ] Create core Domain Enums (`UserRole`, `OrderStatus`, `ReservationStatus`, `PaymentMethod`, `UnitOfMeasure`).
- [ ] Implement core Domain Entities (`ApplicationUser`, `Table`, `Reservation`, `Shift`, `MenuItem`, `Ingredient`, `RecipeItem`, `Order`, `OrderItem`, `Transaction`).
- [ ] Set up EF Core `ApplicationDbContext` and configure entity relationships, entity keys, and indexes.

### Phase 2: Authentication & Authorization (ASP.NET Core Identity & JWT)
- [ ] Configure `IdentityUser<Guid>` and ASP.NET Core Identity in `Infrastructure`.
- [ ] Implement JWT Token Generator producing role claims (`Guest`, `Waiter`, `Manager`, `Owner`).
- [ ] Implement `LoginCommand` and `RegisterGuestCommand` with MediatR.
- [ ] Configure Authorization Policies in `Api` program startup corresponding to the RBAC matrix.
- [ ] Implement `ICurrentUserService` to inject user Context (UserId, UserRole, TableId) safely across application handlers.

### Phase 3: Table Management & QR Code Session Subsystem
- [ ] Implement `CreateTableCommand`, `UpdateTableCommand`, and `DeleteTableCommand` (Manager/Owner only).
- [ ] Implement `GetAvailableTablesQuery` with party size and time range filtering.
- [ ] Implement `GenerateTableQrCodeTokenCommand` to rotate/regenerate table QR tokens.
- [ ] Implement `InitializeTableSessionCommand` allowing guests to trade a QR token for a scoped table JWT.

### Phase 4: Reservation System
- [ ] Implement `CreateReservationCommand` with validation for party size vs table capacity.
- [ ] Implement overlap validation ensuring table isn't double-booked for given `StartTime` to `EndTime`.
- [ ] Implement `CancelReservationCommand` (Guest can cancel own; Manager/Owner can cancel any).
- [ ] Implement `GetGuestReservationsQuery` and `GetDailyReservationsQuery`.

### Phase 5: Order Processing, Payment & Inventory Deduction Engine
- [ ] Implement `CreateOrderCommand` (Staff direct table order).
- [ ] Implement `CreateGuestQrOrderCommand` (Scoped to QR table session).
- [ ] Implement `UpdateOrderItemCommand` (Add/remove items, update quantity or notes).
- [ ] Implement `ProcessPaymentCommand` (Compute total, log `Transaction`, mark `Order` as `Paid`).
- [ ] Build Inventory Deduction Handler: automatically decrease `Ingredient.StockQuantity` on item order.
- [ ] Build Auto-Disable stock check: flip `MenuItem.IsAvailable = false` when ingredients are depleted.

### Phase 6: Manager Shift Scheduling & Conflict Prevention
- [ ] Implement `AssignShiftCommand` with FluentValidation rule checking for overlapping waiter shifts.
- [ ] Implement `GetWaitersScheduleQuery` (Filter by date range or specific waiter ID).
- [ ] Implement `DeleteShiftCommand` / `UpdateShiftCommand`.

### Phase 7: Real-Time SignalR Hub Integration
- [ ] Create `KitchenHub` (`/hubs/kitchen`) and register clients in `Infrastructure`.
- [ ] Create `OrderHub` (`/hubs/order`) for floor alerts.
- [ ] Inject `INotificationService` into CQRS handlers to push real-time events when orders are created, updated, or paid.

### Phase 8: Testing, Refinement & Final Verification
- [ ] Write Unit Tests for Shift Overlap validation logic in `DigitalRegistry.Domain.UnitTests`.
- [ ] Write Unit Tests for Inventory Deduction and Menu Item Availability toggles.
- [ ] Write Integration Tests using `Microsoft.AspNetCore.Mvc.Testing` and in-memory/test-container database for end-to-end API workflows.
- [ ] Generate Swagger/OpenAPI documentation with JWT Bearer security definitions.
