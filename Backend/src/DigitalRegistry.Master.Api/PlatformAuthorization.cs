namespace DigitalRegistry.Master.Api;

/// <summary>
/// The master application's authorization policy.
/// </summary>
/// <remarks>
/// One policy, not a matrix. Managing the platform is a single privilege held by a single kind of
/// account, so subdividing it would add ceremony without expressing anything the role does not
/// already say. The restaurant-facing matrix in
/// <see cref="Application.Common.Security.AuthorizationPolicies"/> is a separate thing entirely and
/// has no member that satisfies this.
/// </remarks>
public static class PlatformAuthorization
{
    public const string PlatformAdminOnly = nameof(PlatformAdminOnly);
}
