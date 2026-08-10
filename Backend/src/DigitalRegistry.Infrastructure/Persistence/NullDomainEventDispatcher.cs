using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Domain.Common;

namespace DigitalRegistry.Infrastructure.Persistence;

/// <summary>
/// A dispatcher that discards events, for contexts constructed outside the application host.
/// </summary>
/// <remarks>
/// Used by the migrations design-time factory and by tests that exercise persistence without
/// wiring up MediatR.
/// </remarks>
public sealed class NullDomainEventDispatcher : IDomainEventDispatcher
{
    public static readonly NullDomainEventDispatcher Instance = new();

    public Task DispatchAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default) => Task.CompletedTask;
}
