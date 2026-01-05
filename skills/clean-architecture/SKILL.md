---
name: clean-architecture
description: Clean Architecture layer organization. Dependencies point inward. Domain independent of frameworks. Use when organizing solution structure or validating layer boundaries.
allowed-tools: Read, Glob, Grep
---

# Clean Architecture

Organize code into concentric layers with dependencies pointing inward. Inner layers define interfaces, outer layers implement them. Domain remains independent of frameworks.

**Source**: Robert C. Martin - Clean Architecture

## Dependency Rule

Dependencies point inward only:

```
API → Infrastructure → Application → Domain
            ↓               ↓
       (implements)   (defines interfaces)
```

- **Domain** knows nothing about other layers
- **Application** knows only Domain
- **Infrastructure** implements Application interfaces
- **API** composes everything via DI

## Layers

### Domain (Innermost)

Pure business logic. No framework dependencies.

Contains:
- Entities and aggregate roots
- Value objects
- Domain events
- Business rules

```csharp
// Domain/Orders/Order.cs
public sealed class Order
{
    private readonly List<OrderItem> _items = [];

    public Guid Id { get; private set; }
    public OrderStatus Status { get; private set; }
    public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();

    private Order() { } // Required for ORM mapping

    public static Result<Order> Create(Guid customerId, string orderNumber)
    {
        // Business validation
        if (customerId == Guid.Empty)
            return Error.Validation("Customer required");

        return new Order { Id = Guid.NewGuid(), Status = OrderStatus.Draft };
    }

    public Result AddItem(Guid productId, int quantity)
    {
        if (Status != OrderStatus.Draft)
            return Error.Validation("Cannot modify non-draft order");

        _items.Add(new OrderItem(productId, quantity));
        return Result.Success();
    }
}
```

### Application

Use cases and orchestration. Defines interfaces for external dependencies.

Contains:
- Commands and queries
- Handlers
- Validators
- Interface definitions (ports)

```csharp
// Application/Common/Interfaces/IDbContext.cs
// Note: Using DbSet<T> is a pragmatic trade-off. For stricter isolation, use repository interfaces.
public interface IDbContext
{
    DbSet<Order> Orders { get; }
    Task<int> SaveChangesAsync(CancellationToken ct);
}

// Application/Orders/Commands/CreateOrderHandler.cs
public sealed class CreateOrderHandler(IDbContext db)
    : ICommandHandler<CreateOrderCommand, Guid>
{
    public async Task<Result<Guid>> HandleAsync(
        CreateOrderCommand cmd,
        CancellationToken ct)
    {
        var order = Order.Create(cmd.CustomerId, cmd.OrderNumber);
        if (order.IsFailure)
            return order.Error;

        db.Orders.Add(order.Value);
        await db.SaveChangesAsync(ct);
        return order.Value.Id;
    }
}
```

### Infrastructure

Implements Application interfaces. External concerns.

Contains:
- DbContext implementation
- External service clients
- File system access
- Configuration

```csharp
// Infrastructure/Data/AppDbContext.cs
public sealed class AppDbContext : DbContext, IDbContext
{
    public DbSet<Order> Orders => Set<Order>();
}
```

### API (Outermost)

Entry point. Composes application via DI.

Contains:
- Controllers or Endpoints
- Middleware
- DI configuration
- Program.cs

## Project Structure

```
src/
├── Domain/              # No project references
├── Application/         # References: Domain
├── Infrastructure/      # References: Application
└── Api/                 # References: Application, Infrastructure
```

## Project References

```xml
<!-- Domain.csproj - NO references -->

<!-- Application.csproj -->
<ProjectReference Include="..\Domain\Domain.csproj" />

<!-- Infrastructure.csproj -->
<ProjectReference Include="..\Application\Application.csproj" />

<!-- Api.csproj -->
<ProjectReference Include="..\Application\Application.csproj" />
<ProjectReference Include="..\Infrastructure\Infrastructure.csproj" />
```

## Validation Checklist

- Domain has no project/package references (except primitives)
- No `using Microsoft.EntityFrameworkCore` in Domain
- No data annotations (`[Required]`, `[Key]`) in Domain entities
- Interfaces defined in Application, implemented in Infrastructure
- Handlers in Application, not Infrastructure
- DbContext only in Infrastructure

## Common Violations

```csharp
// BAD: Domain depends on EF Core
using Microsoft.EntityFrameworkCore;

public class Order
{
    [Key] // Framework dependency
    public Guid Id { get; set; }
}

// GOOD: Pure domain
public class Order
{
    public Guid Id { get; private set; }
}
// Configuration in Infrastructure
builder.HasKey(o => o.Id);
```

## References

See [references/folder-structure.md](references/folder-structure.md) for detailed folder organization.

## Related

- `cqrs` - Handler organization
- `efcore` - Infrastructure implementation
- `validation` - Application layer validation
