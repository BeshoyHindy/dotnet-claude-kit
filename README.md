# dotnet-claude-kit

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

> Claude Code extensibility toolkit for .NET

## Status

**v0.1.0** - Foundation structure (in progress)

## Structure

```
dotnet-claude-kit/
├── .claude-plugin/
│   └── plugin.json      # Plugin manifest
├── commands/            # Slash commands (auto-loaded)
├── agents/              # Subagents (auto-loaded)
├── skills/              # Auto-invoked skills (auto-loaded)
├── output-styles/       # Response styles (via plugin.json)
├── hooks/
│   └── hooks.json       # Automation hooks (auto-loaded)
├── .claude/
│   └── settings.json    # Project settings template
├── docs/
├── scripts/
├── .editorconfig        # .NET code style (Microsoft recommended)
├── .gitignore           # Git ignores
├── CHANGELOG.md         # Version history
├── CLAUDE.md
├── LICENSE              # MIT
└── README.md
```

## Usage

### As Plugin

```bash
claude --plugin-dir /path/to/dotnet-claude-kit
```

### As Project Template

Copy extensibility files to your .NET project:

```bash
cp -r commands agents skills hooks output-styles .claude .editorconfig CLAUDE.md /your-project/
```

## TODO

- [ ] Define commands
- [ ] Define agents
- [ ] Define skills
- [ ] Define output-styles
- [ ] Configure hooks
- [ ] Add documentation
