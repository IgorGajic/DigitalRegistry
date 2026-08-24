namespace DigitalRegistry.Domain.Enums;

/// <summary>
/// What was cancelled.
/// </summary>
/// <remarks>
/// The three differ in what they cost the venue and who may authorise them, which is why they are
/// recorded apart rather than as one undifferentiated "cancellation".
/// </remarks>
public enum VoidType
{
    /// <summary>
    /// Part or all of one line on a running tab — a mis-key, or a guest changing their mind.
    /// </summary>
    Item = 1,

    /// <summary>
    /// A whole tab that was never paid: a party that walked out, or an order opened on the wrong table.
    /// </summary>
    OpenOrder = 2,

    /// <summary>
    /// A settled bill reversed after the fact. The takings go down, so this one needs a manager.
    /// </summary>
    PaidOrder = 3
}
