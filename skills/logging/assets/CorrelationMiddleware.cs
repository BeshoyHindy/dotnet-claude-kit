// Api/Middleware/CorrelationIdMiddleware.cs
namespace YourNamespace.Api.Middleware;

using Microsoft.Extensions.Logging;

/// <summary>
/// Middleware that ensures each request has a correlation ID for distributed tracing.
/// Reads from incoming header or generates a new one.
/// </summary>
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var correlationId = GetOrCreateCorrelationId(context);

        // Store in Items for access by other components
        context.Items["CorrelationId"] = correlationId;

        // Add to response headers
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        // Create logging scope that includes correlation ID in all log entries
        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await next(context);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var existingId)
            && !string.IsNullOrWhiteSpace(existingId))
        {
            return existingId.ToString();
        }

        return Guid.NewGuid().ToString("N");
    }
}

/// <summary>
/// Extension methods for correlation ID middleware registration.
/// </summary>
public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}

/// <summary>
/// Service for accessing correlation ID in handlers and services.
/// </summary>
public interface ICorrelationIdAccessor
{
    string? CorrelationId { get; }
}

/// <summary>
/// HTTP context-based implementation of correlation ID accessor.
/// </summary>
public sealed class HttpContextCorrelationIdAccessor(
    IHttpContextAccessor httpContextAccessor) : ICorrelationIdAccessor
{
    public string? CorrelationId =>
        httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString();
}

// Registration in Program.cs:
// builder.Services.AddHttpContextAccessor();
// builder.Services.AddScoped<ICorrelationIdAccessor, HttpContextCorrelationIdAccessor>();
// app.UseCorrelationId(); // Add early in pipeline
