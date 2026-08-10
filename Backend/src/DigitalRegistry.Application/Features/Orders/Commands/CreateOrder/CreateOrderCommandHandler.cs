using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Orders.Commands.CreateOrder;

internal sealed class CreateOrderCommandHandler(
    OrderOpener orderOpener,
    ICurrentUserService currentUserService)
    : IRequestHandler<CreateOrderCommand, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        // The waiter is whoever is holding the token. An anonymous table session has no user id and
        // is barred from this endpoint by the access policy, but the check keeps the handler honest
        // on its own.
        if (currentUserService.UserId is not { } waiterId)
        {
            return Result<OrderDto>.Forbidden("Only signed-in staff can open a tab directly.");
        }

        return await orderOpener.OpenAsync(request.TableId, waiterId, request.Items, cancellationToken);
    }
}
