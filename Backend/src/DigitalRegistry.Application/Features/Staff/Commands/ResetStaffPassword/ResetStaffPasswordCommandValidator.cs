using FluentValidation;

namespace DigitalRegistry.Application.Features.Staff.Commands.ResetStaffPassword;

public class ResetStaffPasswordCommandValidator : AbstractValidator<ResetStaffPasswordCommand>
{
    public ResetStaffPasswordCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();

        // Mirrors the Identity password options configured in Infrastructure, so a weak password is
        // rejected as a field-level validation error rather than an opaque Identity failure.
        RuleFor(command => command.NewPassword)
            .NotEmpty().WithMessage("A password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
            .MaximumLength(128)
            .Matches("[A-Z]").WithMessage("Password must contain an uppercase letter.")
            .Matches("[a-z]").WithMessage("Password must contain a lowercase letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");
    }
}
