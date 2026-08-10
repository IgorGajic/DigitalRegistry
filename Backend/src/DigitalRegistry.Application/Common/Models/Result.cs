namespace DigitalRegistry.Application.Common.Models;

/// <summary>
/// Why an operation failed, so the API can choose an HTTP status without inspecting messages.
/// </summary>
public enum ResultErrorType
{
    None = 0,
    Validation = 1,
    NotFound = 2,
    Forbidden = 3,
    Conflict = 4,
    Unauthorized = 5
}

/// <summary>
/// The outcome of a command or query: success, or a categorised failure.
/// </summary>
/// <remarks>
/// Expected, user-facing failures travel back as a failed result rather than an exception, so the
/// happy path stays free of try/catch. Genuine invariant breaches still throw
/// <see cref="Domain.Exceptions.DomainException"/> and are caught by the API middleware.
/// </remarks>
public class Result
{
    protected Result(bool succeeded, ResultErrorType errorType, IEnumerable<string> errors)
    {
        Succeeded = succeeded;
        ErrorType = errorType;
        Errors = errors.ToArray();
    }

    public bool Succeeded { get; }

    public ResultErrorType ErrorType { get; }

    public IReadOnlyList<string> Errors { get; }

    /// <summary>The first error message, or an empty string on success.</summary>
    public string Error => Errors.Count > 0 ? Errors[0] : string.Empty;

    public static Result Success() => new(true, ResultErrorType.None, []);

    public static Result Failure(ResultErrorType errorType, params string[] errors) =>
        new(false, errorType, errors);

    public static Result NotFound(string message) => Failure(ResultErrorType.NotFound, message);

    public static Result Forbidden(string message) => Failure(ResultErrorType.Forbidden, message);

    public static Result Conflict(string message) => Failure(ResultErrorType.Conflict, message);

    public static Result Invalid(params string[] errors) => Failure(ResultErrorType.Validation, errors);

    public static Result Unauthorized(string message) => Failure(ResultErrorType.Unauthorized, message);
}

/// <summary>
/// The outcome of an operation that returns a value on success.
/// </summary>
public class Result<T> : Result
{
    private readonly T? _value;

    private Result(bool succeeded, T? value, ResultErrorType errorType, IEnumerable<string> errors)
        : base(succeeded, errorType, errors)
    {
        _value = value;
    }

    /// <summary>
    /// The produced value.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when read from a failed result.</exception>
    public T Value => Succeeded
        ? _value!
        : throw new InvalidOperationException("Cannot read the value of a failed result.");

    public static Result<T> Success(T value) => new(true, value, ResultErrorType.None, []);

    public static new Result<T> Failure(ResultErrorType errorType, params string[] errors) =>
        new(false, default, errorType, errors);

    public static new Result<T> NotFound(string message) => Failure(ResultErrorType.NotFound, message);

    public static new Result<T> Forbidden(string message) => Failure(ResultErrorType.Forbidden, message);

    public static new Result<T> Conflict(string message) => Failure(ResultErrorType.Conflict, message);

    public static new Result<T> Invalid(params string[] errors) => Failure(ResultErrorType.Validation, errors);

    public static new Result<T> Unauthorized(string message) => Failure(ResultErrorType.Unauthorized, message);
}
