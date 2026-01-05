// Application/Common/Logging/LogMessages.cs
namespace YourNamespace.Application.Common.Logging;

using Microsoft.Extensions.Logging;

/// <summary>
/// Source-generated log messages for high-performance logging.
/// Group by feature/domain area in production.
/// </summary>
public static partial class LogMessages
{
    // Order Events (1000-1099)
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
        Level = LogLevel.Information,
        Message = "Order {OrderId} submitted with {ItemCount} items, total {Total}")]
    public static partial void OrderSubmitted(
        this ILogger logger,
        Guid orderId,
        int itemCount,
        decimal total);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Warning,
        Message = "Order {OrderId} processing delayed, attempt {Attempt} of {MaxAttempts}")]
    public static partial void OrderProcessingDelayed(
        this ILogger logger,
        Guid orderId,
        int attempt,
        int maxAttempts);

    [LoggerMessage(
        EventId = 1003,
        Level = LogLevel.Error,
        Message = "Failed to process order {OrderId}")]
    public static partial void OrderProcessingFailed(
        this ILogger logger,
        Exception exception,
        Guid orderId);

    [LoggerMessage(
        EventId = 1004,
        Level = LogLevel.Information,
        Message = "Order {OrderId} cancelled by {UserId}, reason: {Reason}")]
    public static partial void OrderCancelled(
        this ILogger logger,
        Guid orderId,
        Guid userId,
        string reason);

    // Customer Events (1100-1199)
    [LoggerMessage(
        EventId = 1100,
        Level = LogLevel.Information,
        Message = "Customer {CustomerId} registered with email {Email}")]
    public static partial void CustomerRegistered(
        this ILogger logger,
        Guid customerId,
        string email);

    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Warning,
        Message = "Customer {CustomerId} login failed, attempt {Attempt}")]
    public static partial void CustomerLoginFailed(
        this ILogger logger,
        Guid customerId,
        int attempt);

    // Payment Events (1200-1299)
    [LoggerMessage(
        EventId = 1200,
        Level = LogLevel.Information,
        Message = "Payment {PaymentId} initiated for order {OrderId}, amount {Amount}")]
    public static partial void PaymentInitiated(
        this ILogger logger,
        Guid paymentId,
        Guid orderId,
        decimal amount);

    [LoggerMessage(
        EventId = 1201,
        Level = LogLevel.Information,
        Message = "Payment {PaymentId} completed successfully")]
    public static partial void PaymentCompleted(
        this ILogger logger,
        Guid paymentId);

    [LoggerMessage(
        EventId = 1202,
        Level = LogLevel.Error,
        Message = "Payment {PaymentId} failed for order {OrderId}")]
    public static partial void PaymentFailed(
        this ILogger logger,
        Exception exception,
        Guid paymentId,
        Guid orderId);

    // Infrastructure Events (2000-2099)
    [LoggerMessage(
        EventId = 2000,
        Level = LogLevel.Debug,
        Message = "Database query executed in {ElapsedMs}ms")]
    public static partial void DatabaseQueryExecuted(
        this ILogger logger,
        long elapsedMs);

    [LoggerMessage(
        EventId = 2001,
        Level = LogLevel.Warning,
        Message = "Slow database query detected: {ElapsedMs}ms exceeds threshold {ThresholdMs}ms")]
    public static partial void SlowQueryDetected(
        this ILogger logger,
        long elapsedMs,
        long thresholdMs);

    [LoggerMessage(
        EventId = 2002,
        Level = LogLevel.Error,
        Message = "External service {ServiceName} call failed")]
    public static partial void ExternalServiceFailed(
        this ILogger logger,
        Exception exception,
        string serviceName);
}
