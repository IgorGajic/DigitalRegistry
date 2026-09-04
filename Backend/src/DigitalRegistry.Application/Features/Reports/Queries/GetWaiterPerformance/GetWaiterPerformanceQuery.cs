using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Reports.Queries.GetWaiterPerformance;

/// <summary>
/// What each member of the floor staff did over a period.
/// </summary>
/// <remarks>
/// The one report in the set that is about people rather than money. An owner asking "how did Marko
/// do in the first ten days of September" wants four things at once — how much he carried, what it
/// came to, how long his tables waited, and how long he was rostered — and until now every one of
/// them had to be worked out by hand from a different screen.
/// <para>
/// The period is given in the venue's local business days, like the turnover report and for the same
/// reason: a round placed at 00:30 belongs to the night that produced it.
/// </para>
/// </remarks>
public record GetWaiterPerformanceQuery(DateOnly FromDate, DateOnly ToDate)
    : IRequest<Result<WaiterPerformanceReportDto>>;
