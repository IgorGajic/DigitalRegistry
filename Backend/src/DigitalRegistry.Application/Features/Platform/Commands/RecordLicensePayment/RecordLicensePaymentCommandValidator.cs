using FluentValidation;

namespace DigitalRegistry.Application.Features.Platform.Commands.RecordLicensePayment;

public class RecordLicensePaymentCommandValidator : AbstractValidator<RecordLicensePaymentCommand>
{
    public RecordLicensePaymentCommandValidator()
    {
        RuleFor(command => command.LicenseId).NotEmpty();

        RuleFor(command => command.Amount)
            .GreaterThan(0).WithMessage("A payment must be for more than zero.");

        RuleFor(command => command.PaymentMethod).IsInEnum();

        RuleFor(command => command.ReferenceNumber).MaximumLength(100);
        RuleFor(command => command.Notes).MaximumLength(500);
    }
}
