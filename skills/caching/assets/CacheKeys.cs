// Infrastructure/Caching/CacheKeys.cs
namespace YourNamespace.Infrastructure.Caching;

/// <summary>
/// Centralized cache key management.
/// Use consistent naming: {entity}:{identifier} or {entity}:{filter}:{value}
/// </summary>
public static class CacheKeys
{
    private const string Separator = ":";

    // Products
    public static string Product(Guid id) => $"product{Separator}{id}";
    public static string ProductsByCategory(string category) => $"products{Separator}category{Separator}{category}";
    public static string ProductList(int page, int pageSize) => $"products{Separator}list{Separator}{page}{Separator}{pageSize}";
    public static string ProductCount => "products:count";

    // Customers
    public static string Customer(Guid id) => $"customer{Separator}{id}";
    public static string CustomerByEmail(string email) => $"customer{Separator}email{Separator}{email.ToLowerInvariant()}";

    // Orders
    public static string Order(Guid id) => $"order{Separator}{id}";
    public static string OrdersByCustomer(Guid customerId) => $"orders{Separator}customer{Separator}{customerId}";

    // Users
    public static string User(Guid id) => $"user{Separator}{id}";
    public static string UserPermissions(Guid userId) => $"user{Separator}{userId}{Separator}permissions";
    public static string UserRoles(Guid userId) => $"user{Separator}{userId}{Separator}roles";

    // Configuration/Settings
    public static string Settings(string key) => $"settings{Separator}{key}";
    public static string FeatureFlag(string flag) => $"feature{Separator}{flag}";

    // For pattern-based invalidation (Redis)
    public static class Patterns
    {
        public const string AllProducts = "product:*";
        public const string AllCustomers = "customer:*";
        public const string AllOrders = "order:*";

        public static string CustomerOrders(Guid customerId) => $"orders:customer:{customerId}*";
    }
}

// Infrastructure/Caching/CacheInvalidator.cs
namespace YourNamespace.Infrastructure.Caching;

using YourNamespace.Application.Common.Interfaces;

/// <summary>
/// Helper for invalidating related cache entries.
/// </summary>
public sealed class CacheInvalidator(ICacheService cache)
{
    public async Task InvalidateProductAsync(Guid productId, string category, CancellationToken ct = default)
    {
        await Task.WhenAll(
            cache.RemoveAsync(CacheKeys.Product(productId), ct),
            cache.RemoveAsync(CacheKeys.ProductsByCategory(category), ct),
            cache.RemoveAsync(CacheKeys.ProductCount, ct)
        );
    }

    public async Task InvalidateCustomerAsync(Guid customerId, string email, CancellationToken ct = default)
    {
        await Task.WhenAll(
            cache.RemoveAsync(CacheKeys.Customer(customerId), ct),
            cache.RemoveAsync(CacheKeys.CustomerByEmail(email), ct)
        );
    }

    public async Task InvalidateOrderAsync(Guid orderId, Guid customerId, CancellationToken ct = default)
    {
        await Task.WhenAll(
            cache.RemoveAsync(CacheKeys.Order(orderId), ct),
            cache.RemoveAsync(CacheKeys.OrdersByCustomer(customerId), ct)
        );
    }

    public async Task InvalidateUserSecurityAsync(Guid userId, CancellationToken ct = default)
    {
        await Task.WhenAll(
            cache.RemoveAsync(CacheKeys.User(userId), ct),
            cache.RemoveAsync(CacheKeys.UserPermissions(userId), ct),
            cache.RemoveAsync(CacheKeys.UserRoles(userId), ct)
        );
    }
}

// Example usage in event handler
namespace YourNamespace.Application.Products.EventHandlers;

using YourNamespace.Domain.Products.Events;
using YourNamespace.Infrastructure.Caching;

public sealed class InvalidateCacheOnProductUpdated(
    CacheInvalidator invalidator)
    : IDomainEventHandler<ProductUpdatedEvent>
{
    public async Task HandleAsync(ProductUpdatedEvent domainEvent, CancellationToken ct)
    {
        await invalidator.InvalidateProductAsync(
            domainEvent.ProductId,
            domainEvent.Category,
            ct);
    }
}
