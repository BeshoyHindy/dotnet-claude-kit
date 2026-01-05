---
name: validation
description: Input validation patterns for commands and requests. Validate before handlers execute. Use when implementing request validation.
allowed-tools: Read, Write, Edit, Glob, Grep
---

# Validation Pattern

**Source**: [Model Validation in ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/validation) | [FluentValidation Documentation](https://docs.fluentvalidation.net/)

Validate input before processing. Fail fast with meaningful errors. Separate validation from business logic.

## Validation Layers

1. **Input validation** (Application layer): Format, required fields, ranges
2. **Business validation** (Domain layer): Invariants, state transitions

Input validation happens before handlers. Business validation is in domain methods.

## Basic Validator Interface

```csharp
public interface IValidator<T>
{
    Task<ValidationResult> ValidateAsync(T instance, CancellationToken ct = default);
}

public sealed record ValidationResult(bool IsValid, IReadOnlyList<ValidationError> Errors)
{
    public static ValidationResult Success() => new(true, []);
    public static ValidationResult Failure(params ValidationError[] errors) => new(false, errors);
}

public sealed record ValidationError(string PropertyName, string ErrorMessage);
```

## Manual Validator

```csharp
public sealed class CreateOrderValidator : IValidator<CreateOrderCommand>
{
    public Task<ValidationResult> ValidateAsync(
        CreateOrderCommand cmd,
        CancellationToken ct)
    {
        var errors = new List<ValidationError>();

        if (cmd.CustomerId == Guid.Empty)
            errors.Add(new ValidationError("CustomerId", "Customer is required"));

        if (string.IsNullOrWhiteSpace(cmd.OrderNumber))
            errors.Add(new ValidationError("OrderNumber", "Order number is required"));

        if (cmd.Items is null || cmd.Items.Count == 0)
            errors.Add(new ValidationError("Items", "At least one item is required"));

        return Task.FromResult(errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors.ToArray()));
    }
}
```

## Validation in Handler Pipeline

```csharp
public sealed class ValidationDecorator<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    IEnumerable<IValidator<TCommand>> validators)
    : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(
        TCommand command,
        CancellationToken ct)
    {
        var allErrors = new List<ValidationError>();

        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(command, ct);
            if (!result.IsValid)
            {
                allErrors.AddRange(result.Errors);
            }
        }

        if (allErrors.Count > 0)
        {
            return Error.ValidationErrors(allErrors);
        }

        return await inner.HandleAsync(command, ct);
    }
}
```

> **Note**: This uses `Error.ValidationErrors()` which accepts structured errors. See the result-pattern skill for the extended `Error` type.

## With FluentValidation

FluentValidation provides a fluent API for defining validation rules.

See [references/with-fluentvalidation.md](references/with-fluentvalidation.md) for FluentValidation implementation.

## Mapping Errors to HTTP

### With Controllers

```csharp
if (result.IsFailure && result.Error.Type == ErrorType.Validation)
{
    foreach (var error in result.Error.ValidationErrors)
    {
        ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
    }
    return ValidationProblem(ModelState);
}
```

### With Minimal APIs

```csharp
if (result.IsFailure && result.Error.Type == ErrorType.Validation)
{
    return Results.ValidationProblem(
        result.Error.ValidationErrors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(e => e.ErrorMessage).ToArray()));
}
```

### Extension Method (Recommended)

Create a reusable extension for clean mapping:

```csharp
public static class ResultExtensions
{
    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess)
    {
        if (result.IsSuccess)
            return onSuccess(result.Value);

        return result.Error.Type switch
        {
            ErrorType.Validation => Results.ValidationProblem(
                result.Error.ValidationErrors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())),
            ErrorType.NotFound => Results.NotFound(result.Error.Message),
            ErrorType.Unauthorized => Results.Unauthorized(),
            ErrorType.Forbidden => Results.Forbid(),
            ErrorType.Conflict => Results.Conflict(result.Error.Message),
            _ => Results.Problem(result.Error.Message)
        };
    }
}

// Usage - clean one-liner
app.MapPost("/orders", async (CreateOrderCommand cmd, ICommandHandler<CreateOrderCommand, Guid> handler, CancellationToken ct) =>
    (await handler.HandleAsync(cmd, ct)).ToHttpResult(id => Results.Created($"/orders/{id}", id)));
```

## Where Validation Lives

| Type | Location | Example |
|------|----------|---------|
| Input format | Application validator | "Email format invalid" |
| Required fields | Application validator | "Customer required" |
| Range checks | Application validator | "Quantity must be 1-100" |
| Uniqueness | Application validator (async) | "Email already exists" |
| Business rules | Domain method | "Cannot modify submitted order" |

## Async Validation

For database checks:

```csharp
public sealed class CreateCustomerValidator(IDbContext db) : IValidator<CreateCustomerCommand>
{
    public async Task<ValidationResult> ValidateAsync(
        CreateCustomerCommand cmd,
        CancellationToken ct)
    {
        var errors = new List<ValidationError>();

        if (await db.Customers.AnyAsync(c => c.Email == cmd.Email, ct))
            errors.Add(new ValidationError("Email", "Email already registered"));

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors.ToArray());
    }
}
```

## Assets

- [assets/Validator.cs](assets/Validator.cs) - Basic validator interfaces and base class

## Related

- `cqrs` - Handler integration
- `result-pattern` - Error return types
