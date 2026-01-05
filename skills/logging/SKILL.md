---
name: logging
description: Structured logging patterns with ILogger, Serilog integration, correlation IDs. Use when implementing logging, diagnostics, or observability.
allowed-tools: Read, Write, Edit, Glob, Grep
---

# Structured Logging Patterns

Best practices for logging in .NET applications using ILogger and structured logging.

**Source**: [.NET Logging](https://learn.microsoft.com/en-us/dotnet/core/extensions/logging)

## ILogger Basics

### Injection

```csharp
public sealed class OrderService(ILogger<OrderService> logger)
{
    public async Task<Result<Order>> CreateOrderAsync(CreateOrderRequest request)
    {
        logger.LogInformation(
            "Creating order for customer {CustomerId}",
            request.CustomerId);

        // ... business logic

        logger.LogInformation(
            "Order {OrderId} created successfully",
            order.Id);

        return order;
    }
}
```

### Log Levels

Use appropriate levels:

| Level | When to Use |
|-------|-------------|
| `Trace` | Detailed debugging (disabled in production) |
| `Debug` | Development diagnostics |
| `Information` | Normal flow, business events |
| `Warning` | Unexpected but handled situations |
| `Error` | Failures that need attention |
| `Critical` | System failures requiring immediate action |

```csharp
logger.LogTrace("Entered method with {Count} items", items.Count);
logger.LogDebug("Processing batch {BatchId}", batchId);
logger.LogInformation("Order {OrderId} submitted", orderId);
logger.LogWarning("Retry {Attempt} for {Operation}", attempt, operation);
logger.LogError(exception, "Failed to process order {OrderId}", orderId);
logger.LogCritical("Database connection lost");
```

## Structured Logging

### Message Templates

Use semantic placeholders, not string interpolation:

```csharp
// CORRECT - Structured logging
logger.LogInformation("Order {OrderId} created for {CustomerId}", orderId, customerId);

// WRONG - String interpolation loses structure
logger.LogInformation($"Order {orderId} created for {customerId}");
```

### Naming Conventions

Use PascalCase for property names:

```csharp
// Good
logger.LogInformation("Processing {OrderId} with {ItemCount} items", orderId, count);

// Avoid
logger.LogInformation("Processing {order_id} with {item_count} items", orderId, count);
```

## High-Performance Logging

### Source Generators

For hot paths, use compile-time generated methods:

```csharp
public static partial class LogMessages
{
    [LoggerMessage(
        EventId = 1000,
        Level = LogLevel.Information,
        Message = "Order {OrderId} created for customer {CustomerId}")]
    public static partial void OrderCreated(
        this ILogger logger,
        Guid orderId,
        Guid customerId);

    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Warning,
        Message = "Order {OrderId} processing delayed, attempt {Attempt}")]
    public static partial void OrderDelayed(
        this ILogger logger,
        Guid orderId,
        int attempt);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Error,
        Message = "Failed to process order {OrderId}")]
    public static partial void OrderFailed(
        this ILogger logger,
        Exception exception,
        Guid orderId);
}

// Usage
logger.OrderCreated(order.Id, order.CustomerId);
logger.OrderFailed(ex, orderId);
```

## Correlation and Scopes

### Log Scopes

Group related log entries:

```csharp
using (logger.BeginScope(new Dictionary<string, object>
{
    ["OrderId"] = orderId,
    ["CustomerId"] = customerId
}))
{
    logger.LogInformation("Starting order processing");
    // All logs within scope include OrderId and CustomerId
    await ProcessItemsAsync(order.Items);
    logger.LogInformation("Order processing completed");
}
```

### Correlation ID Middleware

Track requests across services:

```csharp
public sealed class CorrelationIdMiddleware(RequestDelegate next)
{
    private const string CorrelationIdHeader = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var correlationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault()
            ?? Guid.NewGuid().ToString();

        context.Items["CorrelationId"] = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }))
        {
            await next(context);
        }
    }
}
```

## Serilog Integration

### Setup

```csharp
// Program.cs
using Serilog;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .CreateLogger();

builder.Host.UseSerilog();

// Ensure proper cleanup
try
{
    var app = builder.Build();
    // ... configure app
    app.Run();
}
finally
{
    Log.CloseAndFlush();
}
```

### Configuration

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/log-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName"]
  }
}
```

### Request Logging

```csharp
// Replaces default request logging with structured version
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("UserId", httpContext.User.FindFirst("sub")?.Value);
        diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress);
    };
});
```

## Sensitive Data

### Avoid Logging Sensitive Information

```csharp
// WRONG - Logs sensitive data
logger.LogInformation("User {Email} logged in with password {Password}", email, password);

