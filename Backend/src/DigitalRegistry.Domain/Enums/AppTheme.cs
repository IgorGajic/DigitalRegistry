namespace DigitalRegistry.Domain.Enums;

/// <summary>
/// The palette a venue's till is painted in.
/// </summary>
/// <remarks>
/// A closed list rather than a colour the owner picks, and the reason is the floor screen. On it,
/// colour <em>means</em> status — free, occupied, reserved — and those four hues have to stay
/// separable from the chrome and from each other on whatever surface sits behind them. Each theme
/// here restates them, and the takings chart, against its own background; an arbitrary colour could
/// not be checked in advance and would eventually produce a till whose most important signal had
/// quietly stopped being readable.
/// <para>
/// Held on the restaurant, not the user: the owner chooses for the venue and the staff find it that
/// way. A member of staff moving between two venues should see each one as its own.
/// </para>
/// </remarks>
public enum AppTheme
{
    /// <summary>Petrol ink on light. What the till has always looked like.</summary>
    Petrol = 1,

    /// <summary>Charcoal. The dark one.</summary>
    Charcoal = 2,

    /// <summary>Deep green, dark.</summary>
    Forest = 3,

    /// <summary>
    /// Warm sand, light.
    /// </summary>
    /// <remarks>
    /// Light on purpose, where the other warm option would have been a dark brown. Occupied is red
    /// and reserved is orange — the two hues nearest brown in the whole application — and a dark
    /// brown ground is the one surface on which they start to read as part of the furniture.
    /// </remarks>
    Sand = 4
}
