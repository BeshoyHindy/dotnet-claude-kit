---
name: caching
description: Caching patterns with IMemoryCache, IDistributedCache, Redis. Cache-aside, invalidation strategies. Use when implementing caching.
allowed-tools: Read, Write, Edit, Glob, Grep
---

# Caching

Patterns for caching in .NET applications.

**Source**: [Caching in .NET](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/overview)

## In-Memory Cache

For single-instance applications:

```csharp
// Registration
builder.Services.AddMemoryCache();

// Usage
public sealed class ProductService(IMemoryCache cache, IDbContext db)
{
    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var cacheKey = $"product:{id}";

        if (cache.TryGetValue(cacheKey, out Product? cached))
            return cached;

        var product = await db.Products.FindAsync([id], ct);

        if (product is not null)
        {
            var options = new MemoryCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                .SetAbsoluteExpiration(TimeSpan.FromHours(1));

            cache.Set(cacheKey, product, options);
        }

        return product;
    }
}
```

## Distributed Cache (Redis)

For multi-instance applications:

```bash
dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis
```

```csharp
// Registration
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "YourApp:";
});

// Usage with serialization
public sealed class ProductService(
    IDistributedCache cache,
    IDbContext db)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var cacheKey = $"product:{id}";

        var cached = await cache.GetStringAsync(cacheKey, ct);
        if (cached is not null)
            return JsonSerializer.Deserialize<Product>(cached, JsonOptions);

        var product = await db.Products.FindAsync([id], ct);

        if (product is not null)
        {
            var options = new DistributedCacheEntryOptions()
                .SetSlidingExpiration(TimeSpan.FromMinutes(5))
                .SetAbsoluteExpiration(TimeSpan.FromHours(1));

            var json = JsonSerializer.Serialize(product, JsonOptions);
            await cache.SetStringAsync(cacheKey, json, options, ct);
        }

        return product;
    }
}
```

## Hybrid Cache (.NET 9+)

Combines memory and distributed cache:

```bash
dotnet add package Microsoft.Extensions.Caching.Hybrid
```

```csharp
// Registration
builder.Services.AddHybridCache(options =>
{
    options.DefaultEntryOptions = new HybridCacheEntryOptions
    {
        Expiration = TimeSpan.FromMinutes(5),
        LocalCacheExpiration = TimeSpan.FromMinutes(1)
    };
});

// Usage - much simpler API
public sealed class ProductService(HybridCache cache, IDbContext db)
{
    public async Task<Product?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await cache.GetOrCreateAsync(
            $"product:{id}",
            async token => await db.Products.FindAsync([id], token),
            cancellationToken: ct);
    }
}
```

## Cache Service Abstraction

```csharp
// Application/Common/Interfaces/ICacheService.cs
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<T> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory, TimeSpan? expiration = null, CancellationToken ct = default);
}

// Infrastructure/Services/RedisCacheService.cs
public sealed class RedisCacheService(IDistributedCache cache) : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        var cached = await cache.GetStringAsync(key, ct);
        return cached is null ? default : JsonSerializer.Deserialize<T>(cached, JsonOptions);
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken ct = default)
    {
        var options = new DistributedCacheEntryOptions();
        if (expiration.HasValue)
            options.SetAbsoluteExpiration(expiration.Value);

        var json = JsonSerializer.Serialize(value, JsonOptions);
        await cache.SetStringAsync(key, json, options, ct);
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        await cache.RemoveAsync(key, ct);
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken ct = default)
    {
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null)
            return cached;

        var value = await factory(ct);
        await SetAsync(key, value, expiration, ct);
        return value;
    }
}
```

## Cache Keys

```csharp
// Infrastructure/Caching/CacheKeys.cs
public static class CacheKeys
{
    public static string Product(Guid id) => $"product:{id}";
    public static string ProductsByCategory(string category) => $"products:category:{category}";
    public static string Customer(Guid id) => $"customer:{id}";
    public static string UserPermissions(Guid userId) => $"user:{userId}:permissions";

    // For invalidation patterns
    public static string ProductPattern => "product:*";
}
```

## Cache Invalidation

### Manual Invalidation

