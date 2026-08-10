using FluentValidation;

namespace DigitalRegistry.Application.Features.Orders.Commands.CreateGuestQrOrder;

public class CreateGuestQrOrderCommandValidator : AbstractValidator<CreateGuestQrOrderCommand>
{
    public CreateGuestQrOrderCommandValidator()
    {
        RuleFor(command => command.Items)
            .NotEmpty().WithMessage("An order must contain at least one item.");

        RuleForEach(command => command.Items).ChildRules(item =>
        {
            item.RuleFor(line => line.MenuItemId)
                .NotEmpty().WithMessage("Each line must name a menu item.");

            item.RuleFor(line => line.Quantity)
                .GreaterThan(0).WithMessage("Quantity must be greater than zero.")
                .LessThanOrEqualTo(20).WithMessage("Quantity must be 20 or fewer per line.");

            item.RuleFor(line => line.Notes)
                .MaximumLength(500).WithMessage("Notes must be 500 characters or fewer.");
        });
    }
}
