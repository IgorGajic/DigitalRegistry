using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Menu.Queries.GetMenu;

/// <summary>
/// The menu, grouped by category.
/// </summary>
/// <remarks>
/// Required by the "View Menu &amp; Availability" row of the access matrix, which every role holds,
/// including an anonymous QR table session.
/// </remarks>
/// <param name="Category">Optionally narrows the list to one category.</param>
/// <param name="IncludeUnavailable">
/// When true, items currently off the menu are listed as well, flagged unavailable. Useful to staff;
/// a guest-facing client would leave it false.
/// </param>
public record GetMenuQuery(string? Category = null, bool IncludeUnavailable = false)
    : IRequest<Result<IReadOnlyList<MenuItemDto>>>;
