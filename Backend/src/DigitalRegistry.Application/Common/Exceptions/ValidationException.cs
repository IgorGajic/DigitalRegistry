using FluentValidation.Results;

namespace DigitalRegistry.Application.Common.Exceptions;

/// <summary>
/// Thrown by the validation pipeline behavior when a request fails its FluentValidation rules.
/// </summary>
/// <remarks>
/// Carries the failures grouped by property name so the API can return an RFC 7807
/// <c>ValidationProblemDetails</c> body that a client can map back onto its form fields.
/// </remarks>
public class ValidationException : Exception
{
    public ValidationException()
        : base("One or more validation failures occurred.")
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IEnumerable<ValidationFailure> failures)
        : this()
    {
        Errors = failures
            .GroupBy(failure => failure.PropertyName, failure => failure.ErrorMessage)
            .ToDictionary(group => group.Key, group => group.ToArray());
    }

    public IDictionary<string, string[]> Errors { get; }
}
