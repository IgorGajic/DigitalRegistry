namespace DigitalRegistry.Domain.Enums;

/// <summary>
/// The colour a room fixture is drawn in, named rather than given as a value.
/// </summary>
/// <remarks>
/// A named tone and not a hex code, for two reasons.
/// <para>
/// The first is the floor screen. On it, colour <em>means</em> status — free, occupied, reserved,
/// out of service — and a waiter reads those from across the room. A free colour picker would sooner
/// or later produce a bar in a shade indistinguishable from an occupied table, and the one screen
/// where colour carries meaning would stop being trustworthy. A short list cannot.
/// </para>
/// <para>
/// The second is that the venue chooses its own palette. A stored hex would stay a pale bar on a
/// dark background; a stored tone is resolved by whichever theme is active, so a fixture follows the
/// room it is drawn in.
/// </para>
/// </remarks>
public enum FixtureTone
{
    /// <summary>Warm brown. The bar, wooden partitions.</summary>
    Wood = 1,

    /// <summary>Dark neutral. Service areas — kitchen, restrooms, back of house.</summary>
    Slate = 2,

    /// <summary>Light neutral. Structure that is simply there: walls, stairs, columns.</summary>
    Stone = 3,

    /// <summary>Cool and pale. Openings — doorways, windows, terrace edges.</summary>
    Glass = 4
}
