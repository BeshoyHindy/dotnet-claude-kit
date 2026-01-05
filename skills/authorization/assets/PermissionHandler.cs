// Domain/Users/Permissions.cs
namespace YourNamespace.Domain.Users;

/// <summary>
/// Permission constants organized by resource.
/// </summary>
public static class Permissions
{
    public static class Orders
    {
        public const string View = "orders:view";
        public const string Create = "orders:create";
        public const string Update = "orders:update";
        public const string Delete = "orders:delete";
        public const string Manage = "orders:manage";
    }

    public static class Users
    {
        public const string View = "users:view";
        public const string Create = "users:create";
        public const string Update = "users:update";
        public const string Delete = "users:delete";
        public const string Manage = "users:manage";
    }

    public static class Reports
    {
        public const string View = "reports:view";
        public const string Export = "reports:export";
    }
}

// Infrastructure/Authorization/Requirements/PermissionRequirement.cs
namespace YourNamespace.Infrastructure.Authorization.Requirements;

using Microsoft.AspNetCore.Authorization;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

// Infrastructure/Authorization/Handlers/PermissionHandler.cs
namespace YourNamespace.Infrastructure.Authorization.Handlers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using YourNamespace.Application.Common.Interfaces;
using YourNamespace.Infrastructure.Authorization.Requirements;

public sealed class PermissionHandler(IDbContext db)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userId = context.User.FindFirst("sub")?.Value;
        if (userId is null)
            return;

        // Check if permission is in claims (added by ClaimsTransformation)
        if (context.User.HasClaim("permission", requirement.Permission))
        {
            context.Succeed(requirement);
            return;
        }

        // Fallback: Check database
        if (!Guid.TryParse(userId, out var userGuid))
            return;

        // Note: AuthorizationHandler doesn't provide CancellationToken.
        // For long-running checks, consider caching permissions at login.
        var hasPermission = await db.Users
            .Where(u => u.Id == userGuid)
            .SelectMany(u => u.Roles)
            .SelectMany(r => r.Permissions)
            .AnyAsync(p => p.Name == requirement.Permission);

        if (hasPermission)
            context.Succeed(requirement);
    }
}

// Infrastructure/Authorization/HasPermissionAttribute.cs
namespace YourNamespace.Infrastructure.Authorization;

using Microsoft.AspNetCore.Authorization;

/// <summary>
/// Shorthand attribute for permission-based authorization.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
        : base(policy: permission)
    {
    }
}

// Infrastructure/Authorization/PermissionPolicyProvider.cs
namespace YourNamespace.Infrastructure.Authorization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using YourNamespace.Infrastructure.Authorization.Requirements;

/// <summary>
/// Dynamically creates authorization policies for permission strings.
/// </summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        // Check for existing policy first
        var policy = await base.GetPolicyAsync(policyName);
        if (policy is not null)
            return policy;

        // Create permission policy on demand if it looks like a permission
        if (policyName.Contains(':'))
        {
            return new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();
        }

        return null;
    }
}

// Registration in DependencyInjection.cs:
//
// services.AddScoped<IAuthorizationHandler, PermissionHandler>();
// services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
//
// Usage:
// [HasPermission(Permissions.Orders.Delete)]
// [HttpDelete("{id}")]
// public async Task<IActionResult> Delete(Guid id) { }
