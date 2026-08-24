using FluentValidation;

namespace DigitalRegistry.Application.Features.Inventory.Commands.RecordStockEntry;

public class RecordStockEntryCommandValidator : AbstractValidator<RecordStockEntryCommand>
{
    public RecordStockEntryCommandValidator()
    {
        RuleFor(command => command.IngredientId).NotEmpty();

        RuleFor(command => command.Quantity)
            .GreaterThan(0).WithMessage("A delivery must be for more than zero.");

        RuleFor(command => command.PurchaseUnitPrice)
            .GreaterThanOrEqualTo(0).WithMessage("A purchase price cannot be negative.");

        RuleFor(command => command.TotalCost!.Value)
            .GreaterThanOrEqualTo(0).WithMessage("A total cost cannot be negative.")
            .When(command => command.TotalCost.HasValue);

        RuleFor(command => command.Supplier).MaximumLength(200);
        RuleFor(command => command.ReferenceNumber).MaximumLength(100);
        RuleFor(command => command.Note).MaximumLength(500);
    }
}
