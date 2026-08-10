namespace DigitalRegistry.Domain.Common;

/// <summary>
/// Marker for something meaningful that happened inside the domain.
/// </summary>
/// <remarks>
/// Deliberately free of any MediatR reference so the Domain layer keeps no dependency on the
/// Application layer's dispatch mechanism. The Application layer adapts these into MediatR
/// notifications when the DbContext saves.
/// </remarks>
public interface IDomainEvent
{
    /// <summary>When the event occurred, in UTC.</summary>
    DateTime OccurredOnUtc { get; }
}
