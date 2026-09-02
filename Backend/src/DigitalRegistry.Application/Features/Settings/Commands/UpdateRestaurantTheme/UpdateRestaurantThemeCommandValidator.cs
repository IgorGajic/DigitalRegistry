using FluentValidation;

namespace DigitalRegistry.Application.Features.Settings.Commands.UpdateRestaurantTheme;

public class UpdateRestaurantThemeCommandValidator : AbstractValidator<UpdateRestaurantThemeCommand>
{
    public UpdateRestaurantThemeCommandValidator()
    {
        // The set is closed because each theme has had its table-state colours and its chart palette
        // checked against its own surface. A number outside it would be a till drawn in nothing.
        RuleFor(command => command.Theme)
            .IsInEnum().WithMessage("No such theme.");
    }
}
