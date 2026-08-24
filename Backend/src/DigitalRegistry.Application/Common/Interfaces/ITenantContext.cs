namespace DigitalRegistry.Application.Common.Interfaces;

/// <summary>
/// The restaurant whose data the current request is allowed to see.
/// </summary>
/// <remarks>
/// Resolved once per request from the token's restaurant claim and consumed by the DbContext, which
/// uses it both to filter every query and to stamp every insert. Handlers should not read it to build
/// <c>Where</c> clauses by hand — that is exactly the duplication the global filter exists to remove.
/// <para>
/// The master application registers an implementation with no tenant at all, and reads across
/// restaurants by explicitly ignoring the query filters.
/// </para>
/// </remarks>
public interface ITenantContext
{
    /// <summary>
    /// The current restaurant, or <see cref="Guid.Empty"/> when the request has no tenant — an
    /// unauthenticated call, or the master application.
    /// </summary>
    Guid RestaurantId { get; }

    /// <summary>True when a restaurant could be resolved for this request.</summary>
    bool HasTenant { get; }
}
