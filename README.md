# dotnet-claude-kit

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Version](https://img.shields.io/badge/version-0.2.0-green.svg)](CHANGELOG.md)
[![Claude Code Plugin](https://img.shields.io/badge/Claude%20Code-Plugin-purple.svg)](https://docs.anthropic.com/en/docs/claude-code)

> Claude Code extensibility toolkit for .NET development

## Status

**v0.2.0** - Pattern-First Redesign

Skills teach patterns first, with framework-specific implementations as optional references. No assumptions about which framework you use.

## Skills

Pattern-first skills with optional framework references:

| Skill | Description |
|-------|-------------|
| `api-design` | API response patterns, pagination, filtering. Consistent API design conventions |
| `authentication` | JWT authentication, token generation/validation, refresh tokens |
| `authorization` | Role-based and policy-based authorization. Permissions, claims, custom requirements |
| `caching` | Caching patterns with IMemoryCache, IDistributedCache, Redis. Cache-aside, invalidation |
| `clean-architecture` | Layer organization and dependency rules |
| `cqrs` | Command Query Responsibility Segregation pattern. Framework-agnostic with optional MediatR/Wolverine references |
| `domain-events` | Domain events for decoupled communication. Event raising, handling, dispatching patterns |
| `efcore` | Entity Framework Core configuration and query patterns |
| `entity-auditing` | Entity audit fields (CreatedBy, CreatedOn, UpdatedBy, UpdatedOn). Automatic tracking |
| `exception-handling` | Global exception handling with Problem Details (RFC 7807) |
| `logging` | Structured logging patterns with ILogger, Serilog integration, correlation IDs |
| `openapi` | OpenAPI (Swagger) documentation. API docs, versioning, request/response examples |
| `outbox-pattern` | Transactional outbox pattern for reliable messaging |
| `rate-limiting` | API rate limiting with built-in .NET rate limiters |
| `result-pattern` | Result<T> for explicit error handling. Railway-oriented programming |
| `soft-delete` | Soft delete pattern - mark entities as deleted instead of removing |
| `testing` | Unit and integration testing patterns with framework options |
| `validation` | Validation patterns with optional FluentValidation reference |

### Skill Structure

Each skill follows progressive disclosure:

```
skills/{skill-name}/
├── SKILL.md           # Core pattern (framework-agnostic)
├── references/        # Framework-specific implementations
│   └── with-{framework}.md
└── assets/            # Reusable code templates
    └── *.cs
```

## Agents

| Agent | Model | When to Use |
|-------|-------|-------------|
| `dotnet-architect` | opus | Architecture reviews, layer validation, refactoring decisions |
| `wolverine-expert` | sonnet | Projects using Wolverine for CQRS/messaging (skip if using MediatR) |
| `efcore-specialist` | sonnet | Entity configuration, query optimization, migrations |
| `testing-specialist` | sonnet | Test design, any test framework (xUnit, MSTest, NUnit) |
| `api-reviewer` | sonnet | Endpoint security, REST conventions, API design review |

## Commands

| Command | Description |
|---------|-------------|
| `/dotnet-feature` | Scaffold vertical slice feature |
| `/dotnet-test` | Run tests with coverage |
| `/dotnet-migrate` | Manage EF Core migrations |
| `/dotnet-validate` | Validate architecture rules |

## Structure

```
dotnet-claude-kit/
├── skills/                      # 18 pattern-first skills
├── agents/                      # 5 specialized agents
├── commands/                    # 4 workflow commands
├── output-styles/               # Response formatting (3 styles)
├── hooks/                       # Event-based automation
├── scripts/                     # Helper shell scripts
├── docs/                        # Documentation
├── .claude-plugin/plugin.json   # Plugin manifest
├── CHANGELOG.md
├── CLAUDE.md
└── README.md
```

## Installation

### Option 1: Claude Code Plugin (Recommended)

Install as a Claude Code plugin to enable skills, agents, and commands:

```bash
# Clone the repository
git clone https://github.com/dotnet-claude-kit/dotnet-claude-kit.git

# Run Claude Code with the plugin
claude --plugin-dir ./dotnet-claude-kit
```

Or add to your Claude Code configuration file (`~/.claude/settings.json`):

```json
{
  "plugins": ["./path/to/dotnet-claude-kit"]
}
```

### Option 2: Copy to Project

Copy relevant files directly into your project's `.claude` directory:

```bash
# Copy all components
cp -r dotnet-claude-kit/skills ./your-project/.claude/
cp -r dotnet-claude-kit/agents ./your-project/.claude/
cp -r dotnet-claude-kit/commands ./your-project/.claude/
```

Or selectively copy only what you need:

```bash
# Example: Only CQRS and Result patterns
cp -r dotnet-claude-kit/skills/cqrs ./your-project/.claude/skills/
cp -r dotnet-claude-kit/skills/result-pattern ./your-project/.claude/skills/
```

### Verification

After installation, verify the plugin is loaded:

```bash
claude /help
# Should list dotnet-claude-kit commands like /dotnet-feature
```

## Usage

### Using Skills

Skills are automatically loaded when relevant. Reference them in prompts:

```
"Using the result-pattern skill, help me implement error handling"
"Following clean-architecture principles, review this project structure"
```

### Using Agents

Invoke specialized agents for domain expertise:

```
"@dotnet-architect review the architecture of this solution"
"@efcore-specialist configure this entity mapping"
```

### Using Commands

Run commands for common workflows:

```bash
/dotnet-feature CreateOrder Order    # Scaffold a new feature
/dotnet-test ./tests                 # Run tests with coverage
/dotnet-migrate AddUserTable         # Create EF migration
/dotnet-validate                     # Validate architecture rules
```

## Design Principles

1. **Pattern-First**: Skills teach patterns, not frameworks. Use MediatR, Wolverine, or raw interfaces
2. **Progressive Disclosure**: Core concepts in SKILL.md, details in references/
3. **No Assumptions**: Skills don't assume which framework or library you use
4. **Research-Based**: Patterns sourced from Microsoft docs, community best practices
5. **Focused**: Each skill under 500 lines, addressing one concern

## Sources

Patterns based on authoritative sources:
- [Microsoft CQRS Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs)
- [Milan Jovanović - CQRS Implementation](https://www.milanjovanovic.tech/blog/cqrs-pattern-the-way-it-should-have-been-from-the-start)
- [Official Claude Code Documentation](https://docs.anthropic.com/en/docs/claude-code)
