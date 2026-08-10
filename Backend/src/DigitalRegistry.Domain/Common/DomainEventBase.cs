namespace DigitalRegistry.Domain.Common;

/// <summary>
/// Convenience base for domain events that stamps the occurrence time.
/// </summary>
public abstract record DomainEventBase : IDomainEvent
{
    public DateTime OccurredOnUtc { get; init; } = DateTime.UtcNow;
}
