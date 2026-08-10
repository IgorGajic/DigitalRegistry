using DigitalRegistry.Application.Common.Interfaces;
using FluentValidation;

namespace DigitalRegistry.Application.Features.Shifts.Commands.AssignShift;

/// <summary>
/// The three rules the specification sets for assigning a shift.
/// </summary>
/// <remarks>
/// All three live here, including the two that need the database, because the specification calls for
/// the overlap check to be a FluentValidation rule. The consequence is that a clash comes back as a
/// 400 with the offending field named, rather than a 409.
/// </remarks>
public class AssignShiftCommandValidator : AbstractValidator<AssignShiftCommand>
{
    /// <summary>Longest single shift that can be assigned.</summary>
    private static readonly TimeSpan MaximumShiftLength = TimeSpan.FromHours(16);

    public AssignShiftCommandValidator(IDigitalRegistryDbContext context)
    {
        // Rule 1: the period must run forwards.
        RuleFor(command => command.StartTime)
            .LessThan(command => command.EndTime)
            .WithMessage("A shift must start before it ends.");

        RuleFor(command => command)
            .Must(command => (command.EndTime - command.StartTime) <= MaximumShiftLength)
            .WithMessage($"A shift cannot be longer than {MaximumShiftLength.TotalHours} hours.")
            .WithName(nameof(AssignShiftCommand.EndTime));

        // Rule 2: the target user must actually be a waiter.
        RuleFor(command => command.WaiterId)
            .NotEmpty().WithMessage("A waiter must be named.")
            .MustAsync((waiterId, cancellationToken) =>
                ShiftOverlapRules.IsWaiterAsync(context, waiterId, cancellationToken))
            .WithMessage("The chosen user does not exist or is not a waiter.");

        // Rule 3: no overlap with that waiter's existing shifts. Only worth asking the database once
        // the period itself makes sense and the user is known to be a waiter.
        RuleFor(command => command)
            .MustAsync(async (command, cancellationToken) =>
                !await ShiftOverlapRules.HasOverlappingShiftAsync(
                    context,
                    command.WaiterId,
                    command.StartTime,
                    command.EndTime,
                    excludingShiftId: null,
                    cancellationToken))
            .WithMessage("This waiter already has a shift that overlaps the requested period.")
            .WithName(nameof(AssignShiftCommand.StartTime))
            .When(command => command.StartTime < command.EndTime && command.WaiterId != Guid.Empty);
    }
}
