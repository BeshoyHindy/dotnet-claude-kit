# dotnet-claude-kit

Claude Code extensibility toolkit for .NET development.

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

## Guiding Principles

1. **Separate design from implementation** - Understand WHAT before HOW
2. **Explicit over implicit** - Clear specifications prevent hallucinations
3. **Progressive disclosure** - Load details on demand, not upfront
4. **Model tiering** - Right model for right task (Opus/Sonnet/Haiku)
5. **Single responsibility** - Each skill/agent does one thing well
6. **Checklists over prose** - Verifiable quality gates

## Model Tiers

| Tier | Model | Use For |
|------|-------|---------|
| Critical | opus | Architecture, security, code review |
| Standard | sonnet | Development, debugging, support |
| Fast | haiku | Quick tasks, scaffolding |

## .NET Standards

- Nullable reference types enabled
- Async/await throughout (no .Result blocking)
- CancellationToken in all async methods
- IOptions<T> for configuration
- Clean Architecture layers

## Anti-Patterns

- DateTime.Now in domain code (use TimeProvider)
- Business logic in handlers (keep handlers thin, delegate to domain)
- Exposing List<T> (use IReadOnlyCollection<T>)
- Blocking async with .Result/.Wait()
- String concatenation in SQL queries (use parameterized queries)

## Templates

- `skills/_TEMPLATE.md` - Skill format
- `agents/_TEMPLATE.md` - Agent format
- `commands/_TEMPLATE.md` - Command format
- `output-styles/_TEMPLATE.md` - Output style format
- `hooks/_TEMPLATE.json` - Hooks format
