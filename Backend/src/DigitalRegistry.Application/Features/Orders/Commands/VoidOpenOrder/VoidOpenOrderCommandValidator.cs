using DigitalRegistry.Application.Features.Orders.Commands.VoidOrderItem;
using FluentValidation;

namespace DigitalRegistry.Application.Features.Orders.Commands.VoidOpenOrder;

public class VoidOpenOrderCommandValidator : AbstractValidator<VoidOpenOrderCommand>
{
    public VoidOpenOrderCommandValidator()
    {
        RuleFor(command => command.OrderId).NotEmpty();

        RuleFor(command => command.Reason)
            .NotEmpty().WithMessage("State why this tab is being cancelled.")
            .MinimumLength(VoidOrderItemCommandValidator.MinimumReasonLength)
                .WithMessage("Give a reason of at least "
                             + $"{VoidOrderItemCommandValidator.MinimumReasonLength} characters.")
            .MaximumLength(500);
    }
}
