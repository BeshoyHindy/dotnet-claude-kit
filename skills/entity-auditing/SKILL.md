---
name: entity-auditing
description: Entity audit fields (CreatedBy, CreatedOn, UpdatedBy, UpdatedOn). Automatic tracking of entity changes. Use when implementing audit trails.
allowed-tools: Read, Write, Edit, Glob, Grep
---

# Entity Auditing

Patterns for automatically tracking who created/modified entities and when.

**Source**: [EF Core Interceptors](https://learn.microsoft.com/en-us/ef/core/logging-events-diagnostics/interceptors)

## Audit Interfaces

Define contracts in Domain layer:

```csharp
// Domain/Common/IAuditableEntity.cs
public interface IAuditableEntity
{
    DateTimeOffset CreatedOn { get; }
    string? CreatedBy { get; }
    DateTimeOffset? UpdatedOn { get; }
    string? UpdatedBy { get; }
}

// For entities that track creation only
public interface ICreationAuditableEntity
{
    DateTimeOffset CreatedOn { get; }
    string? CreatedBy { get; }
}
```

## Base Entity Implementation

```csharp
// Domain/Common/AuditableEntity.cs
public abstract class AuditableEntity : Entity, IAuditableEntity
{
    public DateTimeOffset CreatedOn { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedOn { get; private set; }
    public string? UpdatedBy { get; private set; }

    // Called by interceptor - internal to allow Infrastructure access
    internal void SetCreationAudit(DateTimeOffset timestamp, string? userId)
    {
        CreatedOn = timestamp;
        CreatedBy = userId;
    }

    internal void SetModificationAudit(DateTimeOffset timestamp, string? userId)
    {
        UpdatedOn = timestamp;
        UpdatedBy = userId;
    }
}

// Alternative: Use init setters for simpler approach
public abstract class AuditableEntity : Entity, IAuditableEntity
{
    public DateTimeOffset CreatedOn { get; init; }
    public string? CreatedBy { get; init; }
    public DateTimeOffset? UpdatedOn { get; set; }
    public string? UpdatedBy { get; set; }
}
```

## User Context Service

```csharp
// Application/Common/Interfaces/ICurrentUserService.cs
public interface ICurrentUserService
{
    string? UserId { get; }
    string? UserName { get; }
    bool IsAuthenticated { get; }
}

// Infrastructure/Services/CurrentUserService.cs
public sealed class CurrentUserService(
    IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    public string? UserId =>
        httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value;

    public string? UserName =>
        httpContextAccessor.HttpContext?.User.FindFirst("name")?.Value
        ?? httpContextAccessor.HttpContext?.User.Identity?.Name;

    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}
```

## SaveChanges Interceptor

Recommended approach using EF Core interceptors:

```csharp
// Infrastructure/Data/Interceptors/AuditableEntityInterceptor.cs
public sealed class AuditableEntityInterceptor(
    ICurrentUserService currentUserService,
    TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditInfo(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditInfo(DbContext? context)
    {
        if (context is null) return;

        var now = timeProvider.GetUtcNow();
        var userId = currentUserService.UserId;

        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Property(e => e.CreatedOn).CurrentValue = now;
                    entry.Property(e => e.CreatedBy).CurrentValue = userId;
                    break;

                case EntityState.Modified:
                    entry.Property(e => e.UpdatedOn).CurrentValue = now;
                    entry.Property(e => e.UpdatedBy).CurrentValue = userId;
                    // Prevent overwriting creation audit
                    entry.Property(e => e.CreatedOn).IsModified = false;
                    entry.Property(e => e.CreatedBy).IsModified = false;
                    break;
            }
        }
    }
}
```

## Registration

```csharp
// Infrastructure/DependencyInjection.cs
public static IServiceCollection AddInfrastructure(
    this IServiceCollection services,
    IConfiguration configuration)
{
    services.AddHttpContextAccessor();
    services.AddScoped<ICurrentUserService, CurrentUserService>();
    services.AddSingleton(TimeProvider.System);
    services.AddScoped<AuditableEntityInterceptor>();

    services.AddDbContext<AppDbContext>((sp, options) =>
    {
        var interceptor = sp.GetRequiredService<AuditableEntityInterceptor>();

        options.UseSqlServer(configuration.GetConnectionString("Default"))
               .AddInterceptors(interceptor);
    });

    return services;
}
```

## Entity Configuration

```csharp
// Infrastructure/Data/Configurations/AuditableEntityConfiguration.cs
public abstract class AuditableEntityConfiguration<TEntity>
    : IEntityTypeConfiguration<TEntity>
    where TEntity : AuditableEntity
{
    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.Property(e => e.CreatedOn)
            .IsRequired();

        builder.Property(e => e.CreatedBy)
            .HasMaxLength(256);

        builder.Property(e => e.UpdatedBy)
            .HasMaxLength(256);

        // Index for querying by creator
        builder.HasIndex(e => e.CreatedBy);
    }
}

// Usage
public class OrderConfiguration : AuditableEntityConfiguration<Order>
{
    public override void Configure(EntityTypeBuilder<Order> builder)
    {
        base.Configure(builder);

        builder.Property(o => o.OrderNumber)
            .IsRequired()
            .HasMaxLength(50);

        // ... other configuration
    }
}
```

## Alternative: Override SaveChangesAsync

If not using interceptors:

```csharp
// Infrastructure/Data/AppDbContext.cs
public class AppDbContext(
    DbContextOptions<AppDbContext> options,
    ICurrentUserService currentUserService,
    TimeProvider timeProvider) : DbContext(options)
{
    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        ApplyAuditInfo();
        return await base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyAuditInfo();
        return base.SaveChanges();
    }

    private void ApplyAuditInfo()
    {
        var now = timeProvider.GetUtcNow();
        var userId = currentUserService.UserId;

        foreach (var entry in ChangeTracker.Entries<IAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(e => e.CreatedOn).CurrentValue = now;
                entry.Property(e => e.CreatedBy).CurrentValue = userId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(e => e.UpdatedOn).CurrentValue = now;
                entry.Property(e => e.UpdatedBy).CurrentValue = userId;
            }
        }
    }
}
```

## Audit History Table

For full audit trails, track all changes:

```csharp
// Domain/Common/AuditLog.cs
public sealed class AuditLog
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string EntityType { get; init; } = string.Empty;
    public string EntityId { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty; // Created, Updated, Deleted
    public string? OldValues { get; init; }
    public string? NewValues { get; init; }
    public string? ChangedProperties { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public string? UserId { get; init; }
}

// In interceptor
private void CreateAuditLog(EntityEntry entry, string action)
{
    var auditLog = new AuditLog
    {
        EntityType = entry.Entity.GetType().Name,
        EntityId = GetPrimaryKeyValue(entry),
        Action = action,
        OldValues = action != "Created" ? SerializeOriginalValues(entry) : null,
        NewValues = action != "Deleted" ? SerializeCurrentValues(entry) : null,
        ChangedProperties = GetChangedProperties(entry),
        Timestamp = timeProvider.GetUtcNow(),
        UserId = currentUserService.UserId
    };

    context.Set<AuditLog>().Add(auditLog);
}
```

## Testing

```csharp
[Fact]
public async Task SaveChangesAsync_NewEntity_SetsCreationAudit()
{
    // Arrange
    var userId = "user-123";
    var fixedTime = new DateTimeOffset(2024, 1, 15, 10, 0, 0, TimeSpan.Zero);

    var userService = Substitute.For<ICurrentUserService>();
    userService.UserId.Returns(userId);

    var timeProvider = new FakeTimeProvider(fixedTime);

    await using var context = CreateContext(userService, timeProvider);
    var order = Order.Create("ORD-001").Value;

    // Act
    context.Orders.Add(order);
    await context.SaveChangesAsync();

    // Assert
    order.CreatedOn.Should().Be(fixedTime);
    order.CreatedBy.Should().Be(userId);
    order.UpdatedOn.Should().BeNull();
}
```

## Best Practices

| Practice | Recommendation |
|----------|----------------|
| TimeProvider | Use `TimeProvider` for testability, not `DateTime.UtcNow` |
| Interceptor | Prefer interceptors over overriding SaveChanges |
| User context | Abstract behind interface for testing |
| Audit history | Consider separate table for compliance requirements |
| Indexing | Index audit columns if querying by creator/date |
| Soft delete | Combine with soft-delete for full tracking |

## Assets

- [assets/AuditableEntityInterceptor.cs](assets/AuditableEntityInterceptor.cs) - Complete interceptor
- [assets/AuditableEntity.cs](assets/AuditableEntity.cs) - Base entity class

## Related

- `efcore` - EF Core patterns
- `soft-delete` - Soft delete with audit
- `clean-architecture` - Where interfaces live
