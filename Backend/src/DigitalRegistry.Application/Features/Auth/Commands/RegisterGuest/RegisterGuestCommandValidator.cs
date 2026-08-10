using FluentValidation;

namespace DigitalRegistry.Application.Features.Auth.Commands.RegisterGuest;

public class RegisterGuestCommandValidator : AbstractValidator<RegisterGuestCommand>
{
    public RegisterGuestCommandValidator()
    {
        RuleFor(command => command.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Email must be a valid address.")
            .MaximumLength(256);

        // Mirrors the Identity password options configured in Infrastructure, so a weak password is
        // rejected as a field-level validation error rather than an opaque Identity failure.
        RuleFor(command => command.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");

        RuleFor(command => command.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100);

        RuleFor(command => command.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100);
    }
}
