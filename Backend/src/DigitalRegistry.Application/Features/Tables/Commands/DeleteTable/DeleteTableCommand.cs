using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Tables.Commands.DeleteTable;

/// <summary>
/// Removes a table from the floor plan. Manager and owner only.
/// </summary>
/// <remarks>
/// Only tables with no order or reservation history can be deleted; anything else must be
/// deactivated instead, so financial and booking records are never orphaned.
/// </remarks>
public record DeleteTableCommand(Guid Id) : IRequest<Result>;
