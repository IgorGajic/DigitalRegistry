using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Orders.Queries.GetReceipt;

/// <summary>
/// Everything needed to print a bill.
/// </summary>
/// <remarks>
/// A simulation, not a fiscal receipt. Nothing here is registered with a tax authority and no fiscal
/// device is involved — the venue's own record of what a guest was charged, in a shape the client can
/// print.
/// </remarks>
public record GetReceiptQuery(Guid OrderId) : IRequest<Result<ReceiptDto>>;
