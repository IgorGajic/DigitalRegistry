using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Tables.Queries.GetTableById;

/// <summary>
/// Fetches one table including its QR token. Manager and owner only.
/// </summary>
public record GetTableByIdQuery(Guid Id) : IRequest<Result<TableDto>>;
