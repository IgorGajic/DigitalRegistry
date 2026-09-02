using DigitalRegistry.Domain.Entities;
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

        RuleFor(command => command.Fixtures).NotNull();

        // A room is a drawing, not a database. This is not a correctness rule but a ceiling: it keeps
        // one request from arriving with ten thousand rectangles in it.
        RuleFor(command => command.Fixtures)
            .Must(fixtures => fixtures.Count <= MaxFixturesPerRoom)
            .WithMessage($"A room may hold at most {MaxFixturesPerRoom} fixtures.");

        RuleForEach(command => command.Fixtures).ChildRules(fixture =>
        {
            fixture.RuleFor(entry => entry.Label)
                .NotEmpty().WithMessage("A fixture needs a label.")
                .MaximumLength(RoomFixture.MaxLabelLength);

            fixture.RuleFor(entry => entry.PositionX)
                .GreaterThanOrEqualTo(0).WithMessage("A fixture cannot sit outside the room.");
            fixture.RuleFor(entry => entry.PositionY)
                .GreaterThanOrEqualTo(0).WithMessage("A fixture cannot sit outside the room.");

            // Wider than a table is allowed on purpose: a bar or a partition often runs the length
            // of the room, which is the whole reason it is worth drawing.
            fixture.RuleFor(entry => entry.Width)
                .InclusiveBetween(RoomFixture.MinSize, 2000)
                .WithMessage($"Fixture width must be between {RoomFixture.MinSize} and 2000.");
            fixture.RuleFor(entry => entry.Height)
                .InclusiveBetween(RoomFixture.MinSize, 2000)
                .WithMessage($"Fixture height must be between {RoomFixture.MinSize} and 2000.");

            fixture.RuleFor(entry => entry.Kind).IsInEnum();
            fixture.RuleFor(entry => entry.Shape).IsInEnum();
            fixture.RuleFor(entry => entry.Tone).IsInEnum();

            fixture.RuleFor(entry => entry.Rotation)
                .InclusiveBetween(0, 359).WithMessage("Rotation must be between 0 and 359 degrees.");
        });

        // A null id means "newly drawn", so only the saved ones are checked for duplicates.
        RuleFor(command => command.Fixtures)
            .Must(fixtures =>
            {
                var ids = fixtures.Where(entry => entry.Id.HasValue).Select(entry => entry.Id).ToList();

                return ids.Distinct().Count() == ids.Count;
            })
            .WithMessage("The same fixture appears more than once in the layout.");
    }

    /// <summary>Ceiling on how much can be drawn in one room.</summary>
    private const int MaxFixturesPerRoom = 60;
}
