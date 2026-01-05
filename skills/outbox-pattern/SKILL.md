---
name: outbox-pattern
description: Transactional outbox pattern for reliable messaging. Ensures events are published after database commits. Use when implementing reliable event publishing.
allowed-tools: Read, Write, Edit, Glob, Grep
---

# Outbox Pattern

Guarantees reliable message publishing by storing events in the same transaction as business data.

**Source**: [Microservices.io - Transactional Outbox](https://microservices.io/patterns/data/transactional-outbox.html)

## The Problem

Without the outbox pattern:

```csharp
// WRONG: Not transactionally safe
public async Task HandleAsync(CreateOrderCommand command, CancellationToken ct)
{
    var order = Order.Create(command.CustomerId);
    db.Orders.Add(order);
    await db.SaveChangesAsync(ct); // Might succeed...

    await messageBus.PublishAsync(new OrderCreatedEvent(order.Id)); // ...but this might fail
}
```

If the message publish fails after the database commit, the event is lost.

## Solution: Outbox Table

Store events in the same transaction, publish later:

```csharp
// CORRECT: Transactionally safe
public async Task HandleAsync(CreateOrderCommand command, CancellationToken ct)
{
    var order = Order.Create(command.CustomerId);
    db.Orders.Add(order);

    // Store event in outbox (same transaction)
    var outboxMessage = OutboxMessage.Create(new OrderCreatedEvent(order.Id));
    db.OutboxMessages.Add(outboxMessage);

    await db.SaveChangesAsync(ct); // Both saved atomically
}

// Background job publishes from outbox
```

## Outbox Message Entity

```csharp
// Infrastructure/Data/Outbox/OutboxMessage.cs
public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public string? Error { get; private set; }
    public int RetryCount { get; private set; }

    private OutboxMessage() { }

    public static OutboxMessage Create<T>(T message, DateTimeOffset createdAt) where T : class
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            Type = typeof(T).AssemblyQualifiedName!,
            Content = JsonSerializer.Serialize(message),
            CreatedAt = createdAt
        };
    }

    public void MarkAsProcessed(DateTimeOffset processedAt)
    {
        ProcessedAt = processedAt;
    }

    public void MarkAsFailed(string error)
    {
        Error = error;
        RetryCount++;
    }

    public object? Deserialize()
    {
        var type = System.Type.GetType(Type);
        return type is null ? null : JsonSerializer.Deserialize(Content, type);
    }
}
```

## DbContext Configuration

```csharp
// Infrastructure/Data/Configurations/OutboxMessageConfiguration.cs
public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(x => x.Content)
            .IsRequired();

        // Index for efficient polling
        builder.HasIndex(x => x.ProcessedAt)
            .HasFilter("[ProcessedAt] IS NULL");
    }
}
```

## Interceptor Approach

Automatically collect domain events and store in outbox:

```csharp
// Infrastructure/Data/Interceptors/OutboxInterceptor.cs
public sealed class OutboxInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
            return await base.SavingChangesAsync(eventData, result, cancellationToken);

        var context = eventData.Context;

        // Collect domain events from entities
        var domainEvents = context.ChangeTracker
            .Entries<Entity>()
            .SelectMany(entry => entry.Entity.PopDomainEvents())
            .ToList();

        // Convert to outbox messages
        foreach (var domainEvent in domainEvents)
        {
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                Type = domainEvent.GetType().AssemblyQualifiedName!,
                Content = JsonSerializer.Serialize(domainEvent, domainEvent.GetType()),
                CreatedAt = timeProvider.GetUtcNow()
            };

            context.Set<OutboxMessage>().Add(outboxMessage);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
```

## Background Processor

```csharp
// Infrastructure/BackgroundJobs/OutboxProcessor.cs
public sealed class OutboxProcessor(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<OutboxProcessor> logger) : BackgroundService
{
    private readonly TimeSpan _pollingInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 100;
    private const int MaxRetries = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessOutboxMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing outbox messages");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task ProcessOutboxMessagesAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        var messages = await db.OutboxMessages
            .Where(m => m.ProcessedAt == null && m.RetryCount < MaxRetries)
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        var now = timeProvider.GetUtcNow();
        foreach (var message in messages)
        {
            try
            {
                var @event = message.Deserialize();
                if (@event is not null)
                {
                    await publisher.PublishAsync(@event, ct);
                    message.MarkAsProcessed(now);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process outbox message {Id}", message.Id);
                message.MarkAsFailed(ex.Message);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
```

## Domain Entity with Events

```csharp
// Domain/Common/Entity.cs
public abstract class Entity
{
    public Guid Id { get; protected set; }

    private readonly List<IDomainEvent> _domainEvents = [];

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
    public static Result<Order> Create(Guid customerId, string orderNumber)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            CustomerId = customerId,
            OrderNumber = orderNumber,
            Status = OrderStatus.Created
        };

        order.RaiseDomainEvent(new OrderCreatedEvent(order.Id, customerId));

        return order;
    }
}
```

## Cleanup Job

```csharp
// Infrastructure/BackgroundJobs/OutboxCleanupJob.cs
public sealed class OutboxCleanupJob(
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxCleanupJob> logger,
    TimeProvider timeProvider) : BackgroundService
{
    private readonly TimeSpan _cleanupInterval = TimeSpan.FromHours(1);
    private readonly TimeSpan _retentionPeriod = TimeSpan.FromDays(7);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(_cleanupInterval, stoppingToken);

            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<IDbContext>();

                var cutoff = timeProvider.GetUtcNow() - _retentionPeriod;

                var deleted = await db.OutboxMessages
                    .Where(m => m.ProcessedAt != null && m.ProcessedAt < cutoff)
                    .ExecuteDeleteAsync(stoppingToken);

                if (deleted > 0)
                    logger.LogInformation("Cleaned up {Count} processed outbox messages", deleted);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error cleaning up outbox messages");
            }
        }
    }
}
```

## Registration

```csharp
// Program.cs
builder.Services.AddScoped<OutboxInterceptor>();
builder.Services.AddHostedService<OutboxProcessor>();
builder.Services.AddHostedService<OutboxCleanupJob>();

builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var outboxInterceptor = sp.GetRequiredService<OutboxInterceptor>();
    options.UseSqlServer(connectionString)
           .AddInterceptors(outboxInterceptor);
});
```

## Best Practices

| Practice | Recommendation |
|----------|----------------|
| Idempotency | Make event handlers idempotent (handle duplicates) |
| Ordering | Process messages in order if needed |
| Retries | Limit retries, move to dead letter after max |
| Monitoring | Alert on failed messages |
| Cleanup | Regularly purge processed messages |
| Batching | Process messages in batches for efficiency |

## Alternatives

| Approach | Pros | Cons |
|----------|------|------|
| Outbox table | Simple, works with any message broker | Polling latency |
| Change Data Capture | Real-time, no polling | Complex setup |
| Transactional Saga | Distributed transactions | High complexity |
| Wolverine | Built-in outbox support | Framework dependency |

## Assets

- [assets/OutboxMessage.cs](assets/OutboxMessage.cs) - Complete outbox entity
- [assets/OutboxProcessor.cs](assets/OutboxProcessor.cs) - Background processor

## Related

- `domain-events` - Domain event patterns
- `efcore` - Interceptors
- `cqrs` - Event handlers
