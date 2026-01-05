---
name: authentication
description: JWT authentication, token generation/validation, refresh tokens. Use when implementing user authentication.
allowed-tools: Read, Write, Edit, Glob, Grep
---

# Authentication

JWT-based authentication patterns for .NET APIs.

**Source**: [ASP.NET Core Authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/)

## Setup

```bash
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
```

```csharp
// Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ClockSkew = TimeSpan.Zero // Remove default 5-minute tolerance
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
```

## Configuration

```json
{
  "Jwt": {
    "Key": "your-256-bit-secret-key-here-at-least-32-characters",
    "Issuer": "https://yourapi.com",
    "Audience": "https://yourapi.com",
    "AccessTokenExpirationMinutes": 15,
    "RefreshTokenExpirationDays": 7
  }
}
```

## Token Service

```csharp
// Application/Common/Interfaces/ITokenService.cs
public interface ITokenService
{
    TokenResponse GenerateTokens(User user, IEnumerable<string> roles);
    ClaimsPrincipal? ValidateToken(string token);
    string GenerateRefreshToken();
}

// Application/Common/Models/TokenResponse.cs
public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);
```

```csharp
// Infrastructure/Services/TokenService.cs
public sealed class TokenService(
    IOptions<JwtSettings> jwtSettings,
    TimeProvider timeProvider) : ITokenService
{
    private readonly JwtSettings _settings = jwtSettings.Value;

    public TokenResponse GenerateTokens(User user, IEnumerable<string> roles)
    {
        var now = timeProvider.GetUtcNow();
        var expires = now.AddMinutes(_settings.AccessTokenExpirationMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("name", user.Name)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        var refreshToken = GenerateRefreshToken();

        return new TokenResponse(accessToken, refreshToken, expires);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_settings.Key);

        try
        {
            var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _settings.Issuer,
                ValidateAudience = true,
                ValidAudience = _settings.Audience,
                ValidateLifetime = false // Allow expired tokens for refresh
            }, out _);

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
```

## Refresh Token Entity

```csharp
// Domain/Users/RefreshToken.cs
public sealed class RefreshToken : Entity
{
    public string Token { get; private set; } = string.Empty;
    public Guid UserId { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? ReplacedByToken { get; private set; }

    // Methods take current time for testability (no static DateTime calls)
    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
    public bool IsRevoked => RevokedAt is not null;
    public bool IsActive(DateTimeOffset now) => !IsRevoked && !IsExpired(now);

    private RefreshToken() { }

    public static RefreshToken Create(
        Guid userId,
        string token,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
    {
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            ExpiresAt = expiresAt,
            CreatedAt = createdAt
        };
    }

    public void Revoke(DateTimeOffset revokedAt, string? replacementToken = null)
    {
        RevokedAt = revokedAt;
        ReplacedByToken = replacementToken;
    }
}
```

## Login Handler

```csharp
// Application/Auth/Commands/Login/LoginCommand.cs
public sealed record LoginCommand(
    string Email,
    string Password) : ICommand<TokenResponse>;

// Application/Auth/Commands/Login/LoginHandler.cs
public sealed class LoginHandler(
    IDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    TimeProvider timeProvider,
    IOptions<JwtSettings> jwtSettings) : ICommandHandler<LoginCommand, TokenResponse>
{
    public async Task<Result<TokenResponse>> HandleAsync(
        LoginCommand command,
        CancellationToken ct)
    {
        var user = await db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Email == command.Email, ct);

        if (user is null)
            return Error.Unauthorized("Invalid credentials");

        if (!passwordHasher.Verify(command.Password, user.PasswordHash))
            return Error.Unauthorized("Invalid credentials");

        var roles = user.Roles.Select(r => r.Name);
        var tokenResponse = tokenService.GenerateTokens(user, roles);

        // Store refresh token
        var refreshToken = RefreshToken.Create(
            user.Id,
            tokenResponse.RefreshToken,
            timeProvider.GetUtcNow().AddDays(jwtSettings.Value.RefreshTokenExpirationDays),
            timeProvider.GetUtcNow());

        db.RefreshTokens.Add(refreshToken);
        await db.SaveChangesAsync(ct);

        return tokenResponse;
    }
}
```

## Refresh Token Handler

