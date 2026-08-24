using DigitalRegistry.Domain.Enums;

namespace DigitalRegistry.Application.Features.Inventory;

/// <summary>An ingredient's current stock position.</summary>
public record IngredientStockDto(
    Guid Id,
    string Name,
    decimal StockQuantity,
    UnitOfMeasure Unit,
    decimal LowStockThreshold,
    bool IsLowOnStock);

/// <summary>
/// A low-stock line, including which menu items it holds back.
/// </summary>
/// <param name="BlockedMenuItems">
/// Menu items currently off the menu that use this ingredient, so a manager can see what restocking
/// this one item would bring back.
/// </param>
public record LowStockReportEntryDto(
    Guid Id,
    string Name,
    decimal StockQuantity,
    UnitOfMeasure Unit,
    decimal LowStockThreshold,
    IReadOnlyList<string> BlockedMenuItems);

/// <summary>A delivery received into the store.</summary>
public record StockEntryDto(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    decimal Quantity,
    UnitOfMeasure Unit,
    decimal PurchaseUnitPrice,
    decimal TotalCost,
    string? Supplier,
    string? ReferenceNumber,
    string? Note,
    string RecordedBy,
    DateTime EntryDateUtc,
    decimal StockAfter,
    decimal AveragePurchasePriceAfter);

/// <summary>One line of the stock ledger.</summary>
/// <param name="Quantity">Signed: positive into stock, negative out of it.</param>
public record StockMovementDto(
    Guid Id,
    Guid IngredientId,
    string IngredientName,
    StockMovementType Type,
    decimal Quantity,
    decimal BalanceAfter,
    UnitOfMeasure Unit,
    Guid? OrderId,
    string? Note,
    DateTime OccurredAtUtc);

/// <summary>What a stocktake corrected.</summary>
/// <param name="Difference">Negative when the count came up short.</param>
public record StockAdjustmentResultDto(
    Guid IngredientId,
    string IngredientName,
    decimal PreviousQuantity,
    decimal CountedQuantity,
    decimal Difference,
    UnitOfMeasure Unit,
    bool IsLowOnStock);
