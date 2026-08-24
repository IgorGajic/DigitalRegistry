using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Orders.Commands.VoidOrderItem;

/// <summary>
/// Cancels part or all of a line on a running tab, and returns what it consumed to stock.
/// </summary>
/// <remarks>
/// The waiter does this alone — stopping to find a manager over a mis-keyed coffee would not survive
/// a busy service. The control is the record it writes, which the owner reviews afterwards.
/// </remarks>
/// <param name="Quantity">
/// Servings to cancel. Leave null to cancel the whole line, which is the common case.
/// </param>
public record VoidOrderItemCommand(
    Guid OrderId,
    Guid OrderItemId,
    string Reason,
    int? Quantity = null) : IRequest<Result<VoidResultDto>>;
