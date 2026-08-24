using FluentValidation;

namespace DigitalRegistry.Application.Features.FloorPlan.Commands.CreateRoom;

public class CreateRoomCommandValidator : AbstractValidator<CreateRoomCommand>
{
    /// <summary>
    /// Bounds on a room's coordinate space.
    /// </summary>
    /// <remarks>
    /// Not a physical measurement — the client scales this to its viewport. The range exists so a
    /// mistyped value cannot produce a canvas too small to place anything on, or so large that tables
    /// become invisible specks.
    /// </remarks>
    public const int MinimumCanvasSize = 200;

    /// <inheritdoc cref="MinimumCanvasSize" />
    public const int MaximumCanvasSize = 5000;

    public CreateRoomCommandValidator()
    {
        RuleFor(command => command.Name)
            .NotEmpty().WithMessage("Room name is required.")
            .MaximumLength(100);

        RuleFor(command => command.DisplayOrder)
            .GreaterThanOrEqualTo(0)
            .When(command => command.DisplayOrder.HasValue);

        RuleFor(command => command.CanvasWidth)
            .InclusiveBetween(MinimumCanvasSize, MaximumCanvasSize)
            .When(command => command.CanvasWidth.HasValue);

        RuleFor(command => command.CanvasHeight)
            .InclusiveBetween(MinimumCanvasSize, MaximumCanvasSize)
            .When(command => command.CanvasHeight.HasValue);
    }
}
