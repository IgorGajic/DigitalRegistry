using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Orders.Queries.GetOrderById;

internal sealed class GetOrderByIdQueryHandler(
    IDigitalRegistryDbContext context,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetOrderByIdQuery, Result<OrderDto>>
{
    public async Task<Result<OrderDto>> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await context.Orders
            .AsNoTracking()
            .Include(candidate => candidate.Table)
            .Include(candidate => candidate.OrderItems)
            .ThenInclude(item => item.MenuItem)
            .FirstOrDefaultAsync(candidate => candidate.Id == request.Id, cancellationToken);

        if (order is null)
        {
            return Result<OrderDto>.NotFound($"Order {request.Id} was not found.");
        }

        var isStaff = currentUserService.IsInAnyRole(UserRole.Waiter, UserRole.Manager, UserRole.Owner);

        // A table session may look at its own table's tabs and nothing else.
        if (!isStaff && currentUserService.TableId != order.TableId)
        {
            return Result<OrderDto>.NotFound($"Order {request.Id} was not found.");
        }

        return Result<OrderDto>.Success(order.ToDto());
    }
}
