# Scripts

Helper shell scripts for .NET development workflows.

## Available Scripts

### validate-architecture.sh

Validates Clean Architecture layer dependencies:

```bash
./scripts/validate-architecture.sh ./src

# Checks for:
# - Domain referencing Infrastructure or Application
# - Application referencing Infrastructure directly
# - EF Core usage in Domain layer
# - Data annotations in Domain (prefer Fluent API)
```

### check-conventions.sh

Checks .NET coding conventions:

```bash
./scripts/check-conventions.sh ./src

# Checks for:
# - DateTime.Now usage (should use TimeProvider)
# - Async blocking with .Result or .Wait()
# - Public List<T> (should be IReadOnlyCollection<T>)
# - Missing CancellationToken in async methods
```

## Usage

Make scripts executable before use:

```bash
chmod +x scripts/*.sh
```

Run from your project root:

```bash
./path/to/dotnet-claude-kit/scripts/validate-architecture.sh .
```

## Integration with Hooks

You can integrate these scripts with Claude Code hooks:

```json
{
  "hooks": {
    "PostToolUse": [
      {
        "matcher": "Write|Edit",
        "hooks": [
          {
            "type": "command",
            "command": "${CLAUDE_PLUGIN_ROOT}/scripts/validate-architecture.sh .",
            "timeout": 30
          }
        ]
      }
    ]
  }
}
```
