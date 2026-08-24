using DigitalRegistry.Application.Common.Interfaces;
using DigitalRegistry.Application.Common.Models;
using Microsoft.AspNetCore.Mvc;

namespace DigitalRegistry.Api.Middleware;

/// <summary>
/// Refuses the till to a restaurant whose licence has lapsed.
/// </summary>
/// <remarks>
/// Runs after authentication, because the restaurant comes from the validated token. Enforcing it here
/// rather than in each handler means a new endpoint is covered the moment it is written; forgetting to
/// add an attribute is not a way to give away the product.
/// <para>
/// The answer is <c>402 Payment Required</c> rather than 403: the caller is who they say they are and
/// would be allowed to do this, the venue simply has not paid. The client distinguishes the two by the
/// <c>code</c> extension and shows a renewal screen instead of an access error.
/// </para>
/// </remarks>
public sealed class LicenseGuardMiddleware(
    RequestDelegate next,
    ILogger<LicenseGuardMiddleware> logger)
{
    /// <summary>Machine-readable marker the client keys its "licence expired" screen off.</summary>
    public const string ExpiredCode = "LICENSE_EXPIRED";

    /// <summary>
    /// Paths that must keep working while a licence is lapsed.
    /// </summary>
    /// <remarks>
    /// Signing in has to succeed so the client can find out <em>why</em> it is locked out, and the
    /// status endpoint is what tells it. Refusing both would leave the owner staring at an error with
    /// no way to learn that a payment is what is needed.
    /// </remarks>
    private static readonly string[] AlwaysAllowedPaths =
    [
        "/api/auth",
        "/api/license",
        "/swagger",
        "/health"
    ];

    public async Task InvokeAsync(HttpContext context, ILicenseService licenseService, ITenantContext tenant)
    {
        if (IsAlwaysAllowed(context.Request.Path) || !tenant.HasTenant)
        {
            // No tenant means either an unauthenticated request, which authorization will deal with,
            // or a platform administrator, who is not subject to any one venue's licence.
            await next(context);
            return;
        }

        var state = await licenseService.GetStateAsync(tenant.RestaurantId, context.RequestAborted);

        if (state.IsValid)
        {
            await next(context);
            return;
        }

        logger.LogWarning(
            "Blocked {Method} {Path} for restaurant {RestaurantId}: licence is {Status}.",
            context.Request.Method,
            context.Request.Path,
            tenant.RestaurantId,
            state.Status);

        await WritePaymentRequiredAsync(context, state);
    }

    private static bool IsAlwaysAllowed(PathString path) =>
        AlwaysAllowedPaths.Any(allowed => path.StartsWithSegments(allowed, StringComparison.OrdinalIgnoreCase));

    private static async Task WritePaymentRequiredAsync(HttpContext context, LicenseState state)
    {
        var problem = new ProblemDetails
        {
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.3",
            Title = "The restaurant's licence is not valid.",
            Status = StatusCodes.Status402PaymentRequired,
            Detail = "This restaurant's licence has lapsed. Renew it to continue using the till.",
            Instance = context.Request.Path
        };

        problem.Extensions["code"] = ExpiredCode;
        problem.Extensions["licenseStatus"] = state.Status.ToString();
        problem.Extensions["expiresAtUtc"] = state.ExpiresAtUtc;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(problem);
    }
}
