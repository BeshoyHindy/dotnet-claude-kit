---
name: rate-limiting
description: API rate limiting with built-in .NET rate limiters. Fixed window, sliding window, token bucket algorithms. Use when implementing API throttling.
allowed-tools: Read, Write, Edit, Glob, Grep
---

# Rate Limiting

Patterns for implementing rate limiting in .NET 7+ applications.

**Source**: [Rate limiting middleware](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit)

## Setup

```bash
# Built-in since .NET 7 - no package needed
```

```csharp
// Program.cs
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Detail = "Rate limit exceeded. Please try again later.",
            Type = "https://httpstatuses.com/429"
        };

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter =
                ((int)retryAfter.TotalSeconds).ToString();
            problemDetails.Extensions["retryAfter"] = retryAfter.TotalSeconds;
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            problemDetails,
            cancellationToken);
    };
});

var app = builder.Build();
app.UseRateLimiter();
```

## Rate Limiter Algorithms

### Fixed Window

Limits requests in fixed time windows. Simple but can allow bursts at window boundaries.

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 10;
    });
});
```

### Sliding Window

Smoother limiting by dividing windows into segments.

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddSlidingWindowLimiter("sliding", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.SegmentsPerWindow = 6; // 10-second segments
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 10;
    });
});
```

### Token Bucket

Allows bursts while maintaining average rate. Good for APIs with variable traffic.

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddTokenBucketLimiter("token", limiterOptions =>
    {
        limiterOptions.TokenLimit = 100;
        limiterOptions.ReplenishmentPeriod = TimeSpan.FromSeconds(10);
        limiterOptions.TokensPerPeriod = 10;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 10;
        limiterOptions.AutoReplenishment = true;
    });
});
```

### Concurrency Limiter

Limits concurrent requests, not rate.

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddConcurrencyLimiter("concurrent", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        limiterOptions.QueueLimit = 5;
    });
});
```

## Per-Client Rate Limiting

### By IP Address

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("per-ip", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1)
            }));
});
```

### By User ID

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("per-user", httpContext =>
    {
        var userId = httpContext.User.FindFirst("sub")?.Value;

        return userId is not null
            ? RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: userId,
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 1000,
                    Window = TimeSpan.FromMinutes(1)
                })
            : RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: "anonymous",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1)
                });
    });
});
```

### By API Key

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("per-api-key", httpContext =>
    {
        var apiKey = httpContext.Request.Headers["X-API-Key"].FirstOrDefault();

        return RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: apiKey ?? "no-key",
            factory: key => GetLimiterOptionsForKey(key));
    });
});

FixedWindowRateLimiterOptions GetLimiterOptionsForKey(string apiKey)
{
    // Could look up tier from database/cache
    return apiKey.StartsWith("premium_")
        ? new FixedWindowRateLimiterOptions { PermitLimit = 10000, Window = TimeSpan.FromMinutes(1) }
        : new FixedWindowRateLimiterOptions { PermitLimit = 100, Window = TimeSpan.FromMinutes(1) };
}
```

## Applying Rate Limits

### With Controllers

```csharp
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting("per-user")]
public class OrdersController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() { /* ... */ }

    [HttpPost]
    [EnableRateLimiting("strict")] // Override with stricter limit
    public async Task<IActionResult> Create(CreateOrderRequest request) { /* ... */ }

    [HttpGet("export")]
    [DisableRateLimiting] // Exclude from rate limiting
    public async Task<IActionResult> Export() { /* ... */ }
}
```

### With Minimal APIs

```csharp
var orders = app.MapGroup("/orders")
    .RequireRateLimiting("per-user");

orders.MapGet("/", GetAllOrders);
orders.MapPost("/", CreateOrder).RequireRateLimiting("strict");
orders.MapGet("/export", ExportOrders).DisableRateLimiting();
```

### Global Rate Limiting

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        var clientId = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: clientId,
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 1000,
                Window = TimeSpan.FromMinutes(1)
            });
    });
});
```

## Multiple Policies

```csharp
builder.Services.AddRateLimiter(options =>
{
    // General API limit
    options.AddFixedWindowLimiter("api", opt =>
    {
        opt.PermitLimit = 100;
        opt.Window = TimeSpan.FromMinutes(1);
    });

    // Strict limit for sensitive operations
    options.AddFixedWindowLimiter("strict", opt =>
    {
        opt.PermitLimit = 10;
        opt.Window = TimeSpan.FromMinutes(1);
    });

    // Authentication endpoints
    options.AddSlidingWindowLimiter("auth", opt =>
    {
        opt.PermitLimit = 5;
        opt.Window = TimeSpan.FromMinutes(15);
        opt.SegmentsPerWindow = 3;
    });
});
```

## Response Headers

Standard headers to inform clients:

```csharp
app.Use(async (context, next) =>
{
    await next();

    // Add rate limit headers (consider doing this in a middleware)
    // These would typically come from your rate limiter state
    context.Response.Headers["X-RateLimit-Limit"] = "100";
    context.Response.Headers["X-RateLimit-Remaining"] = "95";
    context.Response.Headers["X-RateLimit-Reset"] = DateTimeOffset.UtcNow
        .AddMinutes(1).ToUnixTimeSeconds().ToString();
});
```

## Configuration from appsettings

```json
{
  "RateLimiting": {
    "Api": {
      "PermitLimit": 100,
      "WindowMinutes": 1
    },
    "Strict": {
      "PermitLimit": 10,
      "WindowMinutes": 1
    }
  }
}
```

```csharp
var rateLimitConfig = builder.Configuration.GetSection("RateLimiting");

builder.Services.AddRateLimiter(options =>
{
    var apiConfig = rateLimitConfig.GetSection("Api");
    options.AddFixedWindowLimiter("api", opt =>
    {
        opt.PermitLimit = apiConfig.GetValue<int>("PermitLimit");
        opt.Window = TimeSpan.FromMinutes(apiConfig.GetValue<int>("WindowMinutes"));
    });
});
```

## Best Practices

| Practice | Recommendation |
|----------|----------------|
| Algorithm choice | Token bucket for variable traffic, sliding window for smooth limiting |
| Partition key | Use user ID for authenticated, IP for anonymous |
| Response | Return 429 with Retry-After header and ProblemDetails |
| Headers | Include X-RateLimit-* headers |
| Queuing | Keep queue limits low to prevent memory issues |
| Tiers | Different limits for different API key tiers |
| Monitoring | Log rate limit events for analysis |

## Assets

- [assets/RateLimitConfiguration.cs](assets/RateLimitConfiguration.cs) - Complete configuration

## Related

- `exception-handling` - Handling 429 responses
- `api-design` - API best practices
