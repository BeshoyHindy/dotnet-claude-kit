---
name: validation
description: Input validation patterns for commands and requests. Validate before handlers execute. Use when implementing request validation.
allowed-tools: Read, Write, Edit, Glob, Grep
---

# Validation Pattern

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
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(command, ct);
            if (!result.IsValid)
            {
                var message = string.Join("; ",
                    result.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
                return Error.Validation(message);
            }
        }

        return await inner.HandleAsync(command, ct);
    }
}
```

## With FluentValidation

FluentValidation provides a fluent API for defining validation rules.

See [references/with-fluentvalidation.md](references/with-fluentvalidation.md) for FluentValidation implementation.

## Mapping Errors to HTTP

### With Controllers

```csharp
if (result.IsFailure && result.Error.Type == ErrorType.Validation)
{
    ModelState.AddModelError("", result.Error.Message);
    return ValidationProblem(ModelState);
}
```

### With Minimal APIs

```csharp
if (result.IsFailure && result.Error.Type == ErrorType.Validation)
{
    return Results.ValidationProblem(
        ParseErrors(result.Error.Message));
}

IDictionary<string, string[]> ParseErrors(string message)
{
    return message
        .Split("; ")
        .Select(e => e.Split(": ", 2))
        .Where(parts => parts.Length == 2)
        .GroupBy(p => p[0])
        .ToDictionary(g => g.Key, g => g.Select(p => p[1]).ToArray());
}
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