```csharp
// Application/Auth/Commands/RefreshToken/RefreshTokenCommand.cs
public sealed record RefreshTokenCommand(
    string AccessToken,
    string RefreshToken) : ICommand<TokenResponse>;

// Application/Auth/Commands/RefreshToken/RefreshTokenHandler.cs
public sealed class RefreshTokenHandler(
    IDbContext db,
    ITokenService tokenService,
    TimeProvider timeProvider,
    IOptions<JwtSettings> jwtSettings) : ICommandHandler<RefreshTokenCommand, TokenResponse>
{
    public async Task<Result<TokenResponse>> HandleAsync(
        RefreshTokenCommand command,
        CancellationToken ct)
    {
        // Validate expired access token
        var principal = tokenService.ValidateToken(command.AccessToken);
        if (principal is null)
            return Error.Unauthorized("Invalid token");

        var userId = Guid.Parse(principal.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);

        // Find and validate refresh token
        var refreshToken = await db.RefreshTokens
            .FirstOrDefaultAsync(rt =>
                rt.Token == command.RefreshToken &&
                rt.UserId == userId,
                ct);

        var now = timeProvider.GetUtcNow();
        if (refreshToken is null || !refreshToken.IsActive(now))
            return Error.Unauthorized("Invalid refresh token");

        // Get user with roles
        var user = await db.Users
            .Include(u => u.Roles)
            .FirstAsync(u => u.Id == userId, ct);

        // Generate new tokens
        var roles = user.Roles.Select(r => r.Name);
        var newTokenResponse = tokenService.GenerateTokens(user, roles);

        // Rotate refresh token
        refreshToken.Revoke(now, newTokenResponse.RefreshToken);

        var newRefreshToken = RefreshToken.Create(
            user.Id,
            newTokenResponse.RefreshToken,
            now.AddDays(jwtSettings.Value.RefreshTokenExpirationDays),
            now);

        db.RefreshTokens.Add(newRefreshToken);
        await db.SaveChangesAsync(ct);

        return newTokenResponse;
    }
}
```

## Endpoint Examples

### With Controllers

```csharp
[ApiController]
[Route("api/[controller]")]
public class AuthController(
    ICommandHandler<LoginCommand, TokenResponse> loginHandler,
    ICommandHandler<RefreshTokenCommand, TokenResponse> refreshHandler) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        LoginRequest request,
        CancellationToken ct)
    {
        var result = await loginHandler.HandleAsync(
            new LoginCommand(request.Email, request.Password),
            ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : Unauthorized(result.ToProblemDetails());
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(
        RefreshTokenRequest request,
        CancellationToken ct)
    {
        var result = await refreshHandler.HandleAsync(
            new RefreshTokenCommand(request.AccessToken, request.RefreshToken),
            ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : Unauthorized(result.ToProblemDetails());
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(
        [FromServices] IDbContext db,
        [FromServices] TimeProvider timeProvider,
        CancellationToken ct)
    {
        var userId = Guid.Parse(User.FindFirst("sub")!.Value);
        var now = timeProvider.GetUtcNow();

        // Revoke all user's refresh tokens
        var tokens = await db.RefreshTokens
            .Where(rt => rt.UserId == userId && rt.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in tokens)
            token.Revoke(now);

        await db.SaveChangesAsync(ct);

        return NoContent();
    }
}
```

### With Minimal APIs

```csharp
var auth = app.MapGroup("/auth");

auth.MapPost("/login", async (
    LoginRequest request,
    ICommandHandler<LoginCommand, TokenResponse> handler,
    CancellationToken ct) =>
{
    var result = await handler.HandleAsync(
        new LoginCommand(request.Email, request.Password),
        ct);

    return result.IsSuccess
        ? Results.Ok(result.Value)
        : Results.Unauthorized();
})
.AllowAnonymous();

auth.MapPost("/refresh", async (
    RefreshTokenRequest request,
    ICommandHandler<RefreshTokenCommand, TokenResponse> handler,
    CancellationToken ct) =>
{
    var result = await handler.HandleAsync(
        new RefreshTokenCommand(request.AccessToken, request.RefreshToken),
        ct);

    return result.IsSuccess
        ? Results.Ok(result.Value)
        : Results.Unauthorized();
})
.AllowAnonymous();
```

## Protecting Endpoints

```csharp
// Controllers
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll() { /* ... */ }

    [AllowAnonymous] // Override class-level [Authorize]
    [HttpGet("public")]
    public async Task<IActionResult> GetPublic() { /* ... */ }
}

// Minimal APIs
app.MapGet("/orders", GetOrders).RequireAuthorization();
app.MapGet("/orders/public", GetPublicOrders).AllowAnonymous();
```

## Best Practices

| Practice | Recommendation |
|----------|----------------|
| Key storage | Use secrets manager, not appsettings in production |
| Token lifetime | Short access tokens (15-30 min), longer refresh tokens |
| Refresh rotation | Rotate refresh tokens on each use |
| Revocation | Store and check refresh tokens, revoke on logout |
| HTTPS | Always use HTTPS in production |
| Claims | Include minimal claims in JWT |

## Security Considerations

| Risk | Mitigation |
|------|------------|
| Token theft | Short expiration, refresh rotation |
| XSS | HttpOnly cookies for refresh tokens (optional) |
| CSRF | Use Bearer tokens, not cookies for access |
| Brute force | Rate limiting on login endpoint |

## Assets

- [assets/TokenService.cs](assets/TokenService.cs) - Token generation
- [assets/JwtSettings.cs](assets/JwtSettings.cs) - Configuration

## Related

- `authorization` - Roles and permissions
- `rate-limiting` - Protect auth endpoints
- `validation` - Validate login requests
