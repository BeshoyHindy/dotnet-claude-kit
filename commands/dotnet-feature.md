---
name: dotnet-feature
description: Scaffold a new vertical slice feature with Command, Handler, Validator, and Tests
allowed-tools: Read, Write, Edit, Glob, Grep
argument-hint: [feature-name] [entity-name]
model: sonnet
---

# Scaffold .NET Feature

Create a complete vertical slice feature following Clean Architecture and CQRS patterns.

## Arguments

- `$1` - Feature name (e.g., CreateOrder, UpdateCustomer)
- `$2` - Entity name (e.g., Order, Customer)
- `$ARGUMENTS` - Full arguments string

## Steps

1. **Analyze project structure** to determine correct paths and existing patterns
2. **Detect CQRS implementation** (MediatR, Wolverine, or custom handlers)
3. **Create Command** record with properties
4. **Create Handler** with Result<T> return type
5. **Create Validator** (if validation library is used)
6. **Create Response** record (if applicable)
7. **Create Unit Test** file with basic test cases

## File Structure

```
src/Application/{Entities}/Commands/{FeatureName}/
├── {FeatureName}Command.cs
├── {FeatureName}Handler.cs
├── {FeatureName}Validator.cs
└── {FeatureName}Response.cs (if applicable)

tests/Unit.Tests/Application/{Entities}/
└── {FeatureName}HandlerTests.cs
```

## Templates

### Command
```csharp
namespace {Namespace}.Application.{Entities}.Commands.{FeatureName};

public sealed record {FeatureName}Command(
    // Properties based on feature
) : ICommand<{Response}>;
```

### Handler (Adapt to project's CQRS implementation)
```csharp
public sealed class {FeatureName}Handler(
    IDbContext db,
    ILogger<{FeatureName}Handler> logger)
    : ICommandHandler<{FeatureName}Command, {Response}>
{
    public async Task<Result<{Response}>> HandleAsync(
        {FeatureName}Command command,
        CancellationToken ct)
    {
        // Implementation
    }
}
```

### Validator (if using FluentValidation)
```csharp
public sealed class {FeatureName}Validator : AbstractValidator<{FeatureName}Command>
{
    public {FeatureName}Validator()
    {
        // Validation rules
    }
}
```

## Detection Logic

Before creating files, detect:
1. **CQRS framework**: Look for MediatR, Wolverine, or custom ICommandHandler
2. **Validation library**: FluentValidation or custom validators
3. **Project structure**: Application layer location, namespace conventions
4. **Existing patterns**: Match handler structure, naming conventions

## Output

Created files:
- Command: `src/Application/{Entities}/Commands/{FeatureName}/{FeatureName}Command.cs`
- Handler: `src/Application/{Entities}/Commands/{FeatureName}/{FeatureName}Handler.cs`
- Validator: `src/Application/{Entities}/Commands/{FeatureName}/{FeatureName}Validator.cs`
- Tests: `tests/Unit.Tests/Application/{Entities}/{FeatureName}HandlerTests.cs`
