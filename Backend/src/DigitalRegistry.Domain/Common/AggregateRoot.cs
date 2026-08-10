namespace DigitalRegistry.Domain.Common;

/// <summary>
/// An entity that owns a consistency boundary and may raise domain events.
/// </summary>
/// <remarks>
/// Events are collected here rather than published immediately, so they are only dispatched once
/// the surrounding transaction has actually committed.
/// </remarks>
public abstract class AggregateRoot : BaseEntity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Events raised since this aggregate was loaded. Not mapped to the database.</summary>
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>Called by the persistence layer after the events have been dispatched.</summary>
    public void ClearDomainEvents() => _domainEvents.Clear();
}
