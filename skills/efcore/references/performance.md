# EF Core Performance

## Avoid N+1 Queries

Problem:
```csharp
var orders = await db.Orders.ToListAsync();
foreach (var order in orders)
{
    // Each access triggers a query
    var count = order.Items.Count;
}
```

Solution 1: Include
```csharp
var orders = await db.Orders
    .Include(o => o.Items)
    .ToListAsync();
```

Solution 2: Projection
```csharp
var summaries = await db.Orders
    .Select(o => new
    {
        o.Id,
        o.OrderNumber,
        ItemCount = o.Items.Count
    })
    .ToListAsync();
```

## Use Projections for Read

```csharp
// Instead of loading full entity
var order = await db.Orders.FindAsync(id);
return new OrderResponse(order.Id, order.OrderNumber);

// Project directly
var response = await db.Orders
    .Where(o => o.Id == id)
    .Select(o => new OrderResponse(o.Id, o.OrderNumber))
    .FirstOrDefaultAsync();
```

## AsNoTracking for Read-Only

```csharp
var orders = await db.Orders
    .AsNoTracking()
    .Where(o => o.Status == OrderStatus.Submitted)
    .ToListAsync();
```

## Split Queries

Avoid cartesian explosion with multiple includes:

```csharp
var orders = await db.Orders
    .Include(o => o.Items)
    .Include(o => o.Payments)
    .AsSplitQuery()
    .ToListAsync();
```

## Compiled Queries

For hot paths:

```csharp
private static readonly Func<AppDbContext, Guid, Task<Order?>> GetOrderById =
    EF.CompileAsyncQuery(
        (AppDbContext db, Guid id) =>
            db.Orders.FirstOrDefault(o => o.Id == id));

// Usage
var order = await GetOrderById(db, orderId);
```

## Batch Operations

```csharp
// Instead of loading + saving each
foreach (var order in db.Orders.Where(o => o.Status == "Pending"))
{
    order.Status = "Cancelled";
}
await db.SaveChangesAsync();

// Use ExecuteUpdate (EF Core 7+)
await db.Orders
    .Where(o => o.Status == "Pending")
    .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, "Cancelled"));
```

## Indexes

Add indexes for:
- Foreign keys
- Frequently filtered columns
- Columns in WHERE clauses
- Columns in ORDER BY

```csharp
builder.HasIndex(o => o.CustomerId);
builder.HasIndex(o => o.Status);
builder.HasIndex(o => o.CreatedAt);
builder.HasIndex(o => new { o.TenantId, o.Status });

// Filtered indexes for common queries
builder.HasIndex(o => o.Status)
    .HasFilter("[Status] = 'Pending'")
    .HasDatabaseName("IX_Orders_Pending");

// Unique indexes
builder.HasIndex(o => o.OrderNumber)
    .IsUnique();
```

## Common Pitfalls

### Client Evaluation

```csharp
// WRONG: Evaluates on client (loads all orders first)
var orders = await db.Orders
    .Where(o => MyCustomMethod(o.Status)) // Can't translate to SQL
    .ToListAsync();

// CORRECT: Use translatable expressions
var orders = await db.Orders
    .Where(o => o.Status == OrderStatus.Pending)
    .ToListAsync();
```

### Loading Unnecessary Data

```csharp
// WRONG: Loads all columns
var names = await db.Customers.ToListAsync();
var result = names.Select(c => c.Name);

// CORRECT: Project only what you need
var names = await db.Customers
    .Select(c => c.Name)
    .ToListAsync();
```

### String Contains vs StartsWith

```csharp
// SLOW: Full table scan (uses LIKE '%term%')
var results = await db.Products
    .Where(p => p.Name.Contains("Widget"))
    .ToListAsync();

// FASTER: Can use index (uses LIKE 'Widget%')
var results = await db.Products
    .Where(p => p.Name.StartsWith("Widget"))
    .ToListAsync();
```

### Tracking When Not Needed

```csharp
// WRONG: Tracking entities you won't modify
var orders = await db.Orders
    .Where(o => o.Status == "Completed")
    .ToListAsync();

// CORRECT: Disable tracking for read-only queries
var orders = await db.Orders
    .AsNoTracking()
    .Where(o => o.Status == "Completed")
    .ToListAsync();
```

### Multiple SaveChanges Calls

```csharp
// WRONG: Multiple round trips
foreach (var order in orders)
{
    order.Status = "Processed";
    await db.SaveChangesAsync();
}

// CORRECT: Single SaveChanges for batch
foreach (var order in orders)
{
    order.Status = "Processed";
}
await db.SaveChangesAsync();

// BETTER: Use ExecuteUpdate for bulk changes
await db.Orders
    .Where(o => orderIds.Contains(o.Id))
    .ExecuteUpdateAsync(s => s.SetProperty(o => o.Status, "Processed"));
```

### Lazy Loading N+1

```csharp
// WRONG: Lazy loading causes N+1
var orders = await db.Orders.ToListAsync();
foreach (var order in orders)
{
    Console.WriteLine(order.Customer.Name); // Triggers query per order
}

// CORRECT: Eager load or project
var orders = await db.Orders
    .Include(o => o.Customer)
    .ToListAsync();

// OR project
var orders = await db.Orders
    .Select(o => new { o.Id, CustomerName = o.Customer.Name })
    .ToListAsync();
```

## Query Debugging

### Enable Logging

```csharp
// In DbContext or configuration
optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information);

// Or in appsettings.json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

### Detect N+1 at Runtime

```csharp
// Throw on multiple queries per request (dev only)
optionsBuilder.ConfigureWarnings(w =>
    w.Throw(RelationalEventId.MultipleCollectionIncludeWarning));
```

### Query Tags

```csharp
var orders = await db.Orders
    .TagWith("GetPendingOrders - OrderService")
    .Where(o => o.Status == "Pending")
    .ToListAsync();
// Tag appears in SQL comments for easier debugging
```

## Performance Checklist

| Check | Recommendation |
|-------|----------------|
| N+1 queries | Use Include or projection |
| Large result sets | Paginate, don't load all |
| Tracking overhead | Use AsNoTracking for reads |
| Missing indexes | Index filtered/sorted columns |
| Client evaluation | Check query logs for warnings |
| Cartesian explosion | Use AsSplitQuery |
| Hot paths | Consider compiled queries |
| Bulk operations | Use ExecuteUpdate/Delete |
