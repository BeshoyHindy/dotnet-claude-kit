---
name: domain-events
description: Domain events for decoupled communication. Event raising, handling, dispatching patterns. Use when implementing event-driven domain logic.
allowed-tools: Read, Write, Edit, Glob, Grep
---

# Domain Events

Decoupled communication within bounded contexts. Events represent something that happened in the domain.

**Source**: [Domain-Driven Design](https://martinfowler.com/eaaDev/DomainEvent.html)

## Event Definition

```csharp
// Domain/Common/IDomainEvent.cs
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredOn { get; }
}

// Domain/Common/DomainEvent.cs
// OccurredOn passed via constructor for TimeProvider testability
public abstract record DomainEvent(DateTimeOffset OccurredOn) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

// Domain/Orders/Events/OrderCreatedEvent.cs
public sealed record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    string OrderNumber,
    DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

public sealed record OrderSubmittedEvent(
    Guid OrderId,
    decimal TotalAmount,
    DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

public sealed record OrderCancelledEvent(
    Guid OrderId,
    string Reason,
    DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);
```

## Entity with Events

```csharp
// Domain/Common/Entity.cs
public abstract class Entity
{
    public Guid Id { get; protected set; }

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public IReadOnlyList<IDomainEvent> PopDomainEvents()
    {
        var events = _domainEvents.ToList();
        _domainEvents.Clear();
        return events;
    }
}

// Domain/Orders/Order.cs
public sealed class Order : Entity
{
    public string OrderNumber { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }
    public OrderStatus Status { get; private set; }

    private Order() { }

    public static Result<Order> Create(
        Guid customerId,
        string orderNumber,
        TimeProvider timeProvider)
    {
        if (customerId == Guid.Empty)
            return Error.Validation("Customer ID is required");

        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            OrderNumber = orderNumber,
            Status = OrderStatus.Draft
        };

        order.RaiseDomainEvent(new OrderCreatedEvent(
            order.Id,
            order.CustomerId,
            order.OrderNumber,
            timeProvider.GetUtcNow()));

        return order;
    }

    public Result Submit(TimeProvider timeProvider)
    {
        if (Status != OrderStatus.Draft)
            return Error.Validation("Only draft orders can be submitted");

        Status = OrderStatus.Submitted;

        RaiseDomainEvent(new OrderSubmittedEvent(
            Id,
            CalculateTotal(),
            timeProvider.GetUtcNow()));

        return Result.Success();
    }

    public Result Cancel(string reason, TimeProvider timeProvider)
    {
        if (Status == OrderStatus.Shipped)
            return Error.Validation("Shipped orders cannot be cancelled");

        Status = OrderStatus.Cancelled;

        RaiseDomainEvent(new OrderCancelledEvent(
            Id,
            reason,
            timeProvider.GetUtcNow()));

        return Result.Success();
    }
}
```

## Event Handler Interface

```csharp
// Application/Common/Interfaces/IDomainEventHandler.cs
public interface IDomainEventHandler<in TEvent> where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct = default);
}
```

## Event Handlers

```csharp
// Application/Orders/EventHandlers/OrderCreatedEventHandler.cs
public sealed class OrderCreatedEventHandler(
    ILogger<OrderCreatedEventHandler> logger,
    IEmailService emailService)
    : IDomainEventHandler<OrderCreatedEvent>
{
    public async Task HandleAsync(OrderCreatedEvent domainEvent, CancellationToken ct)
    {
        logger.LogInformation(
            "Order {OrderId} created for customer {CustomerId}",
            domainEvent.OrderId,
            domainEvent.CustomerId);

        // Send confirmation email, update analytics, etc.
        await emailService.SendOrderConfirmationAsync(
            domainEvent.OrderId,
            ct);
    }
}

// Multiple handlers for same event
public sealed class UpdateInventoryOnOrderSubmitted(
    IInventoryService inventoryService)
    : IDomainEventHandler<OrderSubmittedEvent>
{
    public async Task HandleAsync(OrderSubmittedEvent domainEvent, CancellationToken ct)
    {
        await inventoryService.ReserveInventoryAsync(domainEvent.OrderId, ct);
    }
}

public sealed class NotifyWarehouseOnOrderSubmitted(
    IWarehouseService warehouseService)
    : IDomainEventHandler<OrderSubmittedEvent>
{
    public async Task HandleAsync(OrderSubmittedEvent domainEvent, CancellationToken ct)
    {
        await warehouseService.QueueForPickingAsync(domainEvent.OrderId, ct);
    }
}
```

## Event Dispatcher

### In-Process Dispatcher

```csharp
// Infrastructure/Events/DomainEventDispatcher.cs
public sealed class DomainEventDispatcher(IServiceProvider serviceProvider)
    : IDomainEventDispatcher
{
    public async Task DispatchAsync(
        IEnumerable<IDomainEvent> domainEvents,
        CancellationToken ct = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            await DispatchEventAsync(domainEvent, ct);
        }
    }

    private async Task DispatchEventAsync(IDomainEvent domainEvent, CancellationToken ct)
    {
        var eventType = domainEvent.GetType();
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);

        var handlers = serviceProvider.GetServices(handlerType);

        foreach (var handler in handlers)
        {
            var method = handlerType.GetMethod("HandleAsync");
            await (Task)method!.Invoke(handler, [domainEvent, ct])!;
        }
    }
}
```

### Dispatch via Interceptor

```csharp
// Infrastructure/Data/Interceptors/DomainEventInterceptor.cs
public sealed class DomainEventInterceptor(
    IDomainEventDispatcher dispatcher) : SaveChangesInterceptor
{
    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return result;

        // Collect events from all entities
        var domainEvents = eventData.Context.ChangeTracker
            .Entries<Entity>()
            .SelectMany(e => e.Entity.PopDomainEvents())
            .ToList();

        // Dispatch after successful save
        await dispatcher.DispatchAsync(domainEvents, cancellationToken);

        return result;
    }
}
```

## Registration

```csharp
// Program.cs
services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();
services.AddScoped<DomainEventInterceptor>();

// Register all handlers from assembly
services.Scan(scan => scan
    .FromAssemblyOf<OrderCreatedEventHandler>()
    .AddClasses(c => c.AssignableTo(typeof(IDomainEventHandler<>)))
    .AsImplementedInterfaces()
    .WithScopedLifetime());

// Add interceptor to DbContext
services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseSqlServer(connectionString)
           .AddInterceptors(sp.GetRequiredService<DomainEventInterceptor>());
});
```

## When to Dispatch

| Timing | Use Case | Trade-off |
|--------|----------|-----------|
| Before SaveChanges | Validation, enrichment | Events fire even if save fails |
| After SaveChanges | Notifications, side effects | Consistent with database state |
| Outbox pattern | Reliable external systems | Added complexity |

## Integration Events

For cross-boundary communication:

```csharp
// Application/Common/Interfaces/IIntegrationEvent.cs
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredOn { get; }
}

// Domain events stay internal, integration events go external
public sealed class OrderSubmittedIntegrationEvent : IIntegrationEvent
{
    public Guid EventId { get; init; }
    public DateTimeOffset OccurredOn { get; init; }
    public Guid OrderId { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
}

// Handler converts domain event to integration event
public sealed class PublishOrderSubmittedIntegration(
    IEventPublisher publisher,
    TimeProvider timeProvider)
    : IDomainEventHandler<OrderSubmittedEvent>
{
    public async Task HandleAsync(OrderSubmittedEvent domainEvent, CancellationToken ct)
    {
        var integrationEvent = new OrderSubmittedIntegrationEvent
        {
            EventId = Guid.NewGuid(),
            OccurredOn = timeProvider.GetUtcNow(),
            OrderId = domainEvent.OrderId
        };

        await publisher.PublishAsync(integrationEvent, ct);
    }
}
```

## Best Practices

| Practice | Recommendation |
|----------|----------------|
| Naming | Past tense: OrderCreated, not CreateOrder |
| Immutability | Events are records, never modify |
| Handlers | Keep handlers focused, one responsibility |
| Idempotency | Handlers should be idempotent |
| Ordering | Don't rely on handler execution order |
| Failures | Consider retry/dead letter for failed handlers |

## Event Versioning

Events evolve over time. Handle schema changes gracefully:

```csharp
// Version in event name (preferred for breaking changes)
public sealed record OrderCreatedEventV2(
    Guid OrderId,
    Guid CustomerId,
    string OrderNumber,
    decimal TotalAmount,  // New field in V2
    DateTimeOffset OccurredOn) : DomainEvent(OccurredOn);

// Or version property for minor additions
public sealed record OrderCreatedEvent(
    Guid OrderId,
    Guid CustomerId,
    string OrderNumber,
    DateTimeOffset OccurredOn,
    int Version = 1) : DomainEvent(OccurredOn)
{
    // New optional fields with defaults for backward compatibility
    public decimal? TotalAmount { get; init; }
}

// Handler supports multiple versions
public sealed class OrderCreatedEventHandler :
    IDomainEventHandler<OrderCreatedEvent>,
    IDomainEventHandler<OrderCreatedEventV2>
{
    public Task HandleAsync(OrderCreatedEvent e, CancellationToken ct) =>
        ProcessOrderCreated(e.OrderId, e.CustomerId, null, ct);

    public Task HandleAsync(OrderCreatedEventV2 e, CancellationToken ct) =>
        ProcessOrderCreated(e.OrderId, e.CustomerId, e.TotalAmount, ct);

    private Task ProcessOrderCreated(Guid orderId, Guid customerId, decimal? total, CancellationToken ct)
    {
        // Common processing logic
    }
}
```

## Performance Considerations

### High-Throughput Scenarios

For high-volume event processing, optimize the dispatcher:

```csharp
// Cached dispatcher - avoids reflection on every call
public sealed class CachedDomainEventDispatcher(IServiceProvider serviceProvider)
    : IDomainEventDispatcher
{
    // Cache handler types and methods
    private static readonly ConcurrentDictionary<Type, (Type HandlerType, MethodInfo Method)> _cache = new();

    public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default)
    {
        foreach (var domainEvent in domainEvents)
        {
            var eventType = domainEvent.GetType();
            var (handlerType, method) = _cache.GetOrAdd(eventType, GetHandlerInfo);

            var handlers = serviceProvider.GetServices(handlerType);
            foreach (var handler in handlers)
            {
                await ((Task)method.Invoke(handler, [domainEvent, ct])!).ConfigureAwait(false);
            }
        }
    }

    private static (Type HandlerType, MethodInfo Method) GetHandlerInfo(Type eventType)
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(eventType);
        var method = handlerType.GetMethod("HandleAsync")!;
        return (handlerType, method);
    }
}
```

### Parallel Handler Execution

When handlers are independent, run them in parallel:

```csharp
public async Task DispatchAsync(IEnumerable<IDomainEvent> domainEvents, CancellationToken ct = default)
{
    foreach (var domainEvent in domainEvents)
    {
        var handlers = GetHandlers(domainEvent.GetType());

        // Run independent handlers in parallel
        await Task.WhenAll(handlers.Select(h => InvokeHandler(h, domainEvent, ct)));
    }
}
```

### Source Generators (Advanced)

For zero-reflection dispatching, consider source generators:

```csharp
// With a source generator, you can generate typed dispatch code at compile time
// This eliminates all runtime reflection overhead
// Libraries like MediatR and Wolverine use this approach
```

## Assets

- [assets/DomainEvent.cs](assets/DomainEvent.cs) - Base event types
- [assets/DomainEventDispatcher.cs](assets/DomainEventDispatcher.cs) - Dispatcher

## Related

- `outbox-pattern` - Reliable event publishing
- `cqrs` - Command/query separation
- `clean-architecture` - Event placement
