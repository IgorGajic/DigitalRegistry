using DigitalRegistry.Application.Common.Interfaces;
using FluentValidation;

namespace DigitalRegistry.Application.Features.Reservations.Commands.CreateReservation;

public class CreateReservationCommandValidator : AbstractValidator<CreateReservationCommand>
{
    /// <summary>How far ahead a booking may be made.</summary>
    private static readonly TimeSpan MaximumLeadTime = TimeSpan.FromDays(365);

    /// <summary>Longest single sitting a guest may book.</summary>
    private static readonly TimeSpan MaximumDuration = TimeSpan.FromHours(6);

    public CreateReservationCommandValidator(IDateTimeService dateTimeService)
    {
        RuleFor(command => command.TableId)
            .NotEmpty().WithMessage("A table must be chosen.");

        RuleFor(command => command.PartySize)
            .GreaterThan(0).WithMessage("Party size must be at least one guest.")
            // The upper bound against the table's real capacity needs the database, so the handler
            // checks it; this only rejects obvious nonsense.
            .LessThanOrEqualTo(50).WithMessage("Party size must be 50 guests or fewer.");

        // Whether a name is allowed at all is the handler's decision, since it depends on the
        // caller's role; this only says that one given has to be usable on a service sheet.
        RuleFor(command => command.ContactName)
            .MaximumLength(200).WithMessage("The guest's name must be 200 characters or fewer.")
            .MinimumLength(2).WithMessage("The guest's name is too short to identify anybody.")
            .When(command => !string.IsNullOrWhiteSpace(command.ContactName));

        RuleFor(command => command.ContactPhone)
            .MaximumLength(50).WithMessage("The contact number must be 50 characters or fewer.")
            .When(command => !string.IsNullOrWhiteSpace(command.ContactPhone));

        RuleFor(command => command.StartTime)
            .GreaterThan(_ => dateTimeService.UtcNow)
            .WithMessage("A reservation must start in the future.")
            .LessThan(_ => dateTimeService.UtcNow.Add(MaximumLeadTime))
            .WithMessage($"A reservation cannot be made more than {MaximumLeadTime.Days} days ahead.");

        RuleFor(command => command.EndTime)
            .GreaterThan(command => command.StartTime)
            .WithMessage("The reservation must end after it starts.");

        RuleFor(command => command)
            .Must(command => (command.EndTime - command.StartTime) <= MaximumDuration)
            .WithMessage($"A reservation cannot be longer than {MaximumDuration.TotalHours} hours.")
            .WithName(nameof(CreateReservationCommand.EndTime));
    }
}
