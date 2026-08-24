namespace DigitalRegistry.Domain.Enums;

/// <summary>
/// Where a licence stands.
/// </summary>
/// <remarks>
/// <see cref="Expired"/> is never written by the platform administrator; it is what a licence becomes
/// once its end date passes. Storing it would mean something has to run every night to keep the column
/// honest, and a licence that quietly stayed <see cref="Active"/> because that job failed would let an
/// unpaid venue keep trading. The stored value therefore only records an administrator's decision, and
/// expiry is derived from the date at the moment it is asked about.
/// </remarks>
public enum LicenseStatus
{
    /// <summary>Issued and, if its end date has not passed, valid.</summary>
    Active = 1,

    /// <summary>Its end date has passed. Derived, never stored.</summary>
    Expired = 2,

    /// <summary>Switched off by the platform administrator before its end date.</summary>
    Suspended = 3,

    /// <summary>Ended early and not to be renewed.</summary>
    Cancelled = 4
}
