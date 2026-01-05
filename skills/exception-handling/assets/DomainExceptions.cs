// Domain Exception Types
// Copy to: src/Domain/Common/Exceptions/

namespace YourNamespace.Domain.Common.Exceptions;

/// <summary>
/// Base class for domain-specific exceptions.
/// These exceptions represent expected error conditions that should be
/// caught and converted to appropriate HTTP responses.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
    protected DomainException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when a requested resource is not found.
/// Maps to HTTP 404 Not Found.
/// </summary>
public sealed class NotFoundException : DomainException
{
    public string Resource { get; }
    public object Id { get; }

    public NotFoundException(string resource, object id)
        : base($"{resource} with ID '{id}' was not found")
    {
        Resource = resource;
        Id = id;
    }
}

/// <summary>
/// Thrown when validation fails.
/// Maps to HTTP 400 Bad Request.
/// Prefer using Result pattern for validation in handlers.
/// </summary>
public sealed class ValidationException : DomainException
{
    public IReadOnlyList<ValidationError> Errors { get; }

    public ValidationException(string message) : base(message)
    {
        Errors = [];
    }

    public ValidationException(IEnumerable<ValidationError> errors)
        : base("One or more validation errors occurred")
    {
        Errors = errors.ToList();
    }
}

public sealed record ValidationError(string PropertyName, string ErrorMessage);

/// <summary>
/// Thrown when authentication is required but not provided.
/// Maps to HTTP 401 Unauthorized.
/// </summary>
public sealed class UnauthorizedException : DomainException
{
    public UnauthorizedException() : base("Authentication required") { }
    public UnauthorizedException(string message) : base(message) { }
}

/// <summary>
/// Thrown when the user is authenticated but lacks permission.
/// Maps to HTTP 403 Forbidden.
/// </summary>
public sealed class ForbiddenException : DomainException
{
    public ForbiddenException() : base("Access denied") { }
    public ForbiddenException(string message) : base(message) { }
}

/// <summary>
/// Thrown when an operation conflicts with the current state.
/// Maps to HTTP 409 Conflict.
/// </summary>
public sealed class ConflictException : DomainException
{
    public ConflictException(string message) : base(message) { }
}
