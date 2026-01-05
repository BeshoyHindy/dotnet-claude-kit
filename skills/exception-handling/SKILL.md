---
name: exception-handling
description: Global exception handling with Problem Details (RFC 7807). Map errors to consistent HTTP responses. Use when setting up API error handling.
allowed-tools: Read, Write, Edit, Glob, Grep
---

# Exception Handling

Handle exceptions globally and return consistent error responses using Problem Details (RFC 7807).

**Source**: [RFC 7807](https://datatracker.ietf.org/doc/html/rfc7807), [Microsoft Problem Details](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling)

## Problem Details Format

Standard error response format:

```json
{
  "type": "https://tools.ietf.org/html/rfc7807#section-3.1",
  "title": "Validation Error",
  "status": 400,
  "detail": "One or more validation errors occurred.",
  "instance": "/orders",
  "errors": {
    "OrderNumber": ["Order number is required"]
  }
}
```

## .NET 8+ Built-in Support

### Configure Problem Details Service

```csharp
// Program.cs
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Instance = context.HttpContext.Request.Path;
        context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
    };
});
```

### Global Exception Handler (.NET 8+)

```csharp
// Infrastructure/ExceptionHandling/GlobalExceptionHandler.cs
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken ct)
    {
        logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        var problemDetails = exception switch
        {
            ValidationException ve => CreateValidationProblem(ve),
            NotFoundException nf => CreateNotFoundProblem(nf),
            UnauthorizedException => CreateUnauthorizedProblem(),
            ForbiddenException => CreateForbiddenProblem(),
            ConflictException ce => CreateConflictProblem(ce),
            _ => CreateInternalErrorProblem()
        };

        httpContext.Response.StatusCode = problemDetails.Status ?? 500;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, ct);

        return true;
    }

    private static ProblemDetails CreateValidationProblem(ValidationException ex) =>
        new()
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Validation Error",
            Detail = ex.Message,
            Type = "https://datatracker.ietf.org/doc/html/rfc7807"
        };

    private static ProblemDetails CreateNotFoundProblem(NotFoundException ex) =>
        new()
        {
            Status = StatusCodes.Status404NotFound,
            Title = "Not Found",
            Detail = ex.Message
        };

    private static ProblemDetails CreateUnauthorizedProblem() =>
        new()
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = "Authentication required"
        };

    private static ProblemDetails CreateForbiddenProblem() =>
        new()
        {
            Status = StatusCodes.Status403Forbidden,
            Title = "Forbidden",
            Detail = "Access denied"
        };

    private static ProblemDetails CreateConflictProblem(ConflictException ex) =>
        new()
        {
            Status = StatusCodes.Status409Conflict,
            Title = "Conflict",
            Detail = ex.Message
        };

    private static ProblemDetails CreateInternalErrorProblem() =>
        new()
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "Internal Server Error",
            Detail = "An unexpected error occurred"
        };
}
```

### Registration

```csharp
// Program.cs
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

var app = builder.Build();

app.UseExceptionHandler();
```

## Custom Exception Types

```csharp
// Domain/Common/Exceptions/DomainException.cs
public abstract class DomainException(string message) : Exception(message);

public sealed class NotFoundException(string resource, object id)
    : DomainException($"{resource} with ID '{id}' was not found");

public sealed class ValidationException(string message)
    : DomainException(message);

public sealed class UnauthorizedException()
    : DomainException("Authentication required");

public sealed class ForbiddenException()
    : DomainException("Access denied");

public sealed class ConflictException(string message)
    : DomainException(message);
```

## Mapping Result to HTTP Response

When using Result pattern, map errors to Problem Details:

### With Controllers

```csharp
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult HandleResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
            return Ok(result.Value);

        return result.Error.Type switch
        {
            ErrorType.Validation => BadRequest(CreateProblemDetails(result.Error)),
            ErrorType.NotFound => NotFound(CreateProblemDetails(result.Error)),
            ErrorType.Unauthorized => Unauthorized(CreateProblemDetails(result.Error)),
            ErrorType.Forbidden => Forbid(),
            ErrorType.Conflict => Conflict(CreateProblemDetails(result.Error)),
            _ => Problem(result.Error.Message)
        };
    }

    private ProblemDetails CreateProblemDetails(Error error) =>
        new()
        {
            Title = error.Code,
            Detail = error.Message,
            Status = error.Type switch
            {
                ErrorType.Validation => 400,
                ErrorType.NotFound => 404,
                ErrorType.Unauthorized => 401,
                ErrorType.Forbidden => 403,
                ErrorType.Conflict => 409,
                _ => 500
            }
        };
}
```

### With Minimal APIs

```csharp
public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result)
    {
        if (result.IsSuccess)
            return Results.Ok(result.Value);

        return result.Error.Type switch
        {
            ErrorType.Validation => Results.Problem(
                detail: result.Error.Message,
                statusCode: 400,
                title: "Validation Error"),
            ErrorType.NotFound => Results.Problem(
                detail: result.Error.Message,
                statusCode: 404,
                title: "Not Found"),
            ErrorType.Unauthorized => Results.Problem(
                statusCode: 401,
                title: "Unauthorized"),
            ErrorType.Forbidden => Results.Problem(
                statusCode: 403,
                title: "Forbidden"),
            ErrorType.Conflict => Results.Problem(
                detail: result.Error.Message,
                statusCode: 409,
                title: "Conflict"),
            _ => Results.Problem(
                detail: result.Error.Message,
                statusCode: 500)
        };
    }
}

// Usage
app.MapGet("/orders/{id}", async (Guid id, IQueryHandler<GetOrderQuery, OrderResponse> handler) =>
{
    var result = await handler.HandleAsync(new GetOrderQuery(id));
    return result.ToHttpResult();
});
```

## Validation Errors with Details

For detailed validation errors:

```csharp
public static IResult ToValidationProblem(this ValidationResult result)
{
    var errors = result.Errors
        .GroupBy(e => e.PropertyName)
        .ToDictionary(
            g => g.Key,
            g => g.Select(e => e.ErrorMessage).ToArray());

    return Results.ValidationProblem(errors);
}
```

## When to Use Exceptions vs Result

| Scenario | Approach |
|----------|----------|
| Expected failures (validation, not found) | Result pattern |
| Unexpected failures (network, database) | Exceptions |
| Cross-cutting concerns | Exception middleware |
| Handler/domain logic | Result pattern |

## Assets

- [assets/GlobalExceptionHandler.cs](assets/GlobalExceptionHandler.cs) - Complete exception handler
- [assets/DomainExceptions.cs](assets/DomainExceptions.cs) - Custom exception types

## Related

- `result-pattern` - Error handling without exceptions
- `validation` - Validation error mapping
