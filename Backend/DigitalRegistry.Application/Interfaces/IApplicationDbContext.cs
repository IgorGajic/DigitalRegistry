using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Interfaces
{
    public interface IApplicationDbContext
    {
        DbSet<Table> Tables { get; set; }
        DbSet<TableReservation> TableReservations { get; set; }
        DbSet<Restaurant> Restaurants { get; set; }
        DbSet<MenuItem> MenuItems { get; set; }
        DbSet<RestaurantMenu> RestaurantMenus { get; set; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    }
}
