using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Orders.Queries.GetOrderById;

/// <summary>
/// Fetches one tab with its lines and total.
/// </summary>
/// <remarks>
/// A guest on a table session may only fetch orders belonging to their own table.
/// </remarks>
public record GetOrderByIdQuery(Guid Id) : IRequest<Result<OrderDto>>;
