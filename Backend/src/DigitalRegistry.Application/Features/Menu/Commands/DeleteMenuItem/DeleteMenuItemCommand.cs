using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Menu.Commands.DeleteMenuItem;

/// <summary>
/// Removes a menu item.
/// </summary>
/// <remarks>
/// Refused once the item appears on any order: those lines are sales history, and the reports that
/// read them would lose their subject. An item that has sold is withdrawn by clearing its
/// availability instead, which takes it off the menu and leaves the history intact.
/// </remarks>
public record DeleteMenuItemCommand(Guid Id) : IRequest<Result>;
