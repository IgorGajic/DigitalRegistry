using FluentValidation;

namespace DigitalRegistry.Application.Features.Orders.Commands.ProcessPayment;

public class ProcessPaymentCommandValidator : AbstractValidator<ProcessPaymentCommand>
{
    public ProcessPaymentCommandValidator()
    {
        RuleFor(command => command.OrderId)
            .NotEmpty().WithMessage("An order must be named.");

        RuleFor(command => command.PaymentMethod)
            .IsInEnum().WithMessage("Unknown payment method.");
    }
}
