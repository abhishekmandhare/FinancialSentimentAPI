namespace Application.Exceptions;

/// <summary>
/// Thrown when a requested resource does not exist.
/// The ExceptionHandlingMiddleware maps this to HTTP 404 Not Found.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string resource, object key)
        : base($"{resource} with key '{key}' was not found.") { }
}
