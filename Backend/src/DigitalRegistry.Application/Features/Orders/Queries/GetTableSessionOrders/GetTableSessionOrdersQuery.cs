using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Orders.Queries.GetTableSessionOrders;

/// <summary>
/// Every tab still running at the table the caller's QR session was opened at.
/// </summary>
/// <remarks>
/// The table comes from the session token, never from the request, so a scanned code can only ever
/// show its own table. Each round a guest sends opens a new order, so what they have had this
/// sitting is the sum of several — which is exactly what a guest cannot work out from the
/// confirmation screen of the last round alone.
/// </remarks>
public record GetTableSessionOrdersQuery : IRequest<Result<TableTabDto>>;
