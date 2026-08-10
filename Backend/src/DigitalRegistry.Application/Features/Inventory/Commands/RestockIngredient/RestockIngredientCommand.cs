using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Inventory.Commands.RestockIngredient;

/// <summary>
/// Adds stock to an ingredient. Manager and owner only.
/// </summary>
/// <remarks>
/// Restocking re-evaluates the menu, so items that were taken off for want of this ingredient come
/// back automatically and a <c>MenuItemAvailabilityChanged</c> push tells the displays.
/// </remarks>
public record RestockIngredientCommand(Guid IngredientId, decimal Quantity)
    : IRequest<Result<IngredientStockDto>>;
