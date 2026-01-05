---
name: openapi
description: OpenAPI (Swagger) documentation. API docs, versioning, request/response examples. Use when configuring API documentation.
allowed-tools: Read, Write, Edit, Glob, Grep
---

# OpenAPI Documentation

Patterns for documenting .NET APIs with OpenAPI (Swagger).

**Source**: [ASP.NET Core OpenAPI](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/overview)

## Setup Options

### Option 1: Swashbuckle (Traditional)

```bash
dotnet add package Swashbuckle.AspNetCore
```

```csharp
// Program.cs
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Orders API",
        Version = "v1",
        Description = "API for managing orders",
        Contact = new OpenApiContact
        {
            Name = "API Support",
            Email = "support@example.com"
        }
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Orders API v1");
        options.RoutePrefix = string.Empty; // Swagger at root
    });
}
```

### Option 2: Microsoft.AspNetCore.OpenApi (.NET 9+)

```bash
dotnet add package Microsoft.AspNetCore.OpenApi
```

```csharp
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
```

## XML Documentation

### Enable XML Docs

```xml
<!-- In .csproj -->
<PropertyGroup>
  <GenerateDocumentationFile>true</GenerateDocumentationFile>
  <NoWarn>$(NoWarn);1591</NoWarn>
</PropertyGroup>
```

### Include XML Comments

```csharp
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    options.IncludeXmlComments(xmlPath);
});
```

## Controller Documentation

```csharp
/// <summary>
/// Manages customer orders
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class OrdersController(
    IQueryHandler<GetOrderQuery, OrderResponse> getHandler,
    ICommandHandler<CreateOrderCommand, Guid> createHandler) : ControllerBase
{
    /// <summary>
    /// Retrieves an order by its ID
    /// </summary>
    /// <param name="id">The unique order identifier</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The order details</returns>
    /// <response code="200">Returns the order</response>
    /// <response code="404">Order not found</response>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrderResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await getHandler.HandleAsync(new GetOrderQuery(id), ct);

        return result.IsSuccess
            ? Ok(result.Value)
            : NotFound(result.ToProblemDetails());
    }

    /// <summary>
    /// Creates a new order
    /// </summary>
    /// <param name="request">Order creation details</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The created order ID</returns>
    /// <response code="201">Order created successfully</response>
    /// <response code="400">Invalid request</response>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        CreateOrderRequest request,
        CancellationToken ct)
    {
        var command = new CreateOrderCommand(request.CustomerId, request.Items);
        var result = await createHandler.HandleAsync(command, ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value }, result.Value)
            : BadRequest(result.ToProblemDetails());
    }
}
```

## Request/Response Examples

### Using Annotations

```csharp
/// <summary>
/// Request to create a new order
/// </summary>
public sealed record CreateOrderRequest
{
    /// <summary>
    /// The customer placing the order
    /// </summary>
    /// <example>3fa85f64-5717-4562-b3fc-2c963f66afa6</example>
    public required Guid CustomerId { get; init; }

    /// <summary>
    /// Order line items (1-100 items allowed)
    /// </summary>
    public required List<OrderItemRequest> Items { get; init; }
}

/// <summary>
/// Individual order line item
/// </summary>
public sealed record OrderItemRequest
{
    /// <summary>
    /// Product identifier
    /// </summary>
    /// <example>a1b2c3d4-e5f6-7890-abcd-ef1234567890</example>
    public required Guid ProductId { get; init; }

    /// <summary>
    /// Quantity to order (minimum 1)
    /// </summary>
    /// <example>2</example>
    public required int Quantity { get; init; }
}
```

### Using Swagger Filters

```csharp
public class ExampleSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(CreateOrderRequest))
        {
            schema.Example = new OpenApiObject
            {
                ["customerId"] = new OpenApiString("3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                ["items"] = new OpenApiArray
                {
                    new OpenApiObject
                    {
                        ["productId"] = new OpenApiString("a1b2c3d4-e5f6-7890-abcd-ef1234567890"),
                        ["quantity"] = new OpenApiInteger(2)
                    }
                }
            };
        }
    }
}

// Register
options.SchemaFilter<ExampleSchemaFilter>();
```

## Authentication Documentation

### JWT Bearer

```csharp
builder.Services.AddSwaggerGen(options =>
{
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
});
```

### API Key

```csharp
options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
{
    Type = SecuritySchemeType.ApiKey,
    In = ParameterLocation.Header,
    Name = "X-API-Key",
    Description = "API key for authentication"
});
```

## API Versioning

### Setup

```bash
dotnet add package Asp.Versioning.Mvc
dotnet add package Asp.Versioning.Mvc.ApiExplorer
```

```csharp
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "API", Version = "v1" });
    options.SwaggerDoc("v2", new OpenApiInfo { Title = "API", Version = "v2" });
});
```

### Controller Versioning

```csharp
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public class OrdersController : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id) { /* v1 implementation */ }
}

[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiVersion("2.0")]
public class OrdersV2Controller : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id) { /* v2 implementation */ }
}
```

### Swagger UI for Multiple Versions

```csharp
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1");
    options.SwaggerEndpoint("/swagger/v2/swagger.json", "API v2");
});
```

## Minimal API Documentation

```csharp
app.MapGet("/orders/{id}", async (
    Guid id,
    IQueryHandler<GetOrderQuery, OrderResponse> handler,
    CancellationToken ct) =>
{
    var result = await handler.HandleAsync(new GetOrderQuery(id), ct);
    return result.ToHttpResult();
})
.WithName("GetOrder")
.WithTags("Orders")
.WithSummary("Get an order by ID")
.WithDescription("Retrieves the details of a specific order")
.Produces<OrderResponse>(StatusCodes.Status200OK)
.Produces<ProblemDetails>(StatusCodes.Status404NotFound)
.WithOpenApi();

app.MapPost("/orders", async (
    CreateOrderRequest request,
    ICommandHandler<CreateOrderCommand, Guid> handler,
    CancellationToken ct) =>
{
    var result = await handler.HandleAsync(
        new CreateOrderCommand(request.CustomerId, request.Items),
        ct);
    return result.ToHttpResult(id => Results.Created($"/orders/{id}", id));
})
.WithName("CreateOrder")
.WithTags("Orders")
.WithSummary("Create a new order")
.Produces<Guid>(StatusCodes.Status201Created)
.Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
.WithOpenApi();
```

## Operation Filters

### Add Response Headers

```csharp
public class CorrelationIdHeaderFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        operation.Responses.ToList().ForEach(response =>
        {
            response.Value.Headers ??= new Dictionary<string, OpenApiHeader>();
            response.Value.Headers["X-Correlation-ID"] = new OpenApiHeader
            {
                Description = "Correlation ID for request tracing",
                Schema = new OpenApiSchema { Type = "string" }
            };
        });
    }
}
```

## Best Practices

| Practice | Recommendation |
|----------|----------------|
| XML comments | Document all public endpoints and models |
| Response types | Use `[ProducesResponseType]` for all status codes |
| Examples | Provide realistic example values |
| Tags | Group related endpoints with `[Tags]` |
| Deprecation | Use `[Obsolete]` for deprecated endpoints |
| Descriptions | Be concise but complete |

## Assets

- [assets/OpenApiConfiguration.cs](assets/OpenApiConfiguration.cs) - Complete setup

## Related

- `api-design` - API response patterns
- `exception-handling` - ProblemDetails responses
