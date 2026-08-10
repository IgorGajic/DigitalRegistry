using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Shifts.Queries.GetWaitersSchedule;

internal sealed class GetWaitersScheduleQueryHandler(
    IDigitalRegistryDbContext context,
    IDateTimeService dateTimeService)
    : IRequestHandler<GetWaitersScheduleQuery, Result<IReadOnlyList<ShiftDto>>>
{
    /// <summary>Window used when the caller gives no range.</summary>
    private static readonly TimeSpan DefaultWindow = TimeSpan.FromDays(7);

    public async Task<Result<IReadOnlyList<ShiftDto>>> Handle(
        GetWaitersScheduleQuery request,
        CancellationToken cancellationToken)
    {
        var from = request.From ?? dateTimeService.TodayUtc.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var to = request.To ?? from.Add(DefaultWindow);

        if (to <= from)
        {
            return Result<IReadOnlyList<ShiftDto>>.Invalid("The end of the window must be after its start.");
        }

        // Any shift overlapping the window belongs on the roster, including one that started before
        // the window opened and is still running.
        var query = context.Shifts
            .AsNoTracking()
            .Where(shift => shift.StartTime < to && from < shift.EndTime);

        if (request.WaiterId is { } waiterId)
        {
            query = query.Where(shift => shift.WaiterId == waiterId);
        }

        var schedule = await query
            .OrderBy(shift => shift.StartTime)
            .ThenBy(shift => shift.Waiter!.LastName)
            .Select(shift => new ShiftDto(
                shift.Id,
                shift.WaiterId,
                shift.Waiter!.FirstName + " " + shift.Waiter.LastName,
                shift.StartTime,
                shift.EndTime,
                shift.AssignedByManagerId))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<ShiftDto>>.Success(schedule);
    }
}
