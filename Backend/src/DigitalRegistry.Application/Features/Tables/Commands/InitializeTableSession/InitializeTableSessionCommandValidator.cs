using FluentValidation;

namespace DigitalRegistry.Application.Features.Tables.Commands.InitializeTableSession;

public class InitializeTableSessionCommandValidator : AbstractValidator<InitializeTableSessionCommand>
{
    public InitializeTableSessionCommandValidator()
    {
        RuleFor(command => command.QrCodeToken)
            .NotEmpty().WithMessage("A QR code token is required.");
    }
}
