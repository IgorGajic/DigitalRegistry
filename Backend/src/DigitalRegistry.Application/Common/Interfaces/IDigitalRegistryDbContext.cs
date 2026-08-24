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
    /// <summary>
    /// The tenants themselves. Not restaurant-scoped, so no global query filter applies: sign-in has
    /// to find a restaurant by its slug before any tenant is known.
    /// </summary>
    DbSet<Restaurant> Restaurants { get; }

    /// <summary>Licence terms, platform-wide. Not restaurant-scoped; see <see cref="Restaurants"/>.</summary>
    DbSet<License> Licenses { get; }

    DbSet<LicensePayment> LicensePayments { get; }

    DbSet<ApplicationUser> Users { get; }

    DbSet<Room> Rooms { get; }

    DbSet<Table> Tables { get; }

    DbSet<Reservation> Reservations { get; }

    DbSet<Shift> Shifts { get; }

    DbSet<ShiftTemplate> ShiftTemplates { get; }

    DbSet<ShiftAssignment> ShiftAssignments { get; }

    DbSet<MenuItem> MenuItems { get; }

    DbSet<Ingredient> Ingredients { get; }

    DbSet<RecipeItem> RecipeItems { get; }

    /// <summary>Deliveries received into the store, with what they cost.</summary>
    DbSet<StockEntry> StockEntries { get; }

    /// <summary>The stock ledger. Append-only; see <see cref="StockMovement"/>.</summary>
    DbSet<StockMovement> StockMovements { get; }

    DbSet<Order> Orders { get; }

    DbSet<OrderItem> OrderItems { get; }

    DbSet<Transaction> Transactions { get; }

    /// <summary>The audit trail of everything cancelled. Append-only; see <see cref="VoidRecord"/>.</summary>
    DbSet<VoidRecord> VoidRecords { get; }

    /// <summary>
    /// Commits the tracked changes and then dispatches any domain events raised by the entities
    /// that were saved.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
