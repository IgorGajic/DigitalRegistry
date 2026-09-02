namespace DigitalRegistry.Domain.Enums;

/// <summary>
/// How a room fixture is drawn on the floor plan.
/// </summary>
/// <remarks>
/// Two shapes rather than the three <see cref="TableShape"/> offers, and a separate enum rather than
/// a reuse of it. That one is documented as how a <em>table</em> is drawn, and its square/rectangle
/// distinction only ever mattered for tables. Here a rectangle with equal sides is a square and an
/// ellipse with equal sides is a circle, so two values cover everything a bar, a doorway or a
/// partition needs.
/// </remarks>
public enum FixtureShape
{
    Rectangle = 1,
    Ellipse = 2
}
