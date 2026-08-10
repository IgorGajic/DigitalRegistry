namespace DigitalRegistry.Domain.Common;

/// <summary>
/// Base class for every persisted entity: identity plus audit timestamps.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Set once on insert. Stored in UTC.</summary>
    public DateTime Created { get; set; } = DateTime.UtcNow;

    /// <summary>Refreshed on every update by the DbContext's SaveChanges override. Stored in UTC.</summary>
    public DateTime Modified { get; set; } = DateTime.UtcNow;
}
