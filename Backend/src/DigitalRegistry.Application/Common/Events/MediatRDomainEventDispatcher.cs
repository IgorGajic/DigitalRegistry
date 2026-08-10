using System.Collections.Concurrent;
using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Domain.Common;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DigitalRegistry.Application.Common.Events;

/// <summary>
/// Dispatches domain events through MediatR by wrapping each one in
/// <see cref="DomainEventNotification{TDomainEvent}"/>.
/// </summary>
/// <remarks>
/// Closing the generic requires reflection, which is confined to this class and cached per event
/// type. A handler that throws is logged and swallowed: notification side effects such as pushing a
/// SignalR message must not fail a command whose data has already been committed.
/// </remarks>
public sealed class MediatRDomainEventDispatcher(
    IPublisher publisher,
    ILogger<MediatRDomainEventDispatcher> logger) : IDomainEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, Func<IDomainEvent, INotification>> WrapperCache = new();

    public async Task DispatchAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken cancellationToken = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            var notification = Wrap(domainEvent);

            try
            {
                await publisher.Publish(notification, cancellationToken);
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Handler for domain event {DomainEvent} failed. The originating transaction is already committed.",
                    domainEvent.GetType().Name);
            }
        }
    }

    private static INotification Wrap(IDomainEvent domainEvent)
    {
        var factory = WrapperCache.GetOrAdd(domainEvent.GetType(), CreateWrapperFactory);
        return factory(domainEvent);
    }

    private static Func<IDomainEvent, INotification> CreateWrapperFactory(Type domainEventType)
    {
        var notificationType = typeof(DomainEventNotification<>).MakeGenericType(domainEventType);
        return domainEvent => (INotification)Activator.CreateInstance(notificationType, domainEvent)!;
    }
}
