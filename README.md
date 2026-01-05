# dotnet-claude-kit

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![Version](https://img.shields.io/badge/version-0.1.0-blue.svg)](CHANGELOG.md)
[![Claude Code Plugin](https://img.shields.io/badge/Claude%20Code-Plugin-purple.svg)](https://docs.anthropic.com/en/docs/claude-code)

## The Problem

AI coding assistants are powerful, but they often produce .NET code that:

- **Misses architectural patterns** - No Clean Architecture, CQRS, or proper layering
- **Ignores best practices** - DateTime.Now instead of TimeProvider, exceptions instead of Result pattern
- **Lacks consistency** - Different error handling, naming, and structure across files
- **Requires extensive rework** - Generated code doesn't match your project's standards

## The Solution

**dotnet-claude-kit** gives Claude Code deep knowledge of .NET patterns and practices. Instead of generic code, you get production-ready implementations that follow your architectural decisions.

```
Before: "Create an order handler"
→ Generic handler with try-catch

After (with dotnet-claude-kit):
→ CQRS handler returning Result<T>
→ Validator, tests, proper file organization
```

## Installation

### Quick Start

```
/plugin marketplace add BeshoyHindy/dotnet-claude-kit
/plugin install dotnet-claude-kit@dotnet-tools
```

### Interactive Installation

1. Run `/plugin`
2. Go to **Marketplaces** tab
3. Add marketplace: `BeshoyHindy/dotnet-claude-kit`
4. Go to **Discover** tab
5. Find and install `dotnet-claude-kit`

### For Development

```bash
git clone https://github.com/BeshoyHindy/dotnet-claude-kit.git
claude --plugin-dir ./dotnet-claude-kit
```

That's it! The plugin is now active. Use skills, agents, and commands in your prompts.

## Usage

**Skills** load automatically. Reference them in prompts:
```
"Implement CreateOrder using the cqrs and result-pattern skills"
```

**Agents** provide specialized expertise:
```
"@dotnet-architect review this solution's architecture"
```

**Commands** run workflows:
```bash
/dotnet-feature CreateOrder Order    # Scaffold vertical slice
/dotnet-validate                     # Check architecture rules
```

## What's Included

### 18 Skills

Pattern-first knowledge with framework-agnostic implementations:

| Category | Skills |
|----------|--------|
| **Architecture** | `clean-architecture`, `cqrs`, `result-pattern`, `domain-events` |
| **Data** | `efcore`, `entity-auditing`, `soft-delete`, `outbox-pattern` |
| **Security** | `authentication`, `authorization` |
| **API** | `api-design`, `openapi`, `rate-limiting`, `exception-handling` |
| **Infrastructure** | `caching`, `logging`, `validation`, `testing` |

Each skill follows progressive disclosure:

```
skills/{name}/
├── SKILL.md           # Core pattern (always loaded)
├── references/        # Framework-specific (loaded on demand)
│   └── with-{framework}.md
└── assets/            # Copy-paste code templates
    └── *.cs
```

### 5 Agents

| Agent | Model | Specialty |
|-------|-------|-----------|
| `dotnet-architect` | opus | Architecture reviews, layer validation |
| `cqrs-specialist` | sonnet | Handler creation, validation pipelines |
| `efcore-specialist` | sonnet | Entity configuration, query optimization |
| `testing-specialist` | sonnet | Test design, mocking strategies |
| `api-reviewer` | sonnet | Endpoint security, REST conventions |

### 2 Commands

| Command | Purpose |
|---------|---------|
| `/dotnet-feature` | Scaffold complete vertical slice |
| `/dotnet-validate` | Validate architecture rules |

### 3 Output Styles

| Style | Use Case |
|-------|----------|
| `dotnet-concise` | Experienced developers, minimal explanation |
| `dotnet-teaching` | Learning, includes WHY behind patterns |
| `dotnet-review` | Code reviews with severity levels |

## Design Principles

1. **Pattern-First** - Skills teach patterns, not frameworks
2. **Progressive Disclosure** - Core concepts in SKILL.md, framework details in references/
3. **No Assumptions** - You choose your frameworks and libraries

## Skill Combinations

Skills work together for common scenarios:

```mermaid
graph TD
    subgraph "Vertical Slice Feature"
        CQRS[cqrs] --> VP[validation]
        VP --> RP[result-pattern]
    end

    subgraph "Secure API"
        AUTH[authentication] --> AUTHZ[authorization]
        AUTHZ --> API[api-design]
    end

    subgraph "Reliable Events"
        DE[domain-events] --> OP[outbox-pattern]
    end

    subgraph "Audited Entities"
        EF[efcore] --> EA[entity-auditing]
        EA --> SD[soft-delete]
    end

    subgraph "Observable API"
        LOG[logging] --> EH[exception-handling]
        EH --> OA[openapi]
    end

    CQRS -.->|"handlers use"| EF
    AUTH -.->|"protect"| CQRS
    RP -.->|"used by"| EH
```

| Pattern | Skills | Use Case |
|---------|--------|----------|
| **Vertical Slice** | cqrs + validation + result-pattern | Complete feature implementation |
| **Secure Endpoint** | authentication + authorization + api-design | Protected API endpoints |
| **Reliable Events** | domain-events + outbox-pattern | Events that survive crashes |
| **Audited Entity** | efcore + entity-auditing + soft-delete | Full audit trail |
| **Observable API** | logging + exception-handling + openapi | Production monitoring |

## Project Structure

```
dotnet-claude-kit/
├── .claude-plugin/   # Plugin manifest and marketplace config
├── skills/           # 18 pattern-first skills
│   └── {skill}/
│       ├── SKILL.md      # Core pattern (always loaded)
│       ├── references/   # Framework-specific guides
│       └── assets/       # Copy-paste code templates
├── agents/           # 5 specialized agents
├── commands/         # 2 workflow commands
├── output-styles/    # 3 response formats
└── examples/         # Configuration examples
```

## Sources

Built on authoritative references:

- [Microsoft CQRS Pattern](https://learn.microsoft.com/en-us/azure/architecture/patterns/cqrs)
- [ASP.NET Core Documentation](https://learn.microsoft.com/en-us/aspnet/core/)
- [EF Core Documentation](https://learn.microsoft.com/en-us/ef/core/)
- [Clean Architecture - Milan Jovanovic](https://www.milanjovanovic.tech/blog/clean-architecture-folder-structure)

## Status

**v0.1.0** - Initial release. Production-ready patterns for .NET development.

---

[Changelog](CHANGELOG.md) | [Contributing](CONTRIBUTING.md) | [Code of Conduct](CODE_OF_CONDUCT.md) | [Security](SECURITY.md)
