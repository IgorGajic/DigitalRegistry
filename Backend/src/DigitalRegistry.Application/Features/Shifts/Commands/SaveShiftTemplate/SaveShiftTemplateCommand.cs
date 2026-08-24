using DigitalRegistry.Application.Common.Models;
using MediatR;

namespace DigitalRegistry.Application.Features.Shifts.Commands.SaveShiftTemplate;

/// <summary>
/// Creates or amends a named working period.
/// </summary>
/// <remarks>
/// One command for both, because a manager defining "II smena 15:00–23:00" and correcting it to end
/// at 23:30 is doing the same thing, and splitting it would duplicate the whole validation for no gain.
/// <para>
/// Times are the venue's local clock. A shift ending at or before it starts runs past midnight, so
/// 22:00–06:00 needs no flag to say so.
/// </para>
/// </remarks>
/// <param name="Id">Null to create; an existing template's id to amend it.</param>
public record SaveShiftTemplateCommand(
    Guid? Id,
    string Name,
    TimeOnly StartTime,
    TimeOnly EndTime,
    bool IsActive = true) : IRequest<Result<ShiftTemplateDto>>;
