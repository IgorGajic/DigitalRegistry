namespace DigitalRegistry.Domain.Enums;

/// <summary>
/// How long a licence runs for.
/// </summary>
/// <remarks>
/// The numeric value is the term in months, so extending a licence is
/// <c>ExpiresAtUtc.AddMonths((int)plan)</c> and no lookup table is needed. Adding a term means adding
/// a member whose value is its length — nothing else in the system has to change.
/// </remarks>
public enum LicensePlan
{
    Monthly = 1,
    Quarterly = 3,
    SemiAnnual = 6,
    Annual = 12
}
