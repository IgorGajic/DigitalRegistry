namespace DigitalRegistry.Application.Features.Menu;

/// <summary>
/// A menu item as shown to whoever is ordering.
/// </summary>
/// <remarks>
/// Carries no recipe: what a dish is made of, and how much of it is in stock, is not a guest's
/// business and would leak supplier information.
/// </remarks>
public record MenuItemDto(
    Guid Id,
    string Name,
    string Category,
    decimal UnitPrice,
    bool IsAvailable);

/// <summary>
/// A menu item as a manager sees it, recipe included.
/// </summary>
/// <remarks>
/// The counterpart to <see cref="MenuItemDto"/>: same item, but with what it is made of and what it
/// costs to make. Only ever returned from the management endpoints.
/// </remarks>
/// <param name="CostPrice">
/// What one serving costs in ingredients, at their moving average purchase price. Zero until
/// deliveries have been recorded, since nothing yet knows what anything cost.
/// </param>
/// <param name="MarginPercent">
/// Gross margin over the cost price, or null when the cost is unknown and the figure would be
/// meaningless rather than merely flattering.
/// </param>
public record MenuItemDetailDto(
    Guid Id,
    string Name,
    string Category,
    decimal UnitPrice,
    bool IsAvailable,
    decimal CostPrice,
    decimal? MarginPercent,
    IReadOnlyList<RecipeLineDto> Recipe);

/// <summary>How much of one ingredient a single serving consumes.</summary>
public record RecipeLineDto(
    Guid IngredientId,
    string IngredientName,
    decimal QuantityRequired,
    Domain.Enums.UnitOfMeasure Unit,
    decimal StockQuantity,
    decimal LineCost);

/// <summary>One line of a recipe as submitted.</summary>
public record RecipeLineRequest(Guid IngredientId, decimal QuantityRequired);
