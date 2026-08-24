using FluentValidation;

namespace DigitalRegistry.Application.Features.Menu.Commands.SetRecipe;

public class SetRecipeCommandValidator : AbstractValidator<SetRecipeCommand>
{
    public SetRecipeCommandValidator()
    {
        RuleFor(command => command.MenuItemId).NotEmpty();

        // An empty list is valid: it means the item consumes nothing tracked, which is how a venue
        // sells something it does not keep stock of.
        RuleFor(command => command.Lines).NotNull();

        RuleForEach(command => command.Lines).ChildRules(line =>
        {
            line.RuleFor(entry => entry.IngredientId).NotEmpty();

            line.RuleFor(entry => entry.QuantityRequired)
                .GreaterThan(0).WithMessage("A recipe line must consume more than zero.");
        });

        RuleFor(command => command.Lines)
            .Must(lines => lines.Select(line => line.IngredientId).Distinct().Count() == lines.Count)
            .WithMessage("The same ingredient appears more than once; combine it into one line.");
    }
}
