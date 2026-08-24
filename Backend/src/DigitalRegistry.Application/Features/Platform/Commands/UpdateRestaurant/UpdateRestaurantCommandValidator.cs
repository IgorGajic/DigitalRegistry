using FluentValidation;

namespace DigitalRegistry.Application.Features.Platform.Commands.UpdateRestaurant;

public class UpdateRestaurantCommandValidator : AbstractValidator<UpdateRestaurantCommand>
{
    public UpdateRestaurantCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();

        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("Restaurant name is required.")
            .MaximumLength(200);

        RuleFor(command => command.ContactEmail)
            .EmailAddress().WithMessage("Contact email must be a valid address.")
            .MaximumLength(256)
            .When(command => !string.IsNullOrWhiteSpace(command.ContactEmail));

        RuleFor(command => command.PhoneNumber).MaximumLength(40);
        RuleFor(command => command.Address).MaximumLength(300);

        RuleFor(command => command.CurrencyCode)
            .Matches("^[A-Za-z]{3}$").WithMessage("Currency code must be three letters.")
            .When(command => !string.IsNullOrWhiteSpace(command.CurrencyCode));

        RuleFor(command => command.TimeZoneId).MaximumLength(64);
    }
}
