using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Orders.Commands.CreateOrder;

/// <summary>
/// Opens a tab against a table as staff. Waiter and owner only.
/// </summary>
/// <remarks>
/// The serving waiter is taken from the caller's token, not the request body. A table may carry more
/// than one open tab at once, which is how separate parties or rounds on the same table are kept
/// apart; payment settles one tab.
/// </remarks>
public record CreateOrderCommand(Guid TableId, IReadOnlyList<OrderLineRequest> Items)
    : IRequest<Result<OrderDto>>;
