using FluentValidation;

namespace DigitalRegistry.Application.Features.Platform.Commands.IssueLicense;

public class IssueLicenseCommandValidator : AbstractValidator<IssueLicenseCommand>
{
    public IssueLicenseCommandValidator()
    {
        RuleFor(command => command.RestaurantId).NotEmpty();

        RuleFor(command => command.Plan)
            .IsInEnum().WithMessage("Choose a licence term of 1, 3, 6 or 12 months.");

        RuleFor(command => command.Price)
            .GreaterThanOrEqualTo(0).WithMessage("Price cannot be negative.");

        RuleFor(command => command.Notes).MaximumLength(500);
    }
}
