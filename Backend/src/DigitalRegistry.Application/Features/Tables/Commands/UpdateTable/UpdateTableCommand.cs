using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Tables.Commands.UpdateTable;

/// <summary>
/// Changes a table's number, capacity or in-service flag. Manager and owner only.
/// </summary>
/// <remarks>
/// Setting <paramref name="IsActive"/> to false is the supported way to retire a table that already
/// has order or reservation history, since such a table cannot be deleted.
/// </remarks>
public record UpdateTableCommand(Guid Id, int TableNumber, int Capacity, bool IsActive) : IRequest<Result>;
