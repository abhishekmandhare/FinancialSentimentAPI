namespace Domain.Exceptions;

/// <summary>
/// Base exception for all domain rule violations.
/// Typed exceptions communicate intent — callers catch DomainException
/// to know a business rule was broken, not an infrastructure failure.
/// </summary>
public class DomainException : Exception
{
    public DomainException() : base() { }

    public DomainException(string message) : base(message) { }

    public DomainException(string message, Exception innerException)
        : base(message, innerException) { }
}
