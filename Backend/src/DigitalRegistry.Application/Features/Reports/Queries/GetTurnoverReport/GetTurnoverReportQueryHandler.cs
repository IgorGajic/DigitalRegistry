using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Application.Features.Shifts;
using DigitalRegistry.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Reports.Queries.GetTurnoverReport;

public class GetTurnoverReportQueryHandler(
    IDigitalRegistryDbContext context,
    ITenantContext tenant)
    : IRequestHandler<GetTurnoverReportQuery, Result<TurnoverReportDto>>
{
    public async Task<Result<TurnoverReportDto>> Handle(
        GetTurnoverReportQuery request,
        CancellationToken cancellationToken)
    {
        var timeZoneId = await context.Restaurants
            .Where(restaurant => restaurant.Id == tenant.RestaurantId)
            .Select(restaurant => restaurant.TimeZoneId)
            .FirstOrDefaultAsync(cancellationToken);

        var timeZone = ShiftClock.ResolveTimeZone(timeZoneId);

        var fromUtc = ShiftClock.ToUtc(request.FromDate.ToDateTime(TimeOnly.MinValue), timeZone);
        var toUtc = ShiftClock.ToUtc(request.ToDate.AddDays(1).ToDateTime(TimeOnly.MinValue), timeZone);

        var settlements = await context.Transactions
            .AsNoTracking()
            .Where(transaction => transaction.TransactionDate >= fromUtc
                                  && transaction.TransactionDate < toUtc)
            .Select(transaction => new Settlement(
                transaction.TransactionDate,
                transaction.Amount,
                transaction.PaymentMethod,
                transaction.ReversesTransactionId != null))
            .ToListAsync(cancellationToken);

        // Grouped after loading, because the local business day a settlement belongs to depends on
        // the venue's time zone and its daylight-saving rules — arithmetic SQL cannot do.
        var byDay = settlements
            .GroupBy(settlement => DateOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(settlement.SettledAtUtc, DateTimeKind.Utc),
                    timeZone)))
            .ToDictionary(group => group.Key, group => group.ToList());

        var days = new List<DailyTurnoverDto>();

        for (var date = request.FromDate; date <= request.ToDate; date = date.AddDays(1))
        {
            days.Add(Summarise(date, byDay.GetValueOrDefault(date, [])));
        }

        var totalTurnover = days.Sum(day => day.Turnover);
        var totalBills = days.Sum(day => day.BillCount);

        return Result<TurnoverReportDto>.Success(new TurnoverReportDto(
            FromDate: request.FromDate,
            ToDate: request.ToDate,
            Turnover: decimal.Round(totalTurnover, 2),
            Cash: days.Sum(day => day.Cash),
            Card: days.Sum(day => day.Card),
            DigitalWallet: days.Sum(day => day.DigitalWallet),
            BillCount: totalBills,
            AverageBill: totalBills > 0 ? decimal.Round(totalTurnover / totalBills, 2) : 0m,
            Days: days));
    }

    private static DailyTurnoverDto Summarise(DateOnly date, List<Settlement> settlements)
    {
        // A reversal carries a negative amount, so summing gives the net without a special case.
        var turnover = settlements.Sum(settlement => settlement.Amount);
        var reversalCount = settlements.Count(settlement => settlement.IsReversal);

        // Bills, not settlements: a reversal is not a sale, and counting it would drag the average
        // bill down every time one was issued.
        var billCount = settlements.Count - reversalCount;

        return new DailyTurnoverDto(
            Date: date,
            Turnover: decimal.Round(turnover, 2),
            Cash: SumFor(settlements, PaymentMethod.Cash),
            Card: SumFor(settlements, PaymentMethod.Card),
            DigitalWallet: SumFor(settlements, PaymentMethod.DigitalWallet),
            BillCount: billCount,
            AverageBill: billCount > 0 ? decimal.Round(turnover / billCount, 2) : 0m,
            ReversedAmount: decimal.Round(
                settlements.Where(settlement => settlement.IsReversal).Sum(settlement => -settlement.Amount),
                2),
            ReversalCount: reversalCount);
    }

    private static decimal SumFor(IEnumerable<Settlement> settlements, PaymentMethod method) =>
        decimal.Round(
            settlements.Where(settlement => settlement.Method == method).Sum(settlement => settlement.Amount),
            2);

    /// <summary>A payment or a reversal, reduced to what the report needs of it.</summary>
    private sealed record Settlement(
        DateTime SettledAtUtc,
        decimal Amount,
        PaymentMethod Method,
        bool IsReversal);
}