```csharp
public sealed class UpdateProductHandler(
    IDbContext db,
    ICacheService cache) : ICommandHandler<UpdateProductCommand>
{
    public async Task<Result> HandleAsync(UpdateProductCommand command, CancellationToken ct)
    {
        var product = await db.Products.FindAsync([command.ProductId], ct);
        if (product is null)
            return Error.NotFound("Product", command.ProductId);

        product.Update(command.Name, command.Price);
        await db.SaveChangesAsync(ct);

        // Invalidate cache
        await cache.RemoveAsync(CacheKeys.Product(command.ProductId), ct);
        await cache.RemoveAsync(CacheKeys.ProductsByCategory(product.Category), ct);

        return Result.Success();
    }
}
```

### Event-Based Invalidation

```csharp
public sealed class InvalidateCacheOnProductUpdated(
    ICacheService cache) : IDomainEventHandler<ProductUpdatedEvent>
{
    public async Task HandleAsync(ProductUpdatedEvent domainEvent, CancellationToken ct)
    {
        await cache.RemoveAsync(CacheKeys.Product(domainEvent.ProductId), ct);
    }
}
```

## Response Caching

For HTTP responses:

```csharp
// Program.cs
builder.Services.AddResponseCaching();
app.UseResponseCaching();

// Controller
[HttpGet("{id}")]
[ResponseCache(Duration = 60, VaryByQueryKeys = ["id"])]
public async Task<IActionResult> Get(Guid id) { /* ... */ }

// Minimal API
app.MapGet("/products/{id}", GetProduct)
    .CacheOutput(policy => policy.Expire(TimeSpan.FromMinutes(1)));
```

## Output Caching (.NET 7+)

Server-side response caching:

```csharp
builder.Services.AddOutputCache(options =>
{
    options.AddBasePolicy(builder => builder.Expire(TimeSpan.FromSeconds(30)));

    options.AddPolicy("Products", builder =>
        builder.Expire(TimeSpan.FromMinutes(5))
               .Tag("products"));
});

app.UseOutputCache();

// Minimal API
app.MapGet("/products", GetProducts)
    .CacheOutput("Products");

// Invalidation
app.MapPost("/products", async (IOutputCacheStore cache, ...) =>
{
    // Create product...
    await cache.EvictByTagAsync("products", CancellationToken.None);
});
```

## Cache Stampede Prevention

Prevent multiple concurrent cache misses:

```csharp
public sealed class CacheService(
    IDistributedCache cache,
    IDistributedLockFactory lockFactory) : ICacheService
{
    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken ct = default)
    {
        var cached = await GetAsync<T>(key, ct);
        if (cached is not null)
            return cached;

        // Acquire lock to prevent stampede
        await using var @lock = await lockFactory.CreateLockAsync(
            $"lock:{key}",
            TimeSpan.FromSeconds(30),
            ct);

        // Double-check after acquiring lock
        cached = await GetAsync<T>(key, ct);
        if (cached is not null)
            return cached;

        var value = await factory(ct);
        await SetAsync(key, value, expiration, ct);
        return value;
    }
}
```

## Best Practices

| Practice | Recommendation |
|----------|----------------|
| Key naming | Use prefixes and colons: `entity:id` |
| Serialization | Use consistent JSON settings |
| Expiration | Set both sliding and absolute |
| Invalidation | Invalidate on writes, not reads |
| Stampede | Use locking for expensive operations |
| Monitoring | Track hit/miss ratios |

## When to Cache

| Cache | Don't Cache |
|-------|-------------|
| Frequently read data | Rapidly changing data |
| Expensive computations | User-specific sensitive data |
| Static/reference data | Data requiring real-time accuracy |
| API responses | Write-heavy data |

## Cache Patterns

| Pattern | Use Case |
|---------|----------|
| Cache-aside | Read-heavy, can tolerate stale data |
| Write-through | Data consistency critical |
| Write-behind | High write volume, eventual consistency OK |
| Read-through | Simplify application code |

## Assets

- [assets/CacheService.cs](assets/CacheService.cs) - Cache service implementation
- [assets/CacheKeys.cs](assets/CacheKeys.cs) - Key management

## Related

- `efcore` - Query caching
- `api-design` - Response caching
