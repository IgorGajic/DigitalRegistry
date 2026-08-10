using DigitalRegistry.Application.Interfaces;
using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Infrastructure
{
    internal class ApplicationDbContext : DbContext, IApplicationDbContext
    {
        public DbSet<Table> Tables { get; set; }
        public DbSet<TableReservation> TableReservations { get; set; }
        public DbSet<Restaurant> Restaurants { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }
        public DbSet<RestaurantMenu> RestaurantMenus { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(
                "Server=localhost\\SQLEXPRESS;Initial Catalog=DigitalRegistry;Integrated Security=SSPI;MultipleActiveResultSets=true;TrustServerCertificate=True;");
        }
    }
}