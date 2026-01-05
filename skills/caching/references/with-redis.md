# Caching with Redis

Advanced Redis patterns for distributed caching in .NET applications.

**Source**: [Redis .NET Best Practices](https://redis.io/docs/latest/develop/clients/dotnet/)

## Installation

```bash
dotnet add package Microsoft.Extensions.Caching.StackExchangeRedis
dotnet add package StackExchange.Redis  # For advanced operations
```

## Configuration

### Basic Setup

```csharp
// Program.cs
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "YourApp:";
});

// appsettings.json
{
  "ConnectionStrings": {
    "Redis": "localhost:6379,abortConnect=false,connectTimeout=5000"
  }
}
```

### Production Configuration

```csharp
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.ConfigurationOptions = new ConfigurationOptions
    {
        EndPoints = { { "redis-cluster.example.com", 6379 } },
        Password = builder.Configuration["Redis:Password"],
        Ssl = true,
        AbortOnConnectFail = false,
        ConnectTimeout = 5000,
        SyncTimeout = 5000,
        AsyncTimeout = 5000,
        ReconnectRetryPolicy = new ExponentialRetry(5000),
        DefaultDatabase = 0
    };
    options.InstanceName = $"{builder.Environment.EnvironmentName}:YourApp:";
});
```

## Advanced Redis Operations

### Direct Connection (for complex operations)

```csharp
// Register ConnectionMultiplexer for direct Redis access
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = ConfigurationOptions.Parse(
        builder.Configuration.GetConnectionString("Redis")!);
    return ConnectionMultiplexer.Connect(config);
});

// Usage
public sealed class RedisService(IConnectionMultiplexer redis)
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task<bool> SetWithConditionAsync(
        string key,
        string value,
        TimeSpan expiry,
        When when = When.Always)
    {
        return await _db.StringSetAsync(key, value, expiry, when);
    }

    public async Task<long> IncrementAsync(string key)
    {
        return await _db.StringIncrementAsync(key);
    }

    public async Task<bool> HashSetAsync(string key, string field, string value)
    {
        return await _db.HashSetAsync(key, field, value);
    }
}
```

### Pub/Sub for Cache Invalidation

```csharp
public sealed class RedisCacheInvalidator(IConnectionMultiplexer redis)
{
    private const string InvalidationChannel = "cache:invalidation";

    public async Task PublishInvalidationAsync(string pattern)
    {
        var subscriber = redis.GetSubscriber();
        await subscriber.PublishAsync(
            RedisChannel.Literal(InvalidationChannel),
            pattern);
    }

    public void SubscribeToInvalidation(Action<string> onInvalidate)
    {
        var subscriber = redis.GetSubscriber();
        subscriber.Subscribe(
            RedisChannel.Literal(InvalidationChannel),
            (_, message) => onInvalidate(message!));
    }
}
```

### Distributed Locking

```csharp
public sealed class RedisDistributedLock(IConnectionMultiplexer redis)
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task<IAsyncDisposable?> TryAcquireLockAsync(
        string key,
        TimeSpan expiry,
        CancellationToken ct = default)
    {
        var lockId = Guid.NewGuid().ToString();
        var lockKey = $"lock:{key}";

        var acquired = await _db.StringSetAsync(
            lockKey,
            lockId,
            expiry,
            When.NotExists);

        return acquired ? new LockHandle(_db, lockKey, lockId) : null;
    }

    private sealed class LockHandle(IDatabase db, string key, string id)
        : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            // Only release if we still own the lock
            var script = """
                if redis.call("get", KEYS[1]) == ARGV[1] then
                    return redis.call("del", KEYS[1])
                else
                    return 0
                end
                """;

            await db.ScriptEvaluateAsync(script, [key], [id]);
        }
    }
}
```

## Redis Data Structures

### Sorted Sets for Leaderboards/Rankings

```csharp
public sealed class LeaderboardService(IConnectionMultiplexer redis)
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task UpdateScoreAsync(string leaderboard, string userId, double score)
    {
        await _db.SortedSetAddAsync($"leaderboard:{leaderboard}", userId, score);
    }

    public async Task<IEnumerable<(string UserId, double Score)>> GetTopAsync(
        string leaderboard,
        int count)
    {
        var entries = await _db.SortedSetRangeByRankWithScoresAsync(
            $"leaderboard:{leaderboard}",
            0,
            count - 1,
            Order.Descending);

        return entries.Select(e => (e.Element.ToString(), e.Score));
    }

    public async Task<long?> GetRankAsync(string leaderboard, string userId)
    {
        return await _db.SortedSetRankAsync(
            $"leaderboard:{leaderboard}",
            userId,
            Order.Descending);
    }
}
```

### Sets for Tags/Relationships

```csharp
public sealed class TagService(IConnectionMultiplexer redis)
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task AddTagsAsync(string entityKey, params string[] tags)
    {
        var key = $"tags:{entityKey}";
        var values = tags.Select(t => (RedisValue)t).ToArray();
        await _db.SetAddAsync(key, values);
    }

    public async Task<IEnumerable<string>> GetTagsAsync(string entityKey)
    {
        var members = await _db.SetMembersAsync($"tags:{entityKey}");
        return members.Select(m => m.ToString());
    }

    public async Task<IEnumerable<string>> GetEntitiesByTagAsync(string tag)
    {
        var members = await _db.SetMembersAsync($"tag:{tag}:entities");
        return members.Select(m => m.ToString());
    }
}
```

## Session Storage

```csharp
// Program.cs - Redis session storage
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "Session:";
});

// Enable distributed session
app.UseSession();
```

## Health Checks

```csharp
builder.Services.AddHealthChecks()
    .AddRedis(
        builder.Configuration.GetConnectionString("Redis")!,
        name: "redis",
        failureStatus: HealthStatus.Degraded,
        tags: ["db", "cache"]);
```

## Rate Limiting with Redis

```csharp
public sealed class RedisRateLimiter(IConnectionMultiplexer redis, TimeProvider timeProvider)
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task<bool> IsAllowedAsync(
        string key,
        int maxRequests,
        TimeSpan window)
    {
        var now = timeProvider.GetUtcNow().ToUnixTimeSeconds();
        var windowStart = now - (long)window.TotalSeconds;

        var transaction = _db.CreateTransaction();

        // Remove old entries
        _ = transaction.SortedSetRemoveRangeByScoreAsync(
            key,
            double.NegativeInfinity,
            windowStart);

        // Count current entries
        var countTask = transaction.SortedSetLengthAsync(key);

        // Add new entry
        _ = transaction.SortedSetAddAsync(key, now.ToString(), now);

        // Set expiry
        _ = transaction.KeyExpireAsync(key, window);

        await transaction.ExecuteAsync();

        var count = await countTask;
        return count <= maxRequests;
    }
}
```

## Best Practices

| Practice | Recommendation |
|----------|----------------|
| Connection pooling | Use single ConnectionMultiplexer instance |
| Key naming | Use colons as separators: `app:entity:id` |
| Serialization | Consider MessagePack for performance |
| Memory | Set maxmemory and eviction policy |
| Persistence | Configure RDB/AOF based on needs |
| Clustering | Use Redis Cluster for high availability |

## Monitoring

```csharp
// Log connection events
var multiplexer = ConnectionMultiplexer.Connect(config);

multiplexer.ConnectionFailed += (sender, e) =>
    logger.LogError(e.Exception, "Redis connection failed: {Reason}", e.FailureType);

multiplexer.ConnectionRestored += (sender, e) =>
    logger.LogInformation("Redis connection restored");

multiplexer.ErrorMessage += (sender, e) =>
    logger.LogWarning("Redis error: {Message}", e.Message);
```

## Related

- `caching` - Core caching patterns
- `rate-limiting` - Rate limiting patterns
