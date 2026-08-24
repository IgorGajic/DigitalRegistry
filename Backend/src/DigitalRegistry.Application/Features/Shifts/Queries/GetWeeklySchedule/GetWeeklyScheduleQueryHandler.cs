using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Shifts.Queries.GetWeeklySchedule;

public class GetWeeklyScheduleQueryHandler(
    IDigitalRegistryDbContext context,
    ITenantContext tenant)
    : IRequestHandler<GetWeeklyScheduleQuery, Result<WeeklyScheduleDto>>
{
    public async Task<Result<WeeklyScheduleDto>> Handle(
        GetWeeklyScheduleQuery request,
        CancellationToken cancellationToken)
    {
        var timeZoneId = await context.Restaurants
            .Where(restaurant => restaurant.Id == tenant.RestaurantId)
            .Select(restaurant => restaurant.TimeZoneId)
            .FirstOrDefaultAsync(cancellationToken);

        var timeZone = ShiftClock.ResolveTimeZone(timeZoneId);

        // Weeks run Monday to Sunday, which is how a rota is read here.
        var offset = ((int)request.AnyDateInWeek.DayOfWeek + 6) % 7;
        var weekStart = request.AnyDateInWeek.AddDays(-offset);
        var days = Enumerable.Range(0, 7).Select(weekStart.AddDays).ToList();

        var fromUtc = ShiftClock.ToUtc(weekStart.ToDateTime(TimeOnly.MinValue), timeZone);
        var toUtc = ShiftClock.ToUtc(weekStart.AddDays(7).ToDateTime(TimeOnly.MinValue), timeZone);

        var shifts = await context.Shifts
            .AsNoTracking()
            .Where(shift => shift.StartTime >= fromUtc && shift.StartTime < toUtc)
            .Select(shift => new
            {
                shift.Id,
                shift.WaiterId,
                WaiterName = shift.Waiter!.FirstName + " " + shift.Waiter.LastName,
                shift.StartTime,
                shift.EndTime,
                TemplateName = shift.ShiftAssignment!.ShiftTemplate!.Name,
                shift.ShiftAssignmentId
            })
            .ToListAsync(cancellationToken);

        // Every waiter appears, including those with nothing on this week — an empty row is the whole
        // point of a rota grid, since it shows who is still free.
        var waiters = await context.Users
            .AsNoTracking()
            .Where(user => user.Role == UserRole.Waiter)
            .Select(user => new { user.Id, Name = user.FirstName + " " + user.LastName })
            .ToListAsync(cancellationToken);

        var byWaiter = shifts.ToLookup(shift => shift.WaiterId);

        var rows = waiters
            .Select(waiter =>
            {
                var scheduled = byWaiter[waiter.Id]
                    .OrderBy(shift => shift.StartTime)
                    .Select(shift => new ScheduledShiftDto(
                        Id: shift.Id,
                        // Placed in the column for the local day it starts on, not the UTC one.
                        Date: DateOnly.FromDateTime(
                            TimeZoneInfo.ConvertTimeFromUtc(
                                DateTime.SpecifyKind(shift.StartTime, DateTimeKind.Utc),
                                timeZone)),
                        StartUtc: shift.StartTime,
                        EndUtc: shift.EndTime,
                        Hours: Math.Round((shift.EndTime - shift.StartTime).TotalHours, 2),
                        ShiftTemplateName: shift.TemplateName,
                        IsGenerated: shift.ShiftAssignmentId is not null))
                    .ToList();

                return new WaiterWeekDto(
                    WaiterId: waiter.Id,
                    WaiterName: waiter.Name.Trim(),
                    TotalHours: Math.Round(scheduled.Sum(shift => shift.Hours), 2),
                    Shifts: scheduled);
            })
            .OrderBy(row => row.WaiterName)
            .ToList();

        return Result<WeeklyScheduleDto>.Success(new WeeklyScheduleDto(weekStart, days, rows));
    }
}
