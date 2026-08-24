using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Orders.Commands.VoidOpenOrder;

/// <summary>
/// Cancels a tab that was never paid, returns everything on it to stock and frees the table.
/// </summary>
/// <remarks>
/// For a party that walked out, or an order opened against the wrong table. Nothing had entered the
/// takings, so no counter-transaction is needed and no manager has to sign it off — the record is what
/// makes it reviewable.
/// </remarks>
public record VoidOpenOrderCommand(Guid OrderId, string Reason) : IRequest<Result<VoidResultDto>>;
