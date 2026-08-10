using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;

namespace DigitalRegistry.Application.Features.Orders.Commands.ProcessPayment;

/// <summary>
/// Settles a tab: totals it, records a transaction and marks the order paid. Waiter and owner only.
/// </summary>
/// <remarks>
/// The amount is computed from the order's own lines rather than accepted from the caller, so a
/// client cannot decide what the guest pays. The processing waiter is taken from the token.
/// </remarks>
public record ProcessPaymentCommand(Guid OrderId, PaymentMethod PaymentMethod)
    : IRequest<Result<TransactionDto>>;
