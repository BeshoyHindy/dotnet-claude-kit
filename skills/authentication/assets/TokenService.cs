// Infrastructure/Services/TokenService.cs
namespace YourApp.Infrastructure.Services;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using YourApp.Application.Common.Interfaces;
using YourApp.Application.Common.Models;
using YourApp.Domain.Users;
using YourApp.Infrastructure.Configuration;

public sealed class TokenService(
    IOptions<JwtSettings> jwtSettings,
    TimeProvider timeProvider) : ITokenService
{
    private readonly JwtSettings _settings = jwtSettings.Value;

    public TokenResponse GenerateTokens(User user, IEnumerable<string> roles)
    {
        var now = timeProvider.GetUtcNow();
        var expires = now.AddMinutes(_settings.AccessTokenExpirationMinutes);

        var claims = BuildClaims(user, roles);
        var accessToken = GenerateAccessToken(claims, now, expires);
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
                ValidateLifetime = false // Allow expired tokens for refresh flow
            }, out var validatedToken);

            // Ensure the algorithm is what we expect
            if (validatedToken is not JwtSecurityToken jwtToken ||
                !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return principal;
        }
        catch
        {
            return null;
        }
    }

    private static List<Claim> BuildClaims(User user, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("name", user.Name)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        return claims;
    }

    private string GenerateAccessToken(
        IEnumerable<Claim> claims,
        DateTimeOffset notBefore,
        DateTimeOffset expires)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            notBefore: notBefore.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

// Application/Common/Interfaces/ITokenService.cs
namespace YourApp.Application.Common.Interfaces;

using System.Security.Claims;
using YourApp.Application.Common.Models;
using YourApp.Domain.Users;

public interface ITokenService
{
    TokenResponse GenerateTokens(User user, IEnumerable<string> roles);
    ClaimsPrincipal? ValidateToken(string token);
    string GenerateRefreshToken();
}

// Application/Common/Models/TokenResponse.cs
namespace YourApp.Application.Common.Models;

public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt);
