using FluentValidation;
using MediatR;
using ValidationException = DigitalRegistry.Application.Common.Exceptions.ValidationException;

namespace DigitalRegistry.Application.PipelineBehaviors;

/// <summary>
/// Runs every registered validator for a request before its handler executes.
/// </summary>
/// <remarks>
/// Placing validation in the pipeline means a handler can assume its input is structurally valid
/// and concern itself only with business rules that need the database. Requests with no validator
/// pass straight through.
/// </remarks>
public class ValidationBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var applicableValidators = validators.ToArray();

        if (applicableValidators.Length == 0)
        {
            return await next(cancellationToken);
        }

        var context = new ValidationContext<TRequest>(request);

        var results = await Task.WhenAll(
            applicableValidators.Select(validator => validator.ValidateAsync(context, cancellationToken)));

        var failures = results
            .SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToArray();

        if (failures.Length > 0)
        {
            throw new ValidationException(failures);
        }

        return await next(cancellationToken);
    }
}
