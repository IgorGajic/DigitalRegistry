using FluentValidation;

namespace DigitalRegistry.Application.Features.Orders.Commands.UpdateOrderItem;

/// <summary>
/// Enforces which fields each kind of change requires, so a handler never has to guess what a
/// half-filled request meant.
/// </summary>
public class UpdateOrderItemCommandValidator : AbstractValidator<UpdateOrderItemCommand>
{
    public UpdateOrderItemCommandValidator()
    {
        RuleFor(command => command.OrderId)
            .NotEmpty().WithMessage("An order must be named.");

        RuleFor(command => command.Change)
            .IsInEnum().WithMessage("Unknown change type.");

        RuleFor(command => command.MenuItemId)
            .NotNull().NotEmpty()
            .When(command => command.Change is OrderItemChange.Add)
            .WithMessage("Adding a line requires a menu item.");

        RuleFor(command => command.OrderItemId)
            .NotNull().NotEmpty()
            .When(command => command.Change is not OrderItemChange.Add)
            .WithMessage("Changing or removing a line requires the line's id.");

        RuleFor(command => command.Quantity)
            .NotNull()
            .When(command => command.Change is OrderItemChange.Add or OrderItemChange.ChangeQuantity)
            .WithMessage("A quantity is required.");

        RuleFor(command => command.Quantity!.Value)
            .GreaterThan(0).WithMessage("Quantity must be greater than zero; remove the line instead.")
            .LessThanOrEqualTo(99).WithMessage("Quantity must be 99 or fewer.")
            .When(command => command.Quantity is not null);

        RuleFor(command => command.Notes)
            .MaximumLength(500).WithMessage("Notes must be 500 characters or fewer.");
    }
}
