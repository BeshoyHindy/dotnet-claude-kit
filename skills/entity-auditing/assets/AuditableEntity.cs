// Domain/Common/IAuditableEntity.cs
namespace YourNamespace.Domain.Common;

/// <summary>
/// Contract for entities that track creation and modification audit information.
/// </summary>
public interface IAuditableEntity
{
    DateTimeOffset CreatedOn { get; }
    string? CreatedBy { get; }
    DateTimeOffset? UpdatedOn { get; }
    string? UpdatedBy { get; }
}

/// <summary>
/// Contract for entities that only track creation audit information.
/// </summary>
public interface ICreationAuditableEntity
{
    DateTimeOffset CreatedOn { get; }
    string? CreatedBy { get; }
}

// Domain/Common/AuditableEntity.cs
namespace YourNamespace.Domain.Common;

/// <summary>
/// Base class for entities with full audit tracking.
/// Audit fields are set automatically by AuditableEntityInterceptor.
/// </summary>
public abstract class AuditableEntity : Entity, IAuditableEntity
{
    public DateTimeOffset CreatedOn { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? UpdatedOn { get; private set; }
    public string? UpdatedBy { get; private set; }
}

// Alternative using init setters (simpler but less encapsulated)
// public abstract class AuditableEntity : Entity, IAuditableEntity
// {
//     public DateTimeOffset CreatedOn { get; init; }
//     public string? CreatedBy { get; init; }
//     public DateTimeOffset? UpdatedOn { get; set; }
//     public string? UpdatedBy { get; set; }
// }

// Application/Common/Interfaces/ICurrentUserService.cs
namespace YourNamespace.Application.Common.Interfaces;

/// <summary>
/// Provides access to current user information for audit purposes.
/// </summary>
public interface ICurrentUserService
{
    /// <summary>
    /// Unique identifier of the current user (typically from JWT 'sub' claim).
    /// </summary>
    string? UserId { get; }

    /// <summary>
    /// Display name of the current user.
    /// </summary>
    string? UserName { get; }

    /// <summary>
    /// Whether the current request is authenticated.
    /// </summary>
    bool IsAuthenticated { get; }
}

// Infrastructure/Services/CurrentUserService.cs
namespace YourNamespace.Infrastructure.Services;

using Microsoft.AspNetCore.Http;
using YourNamespace.Application.Common.Interfaces;

/// <summary>
/// HTTP context-based implementation of current user service.
/// </summary>
public sealed class CurrentUserService(
    IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public string? UserId =>
        _httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value
        ?? _httpContextAccessor.HttpContext?.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

    public string? UserName =>
        _httpContextAccessor.HttpContext?.User.FindFirst("name")?.Value
        ?? _httpContextAccessor.HttpContext?.User.Identity?.Name;

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated ?? false;
}

// Example entity using audit
namespace YourNamespace.Domain.Orders;

using YourNamespace.Domain.Common;

public sealed class Order : AuditableEntity
{
    public string OrderNumber { get; private set; } = string.Empty;
    public OrderStatus Status { get; private set; }

    private Order() { }

    public static Result<Order> Create(string orderNumber)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
            return Error.Validation("Order number is required");

        return new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = orderNumber,
            Status = OrderStatus.Draft
        };
    }
}
