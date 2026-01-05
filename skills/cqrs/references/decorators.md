# CQRS Decorators

Cross-cutting concerns (logging, validation, transactions) are added via decorators wrapping handlers.

**Source**: [Milan Jovanović - CQRS Pattern](https://www.milanjovanovic.tech/blog/cqrs-pattern-the-way-it-should-have-been-from-the-start)

## Logging Decorator

```csharp
public sealed class LoggingCommandHandler<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    ILogger<LoggingCommandHandler<TCommand, TResponse>> logger)
    : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(
        TCommand command,
        CancellationToken ct)
    {
        var commandName = typeof(TCommand).Name;

        logger.LogInformation("Handling {Command}", commandName);

        var result = await inner.HandleAsync(command, ct);

        if (result.IsSuccess)
            logger.LogInformation("Handled {Command}", commandName);
        else
            logger.LogWarning("Failed {Command}: {Error}", commandName, result.Error);

        return result;
    }
}
```

## Validation Decorator

```csharp
public sealed class ValidationCommandHandler<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    IEnumerable<IValidator<TCommand>> validators)
    : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(
        TCommand command,
        CancellationToken ct)
    {
        if (!validators.Any())
            return await inner.HandleAsync(command, ct);

        var context = new ValidationContext<TCommand>(command);

        var results = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, ct)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
        {
            var errors = string.Join("; ", failures.Select(f => f.ErrorMessage));
            return Result.Failure<TResponse>(Error.Validation(errors));
        }

        return await inner.HandleAsync(command, ct);
    }
}
```

## Registration with Scrutor

Order matters - last registered is outermost (executes first).

```csharp
// Register handlers
services.Scan(scan => scan
    .FromAssemblyOf<CreateOrderHandler>()
    .AddClasses(c => c.AssignableTo(typeof(ICommandHandler<,>)))
        .AsImplementedInterfaces()
        .WithScopedLifetime());

// Apply decorators (validation runs before logging in this order)
services.Decorate(typeof(ICommandHandler<,>), typeof(LoggingCommandHandler<,>));
services.Decorate(typeof(ICommandHandler<,>), typeof(ValidationCommandHandler<,>));
```

Execution order: Validation → Logging → Handler

## Transaction Decorator

```csharp
public sealed class TransactionCommandHandler<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    IDbContext db)
    : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(
        TCommand command,
        CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        try
        {
            var result = await inner.HandleAsync(command, ct);

            if (result.IsSuccess)
                await transaction.CommitAsync(ct);
            else
                await transaction.RollbackAsync(ct);

            return result;
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }
}
```

## Performance Timing Decorator

```csharp
public sealed class PerformanceCommandHandler<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    ILogger<PerformanceCommandHandler<TCommand, TResponse>> logger,
    TimeProvider timeProvider)
    : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    private const long WarningThresholdMs = 500;

    public async Task<Result<TResponse>> HandleAsync(
        TCommand command,
        CancellationToken ct)
    {
        var commandName = typeof(TCommand).Name;
        var startTime = timeProvider.GetTimestamp();

        var result = await inner.HandleAsync(command, ct);

        var elapsedMs = timeProvider.GetElapsedTime(startTime).TotalMilliseconds;

        if (elapsedMs > WarningThresholdMs)
        {
            logger.LogWarning(
                "Slow command {Command} took {ElapsedMs}ms",
                commandName,
                elapsedMs);
        }

        return result;
    }
}
```

## Exception Handling Decorator

```csharp
public sealed class ExceptionHandlingCommandHandler<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    ILogger<ExceptionHandlingCommandHandler<TCommand, TResponse>> logger)
    : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> HandleAsync(
        TCommand command,
        CancellationToken ct)
    {
        try
        {
            return await inner.HandleAsync(command, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var commandName = typeof(TCommand).Name;
            logger.LogError(ex, "Unhandled exception in {Command}", commandName);

            return Error.Unexpected($"An error occurred processing {commandName}");
        }
    }
}
```

## Full Registration Example

```csharp
// Order: innermost to outermost
// Execution: Exception → Validation → Logging → Performance → Handler
services.Scan(scan => scan
    .FromAssemblyOf<CreateOrderHandler>()
    .AddClasses(c => c.AssignableTo(typeof(ICommandHandler<,>)))
        .AsImplementedInterfaces()
        .WithScopedLifetime());

services.Decorate(typeof(ICommandHandler<,>), typeof(PerformanceCommandHandler<,>));
services.Decorate(typeof(ICommandHandler<,>), typeof(LoggingCommandHandler<,>));
services.Decorate(typeof(ICommandHandler<,>), typeof(ValidationCommandHandler<,>));
services.Decorate(typeof(ICommandHandler<,>), typeof(ExceptionHandlingCommandHandler<,>));
```

## Comparison: Decorators vs MediatR Behaviors

| Aspect | Decorators | MediatR Behaviors |
|--------|------------|-------------------|
| Dependency | Scrutor (optional) | MediatR package |
| Type safety | Full compile-time checking | Generic pipeline |
| Debugging | Clear call stack | Through pipeline |
| Registration | Per handler type | Global or per request |
| Flexibility | More control | More convention |

## Related

- `logging` - Logging best practices
- `exception-handling` - Global exception handling
- [with-mediatr.md](with-mediatr.md) - MediatR pipeline behaviors
