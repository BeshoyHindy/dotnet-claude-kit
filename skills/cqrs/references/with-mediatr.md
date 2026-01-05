# CQRS with MediatR

MediatR is a popular in-process messaging library. As of April 2025, it uses a dual-license model (free for OSS/educational, commercial license required for production).

**Note**: Consider if you need MediatR or if raw CQRS interfaces suffice for your use case.

## Installation

```bash
dotnet add package MediatR
```

## Interfaces

MediatR provides its own interfaces:

```csharp
// Commands
public sealed record CreateOrderCommand(
    Guid CustomerId,
    string OrderNumber) : IRequest<Result<Guid>>;

// Queries
public sealed record GetOrderQuery(Guid OrderId) : IRequest<Result<OrderResponse>>;
```

## Handlers

```csharp
public sealed class CreateOrderHandler(IDbContext db)
    : IRequestHandler<CreateOrderCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(
        CreateOrderCommand request,
        CancellationToken ct)
    {
        var order = Order.Create(request.CustomerId, request.OrderNumber);
        if (order.IsFailure)
            return order.Error;

        db.Orders.Add(order.Value);
        await db.SaveChangesAsync(ct);

        return order.Value.Id;
    }
}
```

## Registration

```csharp
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CreateOrderHandler).Assembly);
});
```

## Pipeline Behaviors

MediatR uses pipeline behaviors for cross-cutting concerns:

```csharp
public sealed class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (!validators.Any())
            return await next();

        var context = new ValidationContext<TRequest>(request);
        var results = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, ct)));

        var failures = results
            .SelectMany(r => r.Errors)
            .Where(f => f is not null)
            .ToList();

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return await next();
    }
}

// Registration
services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(CreateOrderHandler).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});
```

## Endpoint Usage

```csharp
app.MapPost("/orders", async (
    CreateOrderCommand command,
    ISender sender,
    CancellationToken ct) =>
{
    var result = await sender.Send(command, ct);
    return result.IsSuccess
        ? Results.Created($"/orders/{result.Value}", result.Value)
        : Results.BadRequest(result.Error);
});
```

## Comparison with Raw CQRS

| Aspect | Raw CQRS | MediatR |
|--------|----------|---------|
| Dependencies | None | MediatR package |
| Handler injection | Explicit (type-safe) | Via ISender (service locator) |
| Cross-cutting | Decorators | Pipeline behaviors |
| Licensing | N/A | Commercial for production |
| Learning curve | Lower | Higher |
