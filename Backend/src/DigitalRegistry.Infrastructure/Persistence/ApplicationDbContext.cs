using System.Reflection;
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
    /// <summary>
    /// Cached handle to <see cref="ApplyRestaurantFilter{TEntity}"/>, which has to be reached by
    /// reflection because the entity types are only known while the model is being built.
    /// </summary>
    private static readonly MethodInfo ApplyRestaurantFilterMethod = typeof(ApplicationDbContext)
        .GetMethod(nameof(ApplyRestaurantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly IDomainEventDispatcher _domainEventDispatcher;

    private readonly ITenantContext _tenantContext;

    /// <summary>
    /// The single constructor, so dependency injection never has to choose between overloads.
    /// Callers with no interest in domain events or tenancy — migrations tooling, tests — pass
    /// <see cref="NullDomainEventDispatcher.Instance"/> and <see cref="NullTenantContext.Instance"/>.
    /// </summary>
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        IDomainEventDispatcher domainEventDispatcher,
        ITenantContext tenantContext)
        : base(options)
    {
        _domainEventDispatcher = domainEventDispatcher;
        _tenantContext = tenantContext;
    }

    public DbSet<Restaurant> Restaurants => Set<Restaurant>();

    public DbSet<License> Licenses => Set<License>();

    public DbSet<LicensePayment> LicensePayments => Set<LicensePayment>();

    public DbSet<Room> Rooms => Set<Room>();

    public DbSet<Table> Tables => Set<Table>();

    public DbSet<Reservation> Reservations => Set<Reservation>();

    public DbSet<Shift> Shifts => Set<Shift>();

    public DbSet<ShiftTemplate> ShiftTemplates => Set<ShiftTemplate>();

    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();

    public DbSet<MenuItem> MenuItems => Set<MenuItem>();

    public DbSet<Ingredient> Ingredients => Set<Ingredient>();

    public DbSet<RecipeItem> RecipeItems => Set<RecipeItem>();

    public DbSet<StockEntry> StockEntries => Set<StockEntry>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

    public DbSet<Transaction> Transactions => Set<Transaction>();

    public DbSet<VoidRecord> VoidRecords => Set<VoidRecord>();

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
        ApplyRestaurantStamp();

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

        if (domainEvents.Count > 0)
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

        ApplyRestaurantFilters(builder);
    }

    /// <summary>
    /// Confines every restaurant-scoped entity to the current tenant, for every query, everywhere.
    /// </summary>
    /// <remarks>
    /// Doing this once here rather than in each handler is what makes cross-tenant leakage a
    /// structural impossibility instead of a rule somebody has to remember. Types are collected before
    /// the loop because configuring an entity type mutates the model being iterated.
    /// </remarks>
    private void ApplyRestaurantFilters(ModelBuilder builder)
    {
        var scopedTypes = builder.Model.GetEntityTypes()
            .Where(entityType => typeof(IRestaurantScoped).IsAssignableFrom(entityType.ClrType))
            .Select(entityType => entityType.ClrType)
            .Distinct()
            .ToList();

        foreach (var entityType in scopedTypes)
        {
            ApplyRestaurantFilterMethod.MakeGenericMethod(entityType).Invoke(this, [builder]);
        }
    }

    /// <summary>
    /// Adds the tenant filter for one entity type.
    /// </summary>
    /// <remarks>
    /// The predicate reads <c>_tenantContext.RestaurantId</c> through the context instance rather than
    /// capturing the value. EF Core compiles the model once and reuses it for the lifetime of the
    /// application, so a captured value would freeze whichever restaurant happened to sign in first
    /// and hand its data to everybody else. Read through the instance, it becomes a query parameter
    /// evaluated per request.
    /// </remarks>
    private void ApplyRestaurantFilter<TEntity>(ModelBuilder builder)
        where TEntity : class, IRestaurantScoped
    {
        builder.Entity<TEntity>()
            .HasQueryFilter(entity => entity.RestaurantId == _tenantContext.RestaurantId);
    }

    /// <summary>
    /// Stamps the current tenant onto newly inserted restaurant-scoped rows.
    /// </summary>
    /// <remarks>
    /// Only fills in a value that has not already been set, so an aggregate that propagated its own
    /// restaurant to its children — an <see cref="Order"/> to its lines, say — keeps that value, and
    /// the master application can still insert on a named restaurant's behalf.
    /// </remarks>
    private void ApplyRestaurantStamp()
    {
        if (!_tenantContext.HasTenant)
        {
            return;
        }

        foreach (EntityEntry<IRestaurantScoped> entry in ChangeTracker.Entries<IRestaurantScoped>())
        {
            if (entry.State == EntityState.Added && entry.Entity.RestaurantId == Guid.Empty)
            {
                entry.Entity.RestaurantId = _tenantContext.RestaurantId;
            }
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
