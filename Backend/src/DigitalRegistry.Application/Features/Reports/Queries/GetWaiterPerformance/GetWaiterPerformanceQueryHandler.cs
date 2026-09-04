using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Application.Features.Shifts;
using DigitalRegistry.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Reports.Queries.GetWaiterPerformance;

public class GetWaiterPerformanceQueryHandler(
    IDigitalRegistryDbContext context,
    ITenantContext tenant)
    : IRequestHandler<GetWaiterPerformanceQuery, Result<WaiterPerformanceReportDto>>
{
    public async Task<Result<WaiterPerformanceReportDto>> Handle(
        GetWaiterPerformanceQuery request,
        CancellationToken cancellationToken)
    {
        var timeZoneId = await context.Restaurants
            .Where(restaurant => restaurant.Id == tenant.RestaurantId)
            .Select(restaurant => restaurant.TimeZoneId)
            .FirstOrDefaultAsync(cancellationToken);

        var timeZone = ShiftClock.ResolveTimeZone(timeZoneId);

        var fromUtc = ShiftClock.ToUtc(request.FromDate.ToDateTime(TimeOnly.MinValue), timeZone);
        var toUtc = ShiftClock.ToUtc(request.ToDate.AddDays(1).ToDateTime(TimeOnly.MinValue), timeZone);

        var orders = await context.Orders
            .AsNoTracking()
            .Where(order => order.CreatedAt >= fromUtc && order.CreatedAt < toUtc)
            // A cancelled round was never served and a reversed one was handed back; crediting either
            // to a waiter would reward ringing things up and taking them off again.
            .Where(order => order.Status != OrderStatus.Cancelled && order.Status != OrderStatus.Voided)
            .Select(order => new Round(
                // Whoever carried it out, falling back to whoever took it. On a waiter's own tab
                // those are the same act; on a guest QR round only the first exists.
                order.ServedByWaiterId ?? order.WaiterId,
                order.CreatedAt,
                order.ServedAtUtc,
                order.OrderItems.Sum(item => (decimal?)(item.UnitPrice * item.Quantity)) ?? 0m))
            .ToListAsync(cancellationToken);

        // Overlapping rather than contained: a shift that starts before the period or runs past its
        // end still contributes the hours that fall inside it.
        var shifts = await context.Shifts
            .AsNoTracking()
            .Where(shift => shift.StartTime < toUtc && shift.EndTime > fromUtc)
            .Select(shift => new Rostered(shift.WaiterId, shift.StartTime, shift.EndTime))
            .ToListAsync(cancellationToken);

        var attributed = orders.Where(round => round.WaiterId is not null).ToList();

        var staffIds = attributed
            .Select(round => round.WaiterId!.Value)
            .Concat(shifts.Select(shift => shift.WaiterId))
            .Distinct()
            .ToList();

        // Names come from the user table, which Identity owns and no global query filter narrows —
        // hence the explicit tenant check. Without it a shift belonging to another venue could put a
        // stranger's name on this venue's report.
        var names = await context.Users
            .AsNoTracking()
            .Where(user => user.RestaurantId == tenant.RestaurantId && staffIds.Contains(user.Id))
            .Select(user => new { user.Id, Name = user.FirstName + " " + user.LastName })
            .ToDictionaryAsync(user => user.Id, user => user.Name.Trim(), cancellationToken);

        var rows = names
            .Select(entry => Summarise(
                entry.Key,
                entry.Value,
                attributed.Where(round => round.WaiterId == entry.Key).ToList(),
                shifts.Where(shift => shift.WaiterId == entry.Key).ToList(),
                fromUtc,
                toUtc))
            // Turnover first: it is the column an owner reads down before any of the others.
            .OrderByDescending(row => row.TotalValue)
            .ThenBy(row => row.Name, StringComparer.CurrentCulture)
            .ToList();

        return Result<WaiterPerformanceReportDto>.Success(new WaiterPerformanceReportDto(
            FromDate: request.FromDate,
            ToDate: request.ToDate,
            Waiters: rows));
    }

    private static WaiterPerformanceRowDto Summarise(
        Guid waiterId,
        string name,
        List<Round> rounds,
        List<Rostered> shifts,
        DateTime fromUtc,
        DateTime toUtc)
    {
        var total = decimal.Round(rounds.Sum(round => round.Value), 2);

        var timed = rounds
            .Where(round => round.ServedAtUtc is not null)
            .Select(round => (round.ServedAtUtc!.Value - round.PlacedAtUtc).TotalMinutes)
            // A clock skew or an edited row could produce a negative wait, which is not a fast
            // service — it is a bad record, and averaging it in would flatter.
            .Where(minutes => minutes >= 0)
            .ToList();

        var hours = shifts.Sum(shift =>
            (Min(shift.EndUtc, toUtc) - Max(shift.StartUtc, fromUtc)).TotalHours);

        hours = Math.Round(Math.Max(hours, 0), 2);

        return new WaiterPerformanceRowDto(
            WaiterId: waiterId,
            Name: name,
            OrderCount: rounds.Count,
            TotalValue: total,
            AverageServiceMinutes: timed.Count > 0 ? Math.Round(timed.Average(), 1) : null,
            TimedOrderCount: timed.Count,
            HoursWorked: hours,
            // Left at zero rather than dividing by nothing: a waiter who was never rostered has no
            // rate, and an infinity in a spreadsheet cell helps nobody.
            ValuePerHour: hours > 0 ? decimal.Round(total / (decimal)hours, 2) : 0m);
    }

    private static DateTime Min(DateTime left, DateTime right) => left < right ? left : right;

    private static DateTime Max(DateTime left, DateTime right) => left > right ? left : right;

    /// <summary>One order, reduced to what the report needs of it.</summary>
    private sealed record Round(
        Guid? WaiterId,
        DateTime PlacedAtUtc,
        DateTime? ServedAtUtc,
        decimal Value);

    /// <summary>One rostered period.</summary>
    private sealed record Rostered(Guid WaiterId, DateTime StartUtc, DateTime EndUtc);
}
