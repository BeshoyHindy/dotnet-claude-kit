# dotnet-claude-kit

Claude Code extensibility toolkit for production-ready .NET development.

## Context Awareness

### When Working on This Plugin

You are in the **plugin development context**. Focus on:
- Maintaining skill quality and consistency
- Using `YourNamespace.{Layer}.{Feature}` for all code examples
- Never adding framework-specific content to SKILL.md (put it in references/)

### When Using This Plugin in a Project

You are in the **project development context**. Focus on:
- Applying patterns from skills to the user's codebase
- Respecting the user's existing architecture
- Using their namespaces, not `YourNamespace`
- Loading only skills relevant to the current task

## Decision Trees

### Choosing the Right Skill

```
Need to handle errors explicitly?
├── Yes → result-pattern
└── No → continue

Need to separate reads from writes?
├── Yes → cqrs
└── No → continue

Need event-driven communication?
├── Yes → domain-events (in-process) or outbox-pattern (distributed)
└── No → continue

Need to protect endpoints?
├── Authentication (who are you?) → authentication
└── Authorization (what can you do?) → authorization

Need caching?
├── Single server → caching (IMemoryCache)
└── Distributed → caching (IDistributedCache)
```

### Choosing the Right Agent

```
Architecture decisions or reviews?
→ @dotnet-architect (opus - complex reasoning)

CQRS handler issues?
→ @cqrs-specialist (sonnet)

Database/EF Core issues?
→ @efcore-specialist (sonnet)

Test design or coverage?
→ @testing-specialist (sonnet)

API design or security review?
→ @api-reviewer (sonnet)
```

## Skills Available

| Skill | Use When |
|-------|----------|
| `api-design` | Designing API endpoints, pagination, filtering, sorting |
| `authentication` | Implementing JWT tokens, login, refresh tokens |
| `authorization` | Adding roles, policies, permissions, resource ownership |
| `caching` | Adding memory cache, Redis, distributed caching |
| `clean-architecture` | Organizing solution structure or validating layers |
| `cqrs` | Implementing commands, queries, or handlers |
| `domain-events` | Implementing event-driven domain logic, event handlers |
| `efcore` | Working with EF Core configuration or queries |
| `entity-auditing` | Adding audit fields (CreatedBy, UpdatedBy, timestamps) |
| `exception-handling` | Setting up global exception handling, Problem Details |
| `logging` | Implementing structured logging, correlation IDs |
| `openapi` | Configuring Swagger/OpenAPI documentation |
| `outbox-pattern` | Implementing reliable event publishing |
| `rate-limiting` | Adding API throttling, rate limits |
| `result-pattern` | Adding explicit error handling with Result<T> |
| `soft-delete` | Implementing soft delete with query filters |
| `testing` | Writing unit or integration tests |
| `validation` | Implementing request validation |

## Skill Combinations

Common patterns that use multiple skills together:

| Pattern | Skills | Description |
|---------|--------|-------------|
| **Vertical Slice** | cqrs + validation + result-pattern | Complete feature with handler, validator, error handling |
| **Secure Endpoint** | authentication + authorization + api-design | Protected API with proper auth flow |
| **Reliable Events** | domain-events + outbox-pattern | Events that survive crashes |
| **Audited Entity** | efcore + entity-auditing + soft-delete | Full audit trail with soft delete |
| **Observable API** | logging + exception-handling + openapi | Production-ready API with observability |

## Code Style Requirements

### Always Use

```csharp
// TimeProvider for testability
public class OrderService(TimeProvider timeProvider)
{
    public void CreateOrder()
    {
        var now = timeProvider.GetUtcNow(); // NOT DateTimeOffset.UtcNow
    }
}

// Result<T> for operations that can fail
public Result<Order> Create(string orderNumber)
{
    if (string.IsNullOrWhiteSpace(orderNumber))
        return Error.Validation("Order number required");
    return new Order(orderNumber);
}

// Primary constructors (C# 12+)
public sealed class CreateOrderHandler(
    IDbContext db,
    TimeProvider timeProvider) : ICommandHandler<CreateOrderCommand, Guid>

// CancellationToken in async methods
public async Task<Result<Order>> HandleAsync(
    CreateOrderCommand cmd,
    CancellationToken ct) // Always include

// IReadOnlyCollection for exposing lists
public IReadOnlyCollection<OrderItem> Items => _items.AsReadOnly();
```

### Never Use

```csharp
// DateTime.Now or DateTimeOffset.UtcNow directly
var now = DateTime.Now; // BAD - not testable

// Blocking async
var result = SomeAsync().Result; // BAD - deadlock risk
var result = SomeAsync().Wait(); // BAD - deadlock risk

// Exposing mutable collections
public List<OrderItem> Items { get; set; } // BAD - encapsulation broken

// String concatenation in SQL
$"SELECT * FROM Users WHERE Id = {id}" // BAD - SQL injection

// Exceptions for expected failures
throw new OrderNotFoundException(); // BAD - use Result pattern
```

## Guiding Principles

1. **Separate design from implementation** - Understand WHAT before HOW
2. **Explicit over implicit** - Clear specifications prevent hallucinations
3. **Progressive disclosure** - Load details on demand, not upfront
4. **Model tiering** - Right model for right task (Opus for complex, Haiku for fast)
5. **Single responsibility** - Each skill/agent does one thing well
6. **Testable by default** - TimeProvider, interfaces, Result pattern

## Model Tiers

| Tier | Model | Use For |
|------|-------|---------|
| Critical | opus | Architecture, security, code review |
| Standard | sonnet | Development, debugging, handlers |
| Fast | haiku | Quick tasks, scaffolding, tests |

## .NET Standards

- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- Async/await throughout (no .Result blocking)
- CancellationToken in all async methods
- IOptions<T> for configuration
- Clean Architecture layers (Domain → Application → Infrastructure → API)
- Primary constructors for DI (C# 12+)

## Quality Checklist

Before considering work complete:

- [ ] All code uses TimeProvider, not DateTime/DateTimeOffset directly
- [ ] All async methods have CancellationToken
- [ ] All operations that can fail return Result<T>
- [ ] No exposed List<T> - use IReadOnlyCollection<T>
- [ ] All skills have Source references
- [ ] All examples use YourNamespace.{Layer}.{Feature}
- [ ] No framework-specific code in SKILL.md (use references/)
