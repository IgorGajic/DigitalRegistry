namespace DigitalRegistry.Domain.Enums;

public enum OrderStatus
{
    Open = 1,
    InPreparation = 2,
    Served = 3,
    Paid = 4,

    /// <summary>Cancelled before it was ever paid.</summary>
    Cancelled = 5,

    /// <summary>
    /// Reversed after having been paid.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="Cancelled"/> because the two mean different things to the takings: a
    /// cancelled order never entered them, whereas a voided one did and has been backed out with a
    /// counter-transaction. Reports that reconcile against the till have to be able to tell them apart.
    /// </remarks>
    Voided = 6
}
