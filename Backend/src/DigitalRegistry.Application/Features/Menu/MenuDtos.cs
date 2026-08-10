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
