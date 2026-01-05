---
name: authorization
description: Role-based and policy-based authorization. Permissions, claims, custom requirements. Use when implementing access control.
allowed-tools: Read, Write, Edit, Glob, Grep
---

# Authorization

Patterns for role-based and policy-based authorization in .NET APIs.

**Source**: [ASP.NET Core Authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/introduction)

## Role-Based Authorization

### Simple Roles

```csharp
// Controllers
[Authorize(Roles = "Admin")]
[HttpDelete("{id}")]
public async Task<IActionResult> Delete(Guid id) { /* ... */ }

[Authorize(Roles = "Admin,Manager")] // Either role
[HttpPut("{id}")]
public async Task<IActionResult> Update(Guid id) { /* ... */ }

// Minimal APIs
app.MapDelete("/orders/{id}", DeleteOrder)
    .RequireAuthorization(policy => policy.RequireRole("Admin"));
```

### Role Constants

```csharp
// Domain/Users/Roles.cs
public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string User = "User";

    public static readonly IReadOnlyList<string> All = [Admin, Manager, User];
}

// Usage
[Authorize(Roles = Roles.Admin)]
```

## Policy-Based Authorization

### Define Policies

```csharp
// Program.cs
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireRole(Roles.Admin));

    options.AddPolicy("ManagerOrAbove", policy =>
        policy.RequireRole(Roles.Admin, Roles.Manager));

    options.AddPolicy("CanManageOrders", policy =>
        policy.RequireClaim("permission", "orders:manage"));

    options.AddPolicy("MinimumAge", policy =>
        policy.Requirements.Add(new MinimumAgeRequirement(18)));

    options.AddPolicy("ResourceOwner", policy =>
        policy.Requirements.Add(new ResourceOwnerRequirement()));
});
```

### Apply Policies

```csharp
// Controllers
[Authorize(Policy = "CanManageOrders")]
public class OrdersController : ControllerBase { }

// Minimal APIs
app.MapPost("/orders", CreateOrder)
    .RequireAuthorization("CanManageOrders");
```

## Custom Authorization Requirements

### Requirement Definition

```csharp
// Infrastructure/Authorization/Requirements/MinimumAgeRequirement.cs
public sealed class MinimumAgeRequirement(int minimumAge) : IAuthorizationRequirement
{
    public int MinimumAge { get; } = minimumAge;
}

// Infrastructure/Authorization/Handlers/MinimumAgeHandler.cs
public sealed class MinimumAgeHandler
    : AuthorizationHandler<MinimumAgeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        MinimumAgeRequirement requirement)
    {
        var birthDateClaim = context.User.FindFirst("birthdate");
        if (birthDateClaim is null)
            return Task.CompletedTask;

        if (!DateOnly.TryParse(birthDateClaim.Value, out var birthDate))
            return Task.CompletedTask;

        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - birthDate.Year;
        if (birthDate > today.AddYears(-age)) age--;

        if (age >= requirement.MinimumAge)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
```

### Resource-Based Authorization

```csharp
// Infrastructure/Authorization/Requirements/ResourceOwnerRequirement.cs
public sealed class ResourceOwnerRequirement : IAuthorizationRequirement { }

// Infrastructure/Authorization/Handlers/OrderOwnerHandler.cs
public sealed class OrderOwnerHandler
    : AuthorizationHandler<ResourceOwnerRequirement, Order>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ResourceOwnerRequirement requirement,
        Order resource)
    {
        var userId = context.User.FindFirst("sub")?.Value;

        if (userId is not null && resource.CustomerId.ToString() == userId)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

// Usage in handler
public sealed class UpdateOrderHandler(
    IDbContext db,
    IAuthorizationService authorizationService,
    IHttpContextAccessor httpContextAccessor)
    : ICommandHandler<UpdateOrderCommand>
{
    public async Task<Result> HandleAsync(
        UpdateOrderCommand command,
        CancellationToken ct)
    {
        var order = await db.Orders.FindAsync(command.OrderId, ct);
        if (order is null)
            return Error.NotFound("Order", command.OrderId);

        var user = httpContextAccessor.HttpContext!.User;
        var authResult = await authorizationService.AuthorizeAsync(
            user,
            order,
            "ResourceOwner");

        if (!authResult.Succeeded)
            return Error.Forbidden("You can only modify your own orders");

        // Update order...
        return Result.Success();
    }
}
```

