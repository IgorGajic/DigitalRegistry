using FluentValidation;

namespace DigitalRegistry.Application.Features.Tables.Commands.CreateTable;

public class CreateTableCommandValidator : AbstractValidator<CreateTableCommand>
{
    public CreateTableCommandValidator()
    {
        RuleFor(command => command.TableNumber)
            .GreaterThan(0).WithMessage("Table number must be greater than zero.");

        RuleFor(command => command.Capacity)
            .GreaterThan(0).WithMessage("Capacity must be greater than zero.")
            .LessThanOrEqualTo(50).WithMessage("Capacity must be 50 or fewer seats.");
    }
}
