---
name: dotnet-migrate
description: Manage EF Core migrations - add, apply, script, or rollback
allowed-tools: Bash, Read, Glob, Grep
argument-hint: [add|apply|script|rollback] [name]
model: sonnet
---

# EF Core Migration Management

Add, apply, script, or rollback Entity Framework Core migrations.

## Arguments

- `$1` - Action: `add`, `apply`, `script`, or `rollback`
- `$2` - Migration name (for add) or target migration (for rollback)
- `$ARGUMENTS` - Full arguments string

## Steps

1. **Detect Infrastructure and API projects** in solution
2. **Execute migration command** with correct project paths
3. **Show migration status** and any warnings
4. **For add**: Review generated migration file

## Commands

### Add new migration
```bash
dotnet ef migrations add {MigrationName} \
    -p src/Infrastructure \
    -s src/Api
```

### Apply migrations
```bash
dotnet ef database update \
    -p src/Infrastructure \
    -s src/Api
```

### Generate SQL script
```bash
dotnet ef migrations script \
    -p src/Infrastructure \
    -s src/Api \
    -o migrations.sql \
    --idempotent
```

### Rollback to specific migration
```bash
dotnet ef database update {TargetMigration} \
    -p src/Infrastructure \
    -s src/Api
```

### List migrations
```bash
dotnet ef migrations list \
    -p src/Infrastructure \
    -s src/Api
```

### Remove last migration (if not applied)
```bash
dotnet ef migrations remove \
    -p src/Infrastructure \
    -s src/Api
```

## Output

### For `add`
```
Migration '{Name}' created.

Files:
- src/Infrastructure/Data/Migrations/{Timestamp}_{Name}.cs
- src/Infrastructure/Data/Migrations/{Timestamp}_{Name}.Designer.cs

Review the migration before applying:
[Show key changes from migration file]
```

### For `apply`
```
Applied migrations:
- {Migration1}
- {Migration2}

Database is up to date.
```

### For `script`
```
SQL script generated: migrations.sql

Preview:
[First 50 lines of SQL]
```

## Safety Checks

Before applying migrations:
1. Check if there are pending model changes
2. Warn about destructive operations (DROP, DELETE)
3. Suggest running script generation first for production
