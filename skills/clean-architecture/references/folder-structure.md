# Clean Architecture Folder Structure

Example folder organization. Adapt naming and structure to your project.

## Example Layout

```
src/
├── {YourApp}.Domain/          # Core business logic
│   ├── Common/
│   │   ├── Entity.cs
│   │   ├── AggregateRoot.cs
│   │   └── ValueObject.cs
│   └── {Feature}/             # Feature folder
│       ├── {Entity}.cs
│       ├── {ValueObject}.cs
│       └── Events/
│           └── {Event}.cs
│
├── {YourApp}.Application/     # Use cases
│   ├── Common/
│   │   └── Interfaces/
│   │       ├── IDbContext.cs
│   │       └── I{Service}.cs
│   └── {Feature}/
│       ├── Commands/
│       │   └── {Command}/
│       │       ├── {Command}Command.cs
│       │       ├── {Command}Handler.cs
│       │       └── {Command}Validator.cs
│       └── Queries/
│           └── {Query}/
│               ├── {Query}Query.cs
│               ├── {Query}Handler.cs
│               └── {Response}.cs
│
├── {YourApp}.Infrastructure/  # External concerns
│   ├── Data/
│   │   ├── {App}DbContext.cs
│   │   ├── Configurations/
│   │   └── Migrations/
│   └── Services/
│       └── {Service}.cs
│
└── {YourApp}.Api/             # Entry point
    ├── Controllers/           # Or Endpoints/ for Minimal APIs
    ├── Middleware/
    └── Program.cs

tests/
├── {YourApp}.Domain.Tests/
├── {YourApp}.Application.Tests/
└── {YourApp}.Api.Tests/
```

## Naming Variations

Common naming conventions - pick one and stay consistent:

| Layer | Option A | Option B | Option C |
|-------|----------|----------|----------|
| Domain | `Domain` | `Core` | `{App}.Domain` |
| Application | `Application` | `UseCases` | `{App}.Application` |
| Infrastructure | `Infrastructure` | `Persistence` | `{App}.Infrastructure` |
| API | `Api` | `WebApi` | `{App}.Api` |

## Project References

The dependency rule in .csproj form:

```xml
<!-- Domain - NO references -->
<Project Sdk="Microsoft.NET.Sdk">
  <!-- No ProjectReference or PackageReference to frameworks -->
</Project>

<!-- Application - References Domain only -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\{YourApp}.Domain\{YourApp}.Domain.csproj" />
  </ItemGroup>
  <!-- Optional: validation library (avoid EF Core references - use interfaces) -->
</Project>

<!-- Infrastructure - References Application -->
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\{YourApp}.Application\{YourApp}.Application.csproj" />
  </ItemGroup>
  <!-- Database provider, external SDKs, etc. -->
</Project>

<!-- Api - References Application and Infrastructure -->
<Project Sdk="Microsoft.NET.Sdk.Web">
  <ItemGroup>
    <ProjectReference Include="..\{YourApp}.Application\{YourApp}.Application.csproj" />
    <ProjectReference Include="..\{YourApp}.Infrastructure\{YourApp}.Infrastructure.csproj" />
  </ItemGroup>
</Project>
```

## Alternative Structures

### Vertical Slices (Feature-First)

```
src/
├── Features/
│   ├── Orders/
│   │   ├── Domain/
│   │   ├── Commands/
│   │   ├── Queries/
│   │   └── Infrastructure/
│   └── Customers/
│       └── ...
└── Shared/
    └── ...
```

### Modular Monolith

```
src/
├── Modules/
│   ├── Orders/
│   │   ├── Orders.Domain/
│   │   ├── Orders.Application/
│   │   ├── Orders.Infrastructure/
│   │   └── Orders.Api/
│   └── Customers/
│       └── ...
└── Host/
    └── Program.cs
```

## DI Registration Pattern

Each layer registers its own services:

```csharp
// Application layer
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Register handlers, validators, etc.
        return services;
    }
}

// Infrastructure layer
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Register DbContext, external services
        return services;
    }
}

// Program.cs
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);
```
