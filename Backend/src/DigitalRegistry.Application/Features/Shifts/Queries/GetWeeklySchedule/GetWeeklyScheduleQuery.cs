using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Shifts.Queries.GetWeeklySchedule;

/// <summary>
/// The rota for one week, as the grid of waiters against days a manager reads.
/// </summary>
/// <remarks>
/// Days are the venue's local calendar days, not UTC ones. A shift starting at 22:00 local belongs to
/// that evening's column even though in UTC it may already be the next date.
/// </remarks>
/// <param name="AnyDateInWeek">
/// Any date in the week wanted. The handler snaps it back to the Monday, so a client can pass today.
/// </param>
public record GetWeeklyScheduleQuery(DateOnly AnyDateInWeek) : IRequest<Result<WeeklyScheduleDto>>;
