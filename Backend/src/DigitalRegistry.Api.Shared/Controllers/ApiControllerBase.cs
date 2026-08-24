using DigitalRegistry.Application.Common.Models;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace DigitalRegistry.Api.Shared.Controllers;

/// <summary>
/// Shared plumbing for the API controllers: the MediatR sender and result-to-HTTP translation.
/// </summary>
/// <remarks>
/// Controllers in this project do two things only — send a request through MediatR and translate the
/// outcome into a status code. All decisions live in the Application layer's handlers.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ValidationProblemDetails))]
[ProducesResponseType(StatusCodes.Status401Unauthorized, Type = typeof(ProblemDetails))]
[ProducesResponseType(StatusCodes.Status403Forbidden, Type = typeof(ProblemDetails))]
public abstract class ApiControllerBase : ControllerBase
{
    private ISender? _sender;

    /// <summary>Resolved lazily so subclasses need no constructor of their own.</summary>
    protected ISender Sender => _sender ??= HttpContext.RequestServices.GetRequiredService<ISender>();

    /// <summary>Returns 200 with the value, or the status matching the failure.</summary>
    protected ActionResult ToActionResult<TValue>(Result<TValue> result) =>
        result.Succeeded ? Ok(result.Value) : Problem(result);

    /// <summary>Returns 204 on success, or the status matching the failure.</summary>
    protected ActionResult ToActionResult(Result result) =>
        result.Succeeded ? NoContent() : Problem(result);

    /// <summary>
    /// Returns 201 with a <c>Location</c> header on success, or the status matching the failure.
    /// </summary>
    protected ActionResult ToCreatedResult<TValue>(
        Result<TValue> result,
        string actionName,
        object routeValues) =>
        result.Succeeded
            ? CreatedAtAction(actionName, routeValues, result.Value)
            : Problem(result);

    /// <summary>
    /// Translates a failed result into a problem response, mapping the failure category to a status
    /// code so handlers never mention HTTP.
    /// </summary>
    private ActionResult Problem(Result result)
    {
        if (result.ErrorType is ResultErrorType.Validation)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            return ValidationProblem(ModelState);
        }

        var (statusCode, title) = result.ErrorType switch
        {
            ResultErrorType.NotFound => (StatusCodes.Status404NotFound, "Resource not found."),
            ResultErrorType.Forbidden => (StatusCodes.Status403Forbidden, "You are not allowed to perform this action."),
            ResultErrorType.Conflict => (StatusCodes.Status409Conflict, "The request conflicts with the current state."),
            ResultErrorType.Unauthorized => (StatusCodes.Status401Unauthorized, "Authentication failed."),
            _ => (StatusCodes.Status400BadRequest, "The request could not be processed.")
        };

        return Problem(
            detail: string.Join(" ", result.Errors),
            statusCode: statusCode,
            title: title,
            instance: HttpContext.Request.Path);
    }
}
