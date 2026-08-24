using DigitalRegistry.Application.Features.FloorPlan.Commands.CreateRoom;
using FluentValidation;

namespace DigitalRegistry.Application.Features.FloorPlan.Commands.UpdateRoom;

public class UpdateRoomCommandValidator : AbstractValidator<UpdateRoomCommand>
{
    public UpdateRoomCommandValidator()
    {
        RuleFor(command => command.Id).NotEmpty();

        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("Room name is required.")
            .MaximumLength(100);

        RuleFor(command => command.DisplayOrder).GreaterThanOrEqualTo(0);

        // Same bounds as creation; stated once in the create validator.
        RuleFor(command => command.CanvasWidth)
            .InclusiveBetween(CreateRoomCommandValidator.MinimumCanvasSize, CreateRoomCommandValidator.MaximumCanvasSize);

        RuleFor(command => command.CanvasHeight)
            .InclusiveBetween(CreateRoomCommandValidator.MinimumCanvasSize, CreateRoomCommandValidator.MaximumCanvasSize);
    }
}
