using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Orders.Commands.VoidPaidOrder;

/// <summary>
/// Reverses a bill that has already been settled.
/// </summary>
/// <remarks>
/// The only void a waiter cannot do alone: it takes money back out of the day's takings, which is
/// precisely the movement worth requiring a second person for. A counter-transaction is written rather
/// than the payment being amended, so the original stays on file.
/// </remarks>
public record VoidPaidOrderCommand(Guid OrderId, string Reason) : IRequest<Result<VoidResultDto>>;