## Permission-Based Authorization

### Permission Constants

```csharp
// Domain/Users/Permissions.cs
public static class Permissions
{
    public static class Orders
    {
        public const string View = "orders:view";
        public const string Create = "orders:create";
        public const string Update = "orders:update";
        public const string Delete = "orders:delete";
        public const string Manage = "orders:manage"; // All of the above
    }

    public static class Users
    {
        public const string View = "users:view";
        public const string Create = "users:create";
        public const string Update = "users:update";
        public const string Delete = "users:delete";
    }
}
```

### Permission Requirement

```csharp
// Infrastructure/Authorization/Requirements/PermissionRequirement.cs
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

// Infrastructure/Authorization/Handlers/PermissionHandler.cs
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

        // Check if user has permission via role
        var hasPermission = await db.Users
            .Where(u => u.Id == Guid.Parse(userId))
            .SelectMany(u => u.Roles)
            .SelectMany(r => r.Permissions)
            .AnyAsync(p => p.Name == requirement.Permission);

        if (hasPermission)
            context.Succeed(requirement);
    }
}
```

### Permission Attribute

```csharp
// Infrastructure/Authorization/HasPermissionAttribute.cs
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
        : base(policy: permission)
    {
    }
}

// Dynamic policy registration
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var policy = await base.GetPolicyAsync(policyName);

        if (policy is null && policyName.Contains(':'))
        {
            // Create permission policy on demand
            policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();
        }

        return policy;
    }
}

// Usage
[HasPermission(Permissions.Orders.Delete)]
[HttpDelete("{id}")]
public async Task<IActionResult> Delete(Guid id) { /* ... */ }
```

## Registration

```csharp
// Program.cs
builder.Services.AddAuthorization();

// Register handlers
builder.Services.AddScoped<IAuthorizationHandler, MinimumAgeHandler>();
builder.Services.AddScoped<IAuthorizationHandler, OrderOwnerHandler>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();

// Optional: Dynamic policy provider
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
```

## Claims Transformation

Add roles/permissions to claims on login:

```csharp
// Infrastructure/Authorization/ClaimsTransformation.cs
public sealed class AppClaimsTransformation(IDbContext db) : IClaimsTransformation
{
    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var userId = principal.FindFirst("sub")?.Value;
        if (userId is null)
            return principal;

        var permissions = await db.Users
            .Where(u => u.Id == Guid.Parse(userId))
            .SelectMany(u => u.Roles)
            .SelectMany(r => r.Permissions)
            .Select(p => p.Name)
            .Distinct()
            .ToListAsync();

        var identity = (ClaimsIdentity)principal.Identity!;
        foreach (var permission in permissions)
        {
            identity.AddClaim(new Claim("permission", permission));
        }

        return principal;
    }
}

// Register
builder.Services.AddScoped<IClaimsTransformation, AppClaimsTransformation>();
```

## Best Practices

| Practice | Recommendation |
|----------|----------------|
| Policies over roles | Use policies for complex rules |
| Centralize permissions | Define in constants, not strings |
| Resource authorization | Check ownership for user data |
| Fail secure | Deny by default |
| Audit | Log authorization failures |

## Authorization vs Authentication

| Aspect | Authentication | Authorization |
|--------|----------------|---------------|
| Question | Who are you? | What can you do? |
| Mechanism | JWT, cookies, etc. | Roles, policies, claims |
| Failure | 401 Unauthorized | 403 Forbidden |
| Timing | Before authorization | After authentication |

## Assets

- [assets/PermissionHandler.cs](assets/PermissionHandler.cs) - Permission-based auth
- [assets/ResourceOwnerHandler.cs](assets/ResourceOwnerHandler.cs) - Resource ownership

## Related

- `authentication` - JWT authentication
- `cqrs` - Authorization in handlers
