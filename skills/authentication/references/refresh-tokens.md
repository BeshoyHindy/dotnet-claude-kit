# Refresh Token Patterns

Advanced patterns for secure refresh token management including rotation, revocation, and family tracking.

**Source**: [OAuth 2.0 Token Best Practices](https://datatracker.ietf.org/doc/html/draft-ietf-oauth-security-topics)

## Token Rotation Strategy

Each time a refresh token is used, issue a new one and invalidate the old one:

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
    public string? RevokeReason { get; private set; }

    // Token family tracking for detecting theft
    public Guid FamilyId { get; private set; }
    public int GenerationNumber { get; private set; }

    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt;
    public bool IsRevoked => RevokedAt is not null;
    public bool IsActive(DateTimeOffset now) => !IsRevoked && !IsExpired(now);

    private RefreshToken() { }

    public static RefreshToken Create(
        Guid userId,
        string token,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt,
        Guid? familyId = null,
        int generationNumber = 0)
    {
        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            ExpiresAt = expiresAt,
            CreatedAt = createdAt,
            FamilyId = familyId ?? Guid.NewGuid(),
            GenerationNumber = generationNumber
        };
    }

    public RefreshToken Rotate(
        string newToken,
        DateTimeOffset expiresAt,
        DateTimeOffset createdAt)
    {
        Revoke(createdAt, "Rotated", newToken);

        return new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Token = newToken,
            ExpiresAt = expiresAt,
            CreatedAt = createdAt,
            FamilyId = FamilyId,
            GenerationNumber = GenerationNumber + 1
        };
    }

    public void Revoke(DateTimeOffset revokedAt, string reason, string? replacementToken = null)
    {
        RevokedAt = revokedAt;
        RevokeReason = reason;
        ReplacedByToken = replacementToken;
    }
}
```

## Token Family Revocation (Theft Detection)

If a refresh token is reused after rotation, revoke the entire family:

```csharp
public sealed class RefreshTokenHandler(
    IDbContext db,
    ITokenService tokenService,
    TimeProvider timeProvider,
    ILogger<RefreshTokenHandler> logger,
    IOptions<JwtSettings> jwtSettings) : ICommandHandler<RefreshTokenCommand, TokenResponse>
{
    public async Task<Result<TokenResponse>> HandleAsync(
        RefreshTokenCommand command,
        CancellationToken ct)
    {
        var principal = tokenService.ValidateToken(command.AccessToken);
        if (principal is null)
            return Error.Unauthorized("Invalid token");

        var userId = Guid.Parse(principal.FindFirst(JwtRegisteredClaimNames.Sub)!.Value);
        var now = timeProvider.GetUtcNow();

        var refreshToken = await db.RefreshTokens
            .FirstOrDefaultAsync(rt =>
                rt.Token == command.RefreshToken &&
                rt.UserId == userId,
                ct);

        if (refreshToken is null)
            return Error.Unauthorized("Invalid refresh token");

        // Check if this token was already used (potential theft)
        if (refreshToken.IsRevoked)
        {
            logger.LogWarning(
                "Refresh token reuse detected for user {UserId}, family {FamilyId}. " +
                "Revoking entire token family.",
                userId,
                refreshToken.FamilyId);

            // Revoke all tokens in this family
            await RevokeTokenFamilyAsync(refreshToken.FamilyId, now, "Potential theft detected", ct);

            return Error.Unauthorized("Security violation: token reuse detected");
        }

        if (refreshToken.IsExpired(now))
            return Error.Unauthorized("Refresh token expired");

        // Get user with roles
        var user = await db.Users
            .Include(u => u.Roles)
            .FirstAsync(u => u.Id == userId, ct);

        // Generate new tokens
        var roles = user.Roles.Select(r => r.Name);
        var newTokenResponse = tokenService.GenerateTokens(user, roles);

        // Rotate refresh token (creates new token and revokes old one)
        var newRefreshToken = refreshToken.Rotate(
            newTokenResponse.RefreshToken,
            now.AddDays(jwtSettings.Value.RefreshTokenExpirationDays),
            now);

        db.RefreshTokens.Add(newRefreshToken);
        await db.SaveChangesAsync(ct);

        return newTokenResponse;
    }

    private async Task RevokeTokenFamilyAsync(
        Guid familyId,
        DateTimeOffset revokedAt,
        string reason,
        CancellationToken ct)
    {
        var familyTokens = await db.RefreshTokens
            .Where(rt => rt.FamilyId == familyId && rt.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in familyTokens)
        {
            token.Revoke(revokedAt, reason);
        }

        await db.SaveChangesAsync(ct);
    }
}
```

## Refresh Token Storage Options

### Database Storage (Recommended)

```csharp
// Infrastructure/Data/Configurations/RefreshTokenConfiguration.cs
public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.Token)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(rt => rt.Token)
            .IsUnique();

        builder.HasIndex(rt => rt.UserId);

        builder.HasIndex(rt => rt.FamilyId);

        // Index for cleanup job
        builder.HasIndex(rt => new { rt.ExpiresAt, rt.RevokedAt })
            .HasDatabaseName("IX_RefreshTokens_Cleanup");
    }
}
```

### Redis Storage (High Performance)

```csharp
public sealed class RedisRefreshTokenStore(
    IConnectionMultiplexer redis,
    TimeProvider timeProvider)
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task StoreAsync(RefreshToken token)
    {
        var key = $"refresh_token:{token.Token}";
        var value = JsonSerializer.Serialize(token);
        var expiry = token.ExpiresAt - timeProvider.GetUtcNow();

        await _db.StringSetAsync(key, value, expiry);

        // Also index by user for logout-all functionality
        await _db.SetAddAsync($"user_tokens:{token.UserId}", token.Token);
    }

    public async Task<RefreshToken?> GetAsync(string token)
    {
        var key = $"refresh_token:{token}";
        var value = await _db.StringGetAsync(key);

        return value.HasValue
            ? JsonSerializer.Deserialize<RefreshToken>(value!)
            : null;
    }

    public async Task RevokeAsync(string token)
    {
        await _db.KeyDeleteAsync($"refresh_token:{token}");
    }

    public async Task RevokeAllForUserAsync(Guid userId)
    {
        var tokens = await _db.SetMembersAsync($"user_tokens:{userId}");

        foreach (var token in tokens)
        {
            await _db.KeyDeleteAsync($"refresh_token:{token}");
        }

        await _db.KeyDeleteAsync($"user_tokens:{userId}");
    }
}
```

## Cleanup Job

```csharp
public sealed class RefreshTokenCleanupJob(
    IServiceScopeFactory scopeFactory,
    ILogger<RefreshTokenCleanupJob> logger,
    TimeProvider timeProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupExpiredTokensAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error cleaning up refresh tokens");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }

    private async Task CleanupExpiredTokensAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IDbContext>();

        var cutoff = timeProvider.GetUtcNow().AddDays(-7);

        var deleted = await db.RefreshTokens
            .Where(rt => rt.ExpiresAt < cutoff ||
                        (rt.RevokedAt != null && rt.RevokedAt < cutoff))
            .ExecuteDeleteAsync(ct);

        if (deleted > 0)
        {
            logger.LogInformation("Cleaned up {Count} expired refresh tokens", deleted);
        }
    }
}
```

## HTTP-Only Cookie Storage

For enhanced XSS protection, store refresh tokens in HTTP-only cookies:

```csharp
public sealed class AuthController(
    ICommandHandler<LoginCommand, TokenResponse> loginHandler,
    IOptions<JwtSettings> jwtSettings) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var result = await loginHandler.HandleAsync(
            new LoginCommand(request.Email, request.Password),
            ct);

        if (result.IsFailure)
            return Unauthorized(result.ToProblemDetails());

        // Store refresh token in HTTP-only cookie
        Response.Cookies.Append("refreshToken", result.Value.RefreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(jwtSettings.Value.RefreshTokenExpirationDays)
        });

        // Only return access token in response body
        return Ok(new { result.Value.AccessToken, result.Value.ExpiresAt });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized();

        // Get expired access token from Authorization header
        var accessToken = Request.Headers.Authorization
            .ToString()
            .Replace("Bearer ", "");

        // ... refresh logic
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("refreshToken");
        return NoContent();
    }
}
```

## Security Checklist

| Security Measure | Implementation |
|------------------|----------------|
| Token rotation | New refresh token on each use |
| Family tracking | Detect and revoke on reuse |
| Short-lived access | 15-30 minute expiration |
| Secure storage | HTTP-only cookies or encrypted DB |
| Cleanup | Regular removal of expired tokens |
| Logging | Log all token operations |
| Rate limiting | Limit refresh attempts |

## Related

- `authentication` - Core JWT authentication
- `authorization` - Access control
- `rate-limiting` - Protect endpoints
