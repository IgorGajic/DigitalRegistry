using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Orders.Commands.ReopenOrderForService;

/// <summary>Puts a round back on the queue after it was ticked off by mistake.</summary>
public record ReopenOrderForServiceCommand(Guid OrderId) : IRequest<Result>;
