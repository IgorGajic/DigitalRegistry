using DigitalRegistry.Application.Common.Models;

namespace DigitalRegistry.Application.Common.Interfaces;

/// <summary>
/// Answers whether a restaurant is currently licensed to use the till.
/// </summary>
/// <remarks>
/// Asked on every request by the licence guard, and answered from the database every time.
/// <para>
/// An in-process cache was tried and removed. The till and the master application are separate hosts
/// sharing one database, so a cache invalidated by the host that sells a licence is invisible to the
/// host that enforces it: a venue that had just paid stayed locked out until the entry lapsed on its
/// own. What the cache saved — one query against a covering index on a table holding a row per
/// licence term — was never worth that.
/// </para>
/// <para>
/// The token cannot carry this either: an access token lives for hours, and a licence that lapses, or
/// is paid, mid-shift has to take effect long before the staff sign in again.
/// </para>
/// </remarks>
public interface ILicenseService
{
    /// <summary>The restaurant's licence state as it stands now.</summary>
    Task<LicenseState> GetStateAsync(Guid restaurantId, CancellationToken cancellationToken = default);
}
