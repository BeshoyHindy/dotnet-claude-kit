---
name: cqrs
description: Command Query Responsibility Segregation pattern. Separates read and write operations into distinct models. Use when implementing commands, queries, or handlers in .NET.
allowed-tools: Read, Write, Edit, Glob, Grep
---

# CQRS Pattern

Command Query Responsibility Segregation separates read operations (queries) from write operations (commands) into distinct models. Each can be optimized independently.

**Source**: [Microsoft Azure Architecture Center](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs)

## Core Concepts

**Commands** represent intent to change state. They are task-based ("BookHotelRoom") not data-centric ("SetReservationStatus").

**Queries** retrieve data without side effects. They return response objects optimized for the caller's needs.

**Handlers** process commands and queries. One handler per command/query.

## When to Use

Use CQRS when:
- Read and write workloads differ significantly
- Complex domain logic for writes, simple reads
- Independent scaling of read/write operations needed
- Collaborative domains with concurrent access

Avoid when:
- Simple CRUD operations suffice
- Domain rules are straightforward

## Core Interfaces

```csharp
// Marker interfaces
public interface ICommand;
public interface ICommand<TResponse>;
public interface IQuery<TResponse>;

// Handler contracts
public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    Task<Result> HandleAsync(TCommand command, CancellationToken ct = default);
}

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TCommand command, CancellationToken ct = default);
}

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> HandleAsync(TQuery query, CancellationToken ct = default);
}
```

## Basic Implementation

```csharp
// Command
public sealed record CreateOrderCommand(
    Guid CustomerId,
    string OrderNumber) : ICommand<Guid>;

// Handler
public sealed class CreateOrderHandler(IDbContext db)
    : ICommandHandler<CreateOrderCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(
        CreateOrderCommand command,
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

// Query
public sealed record GetOrderQuery(Guid OrderId) : IQuery<OrderResponse>;

// Query handler
public sealed class GetOrderHandler(IDbContext db)
    : IQueryHandler<GetOrderQuery, OrderResponse>
{
    public async Task<Result<OrderResponse>> HandleAsync(
        GetOrderQuery query,
        CancellationToken ct)
    {
        var order = await db.Orders
            .Where(o => o.Id == query.OrderId)
            .Select(o => new OrderResponse(o.Id, o.OrderNumber, o.Status))
            .FirstOrDefaultAsync(ct);

        return order is null
            ? Error.NotFound("Order", query.OrderId)
            : order;
    }
}
```

## Registration

```csharp
// Manual registration
services.AddScoped<ICommandHandler<CreateOrderCommand, Guid>, CreateOrderHandler>();
services.AddScoped<IQueryHandler<GetOrderQuery, OrderResponse>, GetOrderHandler>();

// Or scan assembly (using Scrutor)
services.Scan(scan => scan
    .FromAssemblyOf<CreateOrderHandler>()
    .AddClasses(c => c.AssignableTo(typeof(ICommandHandler<,>)))
        .AsImplementedInterfaces()
        .WithScopedLifetime()
    .AddClasses(c => c.AssignableTo(typeof(IQueryHandler<,>)))
        .AsImplementedInterfaces()
        .WithScopedLifetime());
```

## Endpoint Usage

### With Controllers

```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController(ICommandHandler<CreateOrderCommand, Guid> handler) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        CreateOrderCommand command,
        CancellationToken ct)
    {
        var result = await handler.HandleAsync(command, ct);

        if (result.IsFailure)
            return BadRequest(result.Error);

        return CreatedAtAction(nameof(GetById), new { id = result.Value }, result.Value);
    }
}
```

### With Minimal APIs

```csharp
app.MapPost("/orders", async (
    CreateOrderCommand command,
    ICommandHandler<CreateOrderCommand, Guid> handler,
    CancellationToken ct) =>
{
    var result = await handler.HandleAsync(command, ct);
    return result.IsSuccess
        ? Results.Created($"/orders/{result.Value}", result.Value)
        : Results.BadRequest(result.Error);
});
```

## Cross-Cutting Concerns

Use decorators for validation, logging, transactions. See [references/decorators.md](references/decorators.md).

## Framework Implementations

The pattern above works without any framework. For framework-specific implementations:
- [references/with-mediatr.md](references/with-mediatr.md) - MediatR implementation
- [references/with-wolverine.md](references/with-wolverine.md) - Wolverine implementation

## Related

- `result-pattern` - Return types for handlers
- `validation` - Input validation
- `clean-architecture` - Where handlers live
