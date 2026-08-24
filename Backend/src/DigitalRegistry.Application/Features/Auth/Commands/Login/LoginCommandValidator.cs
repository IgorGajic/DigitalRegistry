using FluentValidation;

namespace DigitalRegistry.Application.Features.Auth.Commands.Login;

public class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(command => command.RestaurantSlug)
            .NotEmpty().WithMessage("Restaurant code is required.")
            .MaximumLength(64);

        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid address.")
            .MaximumLength(256);

        // Deliberately no complexity rules here: the stored password's rules were enforced at
        // registration, and echoing them back on login would only hint at the format to an attacker.
        RuleFor(command => command.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}
