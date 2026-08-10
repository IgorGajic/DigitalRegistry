using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Shifts;

/// <summary>
/// The database-backed checks behind shift assignment: the target must be a waiter, and the period
/// must not collide with one of their existing shifts.
/// </summary>
/// <remarks>
/// Shared by the assign and update validators, which differ only in whether a shift is excluded from
/// the comparison — an update must not find itself and report a clash with itself.
/// </remarks>
internal static class ShiftOverlapRules
{
    public static Task<bool> IsWaiterAsync(
        IDigitalRegistryDbContext context,
        Guid userId,
        CancellationToken cancellationToken) =>
        context.Users.AnyAsync(user => user.Id == userId && user.Role == UserRole.Waiter, cancellationToken);

    /// <summary>
    /// True when the waiter already has a shift sharing any instant with the given period.
    /// </summary>
    /// <param name="excludingShiftId">The shift being edited, so it is not compared against itself.</param>
    public static Task<bool> HasOverlappingShiftAsync(
        IDigitalRegistryDbContext context,
        Guid waiterId,
        DateTime startTime,
        DateTime endTime,
        Guid? excludingShiftId,
        CancellationToken cancellationToken) =>
        context.Shifts.AnyAsync(
            existing => existing.WaiterId == waiterId
                        && (excludingShiftId == null || existing.Id != excludingShiftId)
                        // existing.Start < candidate.End && candidate.Start < existing.End.
                        // The rule itself lives in ShiftTimeRange.Overlaps and is unit tested there;
                        // it is restated here because EF Core has to turn it into SQL and cannot call
                        // into the value object to do so.
                        && existing.StartTime < endTime
                        && startTime < existing.EndTime,
            cancellationToken);
}
