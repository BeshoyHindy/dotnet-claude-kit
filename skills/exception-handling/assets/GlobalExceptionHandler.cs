// Global Exception Handler for .NET 8+
// Copy to: src/Infrastructure/ExceptionHandling/GlobalExceptionHandler.cs
// Requires: .NET 8+, Microsoft.AspNetCore.Http
// Requires: DomainExceptions.cs from this skill (exception-handling)

namespace YourNamespace.Infrastructure.ExceptionHandling;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using YourNamespace.Domain.Common.Exceptions; // NotFoundException, DomainValidationException, etc.

/// <summary>
/// Global exception handler that converts exceptions to Problem Details responses.
/// Implements IExceptionHandler for .NET 8+ exception handling pipeline.
/// </summary>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken ct)
    {
        logger.LogError(
            exception,
            "Unhandled exception occurred. TraceId: {TraceId}",
            httpContext.TraceIdentifier);

        var problemDetails = MapExceptionToProblemDetails(exception, httpContext);

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, ct);

        return true;
    }

    private static ProblemDetails MapExceptionToProblemDetails(
        Exception exception,
        HttpContext httpContext)
    {
        var problemDetails = exception switch
        {
            NotFoundException nf => new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                Status = StatusCodes.Status404NotFound,
                Title = "Resource Not Found",
                Detail = nf.Message
            },
            DomainValidationException ve => new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                Status = StatusCodes.Status400BadRequest,
                Title = "Validation Error",
                Detail = ve.Message
            },
            UnauthorizedException => new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7235#section-3.1",
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = "Authentication is required to access this resource"
            },
            ForbiddenException => new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden",
                Detail = "You do not have permission to access this resource"
            },
            ConflictException ce => new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict",
                Detail = ce.Message
            },
            OperationCanceledException => new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.5",
                Status = StatusCodes.Status499ClientClosedRequest,
                Title = "Request Cancelled",
                Detail = "The request was cancelled"
            },
            _ => new ProblemDetails
            {
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                Status = StatusCodes.Status500InternalServerError,
                Title = "Internal Server Error",
                Detail = "An unexpected error occurred. Please try again later."
            }
        };

        problemDetails.Instance = httpContext.Request.Path;
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        return problemDetails;
    }
}
