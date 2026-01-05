---
name: result-pattern
description: Result pattern for explicit error handling. Returns success or failure without exceptions. Use when implementing domain operations, handlers, or APIs.
allowed-tools: Read, Write, Edit, Glob, Grep
---

# Result Pattern

Explicit error handling using discriminated return types. Operations return `Result<T>` indicating success with value or failure with error. Exceptions reserved for truly exceptional cases.

## Why Use Result

- **Explicit contracts**: Callers must handle both success and failure
- **No exception overhead**: Errors are expected, not exceptional
- **Composable**: Chain operations with Map/Bind
- **Type-safe**: Compiler enforces error handling

## Core Types

```csharp
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly Error? _error;

    public bool IsSuccess => _error is null;
    public bool IsFailure => !IsSuccess;

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Cannot access Value: {_error}");

    public Error Error => IsFailure
        ? _error!
        : throw new InvalidOperationException("No error on success");

    private Result(T value) => _value = value;
    private Result(Error error) => _error = error;

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);

    public static implicit operator Result<T>(T value) => Success(value);
    public static implicit operator Result<T>(Error error) => Failure(error);
}

public readonly struct Result
{
    private readonly Error? _error;

    public bool IsSuccess => _error is null;
    public bool IsFailure => !IsSuccess;
    public Error Error => _error ?? throw new InvalidOperationException("No error");

    private Result(Error error) => _error = error;

    public static Result Success() => new();
    public static Result Failure(Error error) => new(error);

    public static implicit operator Result(Error error) => Failure(error);
}
```

## Error Type

```csharp
public sealed record Error(string Code, string Message, ErrorType Type)
{
    public static Error Validation(string message) =>
        new("VALIDATION_ERROR", message, ErrorType.Validation);

    public static Error NotFound(string resource, object id) =>
        new("NOT_FOUND", $"{resource} '{id}' not found", ErrorType.NotFound);

    public static Error Unauthorized(string? message = null) =>
        new("UNAUTHORIZED", message ?? "Unauthorized", ErrorType.Unauthorized);

    public static Error Forbidden(string? message = null) =>
        new("FORBIDDEN", message ?? "Forbidden", ErrorType.Forbidden);

    public static Error Conflict(string message) =>
        new("CONFLICT", message, ErrorType.Conflict);

    public static Error Internal(string message) =>
        new("INTERNAL_ERROR", message, ErrorType.Internal);
}

public enum ErrorType
{
    Validation,
    NotFound,
    Unauthorized,
    Forbidden,
    Conflict,
    Internal
}
```

## Usage in Domain

```csharp
public sealed class Order
{
    public static Result<Order> Create(Guid customerId, string orderNumber)
    {
        if (customerId == Guid.Empty)
            return Error.Validation("Customer ID required");

        if (string.IsNullOrWhiteSpace(orderNumber))
            return Error.Validation("Order number required");

        return new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            OrderNumber = orderNumber.Trim()
        };
    }

    public Result AddItem(Guid productId, int quantity)
    {
        if (Status != OrderStatus.Draft)
            return Error.Validation("Cannot modify non-draft order");

        if (quantity <= 0)
            return Error.Validation("Quantity must be positive");

        _items.Add(new OrderItem(productId, quantity));
        return Result.Success();
    }
}
```

## Usage in Handlers

```csharp
public async Task<Result<Guid>> HandleAsync(
    CreateOrderCommand cmd,
    CancellationToken ct)
{
    var orderResult = Order.Create(cmd.CustomerId, cmd.OrderNumber);

    if (orderResult.IsFailure)
        return orderResult.Error;

    var order = orderResult.Value;

    foreach (var item in cmd.Items)
    {
        var addResult = order.AddItem(item.ProductId, item.Quantity);
        if (addResult.IsFailure)
            return addResult.Error;
    }

    db.Orders.Add(order);
    await db.SaveChangesAsync(ct);

    return order.Id;
}
```

## Mapping to HTTP

### With Controllers

```csharp
[HttpPost]
public async Task<IActionResult> Create(CreateOrderCommand command, CancellationToken ct)
{
    var result = await handler.HandleAsync(command, ct);

    if (result.IsFailure)
    {
        return result.Error.Type switch
        {
            ErrorType.Validation => BadRequest(result.Error.Message),
            ErrorType.NotFound => NotFound(result.Error.Message),
            ErrorType.Unauthorized => Unauthorized(),
            ErrorType.Forbidden => Forbid(),
            ErrorType.Conflict => Conflict(result.Error.Message),
            _ => Problem(result.Error.Message)
        };
    }

    return CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value);
}
```

### With Minimal APIs

```csharp
app.MapPost("/orders", async (...) =>
{
    var result = await handler.HandleAsync(command, ct);

    return result.IsSuccess
        ? Results.Created($"/orders/{result.Value}", result.Value)
        : result.Error.Type switch
        {
            ErrorType.Validation => Results.BadRequest(result.Error.Message),
            ErrorType.NotFound => Results.NotFound(result.Error.Message),
            ErrorType.Unauthorized => Results.Unauthorized(),
            ErrorType.Forbidden => Results.Forbid(),
            ErrorType.Conflict => Results.Conflict(result.Error.Message),
            _ => Results.Problem(result.Error.Message)
        };
});
```

## Extensions

Optional functional extensions for composition:

```csharp
public static class ResultExtensions
{
    public static Result<TOut> Map<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, TOut> mapper) =>
        result.IsSuccess
            ? mapper(result.Value)
            : result.Error;

    public static Result<TOut> Bind<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, Result<TOut>> binder) =>
        result.IsSuccess
            ? binder(result.Value)
            : result.Error;

    public static TResult Match<T, TResult>(
        this Result<T> result,
        Func<T, TResult> onSuccess,
        Func<Error, TResult> onFailure) =>
        result.IsSuccess ? onSuccess(result.Value) : onFailure(result.Error);

    public static TResult Match<TResult>(
        this Result result,
        Func<TResult> onSuccess,
        Func<Error, TResult> onFailure) =>
        result.IsSuccess ? onSuccess() : onFailure(result.Error);
}
```

## Assets

See [assets/](assets/) for complete implementation files.

## Related

- `cqrs` - Handler return types
- `validation` - Validation error mapping
