namespace DigitalRegistry.Domain.Enums;

/// <summary>
/// How a table is drawn on the floor plan.
/// </summary>
/// <remarks>
/// Presentation, not domain rule: nothing about seating or ordering depends on it. It lives here
/// because the shape is a property of the physical table the owner arranges, and is stored with the
/// rest of its layout.
/// </remarks>
public enum TableShape
{
    Round = 1,
    Rectangle = 2,
    Square = 3
}
