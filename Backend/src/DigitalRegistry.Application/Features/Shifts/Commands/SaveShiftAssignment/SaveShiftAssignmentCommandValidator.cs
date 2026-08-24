using DigitalRegistry.Domain.Enums;
using FluentValidation;

namespace DigitalRegistry.Application.Features.Shifts.Commands.SaveShiftAssignment;

public class SaveShiftAssignmentCommandValidator : AbstractValidator<SaveShiftAssignmentCommand>
{
    public SaveShiftAssignmentCommandValidator()
    {
        RuleFor(command => command.WaiterId).NotEmpty();
        RuleFor(command => command.ShiftTemplateId).NotEmpty();

        RuleFor(command => command.Days)
            .NotEqual(WeekDays.None).WithMessage("Choose at least one day of the week.");

        RuleFor(command => command.ValidTo)
            .GreaterThanOrEqualTo(command => command.ValidFrom)
            .WithMessage("The arrangement cannot end before it starts.")
            .When(command => command.ValidTo.HasValue);
    }
}
