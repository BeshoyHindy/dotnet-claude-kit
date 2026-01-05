# CQRS with Wolverine

Wolverine is a messaging and command execution framework with built-in support for CQRS patterns, transactional outbox, and durable messaging.

**Source**: [Wolverine Documentation](https://wolverine.netlify.app/)

## Installation

```bash
dotnet add package Wolverine
dotnet add package Wolverine.EntityFrameworkCore  # For EF Core integration
```

## Handler Conventions

Wolverine uses conventions - no interfaces required:

```csharp
// Command (just a record)
public sealed record CreateOrderCommand(
    Guid CustomerId,
    string OrderNumber);

// Handler (static or instance method named Handle/HandleAsync)
public static class CreateOrderHandler
{
    public static async Task<Result<Guid>> HandleAsync(
        CreateOrderCommand command,
        IDbContext db,
        CancellationToken ct)
    {
        var order = Order.Create(command.CustomerId, command.OrderNumber);
        if (order.IsFailure)
            return order.Error;

        db.Orders.Add(order.Value);
        await db.SaveChangesAsync(ct);

        return order.Value.Id;
    }
}
```

## Configuration

```csharp
builder.Host.UseWolverine(opts =>
{
    // Discover handlers in assembly
    opts.Discovery.IncludeAssembly(typeof(CreateOrderHandler).Assembly);

    // Auto-apply transactions
    opts.Policies.AutoApplyTransactions();
});
```

## Middleware

Wolverine uses middleware for cross-cutting concerns:

```csharp
public sealed class ValidationMiddleware<TCommand, TResponse>(
    IEnumerable<IValidator<TCommand>> validators)
{
    public async Task<TResponse> BeforeAsync(TCommand command, CancellationToken ct)
    {
        if (!validators.Any())
            return default!;

        var context = new ValidationContext<TCommand>(command);
        var results = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, ct)));

        var failures = results.SelectMany(r => r.Errors).ToList();

        if (failures.Count > 0)
        {
            // Return failure for Result<T> types
            // Return default to continue to handler
        }

        return default!;
    }
}

// Apply to commands
opts.Policies.ForMessagesOfType<object>()
    .WhenMessageTypeNameEndsWith("Command")
    .AddMiddleware(typeof(ValidationMiddleware<,>));
```

## Endpoint Usage

```csharp
app.MapPost("/orders", async (
    CreateOrderCommand command,
    IMessageBus bus,
    CancellationToken ct) =>
{
    var result = await bus.InvokeAsync<Result<Guid>>(command, ct);
    return result.IsSuccess
        ? Results.Created($"/orders/{result.Value}", result.Value)
        : Results.BadRequest(result.Error);
});
```

## Transactional Outbox

Wolverine provides durable messaging with outbox:

```csharp
opts.PersistMessagesWithPostgresql(connectionString, "wolverine");
opts.UseEntityFrameworkCoreTransactions();
```

## Comparison with Raw CQRS

| Aspect | Raw CQRS | Wolverine |
|--------|----------|-----------|
| Dependencies | None | Wolverine packages |
| Handler signature | Interface-based | Convention-based |
| Cross-cutting | Decorators | Middleware |
| Messaging | N/A | Built-in durable messaging |
| Outbox | Manual | Built-in |
| Learning curve | Lower | Medium |
