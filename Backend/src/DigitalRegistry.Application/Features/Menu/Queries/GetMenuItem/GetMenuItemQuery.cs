using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Menu.Queries.GetMenuItem;

/// <summary>One menu item with its recipe and cost. Management only.</summary>
public record GetMenuItemQuery(Guid Id) : IRequest<Result<MenuItemDetailDto>>;
