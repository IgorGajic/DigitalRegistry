using DigitalRegistry.Application.Features.Orders.Commands.VoidOrderItem;
using FluentValidation;

namespace DigitalRegistry.Application.Features.Orders.Commands.VoidPaidOrder;

public class VoidPaidOrderCommandValidator : AbstractValidator<VoidPaidOrderCommand>
{
    /// <summary>
    /// Reversing a settled bill is held to a longer explanation than cancelling a coffee: it moves
    /// money out of the day's takings, and "greska" is not an account of why.
    /// </summary>
    private const int MinimumReasonLength = 10;

    public VoidPaidOrderCommandValidator()
    {
        RuleFor(command => command.OrderId).NotEmpty();

        RuleFor(command => command.Reason)
            .NotEmpty().WithMessage("State why this settled bill is being reversed.")
            .MinimumLength(MinimumReasonLength)
                .WithMessage($"Give a reason of at least {MinimumReasonLength} characters.")
            .MaximumLength(500);
    }
}
