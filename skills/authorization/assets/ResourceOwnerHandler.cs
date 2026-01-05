// Infrastructure/Authorization/Requirements/ResourceOwnerRequirement.cs
namespace YourApp.Infrastructure.Authorization.Requirements;

using Microsoft.AspNetCore.Authorization;

/// <summary>
/// Requirement that checks if the current user owns the resource.
/// </summary>
public sealed class ResourceOwnerRequirement : IAuthorizationRequirement { }

// Domain/Common/IOwnedEntity.cs
namespace YourApp.Domain.Common;

/// <summary>
/// Marker interface for entities that have an owner.
/// </summary>
public interface IOwnedEntity
{
    Guid OwnerId { get; }
}

// Infrastructure/Authorization/Handlers/ResourceOwnerHandler.cs
namespace YourApp.Infrastructure.Authorization.Handlers;

using Microsoft.AspNetCore.Authorization;
using YourApp.Domain.Common;
using YourApp.Infrastructure.Authorization.Requirements;

/// <summary>
/// Generic handler for resource ownership authorization.
/// Works with any entity implementing IOwnedEntity.
/// </summary>
public sealed class ResourceOwnerHandler
    : AuthorizationHandler<ResourceOwnerRequirement, IOwnedEntity>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ResourceOwnerRequirement requirement,
        IOwnedEntity resource)
    {
        var userId = context.User.FindFirst("sub")?.Value;

        if (userId is not null && resource.OwnerId.ToString() == userId)
        {
            context.Succeed(requirement);
        }

        // Admins can access any resource
        if (context.User.IsInRole("Admin"))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

// Example entity implementing IOwnedEntity
namespace YourApp.Domain.Orders;

using YourApp.Domain.Common;

public sealed class Order : AuditableEntity, IOwnedEntity
{
    public string OrderNumber { get; private set; } = string.Empty;
    public Guid CustomerId { get; private set; }

    // Implement IOwnedEntity - customer owns the order
    public Guid OwnerId => CustomerId;

    // ... rest of entity
}

// Example usage in a command handler
namespace YourApp.Application.Orders.Commands.UpdateOrder;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using YourApp.Application.Common.Interfaces;
using YourApp.Domain.Common;

public sealed class UpdateOrderHandler(
    IDbContext db,
    IAuthorizationService authorizationService,
    IHttpContextAccessor httpContextAccessor) : ICommandHandler<UpdateOrderCommand>
{
    public async Task<Result> HandleAsync(
        UpdateOrderCommand command,
        CancellationToken ct)
    {
        var order = await db.Orders.FindAsync([command.OrderId], ct);
        if (order is null)
            return Error.NotFound("Order", command.OrderId);

        // Check ownership
        var user = httpContextAccessor.HttpContext!.User;
        var authResult = await authorizationService.AuthorizeAsync(
            user,
            (IOwnedEntity)order,
            "ResourceOwner");

        if (!authResult.Succeeded)
            return Error.Forbidden("You can only modify your own orders");

        // Proceed with update...
        await db.SaveChangesAsync(ct);

        return Result.Success();
    }
}

// Policy registration in Program.cs:
//
// builder.Services.AddAuthorization(options =>
// {
//     options.AddPolicy("ResourceOwner", policy =>
//         policy.Requirements.Add(new ResourceOwnerRequirement()));
// });
//
// builder.Services.AddScoped<IAuthorizationHandler, ResourceOwnerHandler>();
