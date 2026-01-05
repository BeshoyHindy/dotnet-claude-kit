// Application/Common/Interfaces/ICacheService.cs
namespace YourNamespace.Application.Common.Interfaces;

public interface ICacheService
{
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default);
    Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken ct = default);
    Task RemoveAsync(string key, CancellationToken ct = default);
    Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken ct = default);
}

// Infrastructure/Services/RedisCacheService.cs
namespace YourNamespace.Infrastructure.Services;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using YourNamespace.Application.Common.Interfaces;

public sealed class RedisCacheService(
    IDistributedCache cache,
    ILogger<RedisCacheService> logger) : ICacheService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        try
        {
            var cached = await cache.GetStringAsync(key, ct);

            if (cached is null)
            {
                logger.LogDebug("Cache miss for key {Key}", key);
                return default;
            }

            logger.LogDebug("Cache hit for key {Key}", key);
            return JsonSerializer.Deserialize<T>(cached, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get cache key {Key}", key);
            return default;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken ct = default)
    {
        try
        {
            var options = new DistributedCacheEntryOptions();

            if (expiration.HasValue)
            {
                options.SetAbsoluteExpiration(expiration.Value);
                options.SetSlidingExpiration(expiration.Value / 2);
            }
            else
            {
                // Default expiration
                options.SetAbsoluteExpiration(TimeSpan.FromHours(1));
                options.SetSlidingExpiration(TimeSpan.FromMinutes(15));
            }

            var json = JsonSerializer.Serialize(value, JsonOptions);
            await cache.SetStringAsync(key, json, options, ct);

            logger.LogDebug("Cached key {Key}", key);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to set cache key {Key}", key);
        }
    }

    public async Task RemoveAsync(string key, CancellationToken ct = default)
    {
        try
        {
            await cache.RemoveAsync(key, ct);
            logger.LogDebug("Removed cache key {Key}", key);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to remove cache key {Key}", key);
        }
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

        if (value is not null)
        {
            await SetAsync(key, value, expiration, ct);
        }

        return value;
    }
}

// Infrastructure/Services/MemoryCacheService.cs
namespace YourNamespace.Infrastructure.Services;

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using YourNamespace.Application.Common.Interfaces;

/// <summary>
/// In-memory cache implementation for single-instance deployments.
/// </summary>
public sealed class MemoryCacheService(
    IMemoryCache cache,
    ILogger<MemoryCacheService> logger) : ICacheService
{
    public Task<T?> GetAsync<T>(string key, CancellationToken ct = default)
    {
        if (cache.TryGetValue(key, out T? value))
        {
            logger.LogDebug("Cache hit for key {Key}", key);
            return Task.FromResult(value);
        }

        logger.LogDebug("Cache miss for key {Key}", key);
        return Task.FromResult<T?>(default);
    }

    public Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? expiration = null,
        CancellationToken ct = default)
    {
        var options = new MemoryCacheEntryOptions();

        if (expiration.HasValue)
        {
            options.SetAbsoluteExpiration(expiration.Value);
            options.SetSlidingExpiration(expiration.Value / 2);
        }
        else
        {
            options.SetAbsoluteExpiration(TimeSpan.FromHours(1));
            options.SetSlidingExpiration(TimeSpan.FromMinutes(15));
        }

        cache.Set(key, value, options);
        logger.LogDebug("Cached key {Key}", key);

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        cache.Remove(key);
        logger.LogDebug("Removed cache key {Key}", key);
        return Task.CompletedTask;
    }

    public async Task<T> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiration = null,
        CancellationToken ct = default)
    {
        if (cache.TryGetValue(key, out T? cached) && cached is not null)
            return cached;

        var value = await factory(ct);

        if (value is not null)
        {
            await SetAsync(key, value, expiration, ct);
        }

        return value;
    }
}

// Registration in DependencyInjection.cs:
//
// For Redis (distributed):
// services.AddStackExchangeRedisCache(options =>
// {
//     options.Configuration = configuration.GetConnectionString("Redis");
//     options.InstanceName = "YourApp:";
// });
// services.AddScoped<ICacheService, RedisCacheService>();
//
// For in-memory (single instance):
// services.AddMemoryCache();
// services.AddSingleton<ICacheService, MemoryCacheService>();
