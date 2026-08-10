using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Orders.Commands.CreateGuestQrOrder;

/// <summary>
/// A guest orders for themselves through the table's QR code.
/// </summary>
/// <remarks>
/// There is no table id on the command by design: it is read from the caller's table-session token,
/// so a session opened by scanning table 4 can only ever order for table 4, whatever the client
/// sends.
/// </remarks>
public record CreateGuestQrOrderCommand(IReadOnlyList<OrderLineRequest> Items) : IRequest<Result<OrderDto>>;
