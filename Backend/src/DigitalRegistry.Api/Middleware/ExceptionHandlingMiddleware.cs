using DigitalRegistry.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;
using ValidationException = DigitalRegistry.Application.Common.Exceptions.ValidationException;

namespace DigitalRegistry.Api.Middleware;

/// <summary>
/// Turns unhandled exceptions into RFC 7807 problem responses.
/// </summary>
/// <remarks>
/// Anything not recognised below becomes a 500 whose body carries no exception detail outside
/// development, so stack traces and SQL never reach a client. The full exception is always logged.
/// </remarks>
public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger,
    IHostEnvironment environment)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            await WriteProblemAsync(context, exception);
        }
    }

    private async Task WriteProblemAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            // The response is already on the wire; all that is left is to record what went wrong.
            logger.LogError(exception, "Exception thrown after the response had started; cannot convert it to a problem response.");
            return;
        }

        var problem = CreateProblemDetails(exception, context);

        logger.LogError(
            exception,
            "Request {Method} {Path} failed with {StatusCode}",
            context.Request.Method,
            context.Request.Path,
            problem.Status);

        problem.Instance = context.Request.Path;
        problem.Extensions["traceId"] = context.TraceIdentifier;

        context.Response.Clear();
        context.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsJsonAsync(problem, problem.GetType());
    }

    private ProblemDetails CreateProblemDetails(Exception exception, HttpContext context) => exception switch
    {
        ValidationException validationException => new ValidationProblemDetails(validationException.Errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
        },

        // A domain invariant was reached without being pre-checked: the request is well formed but
        // conflicts with the current state of the data.
        DomainException domainException => new ProblemDetails
        {
            Status = StatusCodes.Status409Conflict,
            Title = "The request conflicts with the current state.",
            Detail = domainException.Message,
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10"
        },

        UnauthorizedAccessException => new ProblemDetails
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "You are not allowed to perform this action.",
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.4"
        },

        OperationCanceledException when context.RequestAborted.IsCancellationRequested => new ProblemDetails
        {
            // The caller hung up; nothing will read this body, but 499 keeps the logs honest.
            Status = 499,
            Title = "The request was cancelled by the client."
        },

        _ => new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Detail = environment.IsDevelopment() ? exception.ToString() : null,
            Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1"
        }
    };
}
