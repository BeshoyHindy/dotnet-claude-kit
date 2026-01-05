# Serilog Integration

Framework-specific patterns for using Serilog with structured logging in .NET applications.

**Source**: [Serilog Documentation](https://serilog.net/)

## Installation

```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File
dotnet add package Serilog.Enrichers.Environment
dotnet add package Serilog.Enrichers.Thread
```

## Program.cs Setup

```csharp
using Serilog;

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithThreadId()
    .CreateLogger();

builder.Host.UseSerilog();

try
{
    var app = builder.Build();

    // Use Serilog request logging instead of default
    app.UseSerilogRequestLogging(options =>
    {
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("UserId", httpContext.User.FindFirst("sub")?.Value);
            diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString());
            diagnosticContext.Set("UserAgent", httpContext.Request.Headers["User-Agent"].ToString());
        };
    });

    // ... configure other middleware
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
```

## Configuration (appsettings.json)

```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "Microsoft.Hosting.Lifetime": "Information",
        "Microsoft.EntityFrameworkCore": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "outputTemplate": "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}{NewLine}      {Message:lj}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/log-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}"
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"],
    "Properties": {
      "Application": "YourAppName"
    }
  }
}
```

## Development vs Production Configuration

```json
// appsettings.Development.json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Debug",
      "Override": {
        "Microsoft.EntityFrameworkCore.Database.Command": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Code, Serilog.Sinks.Console"
        }
      }
    ]
  }
}
```

```json
// appsettings.Production.json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Warning"
    },
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "/var/log/app/log-.txt",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30,
          "fileSizeLimitBytes": 104857600,
          "rollOnFileSizeLimit": true
        }
      }
    ]
  }
}
```

## Seq Integration (Centralized Logging)

```bash
dotnet add package Serilog.Sinks.Seq
```

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://localhost:5341",
          "apiKey": "your-api-key"
        }
      }
    ]
  }
}
```

## Elasticsearch Integration

```bash
dotnet add package Serilog.Sinks.Elasticsearch
```

```json
{
  "Serilog": {
    "WriteTo": [
      {
        "Name": "Elasticsearch",
        "Args": {
          "nodeUris": "http://localhost:9200",
          "indexFormat": "app-logs-{0:yyyy.MM}",
          "autoRegisterTemplate": true
        }
      }
    ]
  }
}
```

## Custom Enrichers

```csharp
// Infrastructure/Logging/CorrelationIdEnricher.cs
namespace YourNamespace.Infrastructure.Logging;

using Serilog.Core;
using Serilog.Events;

public sealed class CorrelationIdEnricher(
    IHttpContextAccessor httpContextAccessor) : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var correlationId = httpContextAccessor.HttpContext?.Items["CorrelationId"]?.ToString()
            ?? "no-correlation-id";

        var property = propertyFactory.CreateProperty("CorrelationId", correlationId);
        logEvent.AddPropertyIfAbsent(property);
    }
}

// Registration
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.With(services.GetRequiredService<CorrelationIdEnricher>());
});
```

## Filtering Sensitive Data

```csharp
// Mask sensitive properties
Log.Logger = new LoggerConfiguration()
    .Destructure.ByTransforming<UserLoginRequest>(r => new
    {
        r.Email,
        Password = "***REDACTED***"
    })
    .CreateLogger();

// Or use a custom policy
public class SensitiveDataDestructuringPolicy : IDestructuringPolicy
{
    private static readonly HashSet<string> SensitiveProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password", "Token", "Secret", "ApiKey", "ConnectionString", "CreditCard"
    };

    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        out LogEventPropertyValue? result)
    {
        result = null;

        if (value is not IDictionary<string, object> dict)
            return false;

        var sanitized = dict.ToDictionary(
            kvp => kvp.Key,
            kvp => SensitiveProperties.Contains(kvp.Key)
                ? "***REDACTED***"
                : kvp.Value);

        result = propertyValueFactory.CreatePropertyValue(sanitized, destructureObjects: true);
        return true;
    }
}
```

## Performance Logging

```csharp
// Measure operation duration
public async Task<Result<Order>> ProcessOrderAsync(Guid orderId, CancellationToken ct)
{
    using var _ = logger.BeginTimedOperation("ProcessOrder", orderId.ToString());

    // ... operation logic

    return order;
}

// Extension method for timed operations
public static class SerilogExtensions
{
    public static IDisposable BeginTimedOperation(
        this ILogger logger,
        string operationName,
        string? identifier = null)
    {
        return new TimedOperation(logger, operationName, identifier);
    }

    private sealed class TimedOperation : IDisposable
    {
        private readonly ILogger _logger;
        private readonly string _operationName;
        private readonly string? _identifier;
        private readonly Stopwatch _stopwatch;

        public TimedOperation(ILogger logger, string operationName, string? identifier)
        {
            _logger = logger;
            _operationName = operationName;
            _identifier = identifier;
            _stopwatch = Stopwatch.StartNew();

            _logger.Information("Starting {Operation} {Identifier}", operationName, identifier);
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _logger.Information(
                "Completed {Operation} {Identifier} in {ElapsedMs}ms",
                _operationName,
                _identifier,
                _stopwatch.ElapsedMilliseconds);
        }
    }
}
```

## Async Batching for High-Volume Logging

```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.Async(a => a.File(
        "logs/log-.txt",
        rollingInterval: RollingInterval.Day,
        buffered: true,
        flushToDiskInterval: TimeSpan.FromSeconds(1)))
    .CreateLogger();
```

## Best Practices

| Practice | Recommendation |
|----------|----------------|
| Use `UseSerilogRequestLogging()` | Replaces verbose ASP.NET Core logs with single request log |
| Enrich with context | Add CorrelationId, UserId, MachineName automatically |
| Filter sensitive data | Use destructuring policies to mask passwords, tokens |
| Configure by environment | Debug in dev, Warning+ in production |
| Use async sinks | For high-volume scenarios, use buffered/async sinks |
| Structured properties | Use `{@Object}` for destructuring, `{$Type}` for type name |

## Related

- `logging` - Core logging patterns
- `exception-handling` - Exception logging
