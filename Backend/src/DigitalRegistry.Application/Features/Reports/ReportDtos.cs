using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Application.Features.Reports;

/// <summary>One cancellation, as it appears in the owner's review.</summary>
public record VoidReportEntryDto(
    Guid Id,
    DateTime VoidedAtUtc,
    VoidType Type,
    Guid OrderId,
    int? TableNumber,
    string? ItemName,
    int Quantity,
    decimal Amount,
    string Reason,
    string PerformedBy,
    string? ApprovedBy);

/// <summary>
/// What one member of staff cancelled over the period.
/// </summary>
/// <remarks>
/// The line the report exists for. A waiter voiding far more than their colleagues is the pattern an
/// owner is looking for, and it only shows up once the individual records are added together.
/// </remarks>
public record VoidsByStaffDto(
    Guid UserId,
    string Name,
    int VoidCount,
    decimal TotalAmount,
    int ItemVoids,
    int OpenOrderVoids,
    int PaidOrderVoids);

/// <summary>The void report over a period.</summary>
public record VoidReportDto(
    DateTime FromUtc,
    DateTime ToUtc,
    int TotalVoids,
    decimal TotalAmount,
    IReadOnlyList<VoidsByStaffDto> ByStaff,
    IReadOnlyList<VoidReportEntryDto> Entries);

/// <summary>One business day's takings.</summary>
/// <param name="Cash">Taken in cash, reversals already netted off.</param>
/// <param name="AverageBill">Turnover over the number of bills; zero on a day with none.</param>
/// <param name="ReversedAmount">
/// What was reversed that day, shown separately. It is already deducted from the turnover — the
/// figure is here so a day with a large reversal explains itself rather than merely looking poor.
/// </param>
public record DailyTurnoverDto(
    DateOnly Date,
    decimal Turnover,
    decimal Cash,
    decimal Card,
    decimal DigitalWallet,
    int BillCount,
    decimal AverageBill,
    decimal ReversedAmount,
    int ReversalCount);

/// <summary>Turnover over a period, day by day.</summary>
public record TurnoverReportDto(
    DateOnly FromDate,
    DateOnly ToDate,
    decimal Turnover,
    decimal Cash,
    decimal Card,
    decimal DigitalWallet,
    int BillCount,
    decimal AverageBill,
    IReadOnlyList<DailyTurnoverDto> Days);

/// <summary>How much of one item sold, and what it brought in.</summary>
/// <param name="QuantitySold">
/// Servings actually paid for. Cancelled servings are excluded, so this cannot be inflated by ringing
/// items up and voiding them.
/// </param>
public record TopSellingItemDto(
    Guid MenuItemId,
    string Name,
    string Category,
    int QuantitySold,
    decimal Revenue,
    decimal? EstimatedCost,
    decimal? EstimatedMargin);

/// <summary>What the store holds and what it is worth.</summary>
/// <param name="ConsumedQuantity">Drawn down by sales over the period, returns netted off.</param>
/// <param name="ConsumedValue">What that consumption cost, at the average purchase price.</param>
/// <param name="PurchasedQuantity">Everything that came into stock, priced deliveries and bare restocks alike.</param>
/// <param name="PurchasedValue">
/// What was actually paid for the deliveries in the period, from the entries themselves. Lower than
/// <paramref name="PurchasedQuantity"/> would suggest when stock was topped up without a price.
/// </param>
public record InventoryValuationLineDto(
    Guid IngredientId,
    string Name,
    Domain.Enums.UnitOfMeasure Unit,
    decimal StockQuantity,
    decimal AveragePurchasePrice,
    decimal StockValue,
    decimal LowStockThreshold,
    bool IsLowOnStock,
    decimal ConsumedQuantity,
    decimal ConsumedValue,
    decimal PurchasedQuantity,
    decimal PurchasedValue,
    decimal AdjustedQuantity);

/// <summary>The store's position over a period.</summary>
public record InventoryValuationDto(
    DateTime FromUtc,
    DateTime ToUtc,
    decimal TotalStockValue,
    decimal TotalConsumedValue,
    decimal TotalPurchasedValue,
    int LowStockCount,
    IReadOnlyList<InventoryValuationLineDto> Lines);
