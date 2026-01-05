---
name: dotnet-validate
description: Validate .NET solution architecture - check layer dependencies and conventions
allowed-tools: Read, Glob, Grep
argument-hint: [--fix]
model: sonnet
---

# Validate .NET Architecture

Check Clean Architecture rules, layer dependencies, and coding conventions.

## Arguments

- `$ARGUMENTS` - May include:
  - `--fix` - Attempt to fix simple violations

## Steps

1. **Scan solution structure** for project references
2. **Check dependency direction** (outer depends on inner)
3. **Validate layer conventions** per project
4. **Report violations** with file locations

## Validation Rules

### Layer Dependencies
```
✓ Domain → (no dependencies)
✓ Application → Domain
✓ Infrastructure → Application, Domain
✓ Api → Application, Infrastructure
✗ Domain → Application (VIOLATION)
✗ Application → Infrastructure (VIOLATION)
```

### Domain Layer
- [ ] No framework dependencies (EF, ASP.NET)
- [ ] No `[JsonProperty]` or `[Column]` attributes
- [ ] Entities have private setters
- [ ] Value objects are immutable
- [ ] Factory methods return `Result<T>`

### Application Layer
- [ ] Handlers are in feature folders
- [ ] Commands/Queries are records
- [ ] Validators exist for commands
- [ ] Interfaces defined here, not implementations

### Infrastructure Layer
- [ ] Implements Application interfaces
- [ ] Entity configurations in Configurations folder
- [ ] No business logic

### Api Layer
- [ ] Controllers/Endpoints use dependency injection
- [ ] No direct database access
- [ ] Proper error handling

## Output

```
## Architecture Validation Report

### Summary
✓ Passed: 12 rules
✗ Failed: 3 rules
⚠ Warnings: 2

### Violations

#### Layer Dependency Violations
✗ Domain/Order.cs:5 - References Infrastructure namespace
  Suggestion: Remove infrastructure dependency

✗ Application/OrderService.cs:12 - Direct DbContext usage
  Suggestion: Use IAppDbContext interface

#### Convention Violations
⚠ Domain/Customer.cs:15 - Public setter on entity property
  Suggestion: Use private setter with method

### Recommendations
1. [High] Remove Infrastructure reference from Domain
2. [Medium] Add validator for CreateCustomerCommand
3. [Low] Consider making Customer.Name setter private
```

## Quick Checks

### Find layer violations
```bash
# Domain referencing Infrastructure
grep -r "using.*Infrastructure" src/Domain/

# Application referencing Infrastructure directly
grep -r "AppDbContext" src/Application/
```

### Check for missing validators
```bash
# Commands without validators
find src/Application -name "*Command.cs" | while read cmd; do
    validator="${cmd/Command/Validator}"
    [ ! -f "$validator" ] && echo "Missing: $validator"
done
```
