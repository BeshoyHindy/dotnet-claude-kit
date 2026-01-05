# dotnet-claude-kit Documentation

> Production-ready Claude Code plugin for .NET development

## User Journey

### Step 1: Install the Plugin

```bash
git clone https://github.com/BeshoyHindy/dotnet-claude-kit.git
claude --plugin-dir ./dotnet-claude-kit
```

### Step 2: Start a New Feature

Use the `/dotnet-feature` command to scaffold a complete vertical slice:

```bash
/dotnet-feature CreateOrder Order
```

This generates:
- `CreateOrderCommand.cs` - The command record
- `CreateOrderHandler.cs` - Handler with Result<T>
- `CreateOrderValidator.cs` - FluentValidation validator
- Test files

### Step 3: Let Skills Guide Your Code

Skills are loaded automatically based on context. Reference them explicitly for best results:

```
"Using the cqrs and result-pattern skills, implement the handler"
"Add authentication following the authentication skill"
"Set up caching using the caching skill patterns"
```

### Step 4: Use Agents for Expertise

When you need specialized help:

```
"@dotnet-architect review this solution's architecture"
"@efcore-specialist help me optimize this query"
"@testing-specialist design tests for this feature"
```

### Step 5: Validate Your Work

```bash
/dotnet-validate     # Check architecture rules
/dotnet-test         # Run tests with coverage
```

## Skills (18 Total)

### Core Architecture
| Skill | Description |
|-------|-------------|
| `clean-architecture` | Layer organization and dependency rules |
| `cqrs` | Command Query Responsibility Segregation with MediatR/Wolverine options |
| `result-pattern` | Result<T> for explicit error handling |
| `validation` | Request validation with optional FluentValidation |
| `domain-events` | Event-driven domain logic and dispatching |

### Data & Persistence
| Skill | Description |
|-------|-------------|
| `efcore` | Entity Framework Core patterns and optimization |
| `entity-auditing` | Audit fields (CreatedBy, UpdatedBy, timestamps) |
| `soft-delete` | Soft delete with EF Core query filters |
| `outbox-pattern` | Transactional outbox for reliable messaging |
| `caching` | Memory, distributed, and Redis caching |

### API & Security
| Skill | Description |
|-------|-------------|
| `api-design` | Response patterns, pagination, filtering |
| `authentication` | JWT tokens and refresh token flows |
| `authorization` | Role-based and policy-based access control |
| `exception-handling` | Problem Details (RFC 7807) responses |
| `rate-limiting` | API throttling with .NET rate limiters |
| `openapi` | Swagger/OpenAPI documentation |

### Cross-Cutting
| Skill | Description |
|-------|-------------|
| `logging` | Structured logging with Serilog, correlation IDs |
| `testing` | Unit and integration testing patterns |

## Skill Combinations

Common patterns that combine multiple skills:

| Use Case | Skills | What You Get |
|----------|--------|--------------|
| **New Feature** | cqrs + validation + result-pattern | Complete handler with validation and error handling |
| **Secure API** | authentication + authorization + api-design | Protected endpoints with proper auth |
| **Reliable Events** | domain-events + outbox-pattern | Events that survive crashes |
| **Audited Data** | efcore + entity-auditing + soft-delete | Full audit trail with soft delete |
| **Observable API** | logging + exception-handling + openapi | Production-ready with observability |

## Agents (5 Total)

| Agent | Model | When to Use |
|-------|-------|-------------|
| `dotnet-architect` | opus | Architecture decisions, layer validation, refactoring |
| `cqrs-specialist` | sonnet | Handler issues, validation pipeline, CQRS patterns |
| `efcore-specialist` | sonnet | Query optimization, migrations, entity configuration |
| `testing-specialist` | sonnet | Test design, mocking, coverage strategies |
| `api-reviewer` | sonnet | Endpoint security, REST conventions, API review |

## Commands (4 Total)

| Command | Description | Example |
|---------|-------------|---------|
| `/dotnet-feature` | Scaffold vertical slice | `/dotnet-feature CreateOrder Order` |
| `/dotnet-test` | Run tests with coverage | `/dotnet-test ./tests --coverage` |
| `/dotnet-migrate` | EF Core migrations | `/dotnet-migrate add AddUserTable` |
| `/dotnet-validate` | Check architecture | `/dotnet-validate` |

## Output Styles (3 Total)

Switch output style based on your needs:

| Style | Use Case |
|-------|----------|
| `dotnet-concise` | Experienced developers - code over explanation |
| `dotnet-teaching` | Learning - explains WHY patterns work |
| `dotnet-review` | Code reviews - structured with severity levels |

## Design Principles

1. **Pattern-First** - Skills teach patterns, not frameworks
2. **Progressive Disclosure** - Core in SKILL.md, details in references/
3. **No Assumptions** - Works with any CQRS framework or custom code
4. **Copyable Assets** - Production-ready code templates in assets/
5. **Testable Code** - TimeProvider, interfaces, Result pattern throughout

## Quality Standards

All code in this plugin follows:

- TimeProvider for testability (never DateTime.Now)
- Result<T> for operations that can fail
- CancellationToken in all async methods
- IReadOnlyCollection<T> for exposed lists
- Primary constructors (C# 12+)
- Clean Architecture layers

## Research Sources

Built on authoritative references:

- [Microsoft CQRS Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs)
- [ASP.NET Core Documentation](https://learn.microsoft.com/en-us/aspnet/core/)
- [EF Core Documentation](https://learn.microsoft.com/en-us/ef/core/)
- [Claude Code Documentation](https://docs.anthropic.com/en/docs/claude-code)
- [Milan Jovanović - Clean Architecture](https://www.milanjovanovic.tech/blog/clean-architecture-folder-structure)

---

*Version: 0.1.0 | Last updated: 2026-01-05*
