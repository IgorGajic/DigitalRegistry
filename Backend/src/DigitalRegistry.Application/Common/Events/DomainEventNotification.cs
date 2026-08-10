using DigitalRegistry.Domain.Common;
using MediatR;

namespace DigitalRegistry.Application.Common.Events;

/// <summary>
/// Wraps a domain event so MediatR can publish it.
/// </summary>
/// <remarks>
/// The wrapper exists because <see cref="IDomainEvent"/> lives in the Domain layer, which must not
/// reference MediatR. Handlers subscribe to the closed generic, for example
/// <c>INotificationHandler&lt;DomainEventNotification&lt;OrderCreatedDomainEvent&gt;&gt;</c>, so
/// each handler still names exactly the event it cares about.
/// </remarks>
public sealed record DomainEventNotification<TDomainEvent>(TDomainEvent DomainEvent) : INotification
    where TDomainEvent : IDomainEvent;
