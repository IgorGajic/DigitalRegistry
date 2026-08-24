using FluentValidation;

namespace DigitalRegistry.Application.Features.Platform.Commands.RenewLicense;

public class RenewLicenseCommandValidator : AbstractValidator<RenewLicenseCommand>
{
    public RenewLicenseCommandValidator()
    {
        RuleFor(command => command.LicenseId).NotEmpty();

        RuleFor(command => command.Plan)
            .IsInEnum().WithMessage("Choose a licence term of 1, 3, 6 or 12 months.");

        RuleFor(command => command.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Price cannot be negative.");

        RuleFor(command => command.Notes).MaximumLength(500);
    }
}
