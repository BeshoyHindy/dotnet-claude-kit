// Api/Configuration/RateLimitConfiguration.cs
namespace YourNamespace.Api.Configuration;

using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

public static class RateLimitConfiguration
{
    public static IServiceCollection AddRateLimitingPolicies(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = HandleRejectedRequest;

            // General API limit per IP
            options.AddPolicy("api", CreateApiPolicy());

            // Strict limit for sensitive operations (login, registration)
            options.AddPolicy("strict", CreateStrictPolicy());

            // Per-user limit for authenticated endpoints
            options.AddPolicy("per-user", CreatePerUserPolicy());

            // High-volume endpoints (search, list)
            options.AddPolicy("high-volume", CreateHighVolumePolicy());
        });

        return services;
    }

    private static Func<HttpContext, RateLimitPartition<string>> CreateApiPolicy()
    {
        return context => RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: GetClientIdentifier(context),
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            });
    }

    private static Func<HttpContext, RateLimitPartition<string>> CreateStrictPolicy()
    {
        return context => RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: GetClientIdentifier(context),
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                SegmentsPerWindow = 3,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 2
            });
    }

    private static Func<HttpContext, RateLimitPartition<string>> CreatePerUserPolicy()
    {
        return context =>
        {
            var userId = context.User.FindFirst("sub")?.Value;

            if (userId is not null)
            {
                return RateLimitPartition.GetTokenBucketLimiter(
                    partitionKey: $"user:{userId}",
                    factory: _ => new TokenBucketRateLimiterOptions
                    {
                        TokenLimit = 1000,
                        ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                        TokensPerPeriod = 100,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 20
                    });
            }

            // Stricter limits for unauthenticated requests
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"anon:{GetClientIdentifier(context)}",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 50,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 5
                });
        };
    }

    private static Func<HttpContext, RateLimitPartition<string>> CreateHighVolumePolicy()
    {
        return context => RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: GetClientIdentifier(context),
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 500,
                ReplenishmentPeriod = TimeSpan.FromSeconds(10),
                TokensPerPeriod = 50,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 50
            });
    }

    private static string GetClientIdentifier(HttpContext context)
    {
        // Try API key first
        if (context.Request.Headers.TryGetValue("X-API-Key", out var apiKey)
            && !string.IsNullOrEmpty(apiKey))
        {
            return $"key:{apiKey}";
        }

        // Fall back to IP address
        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static async ValueTask HandleRejectedRequest(
        OnRejectedContext context,
        CancellationToken cancellationToken)
    {
        context.HttpContext.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Detail = "Rate limit exceeded. Please try again later.",
            Type = "https://httpstatuses.com/429",
            Instance = context.HttpContext.Request.Path
        };

        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            var retryAfterSeconds = (int)retryAfter.TotalSeconds;
            context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
            problemDetails.Extensions["retryAfter"] = retryAfterSeconds;
        }

        await context.HttpContext.Response.WriteAsJsonAsync(
            problemDetails,
            cancellationToken);
    }
}

// Usage in Program.cs:
// builder.Services.AddRateLimitingPolicies(builder.Configuration);
// app.UseRateLimiter();
//
// Controller usage:
// [EnableRateLimiting("api")]
// public class OrdersController : ControllerBase { }
//
// Minimal API usage:
// app.MapGet("/orders", GetOrders).RequireRateLimiting("api");
