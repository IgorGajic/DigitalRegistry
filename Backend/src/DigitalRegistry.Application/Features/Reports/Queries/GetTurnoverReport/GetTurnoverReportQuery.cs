using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Reports.Queries.GetTurnoverReport;

/// <summary>
/// The day's takings, over a period.
/// </summary>
/// <remarks>
/// Days are the venue's local business days, not UTC ones — a bill settled at 00:30 belongs to the
/// night that produced it as far as the clock on the wall is concerned, and grouping in UTC would
/// scatter a late service across two dates.
/// <para>
/// Reversals are netted off rather than hidden: the turnover is what the venue actually kept.
/// </para>
/// </remarks>
public record GetTurnoverReportQuery(DateOnly FromDate, DateOnly ToDate)
    : IRequest<Result<TurnoverReportDto>>;
