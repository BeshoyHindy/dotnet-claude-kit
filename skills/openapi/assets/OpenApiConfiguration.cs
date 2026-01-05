// Api/Configuration/OpenApiConfiguration.cs
namespace YourNamespace.Api.Configuration;

using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

public static class OpenApiConfiguration
{
    public static IServiceCollection AddOpenApiDocumentation(
        this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(ConfigureSwagger);
        return services;
    }

    public static IApplicationBuilder UseOpenApiDocumentation(
        this IApplicationBuilder app,
        IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1");
                options.RoutePrefix = string.Empty;
                options.DocumentTitle = "API Documentation";
                options.DefaultModelsExpandDepth(-1); // Hide schemas section by default
            });
        }

        return app;
    }

    private static void ConfigureSwagger(SwaggerGenOptions options)
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Your API",
            Version = "v1",
            Description = "API for your application",
            Contact = new OpenApiContact
            {
                Name = "API Support",
                Email = "support@example.com"
            },
            License = new OpenApiLicense
            {
                Name = "MIT",
                Url = new Uri("https://opensource.org/licenses/MIT")
            }
        });

        // Include XML comments
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }

        // JWT Authentication
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter your JWT token"
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });

        // Custom operation filters
        options.OperationFilter<CorrelationIdHeaderFilter>();

        // Use full type name for schema IDs to avoid conflicts
        options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));
    }
}

/// <summary>
/// Adds correlation ID header documentation to all responses.
/// </summary>
public class CorrelationIdHeaderFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        foreach (var response in operation.Responses)
        {
            response.Value.Headers ??= new Dictionary<string, OpenApiHeader>();
            response.Value.Headers["X-Correlation-ID"] = new OpenApiHeader
            {
                Description = "Unique identifier for request tracing",
                Schema = new OpenApiSchema { Type = "string", Format = "uuid" }
            };
        }
    }
}

/// <summary>
/// Adds deprecation warning to obsolete endpoints.
/// </summary>
public class DeprecatedOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var isDeprecated = context.MethodInfo
            .GetCustomAttributes(typeof(ObsoleteAttribute), false)
            .Any();

        if (isDeprecated)
        {
            operation.Deprecated = true;
            operation.Description = $"**DEPRECATED**: {operation.Description}";
        }
    }
}

// Usage in Program.cs:
// builder.Services.AddOpenApiDocumentation();
// app.UseOpenApiDocumentation(app.Environment);
