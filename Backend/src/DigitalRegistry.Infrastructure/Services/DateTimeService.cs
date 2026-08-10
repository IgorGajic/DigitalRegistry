using DigitalRegistry.Application.Common.Interfaces;

namespace DigitalRegistry.Infrastructure.Services;

/// <summary>
/// The real clock. Tests substitute their own so time-dependent rules stay deterministic.
/// </summary>
internal sealed class DateTimeService : IDateTimeService
{
    public DateTime UtcNow => DateTime.UtcNow;

    public DateOnly TodayUtc => DateOnly.FromDateTime(DateTime.UtcNow);
}
