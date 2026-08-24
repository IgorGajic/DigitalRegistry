using DigitalRegistry.Domain.Enums;
using FluentValidation;

namespace DigitalRegistry.Application.Features.Staff.Commands.UpdateStaffAccount;

public class UpdateStaffAccountCommandValidator : AbstractValidator<UpdateStaffAccountCommand>
{
    public UpdateStaffAccountCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();

        RuleFor(command => command.FirstName)
            .NotEmpty().WithMessage("First name is required.")
            .MaximumLength(100);

        RuleFor(command => command.LastName)
            .NotEmpty().WithMessage("Last name is required.")
            .MaximumLength(100);

        RuleFor(command => command.Role)
            .Must(role => role is UserRole.Waiter or UserRole.Manager or UserRole.Owner)
            .WithMessage("Staff can only be waiters, managers or the owner.");
    }
}
