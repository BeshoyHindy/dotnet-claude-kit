---
name: efcore
description: Entity Framework Core patterns for .NET. Configuration, queries, migrations. Use when working with database access, entity mapping, or EF Core configuration.
allowed-tools: Read, Write, Edit, Glob, Grep
---

# Entity Framework Core

**Source**: [EF Core Documentation](https://learn.microsoft.com/en-us/ef/core/)

EF Core is the ORM for .NET. Keep domain entities clean by using Fluent API for configuration. No data annotations in domain.

## Entity Configuration

Use `IEntityTypeConfiguration<T>` for each entity:

```csharp
public sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Id)
            .ValueGeneratedNever();

        builder.Property(o => o.OrderNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey("OrderId")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(o => o.OrderNumber)
            .IsUnique();
    }
}
```

## Value Objects

Use `OwnsOne` for value objects:

```csharp
builder.OwnsOne(o => o.Total, money =>
{
    money.Property(m => m.Amount)
        .HasColumnName("total_amount")
        .HasPrecision(18, 4);

    money.Property(m => m.Currency)
        .HasColumnName("total_currency")
        .HasMaxLength(3);
});
```

## Private Collections

Access private backing fields:

```csharp
// Domain
public sealed class Order
{
    private readonly List<OrderItem> _items = [];
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
}

// Configuration
builder.HasMany(o => o.Items)
    .WithOne()
    .HasForeignKey("OrderId");

builder.Navigation(o => o.Items)
    .UsePropertyAccessMode(PropertyAccessMode.Field);
```

## DbContext

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IDbContext
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AppDbContext).Assembly);
    }
}
```

## Query Patterns

### Projection (Read-Only)

```csharp
var orders = await db.Orders
    .AsNoTracking()
    .Where(o => o.Status == OrderStatus.Submitted)
    .Select(o => new OrderResponse(o.Id, o.OrderNumber, o.Status))
    .ToListAsync(ct);
```

### Eager Loading

```csharp
var order = await db.Orders
    .Include(o => o.Items)
    .FirstOrDefaultAsync(o => o.Id == id, ct);
```

### Split Query

For large includes to avoid cartesian explosion:

```csharp
var orders = await db.Orders
    .Include(o => o.Items)
    .AsSplitQuery()
    .ToListAsync(ct);
```

### Compiled Queries

For high-frequency queries, compile once to avoid query tree overhead:

```csharp
// Define as static readonly
private static readonly Func<AppDbContext, Guid, CancellationToken, Task<Order?>> GetOrderByIdQuery =
    EF.CompileAsyncQuery((AppDbContext db, Guid id, CancellationToken ct) =>
        db.Orders
            .Include(o => o.Items)
            .FirstOrDefault(o => o.Id == id));

// Usage
var order = await GetOrderByIdQuery(_db, orderId, ct);
```

**When to use compiled queries:**
- Hot paths called frequently (hundreds+ per second)
- Simple queries without dynamic filters
- Measurable performance improvement needed

## Bulk Operations

EF Core 7+ supports efficient bulk updates and deletes without loading entities:

```csharp
// Bulk update - single SQL statement, no tracking
var updated = await db.Orders
    .Where(o => o.Status == OrderStatus.Pending)
    .Where(o => o.CreatedAt < cutoffDate)
    .ExecuteUpdateAsync(s => s
        .SetProperty(o => o.Status, OrderStatus.Expired)
        .SetProperty(o => o.ModifiedAt, timeProvider.GetUtcNow()),
        ct);

// Bulk delete - single SQL statement
var deleted = await db.OutboxMessages
    .Where(m => m.ProcessedAt != null)
    .Where(m => m.ProcessedAt < retentionCutoff)
    .ExecuteDeleteAsync(ct);

logger.LogInformation("Deleted {Count} old outbox messages", deleted);
```

**Bulk operation caveats:**
- Bypasses change tracker (no events, no interceptors)
- No cascade delete unless configured in database
- Returns count, not entities

## Global Query Filters

For multi-tenancy or soft delete:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    modelBuilder.Entity<Order>()
        .HasQueryFilter(o => o.TenantId == _tenantId);

    modelBuilder.Entity<Customer>()
        .HasQueryFilter(c => !c.IsDeleted);
}

// Bypass when needed
var all = await db.Orders.IgnoreQueryFilters().ToListAsync(ct);
```

## Interceptors

For audit, soft delete, domain events:

```csharp
public sealed class AuditInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken ct = default)
    {
        var context = eventData.Context;
        if (context is null) return ValueTask.FromResult(result);

        var now = timeProvider.GetUtcNow();

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.ModifiedAt = now;
            }
        }

        return ValueTask.FromResult(result);
    }
}

// Registration
services.AddSingleton(TimeProvider.System);
services.AddScoped<AuditInterceptor>();
services.AddDbContext<AppDbContext>((sp, options) =>
{
    options.UseSqlServer(connectionString);  // Or UseNpgsql, UseSqlite
    options.AddInterceptors(sp.GetRequiredService<AuditInterceptor>());
});

## Connection Resiliency

Configure retry policies for transient failures:

```csharp
// SQL Server with retry
services.AddDbContext<AppDbContext>(options =>
{
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);  // Retry on all transient errors
    });
});

// PostgreSQL with retry
services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null);
    });
});
```

**Important**: When using retry, wrap multi-statement operations in explicit transactions:

```csharp
await using var transaction = await db.Database.BeginTransactionAsync(ct);
try
{
    // Multiple operations...
    await db.SaveChangesAsync(ct);
    await transaction.CommitAsync(ct);
}
catch
{
    await transaction.RollbackAsync(ct);
    throw;
}
```

## Migrations

```bash
# Add migration
dotnet ef migrations add InitialCreate -p src/Infrastructure -s src/Api

# Apply
dotnet ef database update -p src/Infrastructure -s src/Api

# Generate script
dotnet ef migrations script -p src/Infrastructure -s src/Api
```

## References

- [references/configuration.md](references/configuration.md) - Complete configuration examples
- [references/performance.md](references/performance.md) - Query optimization

## Related

- `clean-architecture` - Where EF Core lives (Infrastructure)
- `cqrs` - Query handlers using EF Core
