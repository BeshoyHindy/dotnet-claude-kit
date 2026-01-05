// Infrastructure/Configuration/JwtSettings.cs
namespace YourApp.Infrastructure.Configuration;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Key { get; init; } = string.Empty;
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public int AccessTokenExpirationMinutes { get; init; } = 15;
    public int RefreshTokenExpirationDays { get; init; } = 7;
}

// Registration in DependencyInjection.cs:
//
// services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
// services.AddScoped<ITokenService, TokenService>();

// Program.cs setup:
//
// using Microsoft.AspNetCore.Authentication.JwtBearer;
// using Microsoft.IdentityModel.Tokens;
// using System.Text;
//
// var jwtSettings = builder.Configuration
//     .GetSection(JwtSettings.SectionName)
//     .Get<JwtSettings>()!;
//
// builder.Services.AddAuthentication(options =>
// {
//     options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
//     options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
// })
// .AddJwtBearer(options =>
// {
//     options.TokenValidationParameters = new TokenValidationParameters
//     {
//         ValidateIssuer = true,
//         ValidateAudience = true,
//         ValidateLifetime = true,
//         ValidateIssuerSigningKey = true,
//         ValidIssuer = jwtSettings.Issuer,
//         ValidAudience = jwtSettings.Audience,
//         IssuerSigningKey = new SymmetricSecurityKey(
//             Encoding.UTF8.GetBytes(jwtSettings.Key)),
//         ClockSkew = TimeSpan.Zero
//     };
//
//     // Optional: Handle token validation events
//     options.Events = new JwtBearerEvents
//     {
//         OnAuthenticationFailed = context =>
//         {
//             if (context.Exception is SecurityTokenExpiredException)
//             {
//                 context.Response.Headers.Append("Token-Expired", "true");
//             }
//             return Task.CompletedTask;
//         }
//     };
// });
//
// builder.Services.AddAuthorization();
//
// var app = builder.Build();
//
// app.UseAuthentication();
// app.UseAuthorization();

// appsettings.json:
// {
//   "Jwt": {
//     "Key": "your-256-bit-secret-key-at-least-32-characters-long",
//     "Issuer": "https://yourapi.com",
//     "Audience": "https://yourapi.com",
//     "AccessTokenExpirationMinutes": 15,
//     "RefreshTokenExpirationDays": 7
//   }
// }
//
// For production, use secrets manager:
// dotnet user-secrets set "Jwt:Key" "your-secret-key"
