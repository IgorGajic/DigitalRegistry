using DigitalRegistry.Domain.Common;

namespace DigitalRegistry.Application.Common.Interfaces;

/// <summary>
/// Publishes domain events collected by aggregates during a unit of work.
/// </summary>
public interface IDomainEventDispatcher
{
    /// <summary>
    /// Publishes each event to its handlers. Called after the database transaction has committed,
    /// so handlers never react to changes that were subsequently rolled back.
    /// </summary>
    Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken cancellationToken = default);
}
