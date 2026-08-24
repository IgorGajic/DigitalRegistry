using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using DigitalRegistry.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Platform.Queries.GetPlatformDashboard;

public class GetPlatformDashboardQueryHandler(
    IDigitalRegistryDbContext context,
    IDateTimeService dateTime)
    : IRequestHandler<GetPlatformDashboardQuery, Result<PlatformDashboardDto>>
{
    public async Task<Result<PlatformDashboardDto>> Handle(
        GetPlatformDashboardQuery request,
        CancellationToken cancellationToken)
    {
        var utcNow = dateTime.UtcNow;

        var summaries = await context.SummariseAsync(context.AllRestaurants(), utcNow, cancellationToken);

        // Counted from the projected summaries rather than in SQL: expiry is derived, so a venue whose
        // term has simply run out has no column saying so.
        var expiringSoon = summaries
            .Where(summary => summary.LicenseStatus == LicenseStatus.Active
                              && summary.DaysRemaining <= PlatformProjections.ExpiringSoonDays)
            .OrderBy(summary => summary.DaysRemaining)
            .ToList();

        var revenueFrom = new DateTime(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            .AddMonths(-(Math.Max(request.RevenueMonths, 1) - 1));

        // Grouped into an anonymous type rather than straight into the DTO: EF Core cannot translate a
        // projection into a record's positional constructor from inside a GroupBy, and would either
        // fall back to client evaluation or refuse the query outright.
        var revenueRows = await context.AllLicensePayments()
            .AsNoTracking()
            .Where(payment => payment.PaidAtUtc >= revenueFrom)
            .GroupBy(payment => new { payment.PaidAtUtc.Year, payment.PaidAtUtc.Month })
            .Select(group => new
            {
                group.Key.Year,
                group.Key.Month,
                Amount = group.Sum(payment => payment.Amount),
                PaymentCount = group.Count()
            })
            .OrderBy(row => row.Year)
            .ThenBy(row => row.Month)
            .ToListAsync(cancellationToken);

        // Every month in the window, including the ones nobody paid in. A quiet month is a fact worth
        // drawing: leaving it out makes it indistinguishable from a month that never happened, and a
        // chart with a single bar in it reads as a full house rather than as one month's takings.
        var monthlyRevenue = Enumerable
            .Range(0, Math.Max(request.RevenueMonths, 1))
            .Select(offset => revenueFrom.AddMonths(offset))
            .Select(month =>
            {
                var row = revenueRows
                    .FirstOrDefault(candidate => candidate.Year == month.Year
                                                 && candidate.Month == month.Month);

                return new MonthlyRevenueDto(
                    month.Year,
                    month.Month,
                    row?.Amount ?? 0m,
                    row?.PaymentCount ?? 0);
            })
            .ToList();

        var totalRevenue = await context.AllLicensePayments()
            .SumAsync(payment => (decimal?)payment.Amount, cancellationToken) ?? 0m;

        return Result<PlatformDashboardDto>.Success(new PlatformDashboardDto(
            TotalRestaurants: summaries.Count,
            ActiveRestaurants: summaries.Count(summary => summary.IsActive),
            ActiveLicenses: summaries.Count(summary => summary.LicenseStatus == LicenseStatus.Active),
            ExpiredLicenses: summaries.Count(summary => summary.LicenseStatus == LicenseStatus.Expired),
            SuspendedLicenses: summaries.Count(summary => summary.LicenseStatus == LicenseStatus.Suspended),
            ExpiringSoon: expiringSoon.Count,
            TotalLicenseRevenue: totalRevenue,
            MonthlyLicenseRevenue: monthlyRevenue,
            ExpiringRestaurants: expiringSoon));
    }
}
