using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Shifts.Queries.GetWaitersSchedule;

/// <summary>
/// The shift roster, optionally narrowed to a date range or a single waiter.
/// </summary>
/// <param name="From">Start of the window. Defaults to the beginning of today, UTC.</param>
/// <param name="To">End of the window. Defaults to seven days after <paramref name="From"/>.</param>
/// <param name="WaiterId">Optionally narrows the roster to one waiter.</param>
public record GetWaitersScheduleQuery(DateTime? From = null, DateTime? To = null, Guid? WaiterId = null)
    : IRequest<Result<IReadOnlyList<ShiftDto>>>;
