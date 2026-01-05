# Hooks

Claude Code hooks run shell commands in response to events.

## Current Hooks

The default `hooks.json` includes a minimal hook that confirms C# file modifications. Customize as needed for your workflow.

## Recommended .NET Hooks

### Build Check After Edits

Verify build succeeds after C# file changes:

```json
{
  "hooks": {
    "PostToolUse": [
      {
        "matcher": "Edit|Write",
        "hooks": [
          {
            "type": "command",
            "command": "if [[ \"$CLAUDE_TOOL_ARG_FILE_PATH\" == *.cs ]]; then dotnet build --no-restore -v q 2>&1 | tail -5; fi",
            "timeout": 60
          }
        ]
      }
    ]
  }
}
```

### Format Check

Run dotnet format check after edits:

```json
{
  "hooks": {
    "PostToolUse": [
      {
        "matcher": "Edit|Write",
        "hooks": [
          {
            "type": "command",
            "command": "if [[ \"$CLAUDE_TOOL_ARG_FILE_PATH\" == *.cs ]]; then dotnet format --verify-no-changes --verbosity quiet 2>&1 || echo 'Format issues found'; fi",
            "timeout": 30
          }
        ]
      }
    ]
  }
}
```

### Test Run After Handler Changes

Run tests when handler files change:

```json
{
  "hooks": {
    "PostToolUse": [
      {
        "matcher": "Edit|Write",
        "hooks": [
          {
            "type": "command",
            "command": "if [[ \"$CLAUDE_TOOL_ARG_FILE_PATH\" == *Handler.cs ]]; then dotnet test --no-build -v q 2>&1 | tail -10; fi",
            "timeout": 120
          }
        ]
      }
    ]
  }
}
```

## Available Hook Events

| Event | When It Fires |
|-------|---------------|
| `PreToolUse` | Before any tool execution |
| `PostToolUse` | After successful tool execution |
| `PostToolUseFailure` | After tool execution fails |
| `UserPromptSubmit` | When user submits a prompt |
| `SessionStart` | When session begins |
| `SessionEnd` | When session ends |

## Environment Variables

Hooks have access to these variables:
- `$CLAUDE_TOOL_NAME` - Name of the tool being used
- `$CLAUDE_TOOL_ARG_FILE_PATH` - File path argument (for file tools)
- `$CLAUDE_PLUGIN_ROOT` - Root directory of the plugin

## Best Practices

- Keep hooks fast (under 5 seconds for interactive use)
- Use `timeout` to prevent slow hooks from blocking
- Filter by file extension to avoid running on every file
- Test hooks locally before committing
- Use `tail` or `head` to limit output length
