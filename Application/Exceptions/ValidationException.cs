using FluentValidation.Results;

namespace Application.Exceptions;

/// <summary>
/// Thrown when a command or query fails FluentValidation.
/// The ExceptionHandlingMiddleware maps this to HTTP 422 Unprocessable Entity.
/// Different exception types → different HTTP status codes — one place for all mapping.
/// </summary>
public class ValidationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base("One or more validation failures occurred.")
    {
        Errors = failures
            .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray());
    }
}
