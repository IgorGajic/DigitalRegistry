using FluentValidation;

namespace DigitalRegistry.Application.Features.Menu.Commands.SaveMenuItem;

public class SaveMenuItemCommandValidator : AbstractValidator<SaveMenuItemCommand>
{
    public SaveMenuItemCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("A menu item needs a name.")
            .MaximumLength(200);

        RuleFor(command => command.Category)
            .NotEmpty().WithMessage("A menu item needs a category, such as \"Kafa\" or \"Pica\".")
            .MaximumLength(100);

        RuleFor(command => command.UnitPrice)
            .GreaterThanOrEqualTo(0).WithMessage("A price cannot be negative.");
    }
}
