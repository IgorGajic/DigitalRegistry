using DigitalRegistry.Application.Common.Interfaces;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace DigitalRegistry.Application.Features.Shifts.Commands.UpdateShift;

/// <summary>
/// The same period and overlap rules as assignment, with the shift being edited excluded from the
/// comparison so it cannot clash with itself.
/// </summary>
public class UpdateShiftCommandValidator : AbstractValidator<UpdateShiftCommand>
{
    private static readonly TimeSpan MaximumShiftLength = TimeSpan.FromHours(16);

    public UpdateShiftCommandValidator(IDigitalRegistryDbContext context)
    {
        RuleFor(command => command.Id)
            .NotEmpty().WithMessage("A shift must be named.");

        RuleFor(command => command.StartTime)
            .LessThan(command => command.EndTime)
            .WithMessage("A shift must start before it ends.");

        RuleFor(command => command)
            .Must(command => (command.EndTime - command.StartTime) <= MaximumShiftLength)
            .WithMessage($"A shift cannot be longer than {MaximumShiftLength.TotalHours} hours.")
            .WithName(nameof(UpdateShiftCommand.EndTime));

        RuleFor(command => command)
            .MustAsync(async (command, cancellationToken) =>
            {
                var waiterId = await context.Shifts
                    .Where(shift => shift.Id == command.Id)
                    .Select(shift => (Guid?)shift.WaiterId)
                    .FirstOrDefaultAsync(cancellationToken);

                // A missing shift is the handler's 404 to report, not a validation failure.
                if (waiterId is null)
                {
                    return true;
                }

                return !await ShiftOverlapRules.HasOverlappingShiftAsync(
                    context,
                    waiterId.Value,
                    command.StartTime,
                    command.EndTime,
                    excludingShiftId: command.Id,
                    cancellationToken);
            })
            .WithMessage("This waiter already has another shift that overlaps the requested period.")
            .WithName(nameof(UpdateShiftCommand.StartTime))
            .When(command => command.StartTime < command.EndTime && command.Id != Guid.Empty);
    }
}
