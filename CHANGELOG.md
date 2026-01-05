# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-01-05

### Initial Release - Pattern-First Design

Production-ready release with pattern-first, framework-agnostic approach.

- **Skills teach patterns, not frameworks** - Core concepts in SKILL.md, framework-specific implementations in references/
- **No framework assumptions** - Works with MediatR, Wolverine, or custom implementations
- **Progressive disclosure** - Load only what you need

### Skills (Renamed for Clarity)

| Old Name | New Name | Change |
|----------|----------|--------|
| wolverine-cqrs | `cqrs` | Now framework-agnostic with MediatR/Wolverine refs |
| result-pattern | `result-pattern` | Unchanged |
| clean-architecture-dotnet | `clean-architecture` | Simplified naming |
| fluentvalidation | `validation` | Now pattern-first with FluentValidation ref |
| efcore-patterns | `efcore` | Simplified naming |
| testing-dotnet | `testing` | Now supports xUnit, MSTest, NUnit, Moq, NSubstitute |

### New Skills Added (12 production-ready skills)

| Skill | Description |
|-------|-------------|
| `api-design` | API response patterns, pagination, filtering, sorting |
| `authentication` | JWT authentication, token generation/validation, refresh tokens |
| `authorization` | Role-based and policy-based authorization, permissions, claims |
| `caching` | Caching patterns with IMemoryCache, IDistributedCache, Redis |
| `domain-events` | Domain events for decoupled communication, event dispatching |
| `entity-auditing` | Audit fields (CreatedBy, CreatedOn, UpdatedBy, UpdatedOn), EF Core interceptors |
| `exception-handling` | Global exception handling with Problem Details (RFC 7807) |
| `logging` | Structured logging with ILogger, Serilog integration, correlation IDs |
| `openapi` | OpenAPI (Swagger) documentation, versioning, request/response examples |
| `outbox-pattern` | Transactional outbox pattern for reliable messaging |
| `rate-limiting` | API rate limiting with built-in .NET rate limiters |
| `soft-delete` | Soft delete pattern with EF Core query filters |

### Added

- Framework reference files: `with-mediatr.md`, `with-wolverine.md`, `with-fluentvalidation.md`
- Test framework options: `with-xunit.md`, `with-mstest.md`, `with-moq.md`, `with-nsubstitute.md`, `with-fluentassertions.md`
- CQRS decorators reference: `decorators.md`
- Asset file headers with copy-to paths and dependency notes
- `IMessageBus` interface in CQRS interfaces for decoupling
- Skill list in CLAUDE.md for discoverability
- 25 production-ready asset files across all skills
- Both Controllers and Minimal APIs examples in endpoint code
- TimeProvider usage throughout (instead of DateTime.Now)
- Primary constructor patterns (C# 12+)
- Output styles: `dotnet-concise`, `dotnet-teaching` (3 total)
- Scripts: `validate-architecture.sh`, `check-conventions.sh`
- Working hooks.json with C# file modification detection

### Fixed

- Commands now have explicit `name:` field in frontmatter
- Asset namespaces standardized to `YourNamespace.{Layer}.{Feature}`
- TestDataBuilder.cs placeholder types moved to comments
- Result.cs async methods now use `ConfigureAwait(false)`
- Interfaces.cs has proper documentation and copy instructions
- README.md has complete installation and usage documentation
- Agent descriptions clarified with "When to Use" guidance
- plugin.json now includes author field and homepage (per Anthropic best practices)
- All skills use "Response/Request" terminology (not "DTO")

### Removed

- Framework-specific assumptions from core skill files
- Placeholder types with `NotImplementedException` from assets
- "DTO" terminology replaced with "Response/Request"

