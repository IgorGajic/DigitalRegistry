using FluentValidation;

namespace DigitalRegistry.Application.Features.Inventory.Commands.AdjustStock;

public class AdjustStockCommandValidator : AbstractValidator<AdjustStockCommand>
{
    public AdjustStockCommandValidator()
    {
        RuleFor(command => command.IngredientId).NotEmpty();

        RuleFor(command => command.CountedQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("A stocktake cannot find less than nothing.");

        RuleFor(command => command.Reason)
            .NotEmpty().WithMessage("State why the stock is being corrected.")
            .MinimumLength(3).WithMessage("Give a reason of at least 3 characters.")
            .MaximumLength(500);
    }
}
