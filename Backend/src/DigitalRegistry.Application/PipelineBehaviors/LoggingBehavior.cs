using System.Diagnostics;
using DigitalRegistry.Application.Common.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace DigitalRegistry.Application.PipelineBehaviors;

/// <summary>
/// Logs each request with the calling user and how long the handler took.
/// </summary>
/// <remarks>
/// Only the request's type name is logged, never its contents, so passwords and other request data
/// never reach the log. Anything slower than <see cref="SlowRequestThresholdMs"/> is raised to a
/// warning to make problem endpoints visible without trawling the logs.
/// </remarks>
public class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger,
    ICurrentUserService currentUserService) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private const long SlowRequestThresholdMs = 500;

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId = currentUserService.UserId?.ToString() ?? "anonymous";
        var stopwatch = Stopwatch.StartNew();

        logger.LogInformation("Handling {RequestName} for user {UserId}", requestName, userId);

        try
        {
            var response = await next(cancellationToken);
            stopwatch.Stop();

            if (stopwatch.ElapsedMilliseconds > SlowRequestThresholdMs)
            {
                logger.LogWarning(
                    "{RequestName} completed in {ElapsedMilliseconds} ms, exceeding the {Threshold} ms threshold",
                    requestName,
                    stopwatch.ElapsedMilliseconds,
                    SlowRequestThresholdMs);
            }
            else
            {
                logger.LogInformation(
                    "Handled {RequestName} in {ElapsedMilliseconds} ms",
                    requestName,
                    stopwatch.ElapsedMilliseconds);
            }

            return response;
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            logger.LogError(
                exception,
                "{RequestName} failed after {ElapsedMilliseconds} ms for user {UserId}",
                requestName,
                stopwatch.ElapsedMilliseconds,
                userId);
            throw;
        }
    }
}
