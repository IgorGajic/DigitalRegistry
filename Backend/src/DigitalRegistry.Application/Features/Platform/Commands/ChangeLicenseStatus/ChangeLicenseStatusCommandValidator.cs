using FluentValidation;

namespace DigitalRegistry.Application.Features.Platform.Commands.ChangeLicenseStatus;

public class ChangeLicenseStatusCommandValidator : AbstractValidator<ChangeLicenseStatusCommand>
{
    public ChangeLicenseStatusCommandValidator()
    {
        RuleFor(command => command.LicenseId).NotEmpty();

        RuleFor(command => command.Action).IsInEnum();

        // Reactivation is the one action that undoes rather than imposes something, so it is the one
        // that needs no justification recorded against the venue.
        RuleFor(command => command.Reason)
            .NotEmpty().WithMessage("State why the licence is being suspended or cancelled.")
            .MaximumLength(500)
            .When(command => command.Action != LicenseAction.Reactivate);
    }
}
