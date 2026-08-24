using FluentValidation;

namespace DigitalRegistry.Application.Features.Shifts.Commands.SaveShiftTemplate;

public class SaveShiftTemplateCommandValidator : AbstractValidator<SaveShiftTemplateCommand>
{
    public SaveShiftTemplateCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("Give the shift a name, such as \"I smena\".")
            .MaximumLength(100);

        // Equal times would describe a shift of no length, or of a full day, depending on how the
        // midnight rule is read. Neither is what anybody means, so it is rejected outright.
        RuleFor(command => command.EndTime)
            .NotEqual(command => command.StartTime)
            .WithMessage("A shift cannot start and end at the same time.");
    }
}
