// Copy to: src/Api/Configuration/RateLimitConfiguration.cs
// Requires: .NET 7+ built-in rate limiting (Microsoft.AspNetCore.RateLimiting)
// Api/Configuration/RateLimitConfiguration.cs
namespace YourNamespace.Api.Configuration;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

/// <summary>
/// Policy name constants for rate limiting.
/// Use these constants when applying rate limiting to endpoints.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>General API limit (100 requests/minute per IP)</summary>
    public const string Api = "api";

    /// <summary>Strict limit for sensitive operations (5 requests/15 minutes)</summary>
    public const string Strict = "strict";

    /// <summary>Per-user limit for authenticated endpoints (1000 tokens with 100/minute replenishment)</summary>
    public const string PerUser = "per-user";

    /// <summary>High-volume endpoints like search/list (500 tokens with 50/10 seconds replenishment)</summary>
    public const string HighVolume = "high-volume";
}

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
            options.AddPolicy(RateLimitPolicies.Api, CreateApiPolicy());

            // Strict limit for sensitive operations (login, registration)
            options.AddPolicy(RateLimitPolicies.Strict, CreateStrictPolicy());

            // Per-user limit for authenticated endpoints
            options.AddPolicy(RateLimitPolicies.PerUser, CreatePerUserPolicy());

            // High-volume endpoints (search, list)
            options.AddPolicy(RateLimitPolicies.HighVolume, CreateHighVolumePolicy());
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
            Type = "https://tools.ietf.org/html/rfc6585#section-4",
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
// [EnableRateLimiting(RateLimitPolicies.Api)]
// public class OrdersController : ControllerBase { }
//
// [EnableRateLimiting(RateLimitPolicies.Strict)]
// [HttpPost("login")]
// public Task<IActionResult> Login() { }
//
// Minimal API usage:
// app.MapGet("/orders", GetOrders).RequireRateLimiting(RateLimitPolicies.Api);
// app.MapPost("/auth/login", Login).RequireRateLimiting(RateLimitPolicies.Strict);
