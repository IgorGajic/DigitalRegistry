using FluentValidation;

namespace DigitalRegistry.Application.Features.Orders.Commands.VoidOrderItem;

public class VoidOrderItemCommandValidator : AbstractValidator<VoidOrderItemCommand>
{
    /// <summary>
    /// Shortest reason accepted.
    /// </summary>
    /// <remarks>
    /// Long enough to stop "x" and "ok" from passing for an explanation, short enough that "lom" or
    /// "greska" still does. The record is only worth keeping if what it says is worth reading.
    /// </remarks>
    public const int MinimumReasonLength = 3;

    public VoidOrderItemCommandValidator()
    {
        RuleFor(command => command.OrderId).NotEmpty();
        RuleFor(command => command.OrderItemId).NotEmpty();

        RuleFor(command => command.Reason)
            .NotEmpty().WithMessage("State why this is being cancelled.")
            .MinimumLength(MinimumReasonLength)
                .WithMessage($"Give a reason of at least {MinimumReasonLength} characters.")
            .MaximumLength(500);

        RuleFor(command => command.Quantity!.Value)
            .GreaterThan(0).WithMessage("Cancel at least one serving.")
            .When(command => command.Quantity is not null);
    }
}
