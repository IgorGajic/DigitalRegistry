using System.Reflection;
using DigitalRegistry.Application.Common.Events;
using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Services;
using DigitalRegistry.Application.PipelineBehaviors;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace DigitalRegistry.Application;

/// <summary>
/// Registers the Application layer: MediatR handlers, validators and the request pipeline.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var applicationAssembly = Assembly.GetExecutingAssembly();

        services.AddMediatR(configuration =>
        {
            configuration.RegisterServicesFromAssembly(applicationAssembly);

            // Order matters: logging wraps validation, so a request rejected by validation is still
            // recorded, together with how long it took to reject.
            configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
            configuration.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });

        services.AddValidatorsFromAssembly(applicationAssembly, includeInternalTypes: true);

        services.AddScoped<IDomainEventDispatcher, MediatRDomainEventDispatcher>();
        services.AddScoped<IInventoryAllocator, InventoryAllocator>();

        // Shared by the staff and guest-QR order paths. Concrete rather than behind an interface
        // because it is an internal collaborator of this layer, not a seam for other layers.
        services.AddScoped<Features.Orders.OrderOpener>();

        return services;
    }
}
