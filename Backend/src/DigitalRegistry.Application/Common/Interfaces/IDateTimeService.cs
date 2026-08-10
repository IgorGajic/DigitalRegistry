namespace DigitalRegistry.Application.Common.Interfaces;

/// <summary>
/// Supplies the current time, so that time-dependent rules can be tested deterministically.
/// </summary>
public interface IDateTimeService
{
    DateTime UtcNow { get; }

    DateOnly TodayUtc { get; }
}
