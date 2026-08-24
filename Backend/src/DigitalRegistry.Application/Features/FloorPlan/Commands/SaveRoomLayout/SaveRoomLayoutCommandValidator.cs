using FluentValidation;

namespace DigitalRegistry.Application.Features.FloorPlan.Commands.SaveRoomLayout;

public class SaveRoomLayoutCommandValidator : AbstractValidator<SaveRoomLayoutCommand>
{
    public SaveRoomLayoutCommandValidator()
    {
        RuleFor(command => command.RoomId).NotEmpty();

        // An empty list is valid: it means the owner has emptied the room.
        RuleFor(command => command.Tables).NotNull();

        RuleForEach(command => command.Tables).ChildRules(table =>
        {
            table.RuleFor(entry => entry.TableId).NotEmpty();

            // Negative coordinates would put a table off the canvas where it could not be dragged
            // back. The upper bound is checked in the handler, which knows the room's size.
            table.RuleFor(entry => entry.PositionX)
                .GreaterThanOrEqualTo(0).WithMessage("A table cannot sit outside the room.");
            table.RuleFor(entry => entry.PositionY)
                .GreaterThanOrEqualTo(0).WithMessage("A table cannot sit outside the room.");

            table.RuleFor(entry => entry.Width)
                .InclusiveBetween(20, 600).WithMessage("Table width must be between 20 and 600.");
            table.RuleFor(entry => entry.Height)
                .InclusiveBetween(20, 600).WithMessage("Table height must be between 20 and 600.");

            table.RuleFor(entry => entry.Shape).IsInEnum();

            table.RuleFor(entry => entry.Rotation)
                .InclusiveBetween(0, 359).WithMessage("Rotation must be between 0 and 359 degrees.");
        });

        RuleFor(command => command.Tables)
            .Must(tables => tables.Select(table => table.TableId).Distinct().Count() == tables.Count)
            .WithMessage("The same table appears more than once in the layout.");
    }
}
