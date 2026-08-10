namespace DigitalRegistry.Domain.Exceptions;

/// <summary>
/// Thrown when an operation would break a domain invariant.
/// </summary>
/// <remarks>
/// These represent a caller asking for something the domain forbids (editing a paid order, taking
/// more stock than exists), not a programming defect. The API's exception middleware translates
/// them into 409 Conflict rather than 500. Expected, user-facing outcomes are normally returned as
/// a failed <c>Result</c> instead; this type is the invariant backstop for when a rule is reached
/// without having been pre-checked.
/// </remarks>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>Thrown when stock would be driven below zero.</summary>
public sealed class InsufficientStockException : DomainException
{
    public InsufficientStockException(string ingredientName, decimal requested, decimal available)
        : base($"Insufficient stock for '{ingredientName}': requested {requested}, available {available}.")
    {
        IngredientName = ingredientName;
        Requested = requested;
        Available = available;
    }

    public string IngredientName { get; }

    public decimal Requested { get; }

    public decimal Available { get; }
}
