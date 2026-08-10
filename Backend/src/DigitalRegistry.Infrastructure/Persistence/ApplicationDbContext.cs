using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Domain.Common;
using DigitalRegistry.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace DigitalRegistry.Infrastructure.Persistence;

/// <summary>
/// The EF Core unit of work, combining the application schema with ASP.NET Core Identity.
/// </summary>
/// <remarks>
/// Public and options-configured: the provider and connection string are supplied by dependency
/// injection in <c>Program.cs</c>, never hard-coded here, so tests can substitute a different
/// provider and the connection string stays in configuration.
/// </remarks>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>,
    IDigitalRegistryDbContext
{
    private readonly IDomainEventDispatcher? _domainEventDispatcher;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Preferred constructor. The dispatcher is optional so that design-time tooling and tests can
    /// construct the context without wiring up MediatR.
    /// </summary>
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IDomainEventDispatcher domainEventDispatcher)
        : base(options)
    {
        _domainEventDispatcher = domainEventDispatcher;
    }

    public DbSet<Table> Tables => Set<Table>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    public DbSet<Shift> Shifts => Set<Shift>();

    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    public DbSet<Ingredient> Ingredients => Set<Ingredient>();

    public DbSet<RecipeItem> RecipeItems => Set<RecipeItem>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    /// <summary>
    /// Saves the tracked changes, then dispatches the domain events the saved aggregates raised.
    /// </summary>
    /// <remarks>
    /// Events are published only after the write succeeds, so no handler can react to a change that
    /// was rolled back. They are collected before the save because the save itself may detach
    /// entities.
    /// </remarks>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps();

        var aggregatesWithEvents = ChangeTracker
            .Entries<AggregateRoot>()
            .Where(entry => entry.Entity.DomainEvents.Count > 0)
            .Select(entry => entry.Entity)
            .ToList();

        var domainEvents = aggregatesWithEvents
            .SelectMany(aggregate => aggregate.DomainEvents)
            .ToList();

        var affectedRows = await base.SaveChangesAsync(cancellationToken);

        foreach (var aggregate in aggregatesWithEvents)
        {
            aggregate.ClearDomainEvents();
        }

        if (_domainEventDispatcher is not null && domainEvents.Count > 0)
        {
            await _domainEventDispatcher.DispatchAsync(domainEvents, cancellationToken);
        }

        return affectedRows;
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // Domain events are in-memory only; they must never become a mapped navigation.
        // Materialised first because configuring an entity type mutates the model being iterated.
        var aggregateRootTypes = builder.Model.GetEntityTypes()
            .Where(entityType => typeof(AggregateRoot).IsAssignableFrom(entityType.ClrType))
            .Select(entityType => entityType.ClrType)
            .ToList();

        foreach (var entityType in aggregateRootTypes)
        {
            builder.Entity(entityType).Ignore(nameof(AggregateRoot.DomainEvents));
        }
    }

    /// <summary>
    /// Keeps <see cref="BaseEntity.Created"/> and <see cref="BaseEntity.Modified"/> truthful without
    /// every handler having to remember to set them.
    /// </summary>
    private void ApplyAuditTimestamps()
    {
        foreach (EntityEntry<BaseEntity> entry in ChangeTracker.Entries<BaseEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.Created = DateTime.UtcNow;
                    entry.Entity.Modified = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.Modified = DateTime.UtcNow;
                    // Guard against a detached-entity update silently rewriting the creation time.
                    entry.Property(entity => entity.Created).IsModified = false;
                    break;
            }
        }
    }
}
