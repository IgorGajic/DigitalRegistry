using DigitalRegistry.Application.Common.Interfaces;

namespace DigitalRegistry.Application.UnitTests.TestDoubles;

/// <summary>
/// A clock fixed to one instant, so anything derived from "now" is assertable.
/// </summary>
internal sealed class TestDateTimeService(DateTime? utcNow = null) : IDateTimeService
{
    /// <summary>The instant tests run at unless they say otherwise.</summary>
    public static readonly DateTime DefaultNow = new(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);

    public DateTime UtcNow { get; set; } = utcNow ?? DefaultNow;

    public DateOnly TodayUtc => DateOnly.FromDateTime(UtcNow);
}
