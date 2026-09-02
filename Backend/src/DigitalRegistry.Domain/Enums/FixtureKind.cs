namespace DigitalRegistry.Domain.Enums;

/// <summary>
/// What a piece of room furniture is, for the fixtures drawn on a floor plan beside the tables.
/// </summary>
/// <remarks>
/// The kind carries no rule of its own. It exists so the layout editor can offer a sensible starting
/// point — an icon, a default label, a default size and a default tone — for the handful of things
/// every venue draws. Anything it does not cover is <see cref="Other"/> with a typed label.
/// </remarks>
public enum FixtureKind
{
    Bar = 1,
    Restroom = 2,
    Entrance = 3,
    Kitchen = 4,
    Stairs = 5,
    Partition = 6,
    Other = 7
}
