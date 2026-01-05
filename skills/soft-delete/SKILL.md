---
name: soft-delete
description: Soft delete pattern - mark entities as deleted instead of removing. Query filters, restoration, cascading. Use when implementing recoverable deletion.
allowed-tools: Read, Write, Edit, Glob, Grep
---

# Soft Delete Pattern

Mark entities as deleted instead of physically removing them. Enables audit trails, recovery, and referential integrity.

**Source**: [EF Core Global Query Filters](https://learn.microsoft.com/en-us/ef/core/querying/filters)

## Interface Definition

```csharp
// Domain/Common/ISoftDeletable.cs
public interface ISoftDeletable
{
    bool IsDeleted { get; }
    DateTimeOffset? DeletedOn { get; }
    string? DeletedBy { get; }
}
```

## Base Entity

```csharp
// Domain/Common/SoftDeletableEntity.cs
public abstract class SoftDeletableEntity : AuditableEntity, ISoftDeletable
{
    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedOn { get; private set; }
    public string? DeletedBy { get; private set; }

    public virtual void Delete(DateTimeOffset timestamp, string? userId)
    {
        if (IsDeleted) return;

        IsDeleted = true;
        DeletedOn = timestamp;
        DeletedBy = userId;
    }

    public virtual void Restore()
    {
        IsDeleted = false;
        DeletedOn = null;
        DeletedBy = null;
    }
}
```

## Global Query Filter

Automatically exclude soft-deleted entities from queries:

```csharp
// Infrastructure/Data/AppDbContext.cs
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Apply to all soft-deletable entities
    foreach (var entityType in modelBuilder.Model.GetEntityTypes())
    {
        if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
        {
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var property = Expression.Property(parameter, nameof(ISoftDeletable.IsDeleted));
            var filter = Expression.Lambda(
                Expression.Equal(property, Expression.Constant(false)),
                parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }
}
```

## Interceptor for Automatic Soft Delete

Convert physical deletes to soft deletes:

```csharp
// Infrastructure/Data/Interceptors/SoftDeleteInterceptor.cs
public sealed class SoftDeleteInterceptor(
    ICurrentUserService currentUserService,
    TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ConvertToSoftDelete(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ConvertToSoftDelete(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ConvertToSoftDelete(DbContext? context)
    {
        if (context is null) return;

        var now = timeProvider.GetUtcNow();
        var userId = currentUserService.UserId;

        foreach (var entry in context.ChangeTracker.Entries<ISoftDeletable>())
        {
            if (entry.State == EntityState.Deleted)
            {
                // Convert to soft delete
                entry.State = EntityState.Modified;
                entry.Property(e => e.IsDeleted).CurrentValue = true;
                entry.Property(e => e.DeletedOn).CurrentValue = now;
                entry.Property(e => e.DeletedBy).CurrentValue = userId;
            }
        }
    }
}
```

## Querying Soft-Deleted Entities

```csharp
// Include deleted entities when needed
var allOrders = await db.Orders
    .IgnoreQueryFilters()
    .ToListAsync();

// Only deleted entities
var deletedOrders = await db.Orders
    .IgnoreQueryFilters()
    .Where(o => o.IsDeleted)
    .ToListAsync();

// Specific entity including if deleted
var order = await db.Orders
    .IgnoreQueryFilters()
    .FirstOrDefaultAsync(o => o.Id == orderId);
```

## Restore Handler

```csharp
// Application/Orders/Commands/RestoreOrder/RestoreOrderCommand.cs
public sealed record RestoreOrderCommand(Guid OrderId) : ICommand;

// Application/Orders/Commands/RestoreOrder/RestoreOrderHandler.cs
public sealed class RestoreOrderHandler(
    IDbContext db) : ICommandHandler<RestoreOrderCommand>
{
    public async Task<Result> HandleAsync(
        RestoreOrderCommand command,
        CancellationToken ct)
    {
        var order = await db.Orders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == command.OrderId, ct);

        if (order is null)
            return Error.NotFound("Order", command.OrderId);

        if (!order.IsDeleted)
            return Error.Validation("Order is not deleted");

        order.Restore();
        await db.SaveChangesAsync(ct);

        return Result.Success();
    }
}
```

## Cascade Soft Delete

Handle related entities:

```csharp
public sealed class Order : SoftDeletableEntity
{
    private readonly List<OrderItem> _items = [];
    public IReadOnlyList<OrderItem> Items => _items.AsReadOnly();

    public override void Delete(DateTimeOffset timestamp, string? userId)
    {
        base.Delete(timestamp, userId);

        // Cascade to children
        foreach (var item in _items)
        {
            item.Delete(timestamp, userId);
        }
    }

    public override void Restore()
    {
        base.Restore();

        // Restore children
        foreach (var item in _items.Where(i => i.IsDeleted))
        {
            item.Restore();
        }
    }
}
```

## Entity Configuration

```csharp
// Infrastructure/Data/Configurations/SoftDeletableEntityConfiguration.cs
public abstract class SoftDeletableEntityConfiguration<TEntity>
    : AuditableEntityConfiguration<TEntity>
    where TEntity : SoftDeletableEntity
{
    public override void Configure(EntityTypeBuilder<TEntity> builder)
    {
        base.Configure(builder);

        builder.Property(e => e.IsDeleted)
            .HasDefaultValue(false);

        builder.Property(e => e.DeletedBy)
            .HasMaxLength(256);

        // Query filter
        builder.HasQueryFilter(e => !e.IsDeleted);

        // Index for finding deleted entities
        builder.HasIndex(e => e.IsDeleted)
            .HasFilter("[IsDeleted] = 1");
    }
}
```

## Hard Delete When Needed

For actual deletion (e.g., GDPR compliance):

```csharp
public async Task HardDeleteAsync(Guid orderId, CancellationToken ct)
{
    var order = await db.Orders
        .IgnoreQueryFilters()
        .FirstOrDefaultAsync(o => o.Id == orderId, ct);

    if (order is not null)
    {
        db.Orders.Remove(order);
        await db.SaveChangesAsync(ct);
    }
}
```

## Endpoint Examples

### With Controllers

```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController(
    ICommandHandler<DeleteOrderCommand> deleteHandler,
    ICommandHandler<RestoreOrderCommand> restoreHandler) : ControllerBase
{
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await deleteHandler.HandleAsync(new DeleteOrderCommand(id), ct);
        return result.IsSuccess ? NoContent() : NotFound(result.Error);
    }

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        var result = await restoreHandler.HandleAsync(new RestoreOrderCommand(id), ct);
        return result.IsSuccess ? Ok() : BadRequest(result.Error);
    }

    [HttpGet("deleted")]
    public async Task<IActionResult> GetDeleted(
        [FromServices] IDbContext db,
        CancellationToken ct)
    {
        var orders = await db.Orders
            .IgnoreQueryFilters()
            .Where(o => o.IsDeleted)
            .Select(o => new OrderResponse(o.Id, o.OrderNumber, o.DeletedOn))
            .ToListAsync(ct);

        return Ok(orders);
    }
}
```

### With Minimal APIs

```csharp
app.MapDelete("/orders/{id:guid}", async (
    Guid id,
    ICommandHandler<DeleteOrderCommand> handler,
    CancellationToken ct) =>
{
    var result = await handler.HandleAsync(new DeleteOrderCommand(id), ct);
    return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error);
});

app.MapPost("/orders/{id:guid}/restore", async (
    Guid id,
    ICommandHandler<RestoreOrderCommand> handler,
    CancellationToken ct) =>
{
    var result = await handler.HandleAsync(new RestoreOrderCommand(id), ct);
    return result.IsSuccess ? Results.Ok() : Results.BadRequest(result.Error);
});
```

## Best Practices

| Practice | Recommendation |
|----------|----------------|
| Query filters | Always apply globally, use IgnoreQueryFilters when needed |
| Cascade | Consider related entities when soft deleting |
| Indexing | Index IsDeleted column with filter |
| Hard delete | Provide for GDPR/compliance needs |
| Audit | Combine with entity-auditing for full tracking |
| Cleanup | Schedule job to hard delete old soft-deleted records |

## Common Pitfalls

| Pitfall | Solution |
|---------|----------|
| Include() ignores filter | Apply filter to navigation properties too |
| Unique constraints | Include IsDeleted in unique indexes |
| Foreign keys | Consider soft delete in FK relationships |
| Performance | Large tables with many deleted rows need cleanup |

## Assets

- [assets/SoftDeleteInterceptor.cs](assets/SoftDeleteInterceptor.cs) - Interceptor
- [assets/SoftDeletableEntity.cs](assets/SoftDeletableEntity.cs) - Base entity

## Related

- `entity-auditing` - Audit fields
- `efcore` - Query filters
- `clean-architecture` - Entity patterns
