using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Orders.Commands.MarkOrderServed;

/// <summary>Records that a round has been carried out to its table.</summary>
public record MarkOrderServedCommand(Guid OrderId) : IRequest<Result>;
