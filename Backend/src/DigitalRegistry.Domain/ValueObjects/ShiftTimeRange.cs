namespace DigitalRegistry.Domain.ValueObjects;

/// <summary>
/// A half-open time interval <c>[Start, End)</c> used for shifts and reservations.
/// </summary>
/// <remarks>
/// The overlap rule lives here rather than in a validator so that it is stated exactly once and
/// can be unit tested in isolation. Because the interval is half-open, a shift ending at 16:00 and
/// the next one starting at 16:00 are back-to-back, not overlapping.
/// <para>
/// Construction never throws: an inverted range is representable so that FluentValidation can
/// report it as a user-facing validation error instead of the API surfacing an exception.
/// </para>
/// </remarks>
public readonly record struct ShiftTimeRange(DateTime Start, DateTime End)
{
    /// <summary>True when <see cref="Start"/> genuinely precedes <see cref="End"/>.</summary>
    public bool IsChronological => Start < End;

    public TimeSpan Duration => End - Start;

    /// <summary>
    /// True when this range and <paramref name="other"/> share at least one instant.
    /// Implements <c>existing.Start &lt; candidate.End &amp;&amp; candidate.Start &lt; existing.End</c>.
    /// </summary>
    public bool Overlaps(ShiftTimeRange other) => Start < other.End && other.Start < End;

    /// <summary>True when <paramref name="moment"/> falls inside the range.</summary>
    public bool Contains(DateTime moment) => moment >= Start && moment < End;
}
