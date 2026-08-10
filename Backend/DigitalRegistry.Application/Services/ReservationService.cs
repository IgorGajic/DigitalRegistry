using DigitalRegistry.Application.Interfaces;
using DigitalRegistry.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Services
{
    public class ReservationService(IApplicationDbContext dbContext)
    {
        private readonly IApplicationDbContext _dbContext = dbContext;

        public async Task<bool> IsTableAvailableAsync(Guid tableId, DateTime reservedFrom, DateTime reservedTo)
        {
            var overlappingReservations = await _dbContext.TableReservations
                .Where(r => r.TableId == tableId &&
                            ((reservedFrom >= r.ReservedFrom && reservedFrom < r.ReservedTo) ||
                             (reservedTo > r.ReservedFrom && reservedTo <= r.ReservedTo) ||
                             (reservedFrom <= r.ReservedFrom && reservedTo >= r.ReservedTo)))
                .ToListAsync();

            return overlappingReservations.Count == 0;
        }

        public async Task ReserveTableAsync(Guid tableId, DateTime reservedFrom, DateTime reservedTo)
        {
            if (!await IsTableAvailableAsync(tableId, reservedFrom, reservedTo))
            {
                throw new InvalidOperationException("Table is not available for the specified time range.");
            }
            var reservation = new TableReservation
            {
                TableId = tableId,
                ReservedFrom = reservedFrom,
                ReservedTo = reservedTo
            };
            _dbContext.TableReservations.Add(reservation);
            await _dbContext.SaveChangesAsync();
        }
    }
}
