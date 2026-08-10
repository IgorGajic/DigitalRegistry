using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Tables.Commands.CreateTable;

/// <summary>
/// Adds a table to the floor plan. Manager and owner only.
/// </summary>
public record CreateTableCommand(int TableNumber, int Capacity) : IRequest<Result<TableDto>>;
