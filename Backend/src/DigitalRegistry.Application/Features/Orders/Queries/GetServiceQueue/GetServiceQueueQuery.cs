using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Orders.Queries.GetServiceQueue;

/// <summary>
/// What is waiting to be carried out to a table.
/// </summary>
/// <remarks>
/// Guest orders only. A waiter who took an order at the table is already standing at it and knows
/// what they wrote down; the queue exists for the rounds that arrive with nobody attached to them,
/// placed from a phone at a table the staff may not have looked at.
/// </remarks>
public record GetServiceQueueQuery : IRequest<Result<ServiceQueueDto>>;
