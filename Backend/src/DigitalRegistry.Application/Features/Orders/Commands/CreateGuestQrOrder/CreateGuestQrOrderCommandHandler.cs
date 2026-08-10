using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Orders.Commands.CreateGuestQrOrder;

internal sealed class CreateGuestQrOrderCommandHandler(
    OrderOpener orderOpener,
    ICurrentUserService currentUserService)
    : IRequestHandler<CreateGuestQrOrderCommand, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(
        CreateGuestQrOrderCommand request,
        CancellationToken cancellationToken)
    {
        // The table comes from the signed token, never from the request, which is what confines a
        // scanned session to the table it was scanned at.
        if (currentUserService.TableId is not { } tableId)
        {
            return Result<OrderDto>.Forbidden(
                "This endpoint needs a table session. Scan the table's QR code first.");
        }

        // No waiter: this is the guest ordering for themselves, which is what makes the order raise
        // GuestQrOrderPlacedDomainEvent and alert the floor.
        return await orderOpener.OpenAsync(tableId, waiterId: null, request.Items, cancellationToken);
    }
}
