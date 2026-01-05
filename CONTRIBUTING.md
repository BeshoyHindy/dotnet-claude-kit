# Contributing to dotnet-claude-kit

Thank you for your interest in contributing to dotnet-claude-kit!

## How to Contribute

### Reporting Issues

- Use GitHub Issues to report bugs or suggest features
- Include Claude Code version and .NET version
- Provide steps to reproduce for bugs

### Adding Skills

1. Create a new folder under `skills/{skill-name}/`
2. Follow the structure:
   ```
   skills/{skill-name}/
   ├── SKILL.md              # Required: Core pattern (framework-agnostic)
   ├── references/           # Optional: Framework-specific implementations
   │   └── with-{framework}.md
   └── assets/               # Optional: Reusable code templates
       └── *.cs
   ```
3. Copy an existing skill as reference
4. Follow pattern-first design - teach the pattern, not the framework

### Adding Agents

1. Create `agents/{agent-name}.md`
2. Copy an existing agent as reference
3. Include:
   - Clear purpose and capabilities
   - When to invoke this agent
   - Response methodology
   - Code style preferences

### Adding Commands

1. Create `commands/{command-name}.md`
2. Copy an existing command as reference
3. Include `name:` field in frontmatter
4. Document arguments and expected output

### Code Style

- Follow existing patterns in the codebase
- Use C# 12+ features where appropriate (primary constructors, collection expressions)
- Include XML documentation on public APIs in asset files
- Use `YourNamespace.{Layer}.{Feature}` for placeholder namespaces

## Pull Request Process

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/my-feature`
3. Make your changes
4. Update CHANGELOG.md under `[Unreleased]`
5. Submit a pull request

## Design Principles

When contributing, follow these principles:

1. **Pattern-First**: Skills teach patterns, not frameworks
2. **Progressive Disclosure**: Core concepts first, details in references
3. **No Assumptions**: Don't assume which framework the user has
4. **Focused**: Each skill/agent does one thing well
5. **Copyable**: Asset files should be ready to copy and use

## Questions?

Open an issue with the `question` label.
