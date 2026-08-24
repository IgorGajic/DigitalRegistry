using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Menu.Commands.SaveMenuItem;

/// <summary>
/// Creates or amends something the venue sells.
/// </summary>
/// <remarks>
/// The recipe is set separately. An item can exist before anybody has worked out what goes into it —
/// which is how a bar gets its price list up on the first day — and it simply consumes no stock until
/// one is defined.
/// </remarks>
/// <param name="Id">Null to create; an existing item's id to amend it.</param>
/// <param name="IsAvailable">
/// Whether the item is offered. Note that the stock guard overrides this downwards: an item whose
/// ingredients have run out comes off the menu regardless of what is set here.
/// </param>
public record SaveMenuItemCommand(
    Guid? Id,
    string Name,
    string Category,
    decimal UnitPrice,
    bool IsAvailable = true) : IRequest<Result<MenuItemDetailDto>>;
