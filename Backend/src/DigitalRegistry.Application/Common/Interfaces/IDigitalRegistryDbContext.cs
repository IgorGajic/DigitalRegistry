using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Common.Interfaces;

/// <summary>
/// The persistence surface available to command and query handlers.
/// </summary>
/// <remarks>
/// Exposing <see cref="DbSet{TEntity}"/> keeps handlers able to compose exactly the query they
/// need — including projections and existence checks that never materialise entities — while the
/// concrete context, provider and connection string stay in the Infrastructure layer. This is why
/// there are no per-entity repository types: they would only forward to these sets.
/// </remarks>
public interface IDigitalRegistryDbContext
{
    DbSet<ApplicationUser> Users { get; }

    DbSet<Table> Tables { get; }

    DbSet<Reservation> Reservations { get; }

    DbSet<Shift> Shifts { get; }

    DbSet<MenuItem> MenuItems { get; }

    DbSet<Ingredient> Ingredients { get; }

    DbSet<RecipeItem> RecipeItems { get; }

    DbSet<Order> Orders { get; }

    DbSet<OrderItem> OrderItems { get; }

    DbSet<Transaction> Transactions { get; }

    /// <summary>
    /// Commits the tracked changes and then dispatches any domain events raised by the entities
    /// that were saved.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
