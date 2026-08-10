using FluentValidation;

namespace DigitalRegistry.Application.Features.Inventory.Commands.RestockIngredient;

public class RestockIngredientCommandValidator : AbstractValidator<RestockIngredientCommand>
{
    public RestockIngredientCommandValidator()
    {
        RuleFor(command => command.IngredientId)
            .NotEmpty().WithMessage("An ingredient must be named.");

        RuleFor(command => command.Quantity)
            .GreaterThan(0m).WithMessage("The restocked quantity must be greater than zero.")
            .LessThanOrEqualTo(1_000_000m).WithMessage("The restocked quantity is implausibly large.");
    }
}
