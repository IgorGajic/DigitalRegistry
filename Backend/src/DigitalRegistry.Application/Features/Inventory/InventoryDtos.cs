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
