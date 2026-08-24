using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Shifts.Commands.GenerateSchedule;

public class GenerateScheduleCommandHandler(
    IDigitalRegistryDbContext context,
    ICurrentUserService currentUser,
    ITenantContext tenant)
    : IRequestHandler<GenerateScheduleCommand, Result<GenerateScheduleResultDto>>
{
    public async Task<Result<GenerateScheduleResultDto>> Handle(
        GenerateScheduleCommand request,
        CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } managerId)
        {
            return Result<GenerateScheduleResultDto>.Forbidden(
                "Only a signed-in manager or owner can generate the schedule.");
        }

        var timeZoneId = await context.Restaurants
            .Where(restaurant => restaurant.Id == tenant.RestaurantId)
            .Select(restaurant => restaurant.TimeZoneId)
            .FirstOrDefaultAsync(cancellationToken);

        var timeZone = ShiftClock.ResolveTimeZone(timeZoneId);

        var assignments = await context.ShiftAssignments
            .Include(assignment => assignment.ShiftTemplate)
            .Include(assignment => assignment.Waiter)
            .Where(assignment => request.WaiterId == null || assignment.WaiterId == request.WaiterId)
            // An arrangement is irrelevant if its period does not touch the range being generated.
            .Where(assignment => assignment.ValidFrom <= request.ToDate
                                 && (assignment.ValidTo == null || assignment.ValidTo >= request.FromDate))
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
        {
            return Result<GenerateScheduleResultDto>.Success(new GenerateScheduleResultDto(
                request.FromDate, request.ToDate, Created: 0, AlreadyPresent: 0, Conflicts: []));
        }

        // Everything already on the schedule anywhere near the range, loaded once. Generating a month
        // touches hundreds of days; asking the database per day would be hundreds of round trips.
        // The window is widened by a day at each end so a shift running past midnight is still seen.
        var windowStart = ShiftClock.ToUtc(request.FromDate.AddDays(-1).ToDateTime(TimeOnly.MinValue), timeZone);
        var windowEnd = ShiftClock.ToUtc(request.ToDate.AddDays(2).ToDateTime(TimeOnly.MinValue), timeZone);

        var waiterIds = assignments.Select(assignment => assignment.WaiterId).Distinct().ToList();

        var existing = await context.Shifts
            .Where(shift => waiterIds.Contains(shift.WaiterId)
                            && shift.StartTime < windowEnd
                            && shift.EndTime > windowStart)
            .Select(shift => new { shift.WaiterId, shift.StartTime, shift.EndTime })
            .ToListAsync(cancellationToken);

        var booked = existing
            .GroupBy(shift => shift.WaiterId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(shift => (shift.StartTime, shift.EndTime)).ToList());

        var created = new List<Shift>();
        var alreadyPresent = 0;
        var conflicts = new List<ScheduleConflictDto>();

        for (var date = request.FromDate; date <= request.ToDate; date = date.AddDays(1))
        {
            foreach (var assignment in assignments)
            {
                if (assignment.ShiftTemplate is not { IsActive: true } template
                    || !assignment.CoversDate(date))
                {
                    continue;
                }

                var (startUtc, endUtc) = ShiftClock.ToUtcPeriod(template, date, timeZone);

                if (!booked.TryGetValue(assignment.WaiterId, out var periods))
                {
                    periods = [];
                    booked[assignment.WaiterId] = periods;
                }

                // Exactly this shift already exists — the generator has run over this day before.
                if (periods.Any(period => period.Item1 == startUtc && period.Item2 == endUtc))
                {
                    alreadyPresent++;
                    continue;
                }

                // Something else already occupies part of the period. Mirrors ShiftTimeRange.Overlaps,
                // restated in memory because the candidates are held here rather than in the database.
                if (periods.Any(period => period.Item1 < endUtc && startUtc < period.Item2))
                {
                    conflicts.Add(new ScheduleConflictDto(
                        Date: date,
                        WaiterId: assignment.WaiterId,
                        WaiterName: FullName(assignment.Waiter),
                        ShiftTemplateName: template.Name,
                        StartUtc: startUtc,
                        EndUtc: endUtc,
                        Reason: "The waiter is already booked over part of this period."));

                    continue;
                }

                created.Add(new Shift
                {
                    RestaurantId = assignment.RestaurantId,
                    WaiterId = assignment.WaiterId,
                    StartTime = startUtc,
                    EndTime = endUtc,
                    AssignedByManagerId = managerId,
                    ShiftAssignmentId = assignment.Id
                });

                // Added to the running set so a second arrangement on the same day is checked against
                // what this run has just written, not only against what was already stored.
                periods.Add((startUtc, endUtc));
            }
        }

        if (created.Count > 0)
        {
            context.Shifts.AddRange(created);
            await context.SaveChangesAsync(cancellationToken);
        }

        return Result<GenerateScheduleResultDto>.Success(new GenerateScheduleResultDto(
            FromDate: request.FromDate,
            ToDate: request.ToDate,
            Created: created.Count,
            AlreadyPresent: alreadyPresent,
            Conflicts: conflicts));
    }

    private static string FullName(ApplicationUser? user) =>
        user is null ? string.Empty : $"{user.FirstName} {user.LastName}".Trim();
}
