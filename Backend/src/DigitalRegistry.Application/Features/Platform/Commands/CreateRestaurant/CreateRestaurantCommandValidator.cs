using FluentValidation;

namespace DigitalRegistry.Application.Features.Platform.Commands.CreateRestaurant;

public class CreateRestaurantCommandValidator : AbstractValidator<CreateRestaurantCommand>
{
    /// <summary>
    /// Lower-case letters, digits and hyphens only.
    /// </summary>
    /// <remarks>
    /// Narrower than Identity's allowed user-name characters on purpose. The slug is typed by staff at
    /// every sign-in and read aloud over the phone, so anything ambiguous or shifted is a support call.
    /// It also must not contain <c>|</c>, which separates it from the email in the stored user name.
    /// </remarks>
    public const string SlugPattern = "^[a-z0-9]+(-[a-z0-9]+)*$";

    public CreateRestaurantCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("Restaurant name is required.")
            .MaximumLength(200);

        RuleFor(command => command.Slug)
            .NotEmpty().WithMessage("Restaurant code is required.")
            .MinimumLength(3).WithMessage("Restaurant code must be at least 3 characters long.")
            .MaximumLength(64)
            .Matches(SlugPattern)
            .WithMessage("Restaurant code may contain only lower-case letters, digits and hyphens.");

        RuleFor(command => command.ContactEmail)
            .EmailAddress().WithMessage("Contact email must be a valid address.")
            .MaximumLength(256)
            .When(command => !string.IsNullOrWhiteSpace(command.ContactEmail));

        RuleFor(command => command.PhoneNumber).MaximumLength(40);
        RuleFor(command => command.Address).MaximumLength(300);

        RuleFor(command => command.CurrencyCode)
            .Length(3).WithMessage("Currency code must be a three-letter ISO code.")
            .Matches("^[A-Za-z]{3}$").WithMessage("Currency code must be three letters.")
            .When(command => !string.IsNullOrWhiteSpace(command.CurrencyCode));

        RuleFor(command => command.TimeZoneId).MaximumLength(64);
    }
}
