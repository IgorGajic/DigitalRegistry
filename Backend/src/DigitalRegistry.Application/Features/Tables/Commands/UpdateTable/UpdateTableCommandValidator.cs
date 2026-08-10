using FluentValidation;

namespace DigitalRegistry.Application.Features.Tables.Commands.UpdateTable;

public class UpdateTableCommandValidator : AbstractValidator<UpdateTableCommand>
{
    public UpdateTableCommandValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty().WithMessage("Table id is required.");

        RuleFor(command => command.TableNumber)
            .GreaterThan(0).WithMessage("Table number must be greater than zero.");

        RuleFor(command => command.Capacity)
            .GreaterThan(0).WithMessage("Capacity must be greater than zero.")
            .LessThanOrEqualTo(50).WithMessage("Capacity must be 50 or fewer seats.");
    }
}
