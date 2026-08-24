using FluentValidation;

namespace DigitalRegistry.Application.Features.Shifts.Commands.GenerateSchedule;

public class GenerateScheduleCommandValidator : AbstractValidator<GenerateScheduleCommand>
{
    /// <summary>
    /// How far ahead one run may build.
    /// </summary>
    /// <remarks>
    /// A year is more rota than any venue plans at once, and the bound stops a mistyped date from
    /// generating decades of shifts that then have to be found and removed.
    /// </remarks>
    private const int MaximumDays = 366;

    public GenerateScheduleCommandValidator()
    {
        RuleFor(command => command.ToDate)
            .GreaterThanOrEqualTo(command => command.FromDate)
            .WithMessage("The period cannot end before it starts.");

        RuleFor(command => command)
            .Must(command => command.ToDate.DayNumber - command.FromDate.DayNumber < MaximumDays)
            .WithMessage($"Generate at most {MaximumDays} days at a time.");
    }
}