// CORRECT - Omit sensitive fields
logger.LogInformation("User {UserId} logged in", userId);

// For debugging, log masked data
logger.LogDebug("Processing card ending in {Last4}", cardNumber[^4..]);
```

### Destructuring Control

With Serilog, control how objects are logged:

```csharp
// Log specific properties only
logger.LogInformation("Order received: {@Order}", new
{
    order.Id,
    order.OrderNumber,
    order.Status
    // Omit sensitive fields like PaymentDetails
});
```

## Exception Logging

### Correct Pattern

```csharp
try
{
    await ProcessOrderAsync(order);
}
catch (Exception ex)
{
    // Pass exception as first parameter
    logger.LogError(ex, "Failed to process order {OrderId}", order.Id);
    throw;
}
```

### Avoid

```csharp
// WRONG - Exception message as template
logger.LogError("Failed: " + ex.Message);

// WRONG - Exception in template
logger.LogError("Failed to process order {OrderId}: {Error}", orderId, ex.Message);
```

## Endpoint Integration

### With Controllers

```csharp
[ApiController]
[Route("api/[controller]")]
public class OrdersController(
    ILogger<OrdersController> logger,
    ICommandHandler<CreateOrderCommand, Guid> handler) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        CreateOrderRequest request,
        CancellationToken ct)
    {
        logger.LogInformation(
            "Received create order request for customer {CustomerId}",
            request.CustomerId);

        var command = new CreateOrderCommand(request.CustomerId, request.Items);
        var result = await handler.HandleAsync(command, ct);

        if (result.IsFailure)
        {
            logger.LogWarning(
                "Order creation failed: {Error}",
                result.Error.Message);
            return BadRequest(result.Error);
        }

        logger.LogInformation("Order {OrderId} created", result.Value);
        return CreatedAtAction(nameof(Get), new { id = result.Value }, result.Value);
    }
}
```

### With Minimal APIs

```csharp
app.MapPost("/orders", async (
    CreateOrderRequest request,
    ILogger<Program> logger,
    ICommandHandler<CreateOrderCommand, Guid> handler,
    CancellationToken ct) =>
{
    logger.LogInformation(
        "Received create order request for customer {CustomerId}",
        request.CustomerId);

    var result = await handler.HandleAsync(
        new CreateOrderCommand(request.CustomerId, request.Items),
        ct);

    return result.ToHttpResult(id => Results.Created($"/orders/{id}", id));
});
```

## Best Practices

| Practice | Recommendation |
|----------|----------------|
| Use structured logging | Message templates with placeholders, not interpolation |
| Log at boundaries | Entry/exit of handlers, API endpoints |
| Include context | Order IDs, user IDs, correlation IDs |
| Avoid logging in loops | Log summary before/after instead |
| Use appropriate levels | Information for business events, Debug for diagnostics |
| Never log secrets | Passwords, tokens, connection strings |
| Use scopes | Group related operations |

## Assets

- [assets/LogMessages.cs](assets/LogMessages.cs) - Source-generated log messages
- [assets/CorrelationMiddleware.cs](assets/CorrelationMiddleware.cs) - Correlation ID middleware

## Related

- `exception-handling` - Exception logging
- `cqrs` - Handler logging
