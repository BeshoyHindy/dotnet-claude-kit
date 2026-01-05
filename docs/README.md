# dotnet-claude-kit Documentation

> Production-ready Claude Code plugin for .NET development

## Overview

dotnet-claude-kit provides 18 pattern-first skills, 5 specialized agents, and 4 commands for professional .NET development with Claude Code.

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

## Agents (5 Total)

| Agent | Model | Purpose |
|-------|-------|---------|
| `dotnet-architect` | opus | Architecture reviews and layer validation |
| `api-reviewer` | sonnet | API security and REST conventions |
| `efcore-specialist` | sonnet | Query optimization and migrations |
| `testing-specialist` | sonnet | Test design and coverage |
| `wolverine-expert` | sonnet | Wolverine CQRS/messaging |

## Commands (4 Total)

| Command | Description |
|---------|-------------|
| `/dotnet-feature` | Scaffold vertical slice feature |
| `/dotnet-test` | Run tests with coverage |
| `/dotnet-migrate` | EF Core migrations |
| `/dotnet-validate` | Architecture validation |

## Design Principles

1. **Pattern-First**: Skills teach patterns, not frameworks
2. **Progressive Disclosure**: Core in SKILL.md, details in references/
3. **No Assumptions**: Works with any CQRS framework or custom code
4. **Copyable Assets**: Production-ready code templates
5. **Both Controllers and Minimal APIs**: Examples for both styles

## Research Sources

- [Microsoft CQRS Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs)
- [Anthropic Skills Best Practices](https://platform.claude.com/docs/en/agents-and-tools/agent-skills/best-practices)
- [Claude Code Plugin Documentation](https://code.claude.com/docs/en/plugins)

---

*Version: 0.2.0 | Last updated: 2026-01-05*
